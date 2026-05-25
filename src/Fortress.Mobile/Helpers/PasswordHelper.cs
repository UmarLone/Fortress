using OtpNet;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Fortress.Extensions;
namespace Fortress.Helpers
{

    public enum PasswordStrength
    {
        /// <summary>
        /// Blank Password (empty and/or space chars only)
        /// </summary>
        Blank = 0,
        /// <summary>
        /// Either too short (less than 5 chars), one-case letters only or digits only
        /// </summary>
        VeryWeak = 1,
        /// <summary>
        /// At least 5 characters, one strong condition met (>= 8 chars with 1 or more UC letters, LC letters, digits & special chars)
        /// </summary>
        Weak = 2,
        /// <summary>
        /// At least 5 characters, two strong conditions met (>= 8 chars with 1 or more UC letters, LC letters, digits & special chars)
        /// </summary>
        Medium = 3,
        /// <summary>
        /// At least 8 characters, three strong conditions met (>= 8 chars with 1 or more UC letters, LC letters, digits & special chars)
        /// </summary>
        Strong = 4,
        /// <summary>
        /// At least 8 characters, all strong conditions met (>= 8 chars with 1 or more UC letters, LC letters, digits & special chars)
        /// </summary>
        VeryStrong = 5
    }
   
    public static class PasswordHelper
    {
        private const string BASE36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static string EncodeID(ulong value)
        {
            var sb = new StringBuilder();
            while (value > 0)
            {
                sb.Insert(0, BASE36[(int)(value % (ulong)BASE36.Length)]);
                value = value / (ulong)BASE36.Length;
            }
            return sb.ToString();
        }
        public static string GenerateOtp(string secret)
        {
            var secretKey = Base32Encoding.ToBytes(secret);
            var totp = new OtpNet.Totp(secretKey);
            var otp = totp.ComputeTotp();
            return otp;
        }
        public static bool IsValidFortressHubPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            var hasNumber = new Regex(@"[0-9]+");
            var hasUpperChar = new Regex(@"[A-Z]+");
            var hasLowerChar = new Regex(@"[a-z]+");
            var hasMinimum10Chars = new Regex(@".{10,}");
            var has1Symbol = new Regex(@"[!@#$%?=*&]");

            return hasNumber.IsMatch(password) &&
                hasUpperChar.IsMatch(password) &&
                hasLowerChar.IsMatch(password) &&
                hasMinimum10Chars.IsMatch(password) &&
                has1Symbol.IsMatch(password);
        }
        public static string GeneratePassword(bool includeUppercase, bool includeLowercase, bool includeNumbers, bool includeSymbols, int numberOfLetters)
        {
            const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
            const string Numbers = "0123456789";
            const string Symbols = "!@#$%^&*()";

            string charSet = "";

            if (includeUppercase)
                charSet += UppercaseLetters;
            if (includeLowercase)
                charSet += LowercaseLetters;
            if (includeNumbers)
                charSet += Numbers;
            if (includeSymbols)
                charSet += Symbols;

            if (string.IsNullOrEmpty(charSet))
                return string.Empty;

            Random random = new Random();
            char[] password = new char[numberOfLetters];

            for (int i = 0; i < numberOfLetters; i++)
            {
                int index = random.Next(0, charSet.Length);
                password[i] = charSet[index];
            }

            return new string(password);
        }

        /// <summary>
        /// Generic method to retrieve password strength: use this for general purpose scenarios,
        /// i.e. when you don't have a strict policy to follow.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static PasswordStrength GetPasswordStrength(string password)
        {
            int score = 0;
            if (String.IsNullOrEmpty(password) || String.IsNullOrEmpty(password.Trim())) return PasswordStrength.Blank;
            if (HasMinimumLength(password, 10)) score++;
            if (HasMinimumLength(password, 12)) score++;
            if (HasUpperCaseLetter(password) && HasLowerCaseLetter(password)) score++;
            if (HasDigit(password)) score++;
            if (HasSpecialChar(password)) score++;
            return (PasswordStrength)score;
        }

        /// <summary>
        /// Sample password policy implementation:
        /// - minimum 8 characters
        /// - at lease one UC letter
        /// - at least one LC letter
        /// - at least one non-letter char (digit OR special char)
        /// </summary>
        /// <returns></returns>
        public static bool IsStrongPassword(string password)
        {
            return HasMinimumLength(password, 8)
                && HasUpperCaseLetter(password)
                && HasLowerCaseLetter(password)
                && (HasDigit(password) || HasSpecialChar(password));
        }



        /// <summary>
        /// Sample password policy implementation following the Microsoft.AspNetCore.Identity.PasswordOptions standard.
        /// </summary>
        public static bool IsValidPassword(
            string password,
            int requiredLength,
            int requiredUniqueChars,
            bool requireNonAlphanumeric,
            bool requireLowercase,
            bool requireUppercase,
            bool requireDigit)
        {
            if (!HasMinimumLength(password, requiredLength)) return false;
            if (!HasMinimumUniqueChars(password, requiredUniqueChars)) return false;
            if (requireNonAlphanumeric && !HasSpecialChar(password)) return false;
            if (requireLowercase && !HasLowerCaseLetter(password)) return false;
            if (requireUppercase && !HasUpperCaseLetter(password)) return false;
            if (requireDigit && !HasDigit(password)) return false;
            return true;
        }

        #region Helper Methods

        public static bool HasMinimumLength(string password, int minLength)
        {
            return password.Length >= minLength;
        }

        public static bool HasMinimumUniqueChars(string password, int minUniqueChars)
        {
            return password.Distinct().Count() >= minUniqueChars;
        }

        /// <summary>
        /// Returns TRUE if the password has at least one digit
        /// </summary>
        public static bool HasDigit(string password)
        {
            return password.Any(c => char.IsDigit(c));
        }

        /// <summary>
        /// Returns TRUE if the password has at least one special character
        /// </summary>
        public static bool HasSpecialChar(string password)
        {
            // return password.Any(c => char.IsPunctuation(c)) || password.Any(c => char.IsSeparator(c)) || password.Any(c => char.IsSymbol(c));
            return password.IndexOfAny("!@#$%^&*?_~-£().,".ToCharArray()) != -1;
        }

        /// <summary>
        /// Returns TRUE if the password has at least one uppercase letter
        /// </summary>
        public static bool HasUpperCaseLetter(string password)
        {
            return password.Any(c => char.IsUpper(c));
        }

        /// <summary>
        /// Returns TRUE if the password has at least one lowercase letter
        /// </summary>
        public static bool HasLowerCaseLetter(string password)
        {
            return password.Any(c => char.IsLower(c));
        }
        #endregion
    }
}
