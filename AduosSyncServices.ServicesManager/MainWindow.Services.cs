using AduosSyncServices.ServicesManager.Models;
using AduosSyncServices.ServicesManager.Resources;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Media;

namespace AduosSyncServices.ServicesManager
{
    public partial class MainWindow
    {
        private CancellationTokenSource _serviceStatusCts = new();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAvailableServices();

            if (AvailableAccounts.Count == 1)
            {
                SelectedAccount = AvailableAccounts[0];
            }
            else
            {
                ShowSelectionOverlay();
                HideContentViews();
                ResetNavSelection();
            }
        }

        private void LoadAvailableServices()
        {
            AvailableServices.Clear();
            AvailableAccounts.Clear();
            _allServices.Clear();

            _allServices.AddRange(_serviceCatalogService.LoadServices());

            foreach (var account in _serviceCatalogService.GetAccounts(_allServices))
                AvailableAccounts.Add(account);

            if (AvailableAccounts.Count == 1)
                SelectedAccount = AvailableAccounts[0];
        }

        private void ApplyAccountFilter()
        {
            AvailableServices.Clear();

            var filtered = _allServices
                .Where(s => string.Equals(s.Account, _selectedAccount, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var service in filtered)
                AvailableServices.Add(service);

            SelectedService = null;
            ServiceNameTextBox.Text = "...";

            if (!_initialSelectionCompleted)
            {
                ShowSelectionOverlay();
                HideContentViews();
                ResetNavSelection();
                return;
            }

            if (AvailableServices.Count >= 1)
            {
                SelectedService = AvailableServices[0];
                ShowMainNavigation();
            }
            else
            {
                ShowSelectionOverlay();
                HideContentViews();
                ResetNavSelection();
            }
        }

        private void SelectService(ServiceItem service)
        {
            if (service == null) return;

            // Cancel any ongoing status refresh
            _serviceStatusCts.Cancel();
            _serviceStatusCts.Dispose();
            _serviceStatusCts = new CancellationTokenSource();

            _serviceControllerService.SetService(service.ServiceName);

            ResetLogView();
            InitLogWatcher();
            _ = RefreshServiceStatusAsync(_serviceStatusCts.Token);
            _ = LoadLogFilesAsync();
            LoadConfig();
            ServiceNameTextBox.Text = service.Name;
        }


        private void ApplySelectedService(ServiceItem service)
        {
            if (service == null) return;

            SelectService(service);
            CbServiceSelector.Visibility = Visibility.Visible;
            ShowMainNavigation();

            BtnShowLogs.IsChecked = true;
            _ = ShowLogsViewAsync();
            _ = RefreshServiceStatusAsync(_serviceStatusCts.Token);
        }

        private async Task RefreshServiceStatusAsync(CancellationToken cancellationToken)
        {
            try
            {
                var status = await _serviceControllerService.GetStatusAsync();
                if (cancellationToken.IsCancellationRequested)
                    return;
                if (status.HasValue)
                    ApplyServiceStatus(status.Value);
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                    ApplyServiceError(ex);
            }
        }

        private void ApplyServiceStatus(ServiceControllerStatus status)
        {
            switch (status)
            {
                case ServiceControllerStatus.Running:
                    ServiceStatusDot.Fill = Brushes.Green;
                    ServiceStatusText.Text = "Online";
                    BtnStartService.IsEnabled = false;
                    BtnStopService.IsEnabled = true;
                    BtnRestartService.IsEnabled = true;
                    break;

                case ServiceControllerStatus.Stopped:
                    ServiceStatusDot.Fill = Brushes.Red;
                    ServiceStatusText.Text = "Offline";
                    BtnStartService.IsEnabled = true;
                    BtnStopService.IsEnabled = false;
                    BtnRestartService.IsEnabled = false;
                    break;

                case ServiceControllerStatus.Paused:
                    ServiceStatusDot.Fill = Brushes.Orange;
                    ServiceStatusText.Text = "Paused";
                    BtnStartService.IsEnabled = true;
                    BtnStopService.IsEnabled = true;
                    BtnRestartService.IsEnabled = true;
                    break;

                default:
                    ServiceStatusDot.Fill = Brushes.Gray;
                    ServiceStatusText.Text = status.ToString();
                    BtnStartService.IsEnabled = false;
                    BtnStopService.IsEnabled = false;
                    BtnRestartService.IsEnabled = false;
                    break;
            }
        }

        private void ApplyServiceError(Exception ex)
        {
            ServiceStatusDot.Fill = Brushes.Gray;
            ServiceStatusText.Text = "Error";
            BtnStartService.IsEnabled = BtnStopService.IsEnabled = BtnRestartService.IsEnabled = false;
            _dialogService.ShowError(string.Format(UiMessages.ServiceStatusFailed, ex.Message));
        }

        private void SetServiceButtonsTemporarilyEnabled(bool isEnabled)
        {
            BtnStartService.IsEnabled = isEnabled;
            BtnStopService.IsEnabled = isEnabled;
            BtnRestartService.IsEnabled = isEnabled;
        }

        private async Task RunServiceOperationAsync(Action<ServiceController> operation)
        {
            SetServiceButtonsTemporarilyEnabled(false);

            try
            {
                await _serviceControllerService.RunOperationAsync(operation);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "Błąd usługi");
            }
            finally
            {
                await RefreshServiceStatusAsync(_serviceStatusCts.Token);
            }
        }

        private async Task StartServiceAsync()
        {
            await RunServiceOperationAsync(sc =>
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            });
        }

        private async Task StopServiceAsync()
        {
            await RunServiceOperationAsync(sc =>
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            });

            // Reinitialize controller for the current service
            if (SelectedService != null)
                _serviceControllerService.SetService(SelectedService.ServiceName);
        }

        private async Task RestartServiceAsync()
        {
            await RunServiceOperationAsync(sc =>
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));

                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            });
            // Reinitialize controller for the current service
            if (SelectedService != null)
                _serviceControllerService.SetService(SelectedService.ServiceName);
        }
    }
}
