using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text.RegularExpressions;

namespace Fortress.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        public static string RemoveWhitespace(this string input)
        {
            return new string(input.ToCharArray()
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());
        }
        public static byte[] ToBytes(this string input)
        {
            return Convert.FromBase64String(input);
        }
        public static bool IsValidDomain(this string input)
        {
            return Regex.IsMatch(input,
                 @"^(?!.*?_.*?)(?!(?:[\w]+?\.)?\-[\w\.\-]*?)(?![\w]+?\-\.(?:[\w\.\-]+?))(?=[\w])(?=[\w\.\-]*?\.+[\w\.\-]*?)(?![\w\.\-]{254})(?!(?:\.?[\w\-\.]*?[\w\-]{64,}\.)+?)[\w\.\-]+?(?<![\w\-\.]*?\.[\d]+?)(?<=[\w\-]{2,})(?<![\w\-]{25})$");
        }
        public static bool IsValidEmail(this string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            // Regular expression for validating an Email
            string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

            Regex regex = new Regex(emailPattern);

            return regex.IsMatch(email);
        }
        public static bool IsValidSubdomain(this string value)
        {
            return Regex.IsMatch(value, "^[a-zA-Z0-9-]+$");
        }

        public static string ToPlainString(this SecureString secureStr)
        {
            return new NetworkCredential(string.Empty, secureStr).Password;
        }

        public static SecureString ToSecureString(this string plainStr)
        {
            return new NetworkCredential("", plainStr).SecurePassword;
        }
        public static string RemoveAllSpaces(this string value)
        {
            return Regex.Replace(value, @"\s+", "");
        }
        public static Guid ToGuid(this string value)
        {
            return Guid.Parse(value);
        }
        public static DateTime ToDateOnly(this string value)
        {
            var dt = DateTimeOffset.Parse(value);
            return new DateTime(dt.Year, dt.Month, dt.Day);
        }
        public static string MaskAsPhoneNumber(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            var result = "";
            for (var i = 0; i < s.Length - 4; i++)
                result += "*";

            return result += s.Substring(s.Length - 4);
        }
        public static string MaskAsEmail(this string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                return email;

            string[] emailArr = email.Split('@');
            string domainExt = Path.GetExtension(email);

            string maskedEmail = string.Format("{0}****{1}@{2}****{3}{4}",
                emailArr[0][0],
                emailArr[0].Substring(emailArr[0].Length - 1),
                emailArr[1][0],
                emailArr[1].Substring(emailArr[1].Length - domainExt.Length - 1, 1),
                domainExt
                );

            return maskedEmail;
        }
        public static string Before(this string value, string a)
        {
            int posA = value.IndexOf(a);
            if (posA == -1)
            {
                return "";
            }
            return value.Substring(0, posA);
        }


        public static string After(this string value, string a)
        {
            int posA = value.LastIndexOf(a);
            if (posA == -1)
            {
                return "";
            }
            int adjustedPosA = posA + a.Length;
            if (adjustedPosA >= value.Length)
            {
                return "";
            }
            return value.Substring(adjustedPosA);
        }
        public static string ToMax(this string value, int max)
        {
             
            if (value == null)
                return value;
            if (value.Length > max)
                return value.Substring(0, max) + "...";
            else
                return value;
        }
    }
}
