using System.Globalization;

namespace Fortress.Converters
{
/// <summary>Returns true when the integer value is greater than zero.</summary>
    public class IsPositiveConverter : IValueConverter
    {
     public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
  if (value is int i) return i > 0;
    return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
   => BindableProperty.UnsetValue;
    }
}
