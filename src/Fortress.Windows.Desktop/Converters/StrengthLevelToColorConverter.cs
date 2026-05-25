using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts password strength level 0-4 to a colour brush.</summary>
    public class StrengthLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
{
       var level = value is int i ? i : 0;
    var hex = level switch
    {
        0 => "#EF4444",
                1 => "#F97316",
         2 => "#F59E0B",
      3 => "#84CC16",
       4 => "#22C55E",
  _ => "#94A3B8"
      };
    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  => throw new NotImplementedException();
    }
}
