using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Extensions;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.OrderPlacement;
using AduosSyncServices.ServicesManager.Helpers;
using AduosSyncServices.ServicesManager.Models;
using AduosSyncServices.ServicesManager.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace AduosSyncServices.ServicesManager
{
    public partial class AddManualOrderDialog : Window
    {
        private record CourierOption(string Display, GaskaDeliveryCourier Value);

        private class CartItemRow : INotifyPropertyChanged
        {
            public int ProductId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public decimal SalePrice { get; set; }

            // 1 when the product has no PackRequired package - "no packaging constraint" is
            // modeled as a pack size of 1, so the clamping logic below applies uniformly.
            public float PackQty { get; set; } = 1;
            public float MaxStock { get; set; }

            public string? PackQtyHint => PackQty > 1 ? $"Wielokrotność opakowania: {PackQty:0.##}" : null;

            private int _quantity = 1;
            public int Quantity
            {
                get => _quantity;
                set
                {
                    // Only a soft range clamp here (min 1, max stock) - this runs on every keystroke
                    // via UpdateSourceTrigger=PropertyChanged, so rounding to a pack multiple here
                    // (e.g. 1 -> 5) would stomp on digits the user hasn't finished typing yet, making
                    // it impossible to type e.g. "10" over a pack-of-5 default. The pack-multiple is
                    // enforced only on commit - see CommitToPackMultiple, called from the Ilość
                    // TextBox's LostFocus.
                    var stockCap = MaxStock > 0 ? (int)MaxStock : int.MaxValue;
                    var clamped = Math.Max(1, Math.Min(value, stockCap));

                    if (_quantity == clamped)
                        return;

                    _quantity = clamped;
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }

            public void CommitToPackMultiple()
            {
                var packQty = PackQty > 1 ? (int)PackQty : 1;
                if (packQty <= 1)
                    return;

                var stockCap = MaxStock > 0 ? (int)MaxStock : int.MaxValue;

                // Round to the nearest full pack, never below one pack.
                var packs = Math.Max(1, (int)Math.Round(_quantity / (double)packQty, MidpointRounding.AwayFromZero));
                var desired = packs * packQty;

                // If even one pack doesn't fit in stock, fall back to whatever stock allows (can't
                // force a full pack that isn't available) instead of blocking entirely.
                if (desired > stockCap)
                    desired = stockCap >= packQty ? stockCap / packQty * packQty : stockCap;

                Quantity = Math.Max(1, Math.Min(desired, stockCap));
            }

            public decimal LineTotal => SalePrice * Quantity;

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string propertyName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IGaskaOrderPlacementService _placementService;
        private readonly AllegroAccount _account;
        private readonly IntegrationCompany _integrationCompany;
        private readonly DialogService _dialogService = new();
        private readonly ObservableCollection<CartItemRow> _cart = new();
        private readonly ObservableCollection<StockCheckItemViewModel> _stockCheckItems = new();
        private readonly DispatcherTimer _searchDebounceTimer;
        private GaskaDeliveryCourier? _selectedCourier;
        private CancellationTokenSource? _searchCts;
        private string _lastSearchedTerm = string.Empty;
        private List<OrderableProduct> _lastSearchResults = new();
        private bool _hasMoreSearchResults;
        private bool _isLoadingMoreResults;
        private decimal _deliveryCost;
        private bool _codAmountManuallyEdited;
        private bool _isUpdatingCodAmountProgrammatically;

        private const int SearchMinLength = 3;
        private const int SearchPageSize = 50;

        public AddManualOrderDialog(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IGaskaOrderPlacementService placementService,
            AllegroAccount account,
            IntegrationCompany integrationCompany)
        {
            InitializeComponent();

            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _placementService = placementService;
            _account = account;
            _integrationCompany = integrationCompany;

            DgCart.ItemsSource = _cart;
            IcStockCheck.ItemsSource = _stockCheckItems;
            _cart.CollectionChanged += (_, _) => RefreshTotals();

            // Debounce: wait for a pause in typing before querying, instead of hitting the DB on
            // every keystroke - restarted on each TextChanged, only ever fires once typing settles.
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _searchDebounceTimer.Tick += async (_, _) =>
            {
                _searchDebounceTimer.Stop();
                await RunSearchAsync();
            };

            // Set here rather than via Text="0,00" in XAML - a XAML-set Text fires TextChanged
            // synchronously mid-parse, before SummaryText (declared later in the markup) is assigned,
            // so TxtDeliveryCost_TextChanged's UpdateSummary() call would NullReferenceException on it.
            TxtDeliveryCost.Text = "0,00";

            RbWireTransfer.IsChecked = true;

            // Set here rather than via IsChecked="True" in XAML - see the identical comment in
            // OrderPlacementDialog.xaml.cs for why (Target_Checked reads controls declared later in
            // the markup, which aren't assigned yet if this fires synchronously during parsing).
            RbHeadquarters.IsChecked = true;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchCts?.Cancel();

            var term = TxtSearch.Text.Trim();
            if (term.Length < SearchMinLength)
            {
                SearchResultsPopup.IsOpen = false;
                SearchSpinner.Visibility = Visibility.Collapsed;
                // Reset so retyping the same term after clearing the box searches again instead of
                // being silently skipped by the term == _lastSearchedTerm guard below.
                _lastSearchedTerm = string.Empty;
                return;
            }

            _searchDebounceTimer.Start();
        }

        private async void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchResultsPopup.IsOpen = false;
                return;
            }

            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            if (SearchResultsPopup.IsOpen && _lastSearchResults.Count > 0)
            {
                AddProductToCart(_lastSearchResults[0]);
                CloseSearchAfterAdd();
                return;
            }

            // Enter pressed before the debounce timer fired (e.g. very fast typing) - search immediately.
            _searchDebounceTimer.Stop();
            await RunSearchAsync();
        }

        private async Task RunSearchAsync()
        {
            var term = TxtSearch.Text.Trim();
            if (term.Length < SearchMinLength || term == _lastSearchedTerm)
                return;

            _searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _searchCts = cts;

            try
            {
                SearchSpinner.Visibility = Visibility.Visible;
                var results = await _productRepository.SearchOrderableProductsAsync(term, 0, cts.Token);

                if (cts.IsCancellationRequested)
                    return;

                _lastSearchedTerm = term;
                _lastSearchResults = results;
                _hasMoreSearchResults = results.Count == SearchPageSize;
                IcSearchResults.ItemsSource = results;
                NoResultsText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                SearchResultsPopup.IsOpen = true;
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer search - its own results will populate the popup instead.
            }
            catch (Exception ex)
            {
                SearchResultsPopup.IsOpen = false;
                _dialogService.ShowError($"Nie udało się wyszukać produktów: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_searchCts, cts))
                    SearchSpinner.Visibility = Visibility.Collapsed;
            }
        }

        private async void SearchResultsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_hasMoreSearchResults || _isLoadingMoreResults)
                return;

            // Within ~40px of the bottom - load the next page before the user hits the end.
            const double loadMoreThreshold = 40;
            if (e.VerticalOffset + e.ViewportHeight < e.ExtentHeight - loadMoreThreshold)
                return;

            await LoadMoreSearchResultsAsync();
        }

        private async Task LoadMoreSearchResultsAsync()
        {
            var term = _lastSearchedTerm;
            if (string.IsNullOrEmpty(term) || !_hasMoreSearchResults)
                return;

            _isLoadingMoreResults = true;
            LoadMoreSpinner.Visibility = Visibility.Visible;

            try
            {
                var nextPage = await _productRepository.SearchOrderableProductsAsync(term, _lastSearchResults.Count, CancellationToken.None);

                // The search box may have moved on to a different term while this page was loading.
                if (term != _lastSearchedTerm)
                    return;

                _hasMoreSearchResults = nextPage.Count == SearchPageSize;
                if (nextPage.Count == 0)
                    return;

                _lastSearchResults = _lastSearchResults.Concat(nextPage).ToList();
                IcSearchResults.ItemsSource = _lastSearchResults;
            }
            catch (Exception ex)
            {
                _hasMoreSearchResults = false;
                _dialogService.ShowError($"Nie udało się wczytać kolejnych produktów: {ex.Message}");
            }
            finally
            {
                _isLoadingMoreResults = false;
                LoadMoreSpinner.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchResultRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: OrderableProduct product })
                return;

            AddProductToCart(product);
            CloseSearchAfterAdd();
        }

        private void CloseSearchAfterAdd()
        {
            SearchResultsPopup.IsOpen = false;
            TxtSearch.Text = string.Empty;
            _lastSearchedTerm = string.Empty;
            _lastSearchResults = new List<OrderableProduct>();
            TxtSearch.Focus();
        }

        private void AddProductToCart(OrderableProduct product)
        {
            var existing = _cart.FirstOrDefault(c => c.ProductId == product.ProductId);
            if (existing != null)
            {
                // += 1 pack, not += 1 unit - a plain +1 would round straight back down to the
                // nearest pack multiple and silently do nothing for package-required products.
                existing.Quantity += existing.PackQty > 1 ? (int)existing.PackQty : 1;
                return;
            }

            var row = new CartItemRow
            {
                ProductId = product.ProductId,
                Code = product.Code,
                Name = product.Name,
                Unit = product.Unit,
                SalePrice = product.SalePrice,
                PackQty = product.PackQty,
                MaxStock = product.InStock,
                Quantity = product.PackQty > 1 ? (int)product.PackQty : 1
            };

            // CollectionChanged only fires on add/remove, not on a row's own Quantity edits - without
            // this, editing quantity directly in DgCart wouldn't refresh Wartość/COD amount.
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CartItemRow.Quantity))
                    RefreshTotals();
            };

            _cart.Add(row);
        }

        private void TxtCartQuantity_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CartItemRow row })
                row.CommitToPackMultiple();
        }

        private void BtnRemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CartItemRow row })
                _cart.Remove(row);
        }

        private void Target_Checked(object sender, RoutedEventArgs e)
        {
            var isHeadquarters = RbHeadquarters.IsChecked == true;
            AddressPanel.Visibility = isHeadquarters ? Visibility.Collapsed : Visibility.Visible;
            PaymentPanel.Visibility = isHeadquarters ? Visibility.Collapsed : Visibility.Visible;

            PopulateCourierOptions();
            RefreshTotals();
        }

        private void PopulateCourierOptions()
        {
            var isHeadquarters = RbHeadquarters.IsChecked == true;
            // Fedex Dropshipping Pobranie is a COD-only courier and the ONLY courier valid for COD -
            // never offered for headquarters (IsAvailableForHeadquarters), and for a customer address
            // it's the sole option once "Za pobraniem" is selected (every other courier requires
            // Przelew), rather than sitting in the list next to couriers that'd only get rejected on
            // submit.
            var isCod = !isHeadquarters && RbCod.IsChecked == true;
            var previouslySelected = _selectedCourier;

            var options = Enum.GetValues<GaskaDeliveryCourier>()
                .Where(c => !isHeadquarters || c.IsAvailableForHeadquarters())
                .Where(c => c.RequiresCodAmount() == isCod)
                .Select(c => new CourierOption(c.ToPolishDisplayName(), c))
                .ToList();

            CbCourier.ItemsSource = options;

            var matching = options.FirstOrDefault(o => o.Value == previouslySelected);
            CbCourier.SelectedItem = matching ?? options.FirstOrDefault();
        }

        private void CbCourier_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCourier = (CbCourier.SelectedItem as CourierOption)?.Value;
            RefreshTotals();
        }

        private void PaymentType_Checked(object sender, RoutedEventArgs e)
        {
            // Fedex Dropshipping Pobranie's presence in the courier list depends on the payment type
            // too (COD-only) - re-filter it whenever Przelew/Za pobraniem changes, not just on target.
            PopulateCourierOptions();
            RefreshTotals();
        }

        private void TxtDeliveryCost_TextChanged(object sender, TextChangedEventArgs e)
        {
            _deliveryCost = decimal.TryParse(TxtDeliveryCost.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("pl-PL"), out var value)
                ? value
                : 0;
            RefreshTotals();
        }

        private void TxtCodAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Distinguishes the user actually typing a COD amount from our own programmatic
            // overwrite below, so we stop auto-recomputing over a value they deliberately chose.
            if (!_isUpdatingCodAmountProgrammatically)
                _codAmountManuallyEdited = true;
        }

        private void RefreshTotals()
        {
            UpdateSummary();
            UpdateCodVisibility();
        }

        private void UpdateCodVisibility()
        {
            var isHeadquarters = RbHeadquarters.IsChecked == true;
            var requiresCod = _selectedCourier?.RequiresCodAmount() == true;
            var showCod = !isHeadquarters && requiresCod && RbCod.IsChecked == true;

            CodAmountPanel.Visibility = showCod ? Visibility.Visible : Visibility.Collapsed;

            if (showCod && !_codAmountManuallyEdited)
            {
                _isUpdatingCodAmountProgrammatically = true;
                TxtCodAmount.Text = (_cart.Sum(c => c.LineTotal) + _deliveryCost).ToString("N2", CultureInfo.GetCultureInfo("pl-PL"));
                _isUpdatingCodAmountProgrammatically = false;
            }
            else if (!showCod)
            {
                // Reset so the next time COD becomes relevant it starts from a clean auto-computed default.
                _codAmountManuallyEdited = false;
            }
        }

        private void UpdateSummary()
        {
            var itemCount = _cart.Sum(c => c.Quantity);
            var productsTotal = _cart.Sum(c => c.LineTotal);
            var grandTotal = productsTotal + _deliveryCost;
            var courierText = _selectedCourier.HasValue ? _selectedCourier.Value.ToPolishDisplayName() : "(nie wybrano)";
            var itemWord = PolishText.Count(itemCount, "pozycję", "pozycje", "pozycji");

            TxtOrderTotal.Text = grandTotal.ToString("N2", CultureInfo.GetCultureInfo("pl-PL")) + " zł";
            SummaryText.Text = $"Koszyk: {_cart.Count} {PolishText.Count(_cart.Count, "produkt", "produkty", "produktów")}, {itemCount} {itemWord}. Metoda dostawy: {courierText}.";
        }

        private void SetBusy(bool isBusy)
        {
            BusyOverlay.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            BtnSubmit.IsEnabled = !isBusy;
            BtnCancel.IsEnabled = !isBusy;
            RbHeadquarters.IsEnabled = !isBusy;
            RbCustomer.IsEnabled = !isBusy;
            CbCourier.IsEnabled = !isBusy;
            DgCart.IsEnabled = !isBusy;
            TxtSearch.IsEnabled = !isBusy;
            TxtDeliveryCost.IsEnabled = !isBusy;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            // Belt-and-braces: LostFocus normally already committed any in-progress edit to a pack
            // multiple, but re-assert it here too in case focus never left the Ilość TextBox.
            foreach (var row in _cart)
                row.CommitToPackMultiple();

            if (_cart.Count == 0)
            {
                _dialogService.ShowWarning("Dodaj przynajmniej jeden produkt do zamówienia.");
                return;
            }

            if (_selectedCourier == null)
            {
                _dialogService.ShowWarning("Wybierz metodę dostawy.");
                return;
            }

            var isHeadquarters = RbHeadquarters.IsChecked == true;
            var courier = _selectedCourier.Value;

            if (isHeadquarters && !courier.IsAvailableForHeadquarters())
            {
                _dialogService.ShowWarning("Wybrana metoda dostawy jest dostępna tylko przy wysyłce do klienta.");
                return;
            }

            string firstName, lastName, street, city, postalCode, country;
            string? companyName, email, phone;
            AllegroPaymentType paymentType;
            decimal codAmount = 0;

            if (isHeadquarters)
            {
                firstName = "Zamówienie";
                lastName = "na siedzibę";
                companyName = null;
                street = "-";
                city = "-";
                postalCode = "-";
                country = "PL";
                email = null;
                phone = null;
                paymentType = AllegroPaymentType.WIRE_TRANSFER;
            }
            else
            {
                firstName = TxtFirstName.Text.Trim();
                lastName = TxtLastName.Text.Trim();
                companyName = string.IsNullOrWhiteSpace(TxtCompanyName.Text) ? null : TxtCompanyName.Text.Trim();
                street = TxtStreet.Text.Trim();
                city = TxtCity.Text.Trim();
                postalCode = TxtPostalCode.Text.Trim();
                country = TxtCountry.Text.Trim();
                email = string.IsNullOrWhiteSpace(TxtEmail.Text) ? null : TxtEmail.Text.Trim();
                phone = string.IsNullOrWhiteSpace(TxtPhone.Text) ? null : TxtPhone.Text.Trim();

                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(street)
                    || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(postalCode) || string.IsNullOrWhiteSpace(country) || phone == null)
                {
                    _dialogService.ShowWarning("Uzupełnij wymagane dane adresowe odbiorcy (imię, nazwisko, ulica, miasto, kod pocztowy, kraj, telefon).");
                    return;
                }

                paymentType = RbCod.IsChecked == true ? AllegroPaymentType.CASH_ON_DELIVERY : AllegroPaymentType.WIRE_TRANSFER;

                if (courier.RequiresCodAmount() && paymentType != AllegroPaymentType.CASH_ON_DELIVERY)
                {
                    _dialogService.ShowWarning($"Metoda dostawy \"{courier.ToPolishDisplayName()}\" wymaga płatności za pobraniem.");
                    return;
                }

                if (!courier.RequiresCodAmount() && paymentType == AllegroPaymentType.CASH_ON_DELIVERY)
                {
                    _dialogService.ShowWarning($"Płatność za pobraniem wymaga kuriera \"{GaskaDeliveryCourier.FedexDropshippingPobranie.ToPolishDisplayName()}\".");
                    return;
                }

                if (paymentType == AllegroPaymentType.CASH_ON_DELIVERY)
                {
                    if (!decimal.TryParse(TxtCodAmount.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("pl-PL"), out codAmount) || codAmount <= 0)
                    {
                        _dialogService.ShowWarning("Podaj prawidłową kwotę pobrania.");
                        return;
                    }
                }
            }

            var cartTotal = _cart.Sum(c => c.LineTotal);
            var orderTotal = cartTotal + _deliveryCost;
            var confirmMessage = isHeadquarters
                ? $"Utworzyć i złożyć zamówienie u dostawcy na adres siedziby ({_cart.Count} {PolishText.Count(_cart.Count, "pozycja", "pozycje", "pozycji")}, {orderTotal:N2} zł z dostawą), kurier: {courier.ToPolishDisplayName()}?"
                : $"Utworzyć i złożyć zamówienie u dostawcy na adres klienta ({firstName} {lastName}, {orderTotal:N2} zł z dostawą), kurier: {courier.ToPolishDisplayName()}?";

            if (!_dialogService.Confirm(confirmMessage))
                return;

            SetBusy(true);
            _stockCheckItems.Clear();
            StockCheckPanel.Visibility = Visibility.Visible;
            BusyStatusText.Text = "Zapisywanie zamówienia...";

            try
            {
                var order = new AllegroOrder
                {
                    AllegroId = $"MANUAL-{Guid.NewGuid():N}",
                    Status = AllegroCheckoutFormStatus.READY_FOR_PROCESSING,
                    RealizeStatus = AllegroOrderStatus.NEW,
                    Amount = orderTotal,
                    RecipientFirstName = firstName,
                    RecipientLastName = lastName,
                    RecipientStreet = street,
                    RecipientCity = city,
                    RecipientPostalCode = postalCode,
                    RecipientCountry = country,
                    RecipientCompanyName = companyName,
                    RecipientEmail = email,
                    RecipientPhoneNumber = phone,
                    DeliveryMethodId = courier.ToString(),
                    DeliveryMethodName = courier.ToPolishDisplayName(),
                    CreatedAt = DateTime.UtcNow,
                    Revision = "0",
                    SentToExternalCompany = false,
                    ExternalOrderId = 0,
                    PaymentType = paymentType,
                    Account = _account,
                    IntegrationCompany = _integrationCompany,
                    Source = OrderSource.Manual,
                    Items = _cart.Select(c => new AllegroOrderItem
                    {
                        // ProductId must be set on the in-memory items too (not only resolved in the DB
                        // by SaveAllegroOrder) - CheckStockAsync/Place*Async resolve products by it, and
                        // with 0 they'd silently skip every item and place an EMPTY order in Gąska.
                        ProductId = c.ProductId,
                        OrderItemId = Guid.NewGuid().ToString(),
                        OfferId = c.Code,
                        OfferName = c.Name,
                        ExternalId = c.Code,
                        PriceGross = c.SalePrice.ToString(CultureInfo.InvariantCulture),
                        Currency = "PLN",
                        Quantity = c.Quantity,
                        BoughtAt = DateTime.UtcNow
                    }).ToList()
                };

                await _orderRepository.SaveAllegroOrder(order);

                var orders = new List<AllegroOrder> { order };

                BusyStatusText.Text = "Sprawdzanie dostępności produktów w Gąsce...";
                var itemProgress = StockCheckItemViewModel.CreateCollectionProgress(_stockCheckItems);
                var stockCheck = await _placementService.CheckStockAsync(orders, itemProgress);
                if (!stockCheck.IsSuccessful)
                {
                    var shortageDetails = string.Join("\n", stockCheck.Shortages.Select(s =>
                        $"{s.ProductName} ({s.ProductCode}): potrzeba {s.RequestedQty}, dostępne {s.AvailableQty}"));
                    _dialogService.ShowWarning(
                        $"Zamówienie zostało zapisane, ale nie można go jeszcze złożyć u dostawcy - niewystarczający stan magazynowy:\n{shortageDetails}\n\nZłóż je później z listy zamówień, gdy stan się poprawi.",
                        "Zapisano bez złożenia u dostawcy");
                    DialogResult = true;
                    return;
                }

                StockCheckPanel.Visibility = Visibility.Collapsed;
                var statusProgress = new Progress<string>(message => BusyStatusText.Text = message);

                if (isHeadquarters)
                {
                    // skipStockCheck: CheckStockAsync above already validated every product.
                    var result = await _placementService.PlaceHeadquartersOrderAsync(orders, courier, skipStockCheck: true, statusProgress);

                    if (result.IsSuccessful)
                    {
                        var message = result.GaskaOrderNumbers.Count > 0
                            ? $"Utworzono i złożono zamówienie w Gąsce (nr: {string.Join(", ", result.GaskaOrderNumbers)})."
                            : "Zamówienie zostało utworzone i wysłane do Gąski, ale nie udało się pobrać numeru zamówienia (patrz uwagi poniżej).";

                        if (result.Warnings.Count > 0)
                        {
                            message += "\n\nUwaga:\n" + string.Join("\n", result.Warnings);
                            _dialogService.ShowWarning(message, "Złożono z zastrzeżeniami");
                        }
                        else
                        {
                            _dialogService.ShowInfo(message, "Sukces");
                        }
                    }
                    else
                    {
                        _dialogService.ShowWarning(
                            $"Zamówienie zostało zapisane, ale nie udało się go złożyć u dostawcy: {result.ErrorMessage}\n\nSpróbuj ponownie później z listy zamówień.",
                            "Zapisano bez złożenia u dostawcy");
                    }
                }
                else
                {
                    var codAmounts = new Dictionary<int, decimal> { [order.Id] = codAmount };
                    var result = await _placementService.PlaceCustomerOrdersAsync(orders, courier, codAmounts, skipStockCheck: true, statusProgress);
                    var placement = result.Results.FirstOrDefault();

                    if (placement is { IsSuccessful: true })
                    {
                        var message = !string.IsNullOrEmpty(placement.GaskaOrderNumber)
                            ? $"Utworzono i złożono zamówienie w Gąsce (nr: {placement.GaskaOrderNumber})."
                            : "Zamówienie zostało utworzone i wysłane do Gąski, ale nie udało się pobrać numeru zamówienia.";

                        if (!string.IsNullOrEmpty(placement.Warning))
                        {
                            message += "\n\nUwaga:\n" + placement.Warning;
                            _dialogService.ShowWarning(message, "Złożono z zastrzeżeniami");
                        }
                        else
                        {
                            _dialogService.ShowInfo(message, "Sukces");
                        }
                    }
                    else
                    {
                        _dialogService.ShowWarning(
                            $"Zamówienie zostało zapisane, ale nie udało się go złożyć u dostawcy: {placement?.ErrorMessage}\n\nSpróbuj ponownie później z listy zamówień.",
                            "Zapisano bez złożenia u dostawcy");
                    }
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Błąd podczas tworzenia zamówienia: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }
    }
}
