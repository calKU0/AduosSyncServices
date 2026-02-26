using System.Windows;

namespace AduosSyncServices.ServicesManager
{
    public partial class MainWindow
    {
        private void ShowLogsView()
        {
            MainContentArea.Visibility = Visibility.Visible;
            LogsViewContainer.Visibility = Visibility.Visible;
            ConfigViewContainer.Visibility = Visibility.Collapsed;
        }

        private void ShowConfigView()
        {
            MainContentArea.Visibility = Visibility.Visible;
            LogsViewContainer.Visibility = Visibility.Collapsed;
            ConfigViewContainer.Visibility = Visibility.Visible;
        }

        private void HideContentViews()
        {
            MainContentArea.Visibility = Visibility.Collapsed;
            LogsViewContainer.Visibility = Visibility.Collapsed;
            ConfigViewContainer.Visibility = Visibility.Collapsed;
        }

        private void ResetNavSelection()
        {
            BtnShowLogs.IsChecked = false;
            BtnShowConfig.IsChecked = false;
        }

        private void ShowSelectionOverlay()
        {
            ServiceSelectionOverlay.Visibility = Visibility.Visible;
            MainContentAreaNav.Visibility = Visibility.Collapsed;
        }

        private void ShowMainNavigation()
        {
            ServiceSelectionOverlay.Visibility = Visibility.Collapsed;
            MainContentAreaNav.Visibility = Visibility.Visible;
        }
    }
}
