using SkiaSharp.Extended.UI.Controls;
using System.Globalization;
namespace Fortress.Converters
{
    public class LottieSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path))
            {
                // If stored in Android Assets
                return new SKFileLottieImageSource { File = path };

                // OR if stored as resource:
                // return new SKResourceLottieImageSource { Resource = $"Fortress.Mobile.Resources.Raw.{path}" };
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }
}
