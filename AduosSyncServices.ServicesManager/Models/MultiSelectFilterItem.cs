using System.ComponentModel;

namespace AduosSyncServices.ServicesManager.Models
{
    public class MultiSelectFilterItem : INotifyPropertyChanged
    {
        public string Display { get; }
        public object Value { get; }

        public MultiSelectFilterItem(string display, object value)
        {
            Display = display;
            Value = value;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
