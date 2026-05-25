using Fortress.Core.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a NotificationType to a colour.</summary>
    public class NotificationTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not NotificationType t) return new SolidColorBrush(Colors.Gray);
            var hex = t switch
            {
                NotificationType.Alert => "#EF4444",
                NotificationType.BreachDetected => "#EF4444",
                NotificationType.Warning => "#F59E0B",
                NotificationType.SaveLoginPrompt => "#3B82F6",
                NotificationType.Info => "#94A3B8",
                _ => "#94A3B8"
            };
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
     => throw new NotImplementedException();
    }
}
