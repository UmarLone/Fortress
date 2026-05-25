using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Resources.Fonts;
using Fortress.Models;
using System.Globalization;
namespace Fortress.Converters
{
    public class NotificationTypeToIconConverter : IValueConverter
    {
        /// <summary>
        /// This method is used to convert the bool to color.
        /// </summary>
        /// <param name="value">Gets the value.</param>
        /// <param name="targetType">Gets the target type.</param>
        /// <param name="parameter">Gets the parameter.</param>
        /// <param name="culture">Gets the culture.</param>
        /// <returns>Returns the color.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value is null)
            {
                return IconFonts.Info;
            }
            var status = (NotificationType)Enum.Parse(typeof(NotificationType), value.ToString());
            if (status == NotificationType.Warning)
                return IconFonts.Exclamation;
            if (status == NotificationType.Error)
                return IconFonts.Times;

            return IconFonts.Info;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

    }
}
