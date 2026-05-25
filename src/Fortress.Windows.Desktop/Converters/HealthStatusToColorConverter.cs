using Fortress.Core.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts VaultHealthStatus to a display colour.</summary>
    public class HealthStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
     if (value is not VaultHealthStatus s) return new SolidColorBrush(Colors.Gray);
            var hex = s switch
     {
       VaultHealthStatus.Excellent => "#22C55E",
    VaultHealthStatus.Good    => "#84CC16",
      VaultHealthStatus.AtRisk    => "#F59E0B",
        VaultHealthStatus.Critical  => "#EF4444",
           _ => "#94A3B8"
   };
    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
     }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
    }
}
