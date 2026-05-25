using System.Globalization;
using Microsoft.Maui.Controls;

namespace Fortress.Converters
{
    /// <summary>
    /// Returns TrueObject when the bound bool is true, FalseObject otherwise.
    /// Null-safe: never throws, returns BindableProperty.UnsetValue when the
    /// result cannot be coerced to the target type.
    /// </summary>
    public class BoolToObjectConverter : IValueConverter
    {
        public object TrueObject { get; set; }
        public object FalseObject { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                object result = (value is bool b && b) ? TrueObject : FalseObject;

                // Nothing set for this branch — tell MAUI to leave the property alone
                if (result is null)
                    return BindableProperty.UnsetValue;

                // Already assignable — no coercion needed
                if (targetType == null || targetType.IsAssignableFrom(result.GetType()))
                    return result;

                if (result is string str)
                {
                    // Color — Convert.ChangeType cannot handle MAUI's Color
                    if (targetType == typeof(Color))
                    {
                        try { return Color.Parse(str); }
                        catch { return BindableProperty.UnsetValue; }
                    }

                    // Brush — e.g. SolidColorBrush from a hex string
                    if (targetType == typeof(Brush) || targetType.IsSubclassOf(typeof(Brush)))
                    {
                        try { return new SolidColorBrush(Color.Parse(str)); }
                        catch { return BindableProperty.UnsetValue; }
                    }

                    // Everything else — primitive numeric coercion
                    try { return System.Convert.ChangeType(str, targetType, CultureInfo.InvariantCulture); }
                    catch { return BindableProperty.UnsetValue; }
                }

                // Color → Brush widening (e.g. BackgroundColor binding)
                if (result is Color color && (targetType == typeof(Brush) || targetType.IsSubclassOf(typeof(Brush))))
                    return new SolidColorBrush(color);

                return result;
            }
            catch
            {
                return BindableProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (TrueObject != null && value != null && value.Equals(TrueObject))
                return true;
            if (FalseObject != null && value != null && value.Equals(FalseObject))
                return false;
            return BindableProperty.UnsetValue;
        }
    }
}
