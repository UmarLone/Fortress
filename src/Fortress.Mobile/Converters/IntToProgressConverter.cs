using System.Globalization;

namespace Fortress.Converters
{
    /// <summary>
    /// Converts an integer 0–100 to a double 0.0–1.0 for use with ProgressBar.Progress.
    /// ConverterParameter (optional): the divisor, defaults to 100.
    /// </summary>
    public class IntToProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double divisor = 100.0;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
    divisor = p;

            if (value is int i)    return Math.Clamp(i / divisor, 0.0, 1.0);
            if (value is double d) return Math.Clamp(d / divisor, 0.0, 1.0);
  if (value is float f)  return Math.Clamp(f / divisor, 0.0, 1.0);

        return 0.0;
   }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Converts a health score (0–100) to a pixel bar height (0–60).
    /// Used by the sparkline on VaultHealthPage.
    /// ConverterParameter (optional): max pixel height, defaults to 60.
    /// </summary>
    public class ScoreToBarHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
     {
 double maxHeight = 60.0;
            if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
                maxHeight = p;

     double score = value switch
   {
    int i => i,
                double d => d,
      float f => f,
           _ => 0
            };
            return Math.Clamp(score / 100.0 * maxHeight, 2.0, maxHeight);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
       => throw new NotSupportedException();
 }
}
