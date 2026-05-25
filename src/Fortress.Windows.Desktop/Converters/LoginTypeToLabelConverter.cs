using Fortress.Core.Models;
using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a LoginType enum to a display label.</summary>
    public class LoginTypeToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not LoginType t) return string.Empty;
            return t switch
            {
                LoginType.Web => "Web",
                LoginType.PhoneApp => "Phone App",
                LoginType.DesktopApp => "Desktop App",
                LoginType.WindowsLocal => "Windows Local",
                LoginType.MacLocal => "Mac Local",
                LoginType.Domain => "Domain",
                LoginType.AzureAD => "Azure AD",
                _ => t.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
    }
}
