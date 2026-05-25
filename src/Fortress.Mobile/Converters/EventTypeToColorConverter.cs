using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class EventTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Color.FromHex("#407CCA");
            var eventType = value as string;

            if (eventType.Equals("Non-Fortress Unlock"))
            {
                return Color.FromArgb("#f5bf42");
            }
            if (eventType.Equals("Fortress Unlock") || eventType.Equals("Session Unlocked"))
            {
                return Color.FromArgb("#f5bf42");
            }
            if (eventType.Equals("Fortress Lock") || eventType.Equals("Session Locked"))
            {
                return Color.FromArgb("#f55742");
            }
            return Color.FromHex("#407CCA");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Color.FromHex("#407CCA");
        }
    }
}
