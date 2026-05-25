using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class LabelMaxLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string labelText = value as string;

            if (labelText == null)
                return value;
            int maxLength = 20;
            int.TryParse(parameter.ToString(), out maxLength);

            if (labelText.Length > maxLength)
                return labelText.Substring(0, maxLength) + "...";
            else
                return labelText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
