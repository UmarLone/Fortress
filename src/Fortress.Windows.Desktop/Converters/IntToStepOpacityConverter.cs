using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>
    /// Returns 1.0 opacity for the active step, 0.45 for others.
    /// </summary>
    public class IntToStepOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int current && parameter is string pStr && int.TryParse(pStr, out int target))
                return current == target ? 1.0 : 0.45;
            return 0.45;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
 => throw new NotImplementedException();
    }
}
