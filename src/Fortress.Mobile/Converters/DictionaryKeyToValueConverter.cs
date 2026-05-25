using System.Globalization;
namespace Fortress.Converters
{
    public class DictionaryKeyToValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int key && parameter is Dictionary<int, string> dictionary)
            {
                if (dictionary.ContainsKey(key))
                {
                    return dictionary[key];
                }
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
