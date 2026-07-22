using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AduosSyncServices.ServicesManager.Helpers
{
    // Turns a "#RRGGBB" hex string (OrderInternalStatus.Color) into a Brush for the status pill.
    public class HexColorToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                }
                catch
                {
                    // Fall through to the default brush on an unparseable value.
                }
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
