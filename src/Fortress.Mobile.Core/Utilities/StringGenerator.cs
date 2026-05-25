using System;
using System.Text;

namespace Fortress.Mobile.Core.Utilities
{
    public static class StringGenerator
    {
        public static string GenerateRandomString(int length=64)
        {
            string AllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Random random = new Random();
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int randomIndex = random.Next(AllowedChars.Length);
                sb.Append(AllowedChars[randomIndex]);
            }
            return sb.ToString();
        }
    }
}
