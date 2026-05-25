using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var selectedParsed = int.TryParse(value.ToString(),out int selectedValue);
            var paramParsed = int.TryParse(parameter.ToString(), out int compareValue);
            return selectedValue == compareValue;
             
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int timeoutValue && parameter is int radioTimeout)
            {
                return timeoutValue == radioTimeout;
            }

            return false;
        }
    }
}
