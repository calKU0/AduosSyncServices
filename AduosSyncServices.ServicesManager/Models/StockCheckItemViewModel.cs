using AduosSyncServices.Contracts.OrderPlacement;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AduosSyncServices.ServicesManager.Models
{
    public class StockCheckItemViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Creates the IProgress bridge between CheckStockAsync's per-product reports and a live
        /// checklist collection bound to the UI - adds a row on the first report for a product and
        /// updates it in place on subsequent ones. Construct on the UI thread (Progress&lt;T&gt;
        /// captures the SynchronizationContext, so the callback runs on the UI thread).
        /// </summary>
        public static Progress<StockCheckProgressItem> CreateCollectionProgress(ObservableCollection<StockCheckItemViewModel> target) =>
            new(item =>
            {
                var existing = target.FirstOrDefault(x => x.ProductCode == item.ProductCode);
                if (existing == null)
                {
                    target.Add(new StockCheckItemViewModel
                    {
                        ProductCode = item.ProductCode,
                        ProductName = item.ProductName,
                        Status = item.Status,
                        RequestedQty = item.RequestedQty,
                        AvailableQty = item.AvailableQty
                    });
                }
                else
                {
                    existing.Status = item.Status;
                    existing.RequestedQty = item.RequestedQty;
                    existing.AvailableQty = item.AvailableQty;
                }
            });

        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public float RequestedQty { get; set; }

        private float? _availableQty;
        public float? AvailableQty
        {
            get => _availableQty;
            set
            {
                _availableQty = value;
                OnPropertyChanged(nameof(AvailableQty));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private StockCheckItemStatus _status;
        public StockCheckItemStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsChecking));
            }
        }

        public bool IsChecking => Status == StockCheckItemStatus.Checking;

        public string StatusText => Status switch
        {
            StockCheckItemStatus.Checking => "sprawdzanie...",
            StockCheckItemStatus.Available => $"✓ dostępny ({RequestedQty:0.##}/{AvailableQty:0.##})",
            StockCheckItemStatus.Insufficient => $"✗ brak (dostępne {AvailableQty:0.##}, potrzeba {RequestedQty:0.##})",
            _ => string.Empty
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
