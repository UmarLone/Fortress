using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class RemovePrefixConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                RemovePrefix(value as string);
            }
            return value;
        }

        static string RemovePrefix(string domain)
        {

            string[] prefixesToRemove = { "androidapp://", "www.", "http://", "https://", "iosapp://", "http://www.", "https://www.", "com.", ".mobile" };
            foreach (string prefix in prefixesToRemove)
            {
                domain = domain.Replace(prefix, string.Empty);
            }
            return domain;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
