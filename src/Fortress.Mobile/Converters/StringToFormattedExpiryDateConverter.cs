using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class StringToFormattedExpiryDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string input = value as string;
            if (string.IsNullOrWhiteSpace(input)) return "";

            // Remove all non-numeric characters
            input = new string(input.Where(char.IsDigit).ToArray());

            // Add a slash after the first two digits, if they exist
            if (input.Length > 2)
            {
                input = input.Insert(2, "/");
            }

            return input;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;  // For a TwoWay binding scenario, you might want to clean this up similarly as the Convert method.
        }
    }

}
