using System.Globalization;
using Microsoft.Maui.Controls;

namespace Fortress.Converters
{
    public class StringToInitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string name && !string.IsNullOrWhiteSpace(name))
                {
                    var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
                    if (parts.Length == 1 && parts[0].Length > 0)
                        return parts[0][0].ToString().ToUpper();
                }
                return "?";
            }
            catch { return "?"; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => BindableProperty.UnsetValue;
    }
}
