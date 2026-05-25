using Fortress.Helpers;
using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class StringDateTimeToAgoTimeConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parsed = DateTime.TryParse((value as string), out DateTime dateTime);
            if (parsed)
            {
                return dateTime.ToTimeAgo();
            }
            return string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

    }
}
