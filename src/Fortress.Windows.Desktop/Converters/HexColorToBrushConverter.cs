using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a hex colour string to a SolidColorBrush.</summary>
    public class HexColorToBrushConverter : IValueConverter
{
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
   try
       {
     if (value is string hex && !string.IsNullOrWhiteSpace(hex))
      return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
   catch { }
    return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
