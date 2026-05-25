using Fortress.Extensions;
using Fortress.Mobile.Core.Models;
using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class BoolStatusToColorConverter : IValueConverter
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
                return Color.FromArgb("#FFBE23");
            }
            if ((bool)value)
            {
                return Color.FromArgb("#3EB629");
            }
            if (!(bool)value)
            {
                return Color.FromArgb("#FF4A4A");
            }
            return Color.FromArgb("#FFBE23");
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

    }
    public class EmailMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string email = value as string;
            if (string.IsNullOrEmpty(email))
                return string.Empty;
             
            return email.MaskAsEmail();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Since this is a one-way converter for display purposes only, we don't need to implement ConvertBack
            throw new NotImplementedException();
        }

    }
    /// <summary>
    /// Shows/hides UI based on a specific NotificationType (e.g., Ask → Allow/Deny).
    /// </summary>
    public class NotificationTypeToVisibilityConverter : IValueConverter
    {
        public NotificationType TargetType { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
                return type == TargetType;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Shows expiry label only if type is Ask.
    /// </summary>
    public class ExpiryVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
                return type == NotificationType.Ask;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Flips a bool (true → false, false → true).
    /// </summary>
    public class BoolInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Shows UI if NotificationType is in a given group (e.g., Info/Warning/Error → OK button).
    /// </summary>
    public class NotificationTypeGroupVisibilityConverter : IValueConverter
    {
        public NotificationType[] TargetTypes { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type && TargetTypes != null)
            {
                foreach (var t in TargetTypes)
                {
                    if (t == type)
                        return true;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns <c>true</c> when the bound string is non-null and non-empty.
    /// Used to show/hide the clear (×) button in the search bar.
    /// </summary>
    public class StringNotEmptyConverter : IValueConverter
    {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
