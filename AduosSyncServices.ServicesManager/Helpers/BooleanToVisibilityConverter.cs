using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AduosSyncServices.ServicesManager.Helpers
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isTrue = value is true;

            // ConverterParameter="invert" flips the result (Visible when false).
            if (parameter is string s && string.Equals(s, "invert", StringComparison.OrdinalIgnoreCase))
                isTrue = !isTrue;

            return isTrue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Visibility.Visible;
        }
    }
}
