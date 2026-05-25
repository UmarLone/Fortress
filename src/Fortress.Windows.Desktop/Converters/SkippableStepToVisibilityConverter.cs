using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>
    /// Returns Visible for steps that can be skipped (PIN=2, CloudSync=3).
    /// </summary>
    public class SkippableStepToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
          => value is int step && (step == 2 || step == 3)
         ? System.Windows.Visibility.Visible
         : System.Windows.Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
          => throw new NotImplementedException();
    }
}
