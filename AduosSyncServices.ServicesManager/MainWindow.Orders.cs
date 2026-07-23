using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Extensions;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using AduosSyncServices.ServicesManager.Models;
using AduosSyncServices.ServicesManager.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace AduosSyncServices.ServicesManager
{
    public partial class MainWindow
    {
        private static readonly TimeSpan OrdersAutoRefreshInterval = TimeSpan.FromMinutes(2);

        private readonly OrdersManagementServiceFactory _ordersManagementServiceFactory = new();
        private OrdersManagementContext? _ordersContext;
        private ServiceItem? _ordersContextService;
        private ObservableCollection<OrderRowViewModel> _orders = new();
        private ICollectionView? _ordersView;
        private bool _isOrdersOperationInProgress;
        private bool _isManualRefreshInProgress;
        private DispatcherTimer? _ordersAutoRefreshTimer;

        private HashSet<AllegroCheckoutFormStatus> _statusFilter = new();
        private HashSet<AllegroOrderStatus> _realizeStatusFilter = new();
        private HashSet<bool> _sentToExternalFilter = new();
        private HashSet<AllegroPaymentType> _paymentTypeFilter = new();
        private HashSet<OrderSource> _sourceFilter = new();
        private HashSet<bool> _dropshippingFilter = new();
        // Filter values are internal-status ids; 0 is the sentinel for "no internal status".
        private HashSet<int> _internalStatusFilter = new();

        private const int NoInternalStatusKey = 0;
        private List<OrderInternalStatus> _internalStatuses = new();
        private Dictionary<int, OrderInternalStatus> _internalStatusById = new();

        private async Task ShowOrdersViewAsync()
        {
            MainContentArea.Visibility = Visibility.Visible;
            LogsViewContainer.Visibility = Visibility.Collapsed;
            ConfigViewContainer.Visibility = Visibility.Collapsed;
            OrdersViewContainer.Visibility = Visibility.Visible;

            if (CbStatusFilter.Items.Count == 0)
                PopulateFilters();

            EnsureAutoRefreshTimerStarted();

            // Internal statuses are user-managed, so (re)load them each time the panel opens - this
            // populates the status filter and lets the grid resolve each order's status name/colour.
            await LoadInternalStatusesAsync();

            // Refresh (full Allegro sync, same as the Odśwież button) every time the panel is opened,
            // not just the first time, so it never shows stale data on re-entry.
            await RefreshOrdersAsync();
        }

        private OrdersManagementContext GetOrdersContext()
        {
            if (_ordersContext == null || !ReferenceEquals(_ordersContextService, _selectedService))
            {
                _ordersContext = _ordersManagementServiceFactory.Create(_selectedService!);
                _ordersContextService = _selectedService;
            }

            return _ordersContext;
        }

        private void PopulateFilters()
        {
            CbStatusFilter.SetItems(Enum.GetValues<AllegroCheckoutFormStatus>().Select(s => (s.GetDescription(), (object)s)));
            CbRealizeStatusFilter.SetItems(Enum.GetValues<AllegroOrderStatus>().Select(s => (s.GetDescription(), (object)s)));
            CbSentToExternalFilter.SetItems(new (string, object)[] { ("Tak", true), ("Nie", false) });
            CbPaymentTypeFilter.SetItems(Enum.GetValues<AllegroPaymentType>().Select(s => (s.GetDescription(), (object)s)));
            CbSourceFilter.SetItems(Enum.GetValues<OrderSource>().Select(s => (s.GetDescription(), (object)s)));
            CbDropshippingFilter.SetItems(new (string, object)[] { ("Tak", true), ("Nie", false) });
        }

        private async Task LoadInternalStatusesAsync()
        {
            try
            {
                var context = GetOrdersContext();
                _internalStatuses = await context.InternalStatusRepository.GetAll();
                _internalStatusById = _internalStatuses.ToDictionary(s => s.Id);

                // "Bez statusu" (key 0) first, then each defined status.
                var items = new List<(string, object)> { ("Bez statusu", NoInternalStatusKey) };
                items.AddRange(_internalStatuses.Select(s => (s.Name, (object)s.Id)));
                CbInternalStatusFilter.SetItems(items);

                // SetItems clears the control's checked items without raising SelectionChanged, so keep
                // the backing filter set in sync - otherwise a previously selected (possibly since
                // deleted) status would keep hiding rows while the control shows "Wszystkie".
                _internalStatusFilter = new HashSet<int>();
                _ordersView?.Refresh();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Nie udało się wczytać statusów wewnętrznych: {ex.Message}");
            }
        }

        // Fills in each row's status name/colour from the loaded status list (the order itself only
        // stores the id) and refreshes the id/HasInternalStatus notifications.
        private void ApplyInternalStatusToRow(OrderRowViewModel row)
        {
            if (row.InternalStatusId is { } id && _internalStatusById.TryGetValue(id, out var status))
            {
                row.RefreshInternalStatus(status.Name, status.Color);
            }
            else
            {
                row.RefreshInternalStatus(null, null);
            }
        }

        private void EnsureAutoRefreshTimerStarted()
        {
            if (_ordersAutoRefreshTimer != null)
                return;

            _ordersAutoRefreshTimer = new DispatcherTimer { Interval = OrdersAutoRefreshInterval };
            _ordersAutoRefreshTimer.Tick += async (_, _) => await OrdersAutoRefreshTick();
            _ordersAutoRefreshTimer.Start();
        }

        private async Task OrdersAutoRefreshTick()
        {
            if (OrdersViewContainer.Visibility != Visibility.Visible)
                return;

            // Full Allegro sync (same as the Odśwież button), just without the visible busy UI - a
            // DB-only reload would miss orders that only exist on Allegro and haven't synced down yet.
            await RefreshOrdersAsync(showUi: false);
        }

        private async Task LoadOrdersAsync(bool showLoadingState = true)
        {
            if (_isOrdersOperationInProgress)
                return;

            _isOrdersOperationInProgress = true;
            try
            {
                if (showLoadingState)
                    LvOrders.IsEnabled = false;

                var context = GetOrdersContext();
                var previousSelection = _orders.Where(o => o.IsSelected).Select(o => o.Id).ToHashSet();
                // Rows are expanded by default (first load and newly arrived orders alike), so track
                // what the user explicitly COLLAPSED and keep only that closed across refreshes
                // (incl. the 2-minute auto-refresh).
                var previousCollapsed = _orders.Where(o => !o.IsExpanded).Select(o => o.Id).ToHashSet();
                var orders = await context.OrderRepository.GetAllOrdersForExternalCompany();

                foreach (var row in _orders)
                    row.PropertyChanged -= OrderRow_PropertyChanged;

                _orders = new ObservableCollection<OrderRowViewModel>(
                    orders.OrderByDescending(o => o.CreatedAt)
                          .Select(o =>
                          {
                              var row = new OrderRowViewModel(o);
                              row.IsSelected = previousSelection.Contains(o.Id);
                              row.IsExpanded = !previousCollapsed.Contains(o.Id);
                              ApplyInternalStatusToRow(row);
                              return row;
                          }));

                foreach (var row in _orders)
                    row.PropertyChanged += OrderRow_PropertyChanged;

                _ordersView = CollectionViewSource.GetDefaultView(_orders);
                _ordersView.Filter = OrderFilterPredicate;
                LvOrders.ItemsSource = _ordersView;

                UpdatePlaceOrderButtonState();
                UpdateExpandAllButtonLabel();
                UpdateSelectAllButtonLabel();

                // Fill in the item sub-lists (image, delivery type) after the grid is already showing.
                await BuildOrderItemRowsAsync(context);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Nie udało się wczytać zamówień: {ex.Message}");
            }
            finally
            {
                if (showLoadingState)
                    LvOrders.IsEnabled = true;

                _isOrdersOperationInProgress = false;
            }
        }

        // Builds the display rows for every order's items sub-list: product delivery types come from a
        // single batched Products lookup, and each thumbnail is the product's first image file from the
        // products-service image folder (no per-row DB round trips).
        private async Task BuildOrderItemRowsAsync(OrdersManagementContext context)
        {
            var allItems = _orders.SelectMany(r => r.Order.Items).ToList();
            if (allItems.Count == 0)
                return;

            var productsById = new Dictionary<int, Product>();
            var imagePathByProductId = new Dictionary<int, string?>();

            try
            {
                var productIds = allItems.Select(i => i.ProductId).Distinct().ToList();
                var products = await context.ProductRepository.GetProductsByIdsAsync(productIds, CancellationToken.None);
                productsById = products.ToDictionary(p => p.Id);

                // Disk scan off the UI thread - one folder probe per distinct product.
                imagePathByProductId = await Task.Run(() => productIds.ToDictionary(
                    id => id,
                    id => ImageHelper.GetFirstImageFile(ImageHelper.DefaultImagesFolder, id)));
            }
            catch
            {
                // Best-effort enrichment: on failure the sub-lists still show code/name/price from the
                // order items themselves, just without images and delivery types.
            }

            foreach (var row in _orders)
            {
                row.ItemRows = row.Order.Items.Select(item =>
                {
                    productsById.TryGetValue(item.ProductId, out var product);
                    imagePathByProductId.TryGetValue(item.ProductId, out var imagePath);

                    return new OrderItemRowViewModel
                    {
                        ImageUrl = imagePath,
                        Code = product?.Code ?? item.ExternalId,
                        Name = item.OfferName,
                        QuantityDisplay = item.Quantity.ToString(),
                        Unit = product?.Unit ?? "-",
                        DeliveryTypeDisplay = OrderItemRowViewModel.FormatDeliveryType(product?.DeliveryType),
                        PriceDisplay = OrderItemRowViewModel.FormatPrice(item.PriceGross)
                    };
                }).ToList();
            }
        }

        private async Task RefreshOrdersAsync(bool showUi = true)
        {
            if (_isManualRefreshInProgress)
                return;

            _isManualRefreshInProgress = true;
            try
            {
                if (showUi)
                {
                    BtnRefreshOrders.IsEnabled = false;
                    RefreshStatusPanel.Visibility = Visibility.Visible;
                    RefreshStatusText.Text = "Rozpoczynanie synchronizacji...";
                }

                var context = GetOrdersContext();
                var progress = showUi ? new Progress<string>(message => RefreshStatusText.Text = message) : null;
                await context.SyncService.SyncOrdersFromAllegro(context.AllegroDeliveryNames, progress);

                if (showUi)
                    RefreshStatusText.Text = "Wczytywanie zamówień z bazy...";

                await LoadOrdersAsync(showLoadingState: showUi);
            }
            catch (Exception ex)
            {
                if (showUi)
                    _dialogService.ShowError($"Nie udało się odświeżyć zamówień: {ex.Message}");
            }
            finally
            {
                if (showUi)
                {
                    RefreshStatusPanel.Visibility = Visibility.Collapsed;
                    BtnRefreshOrders.IsEnabled = true;
                }

                _isManualRefreshInProgress = false;
            }
        }

        private bool OrderFilterPredicate(object obj)
        {
            if (obj is not OrderRowViewModel row)
                return false;

            if (_statusFilter.Count > 0 && !_statusFilter.Contains(row.Status))
                return false;

            if (_realizeStatusFilter.Count > 0 && !_realizeStatusFilter.Contains(row.RealizeStatus))
                return false;

            if (_sentToExternalFilter.Count > 0 && !_sentToExternalFilter.Contains(row.SentToExternalCompany))
                return false;

            if (_paymentTypeFilter.Count > 0 && !_paymentTypeFilter.Contains(row.PaymentType))
                return false;

            if (_sourceFilter.Count > 0 && !_sourceFilter.Contains(row.Source))
                return false;

            // Dropshipping is nullable on the order; treat anything other than an explicit true as
            // "not dropshipping" so the "Nie" option also catches the null (not-yet-placed) case.
            if (_dropshippingFilter.Count > 0 && !_dropshippingFilter.Contains(row.IsDropshipping == true))
                return false;

            // No internal status maps to the sentinel key so "Bez statusu" can be filtered on.
            if (_internalStatusFilter.Count > 0 && !_internalStatusFilter.Contains(row.InternalStatusId ?? NoInternalStatusKey))
                return false;

            return true;
        }

        private void Filter_SelectionChanged(object? sender, EventArgs e)
        {
            _statusFilter = CbStatusFilter.SelectedValues.Cast<AllegroCheckoutFormStatus>().ToHashSet();
            _realizeStatusFilter = CbRealizeStatusFilter.SelectedValues.Cast<AllegroOrderStatus>().ToHashSet();
            _sentToExternalFilter = CbSentToExternalFilter.SelectedValues.Cast<bool>().ToHashSet();
            _paymentTypeFilter = CbPaymentTypeFilter.SelectedValues.Cast<AllegroPaymentType>().ToHashSet();
            _sourceFilter = CbSourceFilter.SelectedValues.Cast<OrderSource>().ToHashSet();
            _dropshippingFilter = CbDropshippingFilter.SelectedValues.Cast<bool>().ToHashSet();
            _internalStatusFilter = CbInternalStatusFilter.SelectedValues.Cast<int>().ToHashSet();
            _ordersView?.Refresh();

            // The visible set just changed, so the select-all label may flip meaning.
            UpdateSelectAllButtonLabel();
        }

        private void OrderRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderRowViewModel.IsSelected))
            {
                // Counter/label recomputation walks the whole list - during select/unselect-all the
                // caller updates them once after the loop instead of once per row.
                if (!_isBulkSelectInProgress)
                {
                    UpdatePlaceOrderButtonState();
                    UpdateSelectAllButtonLabel();
                }
            }
            else if (e.PropertyName == nameof(OrderRowViewModel.IsExpanded))
            {
                if (sender is OrderRowViewModel row)
                    SyncRowDetailsVisibility(row);

                // Label recomputation walks the whole list - during expand/collapse-all the caller
                // updates it once after the loop instead of once per row.
                if (!_isBulkExpandInProgress)
                    UpdateExpandAllButtonLabel();
            }
        }

        private bool _isBulkExpandInProgress;
        private bool _isBulkSelectInProgress;

        // Rows currently passing the filters - selection shortcuts act on what the user can see.
        private IEnumerable<OrderRowViewModel> VisibleOrderRows() =>
            _ordersView?.Cast<OrderRowViewModel>() ?? Enumerable.Empty<OrderRowViewModel>();

        private void SetAllVisibleSelected(bool selected)
        {
            _isBulkSelectInProgress = true;
            try
            {
                foreach (var row in VisibleOrderRows())
                    row.IsSelected = selected;
            }
            finally
            {
                _isBulkSelectInProgress = false;
            }

            UpdatePlaceOrderButtonState();
            UpdateSelectAllButtonLabel();
        }

        private void BtnToggleSelectAll_Click(object sender, RoutedEventArgs e)
        {
            // Select all visible rows unless every one is already selected - then unselect them.
            var selectAll = VisibleOrderRows().Any(o => !o.IsSelected);
            SetAllVisibleSelected(selectAll);
        }

        private void UpdateSelectAllButtonLabel()
        {
            var anyUnselected = VisibleOrderRows().Any(o => !o.IsSelected);
            var anyVisible = VisibleOrderRows().Any();
            BtnToggleSelectAll.Content = anyVisible && !anyUnselected ? "Odznacz wszystkie" : "Zaznacz wszystkie";
        }

        // PreviewKeyDown, not KeyDown: DataGrid's own Ctrl+A (row selection) handles the bubbling
        // event before it would reach an instance handler, so hook the tunneling one.
        private void LvOrders_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SetAllVisibleSelected(true);
                e.Handled = true;
            }
        }

        // DetailsVisibility must be set as a LOCAL value on the realised DataGridRow container - the
        // grid's explicit RowDetailsVisibilityMode outranks any RowStyle binding, so styles can't
        // drive it. Virtualised (not yet realised) rows are covered by LvOrders_LoadingRow instead.
        private void SyncRowDetailsVisibility(OrderRowViewModel row)
        {
            if (LvOrders.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow container)
                container.DetailsVisibility = row.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LvOrders_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            // Runs for newly realised AND recycled containers, so scrolled-back-into-view rows always
            // reflect their view-model's expansion state.
            if (e.Row.Item is OrderRowViewModel row)
                e.Row.DetailsVisibility = row.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OrderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // The IsChecked<->IsSelected TwoWay binding doesn't reliably push the value back to the
            // row (the checkbox visually toggles but OrderRowViewModel.IsSelected stays false), so set
            // it directly here from the checkbox's own state instead of trusting the binding to have done it.
            if (sender is CheckBox { DataContext: OrderRowViewModel row } checkBox)
                row.IsSelected = checkBox.IsChecked == true;

            UpdatePlaceOrderButtonState();
        }

        private void OrderExpander_Click(object sender, RoutedEventArgs e)
        {
            // Same TwoWay-binding workaround as the selection checkbox: push the toggle state to the
            // row explicitly. The label update happens via the row's PropertyChanged handler.
            if (sender is System.Windows.Controls.Primitives.ToggleButton { DataContext: OrderRowViewModel row } toggle)
                row.IsExpanded = toggle.IsChecked == true;
        }

        private void BtnToggleExpandAll_Click(object sender, RoutedEventArgs e)
        {
            // Expand everything unless every order is already expanded - then collapse everything.
            var expandAll = _orders.Any(o => !o.IsExpanded);

            _isBulkExpandInProgress = true;
            try
            {
                foreach (var row in _orders)
                    row.IsExpanded = expandAll;
            }
            finally
            {
                _isBulkExpandInProgress = false;
            }

            UpdateExpandAllButtonLabel();
        }

        private void UpdateExpandAllButtonLabel()
        {
            var allExpanded = _orders.Count > 0 && _orders.All(o => o.IsExpanded);
            BtnToggleExpandAll.Content = allExpanded ? "Zwiń wszystkie" : "Rozwiń wszystkie";
        }

        private void LvOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The row highlight (not a checkbox) drives the single-order place/delete affordances.
            UpdateActionButtonsState();
        }

        private void UpdatePlaceOrderButtonState() => UpdateActionButtonsState();

        private void UpdateActionButtonsState()
        {
            // Per-selection actions live in the grid's right-click context menu (enabled/disabled in
            // OrdersContextMenu_Opened); here we only keep the "Zaznaczono: N" counter in sync.
            SelectedOrdersCountText.Text = $"Zaznaczono: {_orders.Count(o => o.IsSelected)}";
        }

        private void OrderRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click doesn't select a DataGrid row by default - set SelectedItem (rather than the
            // row's IsSelected) so it replaces any previous highlight and only one row stays selected.
            if (sender is DataGridRow row)
                LvOrders.SelectedItem = row.Item;
        }

        private void OrdersContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var targets = GetSelectedTargets();
            MenuItemDetails.IsEnabled = LvOrders.SelectedItem is OrderRowViewModel;
            MenuItemPlace.IsEnabled = targets.Any(t => t.CanSelect);
            MenuItemSetStatus.IsEnabled = targets.Count > 0;
            MenuItemDelete.IsEnabled = targets.Any(t => t.CanDelete);
        }

        private void MenuItemDetails_Click(object sender, RoutedEventArgs e)
        {
            if (LvOrders.SelectedItem is OrderRowViewModel row)
                OpenOrderDetails(row);
        }

        // The orders an action applies to: the checked rows, or - when nothing is checked - the single
        // highlighted row. No eligibility filtering here; each action validates its own conditions.
        private List<OrderRowViewModel> GetSelectedTargets()
        {
            var checkedRows = _orders.Where(o => o.IsSelected).ToList();
            if (checkedRows.Count > 0)
                return checkedRows;

            return LvOrders.SelectedItem is OrderRowViewModel row
                ? new List<OrderRowViewModel> { row }
                : new List<OrderRowViewModel>();
        }

        private async void BtnDeleteOrders_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetSelectedTargets();
            if (targets.Count == 0)
                return;

            // Only manual, not-yet-placed orders can be deleted - filter and explain any skipped.
            var deletable = targets.Where(t => t.CanDelete).ToList();
            var notDeletable = targets.Where(t => !t.CanDelete).ToList();

            if (deletable.Count == 0)
            {
                _dialogService.ShowWarning("Żadne z wybranych zamówień nie może zostać usunięte. Usuwać można tylko zamówienia ręczne, które nie zostały jeszcze złożone u dostawcy.");
                return;
            }

            var confirmMessage = deletable.Count == 1
                ? $"Czy na pewno usunąć zamówienie ręczne {deletable[0].AllegroId}? Tej operacji nie można cofnąć."
                : $"Czy na pewno usunąć {deletable.Count} zamówień ręcznych? Tej operacji nie można cofnąć.";

            if (notDeletable.Count > 0)
                confirmMessage += $"\n\nPominięte (nie można usunąć): {string.Join(", ", notDeletable.Select(o => o.AllegroId))}.";

            if (!_dialogService.Confirm(confirmMessage))
                return;

            try
            {
                var context = GetOrdersContext();
                var deleted = 0;
                var failed = new List<string>();

                foreach (var target in deletable)
                {
                    if (await context.OrderRepository.DeleteManualOrder(target.Id))
                        deleted++;
                    else
                        failed.Add(target.AllegroId);
                }

                if (failed.Count > 0)
                {
                    _dialogService.ShowWarning(
                        $"Usunięto {deleted} z {deletable.Count} zamówień. Nie udało się usunąć (mogły zostać złożone u dostawcy lub nie są ręczne): {string.Join(", ", failed)}.");
                }

                await LoadOrdersAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Nie udało się usunąć zamówień: {ex.Message}");
            }
            finally
            {
                UpdateActionButtonsState();
            }
        }

        private async void BtnSetInternalStatus_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetSelectedTargets();
            if (targets.Count == 0)
            {
                _dialogService.ShowWarning("Zaznacz lub wybierz zamówienia, którym chcesz ustawić status wewnętrzny.");
                return;
            }

            var dialog = new SetInternalStatusDialog(_internalStatuses, targets.Count) { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var context = GetOrdersContext();
                await context.OrderRepository.SetOrdersInternalStatus(targets.Select(t => t.Id).ToList(), dialog.SelectedStatusId);

                // Reflect the change in memory without a full Allegro re-sync.
                foreach (var target in targets)
                {
                    target.Order.InternalStatusId = dialog.SelectedStatusId;
                    ApplyInternalStatusToRow(target);
                }

                _ordersView?.Refresh();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Nie udało się ustawić statusu wewnętrznego: {ex.Message}");
            }
            finally
            {
                UpdateActionButtonsState();
            }
        }

        private async void BtnManageInternalStatuses_Click(object sender, RoutedEventArgs e)
        {
            var context = GetOrdersContext();
            var dialog = new InternalStatusesDialog(context.InternalStatusRepository) { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            // The list changed - reload it (repopulates the filter), then reload orders from the DB so
            // rows pick up any deletions the FK cleared to NULL and the new names/colours. DB-only
            // reload (no Allegro re-sync) is enough here.
            await LoadInternalStatusesAsync();
            await LoadOrdersAsync();
        }

        private async void BtnRefreshOrders_Click(object sender, RoutedEventArgs e)
        {
            await RefreshOrdersAsync();
        }

        private void BtnAddOrder_Click(object sender, RoutedEventArgs e)
        {
            var context = GetOrdersContext();
            var dialog = new AddManualOrderDialog(context.OrderRepository, context.ProductRepository, context.PlacementService, context.MarginRanges, context.Account, context.IntegrationCompany) { Owner = this };
            var result = dialog.ShowDialog();

            if (result == true)
                _ = RefreshOrdersAsync();
        }

        private void LvOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Two fast clicks on the selection checkbox or the items expander shouldn't ALSO open the
            // details dialog on top of toggling them - ignore double-clicks originating inside either
            // (CheckBox derives from ToggleButton, so one check covers both).
            if (e.OriginalSource is DependencyObject source && HasToggleAncestor(source))
                return;

            if (LvOrders.SelectedItem is OrderRowViewModel row)
                OpenOrderDetails(row);
        }

        private static bool HasToggleAncestor(DependencyObject element)
        {
            while (element != null)
            {
                if (element is System.Windows.Controls.Primitives.ToggleButton)
                    return true;

                if (element is System.Windows.Controls.Primitives.DataGridRowsPresenter or DataGrid)
                    return false;

                element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                    : LogicalTreeHelper.GetParent(element);
            }

            return false;
        }

        private void LvOrders_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || LvOrders.SelectedItem is not OrderRowViewModel row)
                return;

            OpenOrderDetails(row);
            e.Handled = true;
        }

        private void OpenOrderDetails(OrderRowViewModel row)
        {
            var context = GetOrdersContext();
            var dialog = new OrderDetailsDialog(row, context.PlacementService, context.ProductRepository) { Owner = this };
            var result = dialog.ShowDialog();

            if (result == true)
            {
                foreach (var order in _orders)
                    order.IsSelected = false;

                _ = RefreshOrdersAsync();
            }
        }

        private void BtnPlaceOrder_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetSelectedTargets();
            if (targets.Count == 0)
                return;

            // Only orders meeting the placement conditions can be sent; validate here (not on the
            // checkbox) and explain any that will be skipped.
            var eligible = targets.Where(t => t.CanSelect).ToList();
            var ineligible = targets.Where(t => !t.CanSelect).ToList();

            if (eligible.Count == 0)
            {
                _dialogService.ShowWarning(
                    "Żadne z wybranych zamówień nie może zostać złożone u dostawcy:\n" +
                    string.Join("\n", ineligible.Select(o => $"{o.AllegroId}: {o.CannotPlaceReason}")));
                return;
            }

            if (ineligible.Count > 0 &&
                !_dialogService.Confirm(
                    $"Część wybranych zamówień nie może zostać złożona i zostanie pominięta ({string.Join(", ", ineligible.Select(o => o.AllegroId))}).\n\nZłożyć pozostałe {eligible.Count}?"))
            {
                return;
            }

            var selectedOrders = eligible.Select(o => o.Order).ToList();

            var context = GetOrdersContext();
            var dialog = new OrderPlacementDialog(context.PlacementService, context.ProductRepository, selectedOrders) { Owner = this };
            var result = dialog.ShowDialog();

            if (result == true)
            {
                foreach (var order in _orders)
                    order.IsSelected = false;

                _ = RefreshOrdersAsync();
            }
        }
    }
}
