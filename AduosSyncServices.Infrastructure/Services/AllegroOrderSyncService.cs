using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.DTOs.Allegro.GaskaApi;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
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
        private readonly IAllegroApiClient _allegroApiClient;
        private readonly IGaskaApiClient _gaskaApiClient;

        public AllegroOrderSyncService(ILogger<AllegroOrderSyncService> logger, IOrderRepository orderRepo, IAllegroApiClient allegroApiClient, IGaskaApiClient gaskaApiClient)
        {
            _logger = logger;
            _orderRepo = orderRepo;
            _allegroApiClient = allegroApiClient;
            _gaskaApiClient = gaskaApiClient;
        }

        public async Task SyncOrdersFromAllegro(string allegroDeliveryNames, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            const int limit = 100;
            int offset = 0, totalFetched = 0;
            const string minBoughtFrom = "2026-02-11T00:00:00Z";
            var minBoughtDate = DateTime.Parse(minBoughtFrom, null, DateTimeStyles.AdjustToUniversal);
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var boughtDate = sevenDaysAgo < minBoughtDate ? minBoughtDate : sevenDaysAgo;
            string boughtDateIso = boughtDate.ToString("yyyy-MM-ddTHH:mm:ssZ");

            try
            {
                progress?.Report("Pobieranie stawek dostawy z Allegro...");
                var shippingRates = await _allegroApiClient.GetShippingRates(ct);
                var deliveryNames = (allegroDeliveryNames ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var gaskaShippingRateIds = shippingRates.ShippingRates
                    .Where(sr => deliveryNames.Contains(sr.Name))
                    .Select(sr => sr.Id)
                    .ToHashSet();

                if (gaskaShippingRateIds.Count == 0)
                {
                    _logger.LogError("Failed to find Allegro Shipping Rate IDs for delivery methods '{DeliveryName}'. Aborting order sync.", allegroDeliveryNames);
                    progress?.Report($"Nie znaleziono stawek dostawy dla metod '{allegroDeliveryNames}'. Przerwano synchronizację.");
                    return;
                }

                while (true)
                {
                    progress?.Report($"Pobieranie zamówień z Allegro (offset {offset})...");
                    var response = await _allegroApiClient.GetOrders(new AllegroGetOrdersRequest
                    {
                        Limit = limit,
                        Offset = offset,
                        DateFrom = boughtDateIso
                    }, ct);

                    var orders = response?.CheckoutForms ?? Enumerable.Empty<AllegroGetOrdersResponse.CheckoutForm>();
                    if (!orders.Any())
                        break;

                    var semaphore = new SemaphoreSlim(5);

                    foreach (var order in orders)
                    {
                        try
                        {
                            var offerTasks = order.LineItems.Select(async item =>
                            {
                                await semaphore.WaitAsync(ct);
                                try
                                {
                                    return await _allegroApiClient.GetMinimalOfferInfo(item.Offer.Id, ct);
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }).ToList();

                            var offers = await Task.WhenAll(offerTasks);

                            var shippingRateNames = shippingRates.ShippingRates
                                .Where(sr => deliveryNames.Contains(sr.Name)) // optional: only keep your delivery names
                                .ToDictionary(sr => sr.Id, sr => sr.Name, StringComparer.OrdinalIgnoreCase);

                            var offerShippingRates = offers.ToDictionary(
                                o => o.Id,
                                o => shippingRateNames.TryGetValue(o.Delivery.ShippingRates.Id, out var name) ? name : "Unknown"
                            );

                            // Check if all items use the Gąska shipping method
                            bool allItemsUseGaska = offerShippingRates.Values.All(name => deliveryNames.Contains(name));

                            if (!allItemsUseGaska)
                            {
                                _logger.LogInformation("Skipping order {OrderId} - not all items use {ShippingRate} shipping method.", order.Id, allegroDeliveryNames);
                                continue;
                            }

                            // Save the order
                            var model = MapAllegroOrderToModel(order, offerShippingRates);
                            await _orderRepo.SaveAllegroOrder(model);
                            _logger.LogInformation("Order {OrderId} synced from Allegro.", order.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to sync order {OrderId} from Allegro.", order.Id);
                        }
                    }

                    totalFetched += orders.Count();
                    _logger.LogInformation("Fetched {Count} orders from Allegro so far.", totalFetched);
                    progress?.Report($"Pobrano {totalFetched} zamówień z Allegro...");

                    offset += limit;
                    if (totalFetched >= response.TotalCount) break;
                }

                _logger.LogInformation("Finished syncing {Count} orders from Allegro.", totalFetched);
                progress?.Report($"Zakończono synchronizację {totalFetched} zamówień z Allegro.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync orders from Allegro.");
                progress?.Report($"Błąd podczas synchronizacji zamówień z Allegro: {ex.Message}");
            }
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
                    var gaskaItem = gaskaOrderResponse.Order.Items.FirstOrDefault(i => i.Id == item.ProductId);
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
            try
            {
                var orders = await _orderRepo.GetOrdersToUpdateInAllegro();
                progress?.Report($"Aktualizowanie {orders.Count} zamówień w Allegro...");
                foreach (var order in orders)
                {
                    // --- Update Order Status ---
                    try
                    {
                        var status = MapGaskaStatusToAllegro(order.ExternalOrderStatus);
                        if (status != order.RealizeStatus && status != AllegroOrderStatus.NEW)
                        {
                            var statusRequest = new AllegroSetOrderStatusRequest
                            {
                                Status = status
                            };

                            var response = await _allegroApiClient.UpdateOrderStatus(order.AllegroId, statusRequest, ct);
                            if (!response.IsSuccessStatusCode)
                            {
                                var body = await response.Content.ReadAsStringAsync(ct);
                                LogAllegroErrors(response, body, "status", order.AllegroId);
                            }
                            else
                            {
                                _logger.LogInformation("Updated Order Status to {Status} for Allegro Order {AllegroOrderId}.", status, order.AllegroId);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No status change for Allegro order {AllegroOrderId}.", order.AllegroId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update Order Status {AllegroOrderId} in Allegro.", order.AllegroId);
                    }

                    // --- Update Tracking Number ---
                    try
                    {
                        var items = order.Items;
                        if (items == null || !items.Any())
                            continue;

                        // Group items by tracking number (ignore those without tracking)
                        var groups = items
                            .Where(i => !string.IsNullOrWhiteSpace(i.ExternalTrackingNumber))
                            .GroupBy(i => i.ExternalTrackingNumber);

                        foreach (var group in groups)
                        {
                            var trackingNumber = group.Key;
                            var lineItems = group.Select(i => new AllegroAddTrackingNumberRequest.LineItem
                            {
                                Id = i.OrderItemId
                            }).ToList();

                            var carrierId = group.First().ExternalCourier;

                            carrierId = carrierId?.ToUpperInvariant() switch
                            {
                                var s when s.Contains("DPD") => "DPD",
                                var s when s.Contains("FEDEX") => "FEDEX",
                                var s when s.Contains("GLS") => "GLS",
                                _ => carrierId
                            };

                            var request = new AllegroAddTrackingNumberRequest
                            {
                                CarrierId = carrierId,
                                Waybill = trackingNumber,
                                LineItems = lineItems
                            };

                            var response = await _allegroApiClient.AddOrderTrackingNumber(order.AllegroId, request, ct);

                            if (!response.IsSuccessStatusCode)
                            {
                                var body = await response.Content.ReadAsStringAsync(ct);
                                LogAllegroErrors(response, body, "tracking numbers", order.AllegroId);
                            }
                            else
                            {
                                _logger.LogInformation("Updated tracking number {TrackingNumber} for Allegro Order {AllegroOrderId} (Items: {Count}).", trackingNumber, order.AllegroId, lineItems.Count);
                            }
                        }

                        if (!groups.Any())
                        {
                            _logger.LogInformation("No tracking numbers found for Allegro order {AllegroOrderId}.", order.AllegroId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update Order Tracking Number {AllegroOrderId} in Allegro.", order.AllegroId);
                    }
                }
                progress?.Report("Zakończono aktualizację zamówień w Allegro.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update orders in Allegro.");
                progress?.Report($"Błąd podczas aktualizacji zamówień w Allegro: {ex.Message}");
            }
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

            string street = address.Street?.Trim() ?? "";
            string city = address.City?.Trim() ?? "";

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
                RecipientFirstName = (address.FirstName ?? allegroOrder.Buyer?.FirstName).Trim(),
                RecipientLastName = (address.LastName ?? allegroOrder.Buyer?.LastName).Trim(),
                RecipientStreet = street,
                RecipientCity = city,
                RecipientPostalCode = address?.ZipCode?.Trim() ?? address?.PostCode.Trim(),
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
