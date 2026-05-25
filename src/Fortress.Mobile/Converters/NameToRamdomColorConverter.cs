using System.Globalization;
namespace Fortress.Converters
{
    public class NameToRamdomColorConverter : IValueConverter
    {
        private static Dictionary<char, string> CharColors = new Dictionary<char, string>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            #region Colors
            if (CharColors.Count == 0)
            {
                CharColors.Add('a', "#2b64a3");
                CharColors.Add('b', "#2b64a3");
                CharColors.Add('c', "#2b64a3");
                CharColors.Add('d', "#123360");
                CharColors.Add('e', "#ed5858");
                CharColors.Add('f', "#123360");
                CharColors.Add('g', "#fcba03");
                CharColors.Add('h', "#fcba03");
                CharColors.Add('i', "#1ec1e6");
                CharColors.Add('j', "#bfa55e");
                CharColors.Add('k', "#bfa55e");
                CharColors.Add('l', "#bfa55e");
                CharColors.Add('m', "#566dbf");
                CharColors.Add('n', "#566dbf");
                CharColors.Add('o', "#566dbf");
                CharColors.Add('p', "#603cc9");
                CharColors.Add('q', "#603cc9");
                CharColors.Add('r', "#603cc9");
                CharColors.Add('s', "#b656bf");
                CharColors.Add('t', "#b656bf");
                CharColors.Add('u', "#b656bf");
                CharColors.Add('v', "#56C69B");
                CharColors.Add('w', "#fcba03");
                CharColors.Add('x', "#29becc");
                CharColors.Add('y', "#29becc");
                CharColors.Add('z', "#ad4040");

            }
            #endregion
            if (value is null)
                return null;
            var str = value.ToString().ToLower();

            if (CharColors.ContainsKey(str[0]))
                return CharColors[str[0]];
            return null;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }

}
