using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class FormatDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = value as string;
            try
            {
                return string.IsNullOrEmpty(v) ? string.Empty : DateTime.Parse(value as string).ToLocalTime().ToString();
            }
            catch (Exception)
            {

                
            }
            return "NAN";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

    }
}
