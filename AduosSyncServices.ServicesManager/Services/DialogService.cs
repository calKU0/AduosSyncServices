using System.Windows;

namespace AduosSyncServices.ServicesManager.Services
{
    public class DialogService
    {
        public void ShowInfo(string message, string title = "Info")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title = "Błąd walidacji")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title = "Błąd")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
