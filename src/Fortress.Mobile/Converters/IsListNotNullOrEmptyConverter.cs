using System.Collections;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Fortress.Converters
{
    /// <summary>
    /// Returns true when the bound collection is not null and has at least one item.
    /// Drop-in replacement for toolkit:IsListNotNullOrEmptyConverter.
    /// </summary>
    public class IsListNotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is ICollection col)
                    return col.Count > 0;
                if (value is IEnumerable en)
                {
                    var enumerator = en.GetEnumerator();
                    bool hasAny = enumerator.MoveNext();
                    (enumerator as IDisposable)?.Dispose();
                    return hasAny;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => BindableProperty.UnsetValue;
    }
}
