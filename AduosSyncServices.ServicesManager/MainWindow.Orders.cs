using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Extensions;
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
        private static readonly TimeSpan OrdersAutoRefreshInterval = TimeSpan.FromMinutes(5);

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

        private async Task ShowOrdersViewAsync()
        {
            MainContentArea.Visibility = Visibility.Visible;
            LogsViewContainer.Visibility = Visibility.Collapsed;
            ConfigViewContainer.Visibility = Visibility.Collapsed;
            OrdersViewContainer.Visibility = Visibility.Visible;

            if (CbStatusFilter.Items.Count == 0)
                PopulateFilters();

            EnsureAutoRefreshTimerStarted();

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
                var orders = await context.OrderRepository.GetAllOrdersForExternalCompany();

                foreach (var row in _orders)
                    row.PropertyChanged -= OrderRow_PropertyChanged;

                _orders = new ObservableCollection<OrderRowViewModel>(
                    orders.OrderByDescending(o => o.CreatedAt)
                          .Select(o =>
                          {
                              var row = new OrderRowViewModel(o);
                              row.IsSelected = previousSelection.Contains(o.Id) && row.CanSelect;
                              return row;
                          }));

                foreach (var row in _orders)
                    row.PropertyChanged += OrderRow_PropertyChanged;

                _ordersView = CollectionViewSource.GetDefaultView(_orders);
                _ordersView.Filter = OrderFilterPredicate;
                LvOrders.ItemsSource = _ordersView;

                UpdatePlaceOrderButtonState();
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
                    BtnPlaceOrder.IsEnabled = false;
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

            return true;
        }

        private void Filter_SelectionChanged(object? sender, EventArgs e)
        {
            _statusFilter = CbStatusFilter.SelectedValues.Cast<AllegroCheckoutFormStatus>().ToHashSet();
            _realizeStatusFilter = CbRealizeStatusFilter.SelectedValues.Cast<AllegroOrderStatus>().ToHashSet();
            _sentToExternalFilter = CbSentToExternalFilter.SelectedValues.Cast<bool>().ToHashSet();
            _paymentTypeFilter = CbPaymentTypeFilter.SelectedValues.Cast<AllegroPaymentType>().ToHashSet();
            _sourceFilter = CbSourceFilter.SelectedValues.Cast<OrderSource>().ToHashSet();
            _ordersView?.Refresh();
        }

        private void OrderRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrderRowViewModel.IsSelected))
                UpdatePlaceOrderButtonState();
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

        private void UpdatePlaceOrderButtonState()
        {
            var selectedCount = _orders.Count(o => o.IsSelected);
            BtnPlaceOrder.IsEnabled = selectedCount > 0;
            SelectedOrdersCountText.Text = $"Zaznaczono: {selectedCount}";
        }

        private async void BtnRefreshOrders_Click(object sender, RoutedEventArgs e)
        {
            await RefreshOrdersAsync();
        }

        private void BtnAddOrder_Click(object sender, RoutedEventArgs e)
        {
            var context = GetOrdersContext();
            var dialog = new AddManualOrderDialog(context.OrderRepository, context.ProductRepository, context.PlacementService, context.Account, context.IntegrationCompany) { Owner = this };
            var result = dialog.ShowDialog();

            if (result == true)
                _ = RefreshOrdersAsync();
        }

        private void LvOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Two fast clicks on the selection checkbox shouldn't ALSO open the details dialog on top
            // of toggling it - ignore double-clicks that originate inside a CheckBox.
            if (e.OriginalSource is DependencyObject source && HasCheckBoxAncestor(source))
                return;

            if (LvOrders.SelectedItem is OrderRowViewModel row)
                OpenOrderDetails(row);
        }

        private static bool HasCheckBoxAncestor(DependencyObject element)
        {
            while (element != null)
            {
                if (element is CheckBox)
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
            var selectedRows = _orders.Where(o => o.IsSelected).ToList();
            if (selectedRows.Count == 0)
                return;

            var alreadyOrdered = selectedRows.Where(o => o.SentToExternalCompany).ToList();
            if (alreadyOrdered.Count > 0)
            {
                _dialogService.ShowError(
                    $"Następujące zamówienia zostały już złożone u dostawcy i nie mogą zostać złożone ponownie: {string.Join(", ", alreadyOrdered.Select(o => o.AllegroId))}.");
                return;
            }

            var selectedOrders = selectedRows.Select(o => o.Order).ToList();

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
