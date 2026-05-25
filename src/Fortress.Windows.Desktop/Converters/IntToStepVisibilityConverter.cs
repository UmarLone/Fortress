using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>
    /// Converts an int (CurrentStep) and a string ConverterParameter (target step)
    /// to Visible when equal, Collapsed otherwise.
    /// Usage: Visibility="{Binding CurrentStep, Converter={StaticResource StepToVisibility}, ConverterParameter=1}"
    /// </summary>
    public class IntToStepVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int current && parameter is string pStr && int.TryParse(pStr, out int target))
                return current == target ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
