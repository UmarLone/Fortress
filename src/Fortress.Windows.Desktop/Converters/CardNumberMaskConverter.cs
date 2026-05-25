using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a card number string to a masked display string.</summary>
    public class CardNumberMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || s.Length < 4) return "•••• •••• •••• ••••";
            var last4 = s[^4..];
            return $"•••• •••• •••• {last4}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
     => throw new NotImplementedException();
    }
}
