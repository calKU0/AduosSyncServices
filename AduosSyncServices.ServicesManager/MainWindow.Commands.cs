using AduosSyncServices.ServicesManager.Helpers;
using AduosSyncServices.ServicesManager.Models;
using System.Windows;
using System.Windows.Input;

namespace AduosSyncServices.ServicesManager
{
    public partial class MainWindow
    {
        public ICommand ShowLogsCommand { get; private set; } = null!;
        public ICommand ShowConfigCommand { get; private set; } = null!;
        public ICommand StartServiceCommand { get; private set; } = null!;
        public ICommand StopServiceCommand { get; private set; } = null!;
        public ICommand RestartServiceCommand { get; private set; } = null!;
        public ICommand SelectServiceCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            ShowLogsCommand = new RelayCommand(async () => await ShowLogsAsync());
            ShowConfigCommand = new RelayCommand(ShowConfig);
            StartServiceCommand = new RelayCommand(async () => await StartServiceAsync());
            StopServiceCommand = new RelayCommand(async () => await StopServiceAsync());
            RestartServiceCommand = new RelayCommand(async () => await RestartServiceAsync());
            SelectServiceCommand = new RelayCommand<ServiceItem>(SelectServiceFromCommand);
        }

        private async Task ShowLogsAsync()
        {
            await ShowLogsViewAsync();
        }

        private void ShowConfig()
        {
            ShowConfigViewInternal();
        }

        private void SelectServiceFromCommand(ServiceItem? service)
        {
            if (service == null)
                return;

            if (!string.IsNullOrEmpty(service.Account) && SelectedAccount != service.Account)
                SelectedAccount = service.Account;

            SelectedService = service;
            _initialSelectionCompleted = true;
            CbServiceSelector.Visibility = Visibility.Visible;
            ShowMainNavigation();
        }
    }
}
