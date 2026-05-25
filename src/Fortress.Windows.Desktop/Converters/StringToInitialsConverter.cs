using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a string to initials (up to 2 chars) for avatar placeholder.</summary>
    public class StringToInitialsConverter : IValueConverter
    {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s)) return "?";
            var parts = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
   if (parts.Length >= 2) return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return s.Length >= 2 ? s[..2].ToUpper() : s.ToUpper();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
