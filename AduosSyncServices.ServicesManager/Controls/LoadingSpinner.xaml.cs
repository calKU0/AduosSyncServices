using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AduosSyncServices.ServicesManager.Controls
{
    public partial class LoadingSpinner : UserControl
    {
        public LoadingSpinner()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources["SpinStoryboard"]).Begin();
        }
    }
}
