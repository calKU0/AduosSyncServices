using AduosSyncServices.Contracts.OrderPlacement;
using System.ComponentModel;

namespace AduosSyncServices.ServicesManager.Models
{
    public class StockCheckItemViewModel : INotifyPropertyChanged
    {
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
