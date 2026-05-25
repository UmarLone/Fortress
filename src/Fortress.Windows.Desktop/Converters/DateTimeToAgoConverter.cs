using System.Globalization;
using System.Windows.Data;

namespace Fortress.Windows.Desktop.Converters
{
    /// <summary>Converts a DateTime to a human-readable "ago" string.</summary>
    public class DateTimeToAgoConverter : IValueConverter
    {
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
 if (value is not DateTime dt) return string.Empty;
     var ago = DateTime.UtcNow - dt;
if (ago.TotalMinutes < 1)  return "Just now";
            if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes}m ago";
     if (ago.TotalHours   < 24) return $"{(int)ago.TotalHours}h ago";
      if (ago.TotalDays    < 7)return $"{(int)ago.TotalDays}d ago";
   if (ago.TotalDays    < 30) return $"{(int)(ago.TotalDays / 7)}w ago";
     return dt.ToLocalTime().ToString("MMM d, yyyy");
      }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
