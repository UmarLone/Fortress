using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class IntToBoolIsLessThanConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if(parameter !=null)
            {
                var lessThanParse = int.TryParse(parameter.ToString(), out int lessThan);
                var parsed = int.TryParse(value.ToString(), out int progress);
                return progress < lessThan;
            }
            return false;

        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

    }
    public class TabIndexToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return GetInactiveColor();
            }

            int selectedIndex = System.Convert.ToInt32(value);
            int tabIndex = System.Convert.ToInt32(parameter);

            if (selectedIndex == tabIndex)
            {
                return GetActiveColor();
            }

            return GetInactiveColor();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Color GetActiveColor()
        {
            if (Application.Current?.Resources.TryGetValue("PrimaryColor", out var primaryColor) == true && primaryColor is Color color)
            {
                return color;
            }
            return Color.FromArgb("#4b7bc9");
        }

        private static Color GetInactiveColor()
        {
            // Use a light gray background that's clearly visible
            if (Application.Current?.Resources.TryGetValue("SurfaceColor", out var surfaceColor) == true && surfaceColor is Color color)
            {
                return color;
            }
            return Color.FromArgb("#E2E8F0");
        }
    }
    public class TabIndexToTextColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return GetInactiveColor();
            }

            int selectedIndex = System.Convert.ToInt32(value);
            int tabIndex = System.Convert.ToInt32(parameter);

            if (selectedIndex == tabIndex)
            {
                return Colors.White;
            }

            return GetInactiveColor();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Color GetInactiveColor()
        {
            if (Application.Current?.Resources.TryGetValue("TextColor", out var textColor) == true && textColor is Color color)
            {
                return color;
            }
            return Color.FromArgb("#333333");
        }
    }
}
