using System;
using System.Globalization;
namespace Fortress.Converters
{
    public class CardNumberToMaskedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string cardNumber = value as string;

            if (string.IsNullOrWhiteSpace(cardNumber)) return "";
            if (cardNumber.Length <= 4) return cardNumber;

            // Mask all digits except the last 4 with '*'
            string maskedPortion = new string('*', cardNumber.Length - 4);
            string lastFourDigits = cardNumber.Substring(cardNumber.Length - 4);

            return maskedPortion + lastFourDigits;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;  // If you need a TwoWay binding, you might need a way to unmask this value. 
                           // However, it's generally safer to keep the actual card number in memory and not replace it.
        }
    }

}
