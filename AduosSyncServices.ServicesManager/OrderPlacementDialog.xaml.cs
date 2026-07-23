using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Extensions;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.OrderPlacement;
using AduosSyncServices.Infrastructure.Helpers;
using AduosSyncServices.ServicesManager.Helpers;
using AduosSyncServices.ServicesManager.Models;
using AduosSyncServices.ServicesManager.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace AduosSyncServices.ServicesManager
{
    public partial class OrderPlacementDialog : Window
    {
        private record CourierOption(string Display, GaskaDeliveryCourier Value);
        private class CodAmountRow
        {
            public int AllegroOrderId { get; set; }
            public string ClientName { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }
        private record LineItemRow(string? ImageUrl, string ClientName, string Code, string OfferName, int Quantity, string Unit, string DeliveryTypeDisplay);

        private static string GetClientName(AllegroOrder order)
        {
            var name = $"{order.RecipientFirstName} {order.RecipientLastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? "-" : name;
        }

        private readonly IGaskaOrderPlacementService _placementService;
        private readonly IProductRepository _productRepository;
        private readonly List<AllegroOrder> _orders;
        private readonly DialogService _dialogService = new();
        private readonly ObservableCollection<StockCheckItemViewModel> _stockCheckItems = new();
        private readonly List<CodAmountRow> _codRows;
        private GaskaDeliveryCourier? _selectedCourier;

        public OrderPlacementDialog(IGaskaOrderPlacementService placementService, IProductRepository productRepository, List<AllegroOrder> orders)
        {
            InitializeComponent();

            _placementService = placementService;
            _productRepository = productRepository;
            _orders = orders;

            IcStockCheck.ItemsSource = _stockCheckItems;

            _codRows = _orders.Select(o => new CodAmountRow
            {
                AllegroOrderId = o.Id,
                ClientName = GetClientName(o),
                Amount = o.Amount
            }).ToList();
            IcCodAmounts.ItemsSource = _codRows;

            // Set here rather than via IsChecked="True" in XAML: InitializeComponent() assigns named
            // fields top-to-bottom as it parses, and setting IsChecked in XAML fires Checked (and thus
            // Target_Checked -> PopulateCourierOptions -> CbCourier) synchronously mid-parse, before
            // CbCourier - declared further down in the markup - has been assigned to its field yet.
            RbHeadquarters.IsChecked = true;
        }

        private List<LineItemRow> BuildLineItems(Dictionary<int, Product>? productsById) =>
            _orders
                .SelectMany(o => o.Items.Select(i =>
                {
                    Product? product = null;
                    productsById?.TryGetValue(i.ProductId, out product);
                    return new LineItemRow(
                        ImageHelper.GetFirstImageFile(ImageHelper.DefaultImagesFolder, i.ProductId),
                        GetClientName(o),
                        product?.Code ?? "-",
                        i.OfferName,
                        i.Quantity,
                        product?.Unit ?? "-",
                        OrderItemRowViewModel.FormatDeliveryType(product?.DeliveryType));
                }))
                .ToList();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // DgLineItems.ItemsSource is assigned exactly once, here, after codes are resolved (rather
            // than with a placeholder in the constructor followed by a swap) - DataGrid's Width="Auto"
            // columns size themselves once against whatever content they're first given and don't
            // re-measure on a later ItemsSource swap, so an initial narrow placeholder ("...") would
            // permanently lock the Kod column too narrow for the real codes once they loaded.
            Dictionary<int, Product>? productsById = null;
            try
            {
                var productIds = _orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
                var products = await _productRepository.GetProductsByIdsAsync(productIds, CancellationToken.None);
                productsById = products.ToDictionary(p => p.Id);
            }
            catch
            {
                // Best-effort: fall through with productsById == null, showing "-" for Code/Jm and no
                // delivery type - it isn't required for placing the order itself.
            }

            DgLineItems.ItemsSource = BuildLineItems(productsById);
        }

        private void Target_Checked(object sender, RoutedEventArgs e)
        {
            PopulateCourierOptions();
            UpdateCodVisibility();
            UpdateSubmitButtonLabel();
            UpdateSummary();
        }

        private void PopulateCourierOptions()
        {
            var isHeadquarters = RbHeadquarters.IsChecked == true;
            var previouslySelected = _selectedCourier;

            var options = Enum.GetValues<GaskaDeliveryCourier>()
                .Where(c => !isHeadquarters || c.IsAvailableForHeadquarters())
                .Select(c => new CourierOption(c.GetDescription(), c))
                .ToList();

            CbCourier.ItemsSource = options;

            var matching = options.FirstOrDefault(o => o.Value == previouslySelected);
            CbCourier.SelectedItem = matching ?? options.FirstOrDefault();
        }

        private void CbCourier_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCourier = (CbCourier.SelectedItem as CourierOption)?.Value;
            UpdateCodVisibility();
            UpdateSummary();
        }

        private void UpdateCodVisibility()
        {
            var isHeadquarters = RbHeadquarters.IsChecked == true;
            var requiresCod = _selectedCourier?.RequiresCodAmount() == true;
            CodPanel.Visibility = (!isHeadquarters && requiresCod) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateSubmitButtonLabel()
        {
            BtnSubmit.Content = RbHeadquarters.IsChecked == true
                ? "Złóż zamówienie na adres siedziby"
                : "Złóż zamówienie na adresy klientów";
        }

        private void UpdateSummary()
        {
            var itemCount = _orders.Sum(o => o.Items.Count);
            var courierText = _selectedCourier.HasValue ? _selectedCourier.Value.GetDescription() : "(nie wybrano)";
            var orderWord = PolishText.Count(_orders.Count, "zamówienie", "zamówienia", "zamówień");
            var itemWord = PolishText.Count(itemCount, "pozycję", "pozycje", "pozycji");
            SummaryText.Text = $"Wybrano {_orders.Count} {orderWord}, {itemCount} {itemWord}. Metoda dostawy: {courierText}.";
        }

        private void SetBusy(bool isBusy)
        {
            BusyOverlay.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            BtnSubmit.IsEnabled = !isBusy;
            BtnCancel.IsEnabled = !isBusy;
            RbHeadquarters.IsEnabled = !isBusy;
            RbCustomers.IsEnabled = !isBusy;
            CbCourier.IsEnabled = !isBusy;
            IcCodAmounts.IsEnabled = !isBusy;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCourier == null)
            {
                _dialogService.ShowWarning("Wybierz metodę dostawy.");
                return;
            }

            var isHeadquarters = RbHeadquarters.IsChecked == true;
            var courier = _selectedCourier.Value;

            if (isHeadquarters && !courier.IsAvailableForHeadquarters())
            {
                _dialogService.ShowWarning("Wybrana metoda dostawy jest dostępna tylko przy wysyłce do klientów.");
                return;
            }

            var confirmMessage = isHeadquarters
                ? $"Złożyć jedno zamówienie u dostawcy na adres siedziby dla {_orders.Count} {PolishText.Count(_orders.Count, "zamówienia", "zamówień", "zamówień")}, kurier: {courier.GetDescription()}?"
                : $"Złożyć {_orders.Count} {PolishText.Count(_orders.Count, "osobne zamówienie", "osobne zamówienia", "osobnych zamówień")} u dostawcy na adresy klientów, kurier: {courier.GetDescription()}?";

            if (!_dialogService.Confirm(confirmMessage))
                return;

            SetBusy(true);
            _stockCheckItems.Clear();
            StockCheckPanel.Visibility = Visibility.Visible;
            BusyStatusText.Text = "Sprawdzanie dostępności produktów w Gąsce...";

            try
            {
                var itemProgress = StockCheckItemViewModel.CreateCollectionProgress(_stockCheckItems);
                var stockCheck = await _placementService.CheckStockAsync(_orders, itemProgress);

                // Shortages no longer sink the whole batch: only the orders that don't fit in stock
                // are skipped (not sent to Gąska, Allegro status untouched); the rest is placed after
                // an explicit confirmation.
                var ordersToPlace = _orders;
                var skippedOrders = new List<AllegroOrder>();
                if (!stockCheck.IsSuccessful)
                {
                    skippedOrders = _orders.Where(o => stockCheck.ShortagesByOrderId.ContainsKey(o.Id)).ToList();
                    var feasible = _orders.Where(o => !stockCheck.ShortagesByOrderId.ContainsKey(o.Id)).ToList();

                    if (feasible.Count == 0)
                    {
                        var shortageDetails = string.Join("\n", stockCheck.Shortages.Select(s =>
                            $"{s.ProductName} ({s.ProductCode}): potrzeba {s.RequestedQty}, dostępne {s.AvailableQty}"));
                        _dialogService.ShowWarning($"Niewystarczający stan magazynowy w Gąsce:\n{shortageDetails}", "Brak dostępności");
                        return;
                    }

                    var skippedDetails = string.Join("\n", skippedOrders.Select(o =>
                        $"• {o.AllegroId}: " + string.Join("; ", stockCheck.ShortagesByOrderId[o.Id].Select(s =>
                            $"{s.ProductName} ({s.ProductCode}) - potrzeba {s.RequestedQty}, dostępne {s.AvailableQty}"))));

                    if (!_dialogService.Confirm(
                        $"Braki magazynowe w Gąsce blokują {skippedOrders.Count} z {_orders.Count} zamówień:\n{skippedDetails}\n\n" +
                        $"Zamówienia z brakami zostaną pominięte (nie zostaną złożone ani oznaczone jako realizowane). Złożyć pozostałe: {feasible.Count}?",
                        "Częściowe braki magazynowe"))
                    {
                        return;
                    }

                    ordersToPlace = feasible;
                }

                StockCheckPanel.Visibility = Visibility.Collapsed;
                var statusProgress = new Progress<string>(message => BusyStatusText.Text = message);

                if (isHeadquarters)
                {
                    // skipStockCheck: the CheckStockAsync above already validated every product (and
                    // drove the visible checklist) - re-checking inside the service would repeat all
                    // the rate-limited Gąska product calls.
                    var result = await _placementService.PlaceHeadquartersOrderAsync(ordersToPlace, courier, skipStockCheck: true, statusProgress);
                    result.Warnings.InsertRange(0, skippedOrders.Select(o => $"Pominięto zamówienie {o.AllegroId} z powodu braków magazynowych."));

                    if (result.IsSuccessful)
                    {
                        var message = result.GaskaOrderNumbers.Count > 0
                            ? $"Złożono zamówienie w Gąsce (nr: {string.Join(", ", result.GaskaOrderNumbers)})."
                            : "Zamówienie zostało wysłane do Gąski, ale nie udało się pobrać numeru zamówienia (patrz uwagi poniżej).";

                        if (result.Warnings.Count > 0)
                        {
                            message += "\n\nUwaga:\n" + string.Join("\n", result.Warnings);
                            _dialogService.ShowWarning(message, "Złożono z zastrzeżeniami");
                        }
                        else
                        {
                            _dialogService.ShowInfo(message, "Sukces");
                        }

                        DialogResult = true;
                    }
                    else
                    {
                        _dialogService.ShowError(result.ErrorMessage ?? "Nieznany błąd podczas składania zamówienia.");
                    }
                }
                else
                {
                    var codAmounts = _codRows.ToDictionary(r => r.AllegroOrderId, r => r.Amount);
                    var result = await _placementService.PlaceCustomerOrdersAsync(ordersToPlace, courier, codAmounts, skipStockCheck: true, statusProgress);

                    // Surface the stock-skipped orders in the same summary as real failures.
                    foreach (var skipped in skippedOrders)
                        result.Results.Add(new CustomerOrderPlacementResult { AllegroOrderId = skipped.Id, IsSuccessful = false, ErrorMessage = "Pominięto z powodu braków magazynowych w Gąsce." });

                    var succeeded = result.Results.Where(r => r.IsSuccessful).ToList();
                    var failed = result.Results.Where(r => !r.IsSuccessful).ToList();

                    if (failed.Count == 0)
                    {
                        var orderNumbers = succeeded.Where(r => !string.IsNullOrEmpty(r.GaskaOrderNumber)).Select(r => r.GaskaOrderNumber).ToList();
                        var succeededWord = PolishText.Count(succeeded.Count, "zamówienie", "zamówienia", "zamówień");
                        var message = orderNumbers.Count > 0
                            ? $"Złożono {succeeded.Count} {succeededWord} w Gąsce (nr: {string.Join(", ", orderNumbers)})."
                            : $"Złożono {succeeded.Count} {succeededWord} w Gąsce.";
                        var warnings = succeeded.Where(r => !string.IsNullOrEmpty(r.Warning)).Select(r => r.Warning).ToList();

                        if (warnings.Count > 0)
                        {
                            message += "\n\nUwaga:\n" + string.Join("\n", warnings);
                            _dialogService.ShowWarning(message, "Złożono z zastrzeżeniami");
                        }
                        else
                        {
                            _dialogService.ShowInfo(message, "Sukces");
                        }

                        DialogResult = true;
                    }
                    else
                    {
                        var details = string.Join("\n", failed.Select(f =>
                        {
                            var allegroId = _orders.FirstOrDefault(o => o.Id == f.AllegroOrderId)?.AllegroId ?? f.AllegroOrderId.ToString();
                            return $"Zamówienie {allegroId}: {f.ErrorMessage}";
                        }));

                        var totalWord = PolishText.Count(result.Results.Count, "zamówienia", "zamówień", "zamówień");
                        _dialogService.ShowWarning($"Złożono {succeeded.Count}/{result.Results.Count} {totalWord}. Błędy:\n{details}", "Częściowy sukces");
                        DialogResult = succeeded.Count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Błąd podczas składania zamówienia: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }
    }
}
