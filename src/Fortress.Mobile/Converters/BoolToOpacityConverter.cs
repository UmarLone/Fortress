using System.Globalization;

namespace Fortress.Converters
{
  /// <summary>Returns 1.0 when true, 0.3 when false – for fading inactive achievement badges.</summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? 1.0 : 0.3;

 public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => BindableProperty.UnsetValue;
  }
}
