using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts password strength score 0-100 to a label.</summary>
    public class StrengthLevelToLabelConverter : IValueConverter
 {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
      {
  var level = value is int i ? i : 0;
            return level switch
       {
      0 => "Very Weak",
        1 => "Weak",
          2 => "Fair",
3 => "Strong",
       4 => "Very Strong",
       _ => "Unknown"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    => throw new NotImplementedException();
    }
}
