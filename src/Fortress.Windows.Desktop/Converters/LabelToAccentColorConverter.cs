using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a string (label/issuer) to a deterministic accent colour.</summary>
    public class LabelToAccentColorConverter : IValueConverter
    {
        private static readonly string[] Palette =
        {
          "#3B82F6","#10B981","#F59E0B","#EF4444","#8B5CF6",
     "#EC4899","#06B6D4","#84CC16","#F97316","#6366F1"
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
  if (value is not string s || string.IsNullOrWhiteSpace(s))
     return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Palette[0]));
   var idx = Math.Abs(s.GetHashCode()) % Palette.Length;
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Palette[idx]));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
