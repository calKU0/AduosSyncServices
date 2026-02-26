using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AduosSyncServices.ServicesManager.Models
{
    public class LogFileItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public DateTime Date { get; set; }

        private int warningsCount;

        public int WarningsCount
        {
            get => warningsCount;
            set
            {
                if (warningsCount != value)
                {
                    warningsCount = value;
                    OnPropertyChanged();
                }
            }
        }

        private int errorsCount;

        public int ErrorsCount
        {
            get => errorsCount;
            set
            {
                if (errorsCount != value)
                {
                    errorsCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}