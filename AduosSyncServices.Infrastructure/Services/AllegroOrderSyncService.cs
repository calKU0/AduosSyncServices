using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AduosSyncServices.Infrastructure.Services
{
    public class AllegroOrderSyncService : IAllegroOrderSyncService
    {
        private readonly ILogger<AllegroOrderSyncService> _logger;
        private readonly IOrderRepository _orderRepo;
        private readonly IOfferRepository _offerRepo;
        private readonly IAllegroApiClient _allegroApiClient;
        private readonly IGaskaApiClient _gaskaApiClient;

        private const int OrdersPageSize = 100;

        // Bounds on concurrency. Order pages are fetched in parallel from Allegro (the first page
        // reveals the total, the rest are fetched at once); orders are then saved in parallel, each
        // SaveAllegroOrder opening its own connection/transaction.
        private const int OrderFetchParallelism = 5;
        private const int OrderSaveParallelism = 5;

        public AllegroOrderSyncService(ILogger<AllegroOrderSyncService> logger, IOrderRepository orderRepo, IOfferRepository offerRepo, IAllegroApiClient allegroApiClient, IGaskaApiClient gaskaApiClient)
        {
            _logger = logger;
            _orderRepo = orderRepo;
            _offerRepo = offerRepo;
            _allegroApiClient = allegroApiClient;
            _gaskaApiClient = gaskaApiClient;
        }

        public async Task SyncOrdersFromAllegro(List<string> allegroDeliveryNames, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            const string minBoughtFrom = "2026-07-11T00:00:00Z";
            var minBoughtDate = DateTime.Parse(minBoughtFrom, null, DateTimeStyles.AdjustToUniversal);
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-4);
            var boughtDate = sevenDaysAgo < minBoughtDate ? minBoughtDate : sevenDaysAgo;
            string boughtDateIso = boughtDate.ToString("yyyy-MM-ddTHH:mm:ssZ");

            try
            {
                var deliveryNames = (allegroDeliveryNames ?? new List<string>())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (deliveryNames.Count == 0)
                {
                    _logger.LogError("No Allegro delivery method names configured. Aborting order sync.");
                    progress?.Report("Nie skonfigurowano nazw metod dostawy Allegro. Przerwano synchronizację.");
                    return;
                }

                // 1. Pull every order page from Allegro (pages after the first are fetched in parallel).
                progress?.Report("Pobieranie zamówień z Allegro...");
                var orders = await FetchAllOrdersAsync(boughtDateIso, progress, ct);
                if (orders.Count == 0)
                {
                    _logger.LogInformation("No orders returned from Allegro.");
                    progress?.Report("Brak zamówień do synchronizacji.");
                    return;
                }

                // 2. Resolve each line item's shipping method from the offers referenced by these
                //    orders (one targeted DB read), instead of an Allegro API call per line item.
                progress?.Report("Wczytywanie metod dostawy ofert z bazy...");
                var offerIds = orders
                    .SelectMany(o => o.LineItems ?? new List<AllegroGetOrdersResponse.LineItem>())
                    .Select(li => li.Offer?.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var deliveryNameByOfferId = await _offerRepo.GetOfferDeliveryNamesByIds(offerIds, ct);

                // 3. Save the qualifying orders in parallel (MapAllegroOrderToModel is pure and each
                //    SaveAllegroOrder uses its own connection, so this is safe to fan out).
                progress?.Report($"Zapisywanie {orders.Count} zamówień...");
                var syncedCount = 0;
                await Parallel.ForEachAsync(
                    orders,
                    new ParallelOptions { MaxDegreeOfParallelism = OrderSaveParallelism, CancellationToken = ct },
                    async (order, _) =>
                    {
                        try
                        {
                            // Every line item's offer must ship via one of our Gąska delivery methods
                            // (looked up locally); if any item doesn't, skip the order.
                            bool allItemsUseGaska = (order.LineItems ?? new List<AllegroGetOrdersResponse.LineItem>()).All(item =>
                                item.Offer?.Id is { } offerId
                                && deliveryNameByOfferId.TryGetValue(offerId, out var deliveryName)
                                && deliveryNames.Contains(deliveryName));

                            if (!allItemsUseGaska)
                            {
                                _logger.LogInformation("Skipping order {OrderId} - not all items use a Gąska shipping method.", order.Id);
                                return;
                            }

                            var model = MapAllegroOrderToModel(order, deliveryNameByOfferId);
                            await _orderRepo.SaveAllegroOrder(model);
                            Interlocked.Increment(ref syncedCount);
                            _logger.LogInformation("Order {OrderId} synced from Allegro.", order.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to sync order {OrderId} from Allegro.", order.Id);
                        }
                    });

                _logger.LogInformation("Finished syncing {Synced}/{Total} orders from Allegro.", syncedCount, orders.Count);
                progress?.Report($"Zakończono synchronizację {syncedCount} z {orders.Count} zamówień z Allegro.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync orders from Allegro.");
                progress?.Report($"Błąd podczas synchronizacji zamówień z Allegro: {ex.Message}");
            }
        }

        // Fetches all order pages for the window. The first page is fetched to learn the total count,
        // then every remaining page is fetched in parallel (offset pagination is stateless, so page
        // order doesn't matter here).
        private async Task<List<AllegroGetOrdersResponse.CheckoutForm>> FetchAllOrdersAsync(
            string boughtDateIso, IProgress<string>? progress, CancellationToken ct)
        {
            var firstPage = await _allegroApiClient.GetOrders(new AllegroGetOrdersRequest
            {
                Limit = OrdersPageSize,
                Offset = 0,
                DateFrom = boughtDateIso
            }, ct);

            var orders = new List<AllegroGetOrdersResponse.CheckoutForm>(firstPage?.CheckoutForms ?? new List<AllegroGetOrdersResponse.CheckoutForm>());
            var totalCount = firstPage?.TotalCount ?? orders.Count;

            if (orders.Count == 0 || orders.Count >= totalCount)
                return orders;

            var remainingOffsets = new List<int>();
            for (int offset = OrdersPageSize; offset < totalCount; offset += OrdersPageSize)
                remainingOffsets.Add(offset);

            var pages = new System.Collections.Concurrent.ConcurrentBag<List<AllegroGetOrdersResponse.CheckoutForm>>();
            await Parallel.ForEachAsync(
                remainingOffsets,
                new ParallelOptions { MaxDegreeOfParallelism = OrderFetchParallelism, CancellationToken = ct },
                async (offset, token) =>
                {
                    var page = await _allegroApiClient.GetOrders(new AllegroGetOrdersRequest
                    {
                        Limit = OrdersPageSize,
                        Offset = offset,
                        DateFrom = boughtDateIso
                    }, token);

                    if (page?.CheckoutForms is { Count: > 0 } forms)
                        pages.Add(forms);
                });

            foreach (var page in pages)
                orders.AddRange(page);

            progress?.Report($"Pobrano {orders.Count} zamówień z Allegro.");
            return orders;
        }

        public async Task UpdateOrderGaskaInfo(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            try
            {
                var orders = await _orderRepo.GetOrdersToUpdateExternalInfo();
                progress?.Report($"Aktualizowanie informacji z Gąski dla {orders.Count} zamówień...");
                foreach (var order in orders)
                {
                    await FetchAndUpdateGaskaOrder(order, ct);
                }
                progress?.Report("Zakończono aktualizację informacji z Gąski.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update orders from Gąska API.");
                progress?.Report($"Błąd podczas aktualizacji informacji z Gąski: {ex.Message}");
            }
        }

        private async Task FetchAndUpdateGaskaOrder(AllegroOrder order, CancellationToken ct = default!)
        {
            try
            {
                var gaskaOrderResponse = await _gaskaApiClient.GetOrder(order.ExternalOrderId, "pl", ct);
                if (gaskaOrderResponse.Result != 0)
                {
                    _logger.LogError($"Failed to fetch Order {order.ExternalOrderId} from Gąska. Error: {gaskaOrderResponse.Message}");
                    return;
                }

                if (gaskaOrderResponse.Order == null)
                {
                    _logger.LogError($"No Order data returned from Gąska for Order ID {order.ExternalOrderId}.");
                    return;
                }

                order.ExternalDeliveryName = gaskaOrderResponse.Order.Delivery;
                order.ExternalOrderStatus = gaskaOrderResponse.Order.Items.FirstOrDefault()?.RealizeDeliveryStatus;
                order.ExternalOrderNumber = gaskaOrderResponse.Order.OrderNumber;

                foreach (var item in order.Items)
                {
                    // Match by Gąska product code (item.ExternalId holds our Product.Code, which is
                    // Gąska's code) - NOT by id: gaskaItem.Id is Gąska's own product id (IntegrationId),
                    // while item.ProductId is the local Products.Id PK; the two virtually never coincide.
                    var gaskaItem = gaskaOrderResponse.Order.Items.FirstOrDefault(i =>
                        string.Equals(i.CodeGaska, item.ExternalId, StringComparison.OrdinalIgnoreCase));
                    if (gaskaItem != null)
                    {
                        item.ExternalTrackingNumber = gaskaItem.RealizeTrackingNumber;
                        item.ExternalCourier = gaskaItem.RealizeDelivery;
                    }
                }

                await _orderRepo.UpdateOrderExternalInfo(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Order {AllegroOrderId} with Gąska info.", order.AllegroId);
            }
        }

        public async Task UpdateOrdersInAllegro(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            int statuses = 0, shipments = 0;

            try
            {
                var orders = await _orderRepo.GetOrdersToUpdateInAllegro();
                progress?.Report($"Aktualizowanie {orders.Count} zamówień w Allegro...");

                foreach (var order in orders)
                {
                    ct.ThrowIfCancellationRequested();

                    var status = MapGaskaStatusToAllegro(order.ExternalOrderStatus);
                    if (status != order.RealizeStatus && status != AllegroOrderStatus.NEW)
                    {
                        if (await SendStatusToAllegro(order, status, ct))
                            statuses++;
                    }

                    shipments += await SendTrackingNumbersToAllegro(order, ct);
                }

                _logger.LogInformation("Allegro orders updated - statuses: {Statuses}, tracking numbers: {Shipments}.", statuses, shipments);
                progress?.Report($"Zakończono aktualizację zamówień w Allegro (statusy: {statuses}, numery przesyłek: {shipments}).");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update orders in Allegro.");
                progress?.Report($"Błąd podczas aktualizacji zamówień w Allegro: {ex.Message}");
            }
        }

        private async Task<bool> SendStatusToAllegro(AllegroOrder order, AllegroOrderStatus status, CancellationToken ct)
        {
            try
            {
                var response = await _allegroApiClient.UpdateOrderStatus(order.AllegroId, new AllegroSetOrderStatusRequest { Status = status }, ct);

                if (response.IsSuccessStatusCode)
                {
                    // Zapisujemy stan, który Allegro właśnie przyjęło. Bez tego kolejny cykl
                    // porównuje nowy status ze starym i wysyła dokładnie to samo jeszcze raz.
                    order.RealizeStatus = status;
                    await _orderRepo.UpdateRealizeStatus(order.Id, status);

                    _logger.LogInformation("Updated Order Status to {Status} for Allegro Order {AllegroOrderId}.", status, order.AllegroId);
                    return true;
                }

                LogAllegroErrors(response, await response.Content.ReadAsStringAsync(ct), "status", order.AllegroId);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to update Order Status {AllegroOrderId} in Allegro.", order.AllegroId);
            }

            return false;
        }

        /// <summary>
        /// Wysyła do Allegro numery przesyłek, których Allegro jeszcze nie ma. Zwraca liczbę wysłanych.
        /// </summary>
        private async Task<int> SendTrackingNumbersToAllegro(AllegroOrder order, CancellationToken ct)
        {
            var sent = 0;

            try
            {
                var shipments = GroupShipments(order.Items);
                if (shipments.Count == 0)
                    return 0;

                var known = await GetKnownShipments(order, ct);

                foreach (var shipment in shipments)
                {
                    // Ten numer Allegro już zna - ponowne wysłanie tylko zakładałoby duplikat przesyłki.
                    if (known.Contains(TrackingNumbers.ShipmentKey(shipment.CarrierId, shipment.Waybill)))
                        continue;

                    var request = new AllegroAddTrackingNumberRequest
                    {
                        CarrierId = shipment.CarrierId,
                        Waybill = shipment.Waybill,
                        LineItems = shipment.LineItemIds
                            .Select(id => new AllegroAddTrackingNumberRequest.LineItem { Id = id })
                            .ToList()
                    };

                    var response = await _allegroApiClient.AddOrderTrackingNumber(order.AllegroId, request, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        sent++;
                        known.Add(TrackingNumbers.ShipmentKey(shipment.CarrierId, shipment.Waybill));
                        await _orderRepo.AddSentShipment(order.Id, shipment.CarrierId, shipment.Waybill);

                        _logger.LogInformation("Tracking number {TrackingNumber} ({Carrier}) sent for Allegro order {AllegroOrderId} (items: {Count}).",
                            shipment.Waybill, shipment.CarrierId, order.AllegroId, shipment.LineItemIds.Count);
                    }
                    else
                    {
                        LogAllegroErrors(response, await response.Content.ReadAsStringAsync(ct), "tracking numbers", order.AllegroId);
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to update Order Tracking Number {AllegroOrderId} in Allegro.", order.AllegroId);
            }

            return sent;
        }

        /// <summary>Przesyłka do zgłoszenia w Allegro: jeden numer u jednego przewoźnika i pozycje zamówienia w tej paczce.</summary>
        internal sealed record ShipmentToSend(string CarrierId, string Waybill, IReadOnlyList<string> LineItemIds);

        /// <summary>
        /// Gąska potrafi zwrócić kilka numerów przesyłki w jednym polu, rozdzielonych przecinkiem.
        /// Allegro nie przyjmuje takiego zlepka - każdy numer idzie osobno, z pozycjami, których dotyczy.
        /// </summary>
        internal static List<ShipmentToSend> GroupShipments(IEnumerable<AllegroOrderItem>? items) =>
            (items ?? Enumerable.Empty<AllegroOrderItem>())
                .SelectMany(item => TrackingNumbers.Split(item.ExternalTrackingNumber)
                    .Select(waybill => new
                    {
                        Waybill = waybill,
                        Carrier = TrackingNumbers.NormalizeCarrierId(item.ExternalCourier) ?? string.Empty,
                        item.OrderItemId
                    }))
                .GroupBy(x => TrackingNumbers.ShipmentKey(x.Carrier, x.Waybill))
                .Select(g => new ShipmentToSend(
                    g.First().Carrier,
                    g.First().Waybill,
                    g.Select(x => x.OrderItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList()))
                .ToList();

        /// <summary>
        /// Numery przesyłek, które Allegro już ma. Przy pierwszym przetwarzaniu zamówienia pytamy
        /// o nie Allegro - zamówienie mogło dostać przesyłki wcześniej (także z tego serwisu, zanim
        /// pamiętał, co wysłał). Potem wystarczy zapis w bazie.
        /// </summary>
        private async Task<HashSet<string>> GetKnownShipments(AllegroOrder order, CancellationToken ct)
        {
            var known = (await _orderRepo.GetSentShipments(order.Id))
                .Select(s => TrackingNumbers.ShipmentKey(s.CarrierId, s.Waybill))
                .ToHashSet(StringComparer.Ordinal);

            if (order.ShipmentsCheckedAt != null)
                return known;

            try
            {
                var response = await _allegroApiClient.GetOrderShipments(order.AllegroId, ct);

                if (response == null)
                {
                    // Null przy nieudanej odpowiedzi - pusta lista znaczyłaby "brak przesyłek", a po oznaczeniu
                    // zamówienia jako sprawdzone nigdy byśmy tego nie zweryfikowali.
                    _logger.LogWarning("Reading shipments of Allegro order {AllegroOrderId} returned no data - the order stays unreconciled.", order.AllegroId);
                    return known;
                }

                foreach (var shipment in response.Shipments ?? new List<AllegroShipmentsResponse.Shipment>())
                {
                    if (string.IsNullOrWhiteSpace(shipment.Waybill))
                        continue;

                    var carrier = shipment.CarrierId ?? string.Empty;
                    known.Add(TrackingNumbers.ShipmentKey(carrier, shipment.Waybill));
                    await _orderRepo.AddSentShipment(order.Id, carrier, shipment.Waybill);
                }

                await _orderRepo.MarkShipmentsChecked(order.Id);
                order.ShipmentsCheckedAt = DateTime.UtcNow;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Bez tej listy wyślemy numer, który może już tam być - gorsze byłoby pominięcie wysyłki.
                _logger.LogWarning(ex, "Reading shipments of Allegro order {AllegroOrderId} failed.", order.AllegroId);
            }

            return known;
        }

        private void LogAllegroErrors(HttpResponseMessage response, string body, string action, string orderId)
        {
            try
            {
                var errorResponse = JsonSerializer.Deserialize<AllegroErrorResponse>(body);
                if (errorResponse?.Errors != null)
                {
                    foreach (var err in errorResponse.Errors)
                    {
                        _logger.LogError("Failed to update {Action} for order {Order}: Code={Code}, Message={Message}, UserMessage={UserMessage}, Path={Path}, Details={Details}",
                             action, orderId, err.Code, err.Message, err.UserMessage ?? "N/A", err.Path ?? "N/A", err.Details ?? "N/A");
                    }
                }
                else
                {
                    _logger.LogError($"Offer error {response.StatusCode}: {body}");
                }
            }
            catch (Exception exParse)
            {
                _logger.LogError(exParse, $"Failed to parse Allegro error ({response.StatusCode}) while  offer for. Body={body}");
            }
        }

        private AllegroOrder MapAllegroOrderToModel(AllegroGetOrdersResponse.CheckoutForm allegroOrder, Dictionary<string, string> offerShippingRates)
        {
            var address = allegroOrder.Delivery?.Address ?? allegroOrder.Buyer?.Address;
            var fulfillment = allegroOrder.Fulfillment;

            string street = address?.Street?.Trim() ?? "";
            string city = address?.City?.Trim() ?? "";

            // Detect and split street if it contains a city and "ul." pattern
            if (!string.IsNullOrEmpty(street))
            {
                // Regex matches: "something (ul\.?|ulica\.?) rest"
                var match = Regex.Match(street, @"^(?<city>.+?)\s+(?:ul\.?|ulica\.?)\s*(?<street>.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    city = match.Groups["city"].Value.Trim();
                    street = match.Groups["street"].Value.Trim();
                }
            }

            return new AllegroOrder
            {
                AllegroId = allegroOrder.Id,
                MessageToSeller = allegroOrder.MessageToSeller,
                Note = allegroOrder.Note?.Text,
                Status = allegroOrder.Status,
                RealizeStatus = fulfillment.Status,
                ClientNickname = allegroOrder.Buyer?.Login?.Trim(),
                RecipientFirstName = (address?.FirstName ?? allegroOrder.Buyer?.FirstName ?? "").Trim(),
                RecipientLastName = (address?.LastName ?? allegroOrder.Buyer?.LastName ?? "").Trim(),
                RecipientStreet = street,
                RecipientCity = city,
                RecipientPostalCode = address?.ZipCode?.Trim() ?? address?.PostCode?.Trim() ?? "",
                RecipientCountry = address?.CountryCode?.Trim() ?? "",
                RecipientCompanyName = address?.CompanyName ?? allegroOrder.Buyer?.CompanyName,
                RecipientEmail = allegroOrder.Buyer?.Email,
                RecipientPhoneNumber = (address?.PhoneNumber ?? allegroOrder.Buyer?.PhoneNumber).Trim(),
                DeliveryMethodId = allegroOrder.Delivery?.Method?.Id,
                DeliveryMethodName = allegroOrder.Delivery?.Method?.Name?.ToUpper() ?? "",
                IntegrationCompany = IntegrationCompany.Gaska,
                Account = AllegroAccount.Aduos,
                CancellationDate = allegroOrder.Delivery?.Cancellation?.Date,
                Amount = decimal.TryParse(allegroOrder.Summary?.TotalToPay?.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt) ? amt : 0m,
                CreatedAt = allegroOrder.LineItems?.Max(i => (DateTime?)i.BoughtAt) ?? default,
                Revision = allegroOrder.Revision,
                PaymentType = allegroOrder.Payment.Type,
                Items = allegroOrder.LineItems?.Select(item =>
                {
                    // Default quantity from item
                    int quantity = item.Quantity;

                    // Check for productSet and multiply quantities if available
                    var productQuantity = item.Offer?.ProductSet?.Products?.FirstOrDefault()?.Quantity;
                    if (productQuantity.HasValue)
                    {
                        quantity = quantity * productQuantity.Value;
                    }

                    offerShippingRates.TryGetValue(item.Offer.Id, out var shippingRate);

                    return new AllegroOrderItem
                    {
                        ExternalId = item.Offer?.External?.Id,
                        Quantity = quantity,
                        PriceGross = item.Price?.Amount,
                        Currency = item.Price?.Currency,
                        OfferName = item.Offer?.Name,
                        OfferId = item.Offer?.Id,
                        OrderItemId = item.Id,
                        ShippingRate = shippingRate,
                        BoughtAt = item.BoughtAt,
                    };
                }).ToList() ?? new List<AllegroOrderItem>()
            };
        }

        private AllegroOrderStatus MapGaskaStatusToAllegro(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return AllegroOrderStatus.PROCESSING;

            // Normalize input for case-insensitive matching
            status = status.Trim().ToLowerInvariant();

            return status switch
            {
                "spakowane" or "czeka na kuriera" or "zrealizowane" => AllegroOrderStatus.READY_FOR_SHIPMENT,
                "wysłane" or "w drodze" => AllegroOrderStatus.SENT,
                "dostarczone" => AllegroOrderStatus.PICKED_UP,
                "w realizacji" => AllegroOrderStatus.PROCESSING,
                _ => AllegroOrderStatus.PROCESSING
            };
        }
    }
}
