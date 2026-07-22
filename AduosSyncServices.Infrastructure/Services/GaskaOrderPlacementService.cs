using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.DTOs.GaskaApi;
using AduosSyncServices.Contracts.Extensions;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.OrderPlacement;
using System.Globalization;

namespace AduosSyncServices.Infrastructure.Services
{
    public class GaskaOrderPlacementService : IGaskaOrderPlacementService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IGaskaApiClient _gaskaApiClient;
        private readonly IAllegroApiClient _allegroApiClient;
        private readonly int _productIntervalSeconds;

        // productIntervalSeconds mirrors GaskaApiCredentials.ProductInterval, the same per-product delay
        // GaskaApiService already applies when fetching single products from Gąska - CheckStockAsync hits
        // the identical GetProduct endpoint once per distinct product and is subject to the same rate limit.
        public GaskaOrderPlacementService(IOrderRepository orderRepository, IProductRepository productRepository, IGaskaApiClient gaskaApiClient, IAllegroApiClient allegroApiClient, int productIntervalSeconds)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _gaskaApiClient = gaskaApiClient;
            _allegroApiClient = allegroApiClient;
            _productIntervalSeconds = productIntervalSeconds;
        }

        public async Task<StockCheckResult> CheckStockAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            IProgress<StockCheckProgressItem>? itemProgress = null,
            CancellationToken ct = default)
        {
            var items = orders.SelectMany(o => o.Items).ToList();
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _productRepository.GetProductsByIdsAsync(productIds, ct);
            var productsById = products.ToDictionary(p => p.Id);

            var requestedByIntegrationId = new Dictionary<int, (float Qty, Product Product)>();
            foreach (var item in items)
            {
                if (!productsById.TryGetValue(item.ProductId, out var product))
                    continue;

                requestedByIntegrationId[product.IntegrationId] = requestedByIntegrationId.TryGetValue(product.IntegrationId, out var existing)
                    ? (existing.Qty + item.Quantity, product)
                    : (item.Quantity, product);
            }

            var shortages = new List<StockShortage>();
            var remaining = requestedByIntegrationId.Count;

            foreach (var (integrationId, (requestedQty, product)) in requestedByIntegrationId)
            {
                itemProgress?.Report(new StockCheckProgressItem
                {
                    ProductCode = product.Code,
                    ProductName = product.Name,
                    Status = StockCheckItemStatus.Checking,
                    RequestedQty = requestedQty
                });

                var response = await _gaskaApiClient.GetProduct(integrationId, "pl", ct);
                var availableQty = response?.Product?.InStock ?? 0f;
                var isAvailable = availableQty >= requestedQty;

                if (!isAvailable)
                {
                    shortages.Add(new StockShortage
                    {
                        ProductCode = product.Code,
                        ProductName = product.Name,
                        RequestedQty = requestedQty,
                        AvailableQty = availableQty
                    });
                }

                itemProgress?.Report(new StockCheckProgressItem
                {
                    ProductCode = product.Code,
                    ProductName = product.Name,
                    Status = isAvailable ? StockCheckItemStatus.Available : StockCheckItemStatus.Insufficient,
                    RequestedQty = requestedQty,
                    AvailableQty = availableQty
                });

                // Rate-limit pause between product lookups, but not after the last one - there's
                // nothing left to throttle and it would just delay the result for the caller.
                if (--remaining > 0)
                    await Task.Delay(TimeSpan.FromSeconds(_productIntervalSeconds), ct);
            }

            return new StockCheckResult { IsSuccessful = shortages.Count == 0, Shortages = shortages };
        }

        public async Task<HeadquartersOrderPlacementResult> PlaceHeadquartersOrderAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            GaskaDeliveryCourier courier,
            bool skipStockCheck = false,
            IProgress<string>? statusProgress = null,
            CancellationToken ct = default)
        {
            var alreadyOrdered = orders.Where(o => o.SentToExternalCompany).ToList();
            if (alreadyOrdered.Count > 0)
            {
                var offending = string.Join(", ", alreadyOrdered.Select(o => o.AllegroId));
                return Failure($"Następujące zamówienia zostały już złożone u dostawcy i nie mogą zostać złożone ponownie: {offending}.");
            }

            if (!courier.IsAvailableForHeadquarters())
                return Failure("Wybrana metoda dostawy jest dostępna wyłącznie przy wysyłce bezpośrednio do klientów.");

            if (!skipStockCheck)
            {
                statusProgress?.Report("Sprawdzanie dostępności produktów...");
                var stockCheck = await CheckStockAsync(orders, null, ct);
                if (!stockCheck.IsSuccessful)
                    return Failure(BuildShortageMessage(stockCheck.Shortages));
            }

            statusProgress?.Report("Pobieranie domyślnego adresu dostawy...");
            var addresses = await _gaskaApiClient.GetDeliveryAddresses(ct);
            var defaultAddress = addresses?.AdressDetails?.FirstOrDefault(a => a.Default);
            if (defaultAddress == null)
                return Failure("Brak domyślnego adresu dostawy skonfigurowanego w Gąsce (deliveryAddressDefault).");

            var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
            var products = await _productRepository.GetProductsByIdsAsync(productIds, ct);
            var productsById = products.ToDictionary(p => p.Id);

            var aggregatedQtyByIntegrationId = new Dictionary<int, int>();
            foreach (var item in orders.SelectMany(o => o.Items))
            {
                if (!productsById.TryGetValue(item.ProductId, out var product))
                    continue;

                aggregatedQtyByIntegrationId[product.IntegrationId] = aggregatedQtyByIntegrationId.TryGetValue(product.IntegrationId, out var qty)
                    ? qty + item.Quantity
                    : item.Quantity;
            }

            var request = new GaskaCreateOrderRequest
            {
                DeliveryAddressId = defaultAddress.Id,
                DeliveryMethod = courier.GetDescription(),
                CustomerNumber = orders.First().AllegroId,
                Items = aggregatedQtyByIntegrationId
                    .Select(kvp => new GaskaCreateOrderItemRequest { Id = kvp.Key, Qty = kvp.Value.ToString(CultureInfo.InvariantCulture) })
                    .ToList()
            };

            statusProgress?.Report("Składanie zamówienia w Gąsce...");
            GaskaCreateOrderResponse? response;
            try
            {
                response = await _gaskaApiClient.CreateOrder(request, ct);
            }
            catch (Exception ex) when (IsHttpTimeout(ex))
            {
                return await MarkAsOrderedAfterTimeoutAsync(orders, isDropshipping: false, statusProgress, ct);
            }
            catch (Exception ex)
            {
                return Failure($"Błąd podczas składania zamówienia w Gąsce: {ex.Message}");
            }

            if (response == null || response.Result != 0 || response.NewOrders.Count == 0)
                return Failure(response?.Message ?? "Nieznany błąd podczas składania zamówienia w Gąsce.");

            var warnings = new List<string>();
            var primaryGaskaOrderId = response.NewOrders.First();

            statusProgress?.Report("Weryfikowanie złożonego zamówienia w Gąsce...");
            var orderNumbers = new List<string>();
            GaskaGetOrderResponse.OrderDto? primaryOrderDetails = null;
            foreach (var gaskaOrderId in response.NewOrders)
            {
                var (details, error) = await TryGetGaskaOrderAsync(gaskaOrderId, ct);
                if (details != null)
                {
                    orderNumbers.Add(details.OrderNumber);
                    primaryOrderDetails ??= details;
                }
                else if (error != null)
                {
                    warnings.Add(error);
                }
            }

            statusProgress?.Report("Zapisywanie wyniku...");
            foreach (var order in orders)
            {
                await _orderRepository.MarkAsOrderedInExternalCompany(order.Id, primaryGaskaOrderId, isDropshipping: false);

                if (primaryOrderDetails != null)
                {
                    ApplyGaskaOrderDetails(order, primaryOrderDetails);
                    await _orderRepository.UpdateOrderExternalInfo(order);
                }

                statusProgress?.Report($"Aktualizowanie statusu zamówienia {order.AllegroId} w Allegro na PROCESSING...");
                var warning = await UpdateAllegroOrderStatusToProcessingAsync(order, ct);
                if (warning != null)
                    warnings.Add(warning);
            }

            return new HeadquartersOrderPlacementResult
            {
                IsSuccessful = true,
                GaskaOrderIds = response.NewOrders,
                GaskaOrderNumbers = orderNumbers,
                AllegroOrderIds = orders.Select(o => o.Id).ToList(),
                Warnings = warnings
            };
        }

        public async Task<CustomerOrdersPlacementResult> PlaceCustomerOrdersAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            GaskaDeliveryCourier courier,
            IReadOnlyDictionary<int, decimal> codAmountsByAllegroOrderId,
            bool skipStockCheck = false,
            IProgress<string>? statusProgress = null,
            CancellationToken ct = default)
        {
            var result = new CustomerOrdersPlacementResult();

            var alreadyOrdered = orders.Where(o => o.SentToExternalCompany).ToList();
            foreach (var order in alreadyOrdered)
                result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = order.Id, IsSuccessful = false, ErrorMessage = $"Zamówienie {order.AllegroId} zostało już złożone u dostawcy i nie może zostać złożone ponownie." });

            orders = orders.Where(o => !o.SentToExternalCompany).ToList();
            if (orders.Count == 0)
                return result;

            if (courier.RequiresCodAmount())
            {
                var nonCodOrders = orders.Where(o => o.PaymentType != AllegroPaymentType.CASH_ON_DELIVERY).ToList();
                if (nonCodOrders.Count > 0)
                {
                    var offending = string.Join(", ", nonCodOrders.Select(o => o.AllegroId));
                    foreach (var order in orders)
                        result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = order.Id, IsSuccessful = false, ErrorMessage = $"Metoda \"{courier.GetDescription()}\" wymaga, aby wszystkie zamówienia były na pobranie. Zamówienia bez pobrania: {offending}." });
                    return result;
                }
            }
            else
            {
                var codOrders = orders.Where(o => o.PaymentType == AllegroPaymentType.CASH_ON_DELIVERY).ToList();
                if (codOrders.Count > 0)
                {
                    var offending = string.Join(", ", codOrders.Select(o => o.AllegroId));
                    foreach (var order in orders)
                        result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = order.Id, IsSuccessful = false, ErrorMessage = $"Zamówienia na pobranie muszą być wysyłane metodą \"{GaskaDeliveryCourier.FedexDropshippingPobranie.GetDescription()}\". Zamówienia na pobranie: {offending}." });
                    return result;
                }
            }

            if (!skipStockCheck)
            {
                statusProgress?.Report("Sprawdzanie dostępności produktów...");
                var stockCheck = await CheckStockAsync(orders, null, ct);
                if (!stockCheck.IsSuccessful)
                {
                    var message = BuildShortageMessage(stockCheck.Shortages);
                    foreach (var order in orders)
                        result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = order.Id, IsSuccessful = false, ErrorMessage = message });
                    return result;
                }
            }

            var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
            var products = await _productRepository.GetProductsByIdsAsync(productIds, ct);
            var productsById = products.ToDictionary(p => p.Id);

            foreach (var order in orders)
            {
                try
                {
                    statusProgress?.Report($"Tworzenie adresu dostawy dla zamówienia {order.AllegroId}...");

                    var hasCompanyName = !string.IsNullOrWhiteSpace(order.RecipientCompanyName);
                    var addressRequest = new GaskaCreateDeliveryAddressRequest
                    {
                        Name1 = hasCompanyName ? order.RecipientCompanyName! : $"{order.RecipientFirstName} {order.RecipientLastName}",
                        Name2 = hasCompanyName ? $"{order.RecipientFirstName} {order.RecipientLastName}" : null,
                        Street = order.RecipientStreet,
                        City = order.RecipientCity,
                        PostalCode = order.RecipientPostalCode,
                        Country = order.RecipientCountry,
                        Phone = order.RecipientPhoneNumber,
                        Email = ResolveDeliveryEmail(order.RecipientEmail),
                        OneUse = true
                    };

                    var address = await _gaskaApiClient.CreateDeliveryAddress(addressRequest, ct);
                    if (address == null || address.Result != 0)
                    {
                        result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = order.Id, IsSuccessful = false, ErrorMessage = address?.Message ?? "Nie udało się utworzyć adresu dostawy w Gąsce." });
                        continue;
                    }

                    var qtyByIntegrationId = new Dictionary<int, int>();
                    foreach (var item in order.Items)
                    {
                        if (!productsById.TryGetValue(item.ProductId, out var product))
                            continue;

                        qtyByIntegrationId[product.IntegrationId] = qtyByIntegrationId.TryGetValue(product.IntegrationId, out var qty)
                            ? qty + item.Quantity
                            : item.Quantity;
                    }

                    decimal? dropshippingAmount = null;
                    if (courier.RequiresCodAmount())
                    {
                        dropshippingAmount = codAmountsByAllegroOrderId.TryGetValue(order.Id, out var amount) ? amount : order.Amount;
                    }

                    var orderRequest = new GaskaCreateOrderRequest
                    {
                        DeliveryAddressId = address.AddressId,
                        DeliveryMethod = courier.GetDescription(),
                        DropshippingAmount = dropshippingAmount,
                        CustomerNumber = order.AllegroId,
                        Items = qtyByIntegrationId
                            .Select(kvp => new GaskaCreateOrderItemRequest { Id = kvp.Key, Qty = kvp.Value.ToString(CultureInfo.InvariantCulture) })
                            .ToList()
                    };

                    statusProgress?.Report($"Składanie zamówienia dla {order.AllegroId}...");

                    GaskaCreateOrderResponse? orderResponse;
                    try
                    {
                        orderResponse = await _gaskaApiClient.CreateOrder(orderRequest, ct);
                    }
                    catch (Exception ex) when (IsHttpTimeout(ex))
                    {
                        await _orderRepository.MarkAsOrderedInExternalCompany(order.Id, 0, isDropshipping: true);
                        var statusWarning = await UpdateAllegroOrderStatusToProcessingAsync(order, ct);
                        var timeoutWarning = $"Przekroczono czas oczekiwania na odpowiedź z Gąski dla zamówienia {order.AllegroId}. Oznaczono jako złożone, ale bez numeru zamówienia — sprawdź ręcznie w panelu Gąski.";

                        statusProgress?.Report($"Timeout dla zamówienia {order.AllegroId} — oznaczono jako złożone bez potwierdzenia numeru.");
                        result.Results.Add(new CustomerOrderPlacementResult
                        {
                            AllegroOrderId = order.Id,
                            IsSuccessful = true,
                            Warning = statusWarning != null ? $"{timeoutWarning} {statusWarning}" : timeoutWarning
                        });
                        continue;
                    }

                    if (orderResponse == null || orderResponse.Result != 0 || orderResponse.NewOrders.Count == 0)
                    {
                        result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = order.Id, IsSuccessful = false, ErrorMessage = orderResponse?.Message ?? "Nieznany błąd podczas składania zamówienia w Gąsce." });
                        continue;
                    }

                    var gaskaOrderId = orderResponse.NewOrders.First();
                    await _orderRepository.MarkAsOrderedInExternalCompany(order.Id, gaskaOrderId, isDropshipping: true);

                    statusProgress?.Report($"Weryfikowanie zamówienia {order.AllegroId} w Gąsce...");
                    var (details, verifyError) = await TryGetGaskaOrderAsync(gaskaOrderId, ct);
                    string? orderNumber = null;
                    if (details != null)
                    {
                        orderNumber = details.OrderNumber;
                        ApplyGaskaOrderDetails(order, details);
                        await _orderRepository.UpdateOrderExternalInfo(order);
                    }

                    statusProgress?.Report($"Aktualizowanie statusu zamówienia {order.AllegroId} w Allegro na PROCESSING...");
                    var warning = await UpdateAllegroOrderStatusToProcessingAsync(order, ct);
                    var combinedWarning = string.Join(" ", new[] { verifyError, warning }.Where(w => !string.IsNullOrEmpty(w)));

                    statusProgress?.Report(orderNumber != null
                        ? $"Zamówienie {order.AllegroId} złożone (nr Gąska: {orderNumber})."
                        : $"Zamówienie {order.AllegroId} złożone (Gąska #{gaskaOrderId}).");

                    result.Results.Add(new CustomerOrderPlacementResult
                    {
                        AllegroOrderId = order.Id,
                        IsSuccessful = true,
                        GaskaOrderId = gaskaOrderId,
                        GaskaOrderNumber = orderNumber,
                        Warning = string.IsNullOrEmpty(combinedWarning) ? null : combinedWarning
                    });
                }
                catch (Exception ex)
                {
                    statusProgress?.Report($"Błąd dla {order.AllegroId}: {ex.Message}");
                    result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = order.Id, IsSuccessful = false, ErrorMessage = ex.Message });
                }
            }

            return result;
        }

        private async Task<HeadquartersOrderPlacementResult> MarkAsOrderedAfterTimeoutAsync(
            IReadOnlyCollection<AllegroOrder> orders, bool isDropshipping, IProgress<string>? statusProgress, CancellationToken ct)
        {
            statusProgress?.Report("Przekroczono czas oczekiwania na odpowiedź z Gąski. Oznaczanie zamówień jako złożone bez numeru...");

            var warnings = new List<string>
            {
                "Przekroczono czas oczekiwania na odpowiedź z Gąski podczas składania zamówienia. Zamówienie mogło zostać złożone, ale nie otrzymano numeru zamówienia — sprawdź ręcznie w panelu Gąski."
            };

            foreach (var order in orders)
            {
                await _orderRepository.MarkAsOrderedInExternalCompany(order.Id, 0, isDropshipping);

                var warning = await UpdateAllegroOrderStatusToProcessingAsync(order, ct);
                if (warning != null)
                    warnings.Add(warning);
            }

            return new HeadquartersOrderPlacementResult
            {
                IsSuccessful = true,
                GaskaOrderIds = new List<int>(),
                AllegroOrderIds = orders.Select(o => o.Id).ToList(),
                Warnings = warnings
            };
        }

        private static bool IsHttpTimeout(Exception ex) =>
            ex is TaskCanceledException { InnerException: TimeoutException };

        private async Task<string?> UpdateAllegroOrderStatusToProcessingAsync(AllegroOrder order, CancellationToken ct)
        {
            // Manual orders (AllegroId = "MANUAL-{guid}") have no corresponding Allegro order/offer -
            // there's nothing to update on Allegro's side, and calling the API with a synthetic id
            // would just fail and surface a confusing warning after an otherwise-successful placement.
            if (order.Source == OrderSource.Manual)
                return null;

            try
            {
                var statusRequest = new AllegroSetOrderStatusRequest { Status = AllegroOrderStatus.PROCESSING };
                var response = await _allegroApiClient.UpdateOrderStatus(order.AllegroId, statusRequest, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    return $"Nie udało się zaktualizować statusu zamówienia {order.AllegroId} w Allegro na PROCESSING ({response.StatusCode}): {body}";
                }

                return null;
            }
            catch (Exception ex)
            {
                return $"Nie udało się zaktualizować statusu zamówienia {order.AllegroId} w Allegro na PROCESSING: {ex.Message}";
            }
        }

        private static HeadquartersOrderPlacementResult Failure(string message) =>
            new() { IsSuccessful = false, ErrorMessage = message };

        // Called immediately after CreateOrder succeeds, to confirm the order actually exists in Gąska
        // and pull back its order number (customer-facing, unlike the internal Gąska order id) plus
        // status/tracking - the same data UpdateOrderGaskaInfo would otherwise only fetch on its next
        // periodic pass.
        private async Task<(GaskaGetOrderResponse.OrderDto? Order, string? Error)> TryGetGaskaOrderAsync(int gaskaOrderId, CancellationToken ct)
        {
            try
            {
                var response = await _gaskaApiClient.GetOrder(gaskaOrderId, "pl", ct);
                if (response.Result != 0 || response.Order == null)
                    return (null, $"Nie udało się zweryfikować zamówienia w Gąsce (ID {gaskaOrderId}): {response.Message}");

                return (response.Order, null);
            }
            catch (Exception ex)
            {
                return (null, $"Zamówienie zostało złożone w Gąsce (ID {gaskaOrderId}), ale weryfikacja/pobranie szczegółów nie powiodła się: {ex.Message}");
            }
        }

        private static void ApplyGaskaOrderDetails(AllegroOrder order, GaskaGetOrderResponse.OrderDto details)
        {
            order.ExternalDeliveryName = details.Delivery;
            order.ExternalOrderStatus = details.Items.FirstOrDefault()?.RealizeDeliveryStatus;
            order.ExternalOrderNumber = details.OrderNumber;

            foreach (var item in order.Items)
            {
                // Match by Gąska product code (item.ExternalId holds our Product.Code, which is Gąska's
                // code) - NOT by id: gaskaItem.Id is Gąska's own product id (our IntegrationId), while
                // item.ProductId is the local Products.Id PK; the two virtually never coincide, so an
                // id comparison would leave tracking numbers unmatched for every item.
                var gaskaItem = details.Items.FirstOrDefault(i =>
                    string.Equals(i.CodeGaska, item.ExternalId, StringComparison.OrdinalIgnoreCase));
                if (gaskaItem != null)
                {
                    item.ExternalTrackingNumber = gaskaItem.RealizeTrackingNumber;
                    item.ExternalCourier = gaskaItem.RealizeDelivery;
                }
            }
        }

        private static string BuildShortageMessage(List<StockShortage> shortages) =>
            "Niewystarczający stan magazynowy w Gąsce: " +
            string.Join("; ", shortages.Select(s => $"{s.ProductName} ({s.ProductCode}): potrzeba {s.RequestedQty}, dostępne {s.AvailableQty}"));

        // Allegro anonymizes buyer emails behind this relay domain, which doesn't accept real
        // correspondence from the courier - fall back to our own contact address in that case,
        // same as when there's no email on the order at all.
        private const string DefaultDeliveryEmail = "kontakt@agro-aduos.pl";
        private const string AllegroRelayEmailDomain = "@user.allegrogroup.pl";

        private static string ResolveDeliveryEmail(string? recipientEmail) => DefaultDeliveryEmail;
    }
}
