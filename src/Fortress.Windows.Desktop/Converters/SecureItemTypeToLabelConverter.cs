using Fortress.Core.Models;
using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a SecureItemType to a display label.</summary>
    public class SecureItemTypeToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not SecureItemType t) return string.Empty;
            return t switch
            {
                SecureItemType.IdCard => "ID Card",
                SecureItemType.Passport => "Passport",
                SecureItemType.DriversLicense => "Driver's License",
                SecureItemType.SocialSecurity => "Social Security",
                SecureItemType.TaxNumber => "Tax Number",
                SecureItemType.WiFi => "Wi-Fi",
                SecureItemType.Ssh => "SSH Key",
                SecureItemType.Identity => "Identity",
                SecureItemType.SecureNote => "Secure Note",
                _ => t.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
    }
}
