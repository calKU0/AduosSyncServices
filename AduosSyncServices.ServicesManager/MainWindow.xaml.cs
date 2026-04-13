using AduosSyncServices.ServicesManager.Helpers;
using AduosSyncServices.ServicesManager.Models;
using AduosSyncServices.ServicesManager.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace AduosSyncServices.ServicesManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<LogFileItem> logFiles = new ObservableCollection<LogFileItem>();
        private readonly LogRefreshService _logRefreshService = new();
        private readonly ServiceControllerService _serviceControllerService = new();
        private FileSystemWatcher _logWatcher;
        private readonly DispatcherTimer _logReloadDebounce;
        public ObservableCollection<ServiceItem> AvailableServices { get; } = new ObservableCollection<ServiceItem>();
        private const int InitialTailLines = 2000;
        private const int PageLines = 1000;

        private readonly BulkObservableCollection<LogLine> _currentLogLines = new();
        private BulkObservableCollection<LogLine> _filteredLogLines = new();
        private string? _currentPath;
        private long _loadedStartOffset = 0;
        private bool _isLoadingMore = false;
        private bool _reachedFileStart = false;
        private long _lastReadOffset = 0;
        private object _lastSelectedLog;
        private bool _isAtBottom = true;
        private bool _suppressLogSelection;
        private LogFileItem? _selectedLogFile;
        private bool _showOnlyWarningsAndErrors;
        private List<Delivery> _deliveries = new();
        private List<MarginRange> _marginRanges = new();
        private Controls.DeliveryEditor? _deliveryEditor;
        private Controls.MarginRangeEditor? _marginRangeEditor;
        private bool _deliveriesSectionExists;
        private bool _marginRangesSectionExists;
        private bool _initialSelectionCompleted;
        private readonly List<ServiceItem> _allServices = new();
        public ObservableCollection<string> AvailableAccounts { get; } = new ObservableCollection<string>();
        private string? _selectedAccount;
        private ServiceItem? _selectedService;
        private readonly ConfigService _configService = new();
        private readonly LogService _logService = new();
        private readonly DialogService _dialogService = new();
        private readonly ConfigFieldUiBuilder _configFieldUiBuilder = new();
        private readonly ServiceCatalogService _serviceCatalogService = new();
        private readonly ConfigFieldValueCollector _configFieldValueCollector = new();
        private bool _isConfigDirty;
        private bool _isConfigLoading;
        private bool _isConfigSaving;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            IcAvailableServices.ItemsSource = AvailableServices;
            CbServiceSelector.ItemsSource = AvailableServices;
            CbAccountSelector.ItemsSource = AvailableAccounts;
            CbAccountSelectorOverlay.ItemsSource = AvailableAccounts;
            InitializeCommands();
            InitializeConfigDirtyTracking();

            _logReloadDebounce = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };

            _logReloadDebounce.Tick += async (_, _) =>
            {
                _logReloadDebounce.Stop();
                await LoadLogFilesAsync();
            };
        }

        public string? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (_selectedAccount == value)
                    return;

                if (!TryHandleUnsavedConfigChanges())
                {
                    OnPropertyChanged(nameof(SelectedAccount));
                    return;
                }

                _selectedAccount = value;
                OnPropertyChanged(nameof(SelectedAccount));

                if (!string.IsNullOrWhiteSpace(_selectedAccount))
                    ApplyAccountFilter();
            }
        }

        public ServiceItem? SelectedService
        {
            get => _selectedService;
            set
            {
                if (_selectedService == value)
                    return;

                if (!TryHandleUnsavedConfigChanges())
                {
                    OnPropertyChanged(nameof(SelectedService));
                    return;
                }

                _selectedService = value;
                OnPropertyChanged(nameof(SelectedService));

                if (_selectedService != null)
                    ApplySelectedService(_selectedService);
            }
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public LogFileItem? SelectedLogFile
        {
            get => _selectedLogFile;
            set
            {
                if (_selectedLogFile == value)
                    return;

                _selectedLogFile = value;
                OnPropertyChanged(nameof(SelectedLogFile));

                if (_suppressLogSelection)
                    return;

                if (_selectedLogFile == null)
                {
                    if (_lastSelectedLog is LogFileItem lastSelected && logFiles.Contains(lastSelected))
                    {
                        _suppressLogSelection = true;
                        SelectedLogFile = lastSelected;
                        _suppressLogSelection = false;
                    }
                    else
                    {
                        TxtSelectedFileName.Text = string.Empty;
                    }
                    return;
                }

                _lastSelectedLog = _selectedLogFile;
                TxtSelectedFileName.Text = _selectedLogFile.Name;
                _ = LoadSelectedFileContentAsync();
            }
        }

        public bool ShowOnlyWarningsAndErrors
        {
            get => _showOnlyWarningsAndErrors;
            set
            {
                if (_showOnlyWarningsAndErrors == value)
                    return;

                _showOnlyWarningsAndErrors = value;
                OnPropertyChanged(nameof(ShowOnlyWarningsAndErrors));
                _ = HandleShowOnlyWarningsChangedAsync();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!TryHandleUnsavedConfigChanges())
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        private void InitializeConfigDirtyTracking()
        {
            ConfigStackPanel.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => MarkConfigDirty()));
            ConfigStackPanel.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => MarkConfigDirty()));
            ConfigStackPanel.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => MarkConfigDirty()));
            ConfigStackPanel.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => MarkConfigDirty()));
        }

        private void MarkConfigDirty()
        {
            if (_isConfigLoading || _isConfigSaving || ConfigViewContainer.Visibility != Visibility.Visible)
                return;

            _isConfigDirty = true;
        }

    }
}