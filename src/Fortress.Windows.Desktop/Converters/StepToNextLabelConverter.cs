using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{

    /// <summary>
    /// Returns "Continue", "Finish", etc. based on the current step index.
    /// </summary>
    public class StepToNextLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int step ? step switch
        {
            0 => "Get Started",
            1 => "Continue",
            2 => "Continue",
            3 => "Continue",
            _ => "Open Fortress"
        } : "Continue";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotImplementedException();
    }
}
