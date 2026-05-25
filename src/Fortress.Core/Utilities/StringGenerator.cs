using System.Text;

namespace Fortress.Core.Utilities
{
    public static class StringGenerator
    {
        private const string AllowedChars =
     "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

   public static string GenerateRandomString(int length = 64)
        {
     var random = new Random();
         var sb = new StringBuilder(length);
 for (int i = 0; i < length; i++)
         sb.Append(AllowedChars[random.Next(AllowedChars.Length)]);
            return sb.ToString();
  }

    /// <summary>Generates a cryptographically random password.</summary>
    public static string GeneratePassword(int length = 20, bool useSymbols = true)
    {
      const string lower   = "abcdefghijklmnopqrstuvwxyz";
        const string upper   = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits  = "0123456789";
   const string symbols = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    var pool = lower + upper + digits + (useSymbols ? symbols : string.Empty);
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(length * 2);

        var sb = new StringBuilder(length);
        int i = 0;
     // Guarantee at least one from each required class
        sb.Append(lower[bytes[i++] % lower.Length]);
    sb.Append(upper[bytes[i++] % upper.Length]);
        sb.Append(digits[bytes[i++] % digits.Length]);
        if (useSymbols) sb.Append(symbols[bytes[i++] % symbols.Length]);

      while (sb.Length < length)
            sb.Append(pool[bytes[i++ % bytes.Length] % pool.Length]);

        // Fisher-Yates shuffle so the guaranteed chars aren't always at the start
var arr = sb.ToString().ToCharArray();
        var rng = System.Security.Cryptography.RandomNumberGenerator.GetBytes(arr.Length);
        for (int j = arr.Length - 1; j > 0; j--)
{
            int k = rng[j] % (j + 1);
       (arr[j], arr[k]) = (arr[k], arr[j]);
  }
        return new string(arr);
    }
    }
}
