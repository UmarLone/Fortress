using Fortress.Core.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts FindingSeverity to a colour.</summary>
    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
     {
    if (value is not FindingSeverity s) return new SolidColorBrush(Colors.Gray);
            var hex = s switch
            {
   FindingSeverity.Critical => "#EF4444",
      FindingSeverity.High     => "#F97316",
    FindingSeverity.Medium   => "#F59E0B",
             FindingSeverity.Low  => "#84CC16",
    FindingSeverity.Info     => "#3B82F6",
              _ => "#94A3B8"
            };
  return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
   => throw new NotImplementedException();
    }
}
