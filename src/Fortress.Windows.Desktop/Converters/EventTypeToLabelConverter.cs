using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts an event type int to a readable label.</summary>
    public class EventTypeToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int t) return string.Empty;
            return t switch
            {
                1 => "Copied",
                2 => "Modified",
                3 => "Blocked",
                4 => "Updated",
                5 => "Unlocked",
                6 => "Locked",
                _ => "Event"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    => throw new NotImplementedException();
    }
}
