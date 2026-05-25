using LiteDB;
using System;

namespace Fortress.Mobile.Core.Models
{
    public class Credential
    {
        public Guid Id { get; set; }
        public CredentialType CredentialType { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        /// <summary>Data holds the TOTP secret when HasOtp is true.</summary>
        public string Data { get; set; }
        public string Meta { get; set; }

        /// <summary>
        /// Password strength score (0–100) calculated against the plaintext password
        /// before encryption. Stored so the VaultHealthCalculator can analyse
        /// without needing to decrypt.
        /// 0 = not yet calculated (legacy record).
        /// </summary>
        public int PasswordStrengthScore { get; set; }

        /// <summary>
        /// Cached strength level derived from <see cref="PasswordStrengthScore"/>.
        /// Stored as int to avoid LiteDB enum serialisation issues.
        /// </summary>
        public int PasswordStrengthLevel { get; set; }

        /// <summary>
        /// SHA-256 hash of the plaintext password (hex, lowercase), stored before encryption.
        /// Used by <see cref="VaultHealthCalculator"/> to detect reused passwords without decrypting.
        /// Empty string on legacy records.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;
    }


    public sealed class ExtraInfo
    {
        public bool DefaultPassword { get; set; }
        public bool DisableAutoFill { get; set; }
    }
    public class PasswordGenerationOptions
    {
        public PasswordGenerationOptions() { }

        public PasswordGenerationOptions(bool defaultOptions)
        {
            if (defaultOptions)
            {
                Length = 14;
                AllowAmbiguousChar = true;
                Number = true;
                MinNumber = 1;
                Uppercase = true;
                MinUppercase = 0;
                Lowercase = true;
                MinLowercase = 0;
                Special = false;
                MinSpecial = 1;
                Type = "password";
                NumWords = 3;
                WordSeparator = "-";
                Capitalize = false;
                IncludeNumber = false;
            }
        }

        public int? Length { get; set; }
        public bool? AllowAmbiguousChar { get; set; }
        public bool? Number { get; set; }
        public int? MinNumber { get; set; }
        public bool? Uppercase { get; set; }
        public int? MinUppercase { get; set; }
        public bool? Lowercase { get; set; }
        public int? MinLowercase { get; set; }
        public bool? Special { get; set; }
        public int? MinSpecial { get; set; }
        public string Type { get; set; }
        public int? NumWords { get; set; }
        public string WordSeparator { get; set; }
        public bool? Capitalize { get; set; }
        public bool? IncludeNumber { get; set; }

        public void Merge(PasswordGenerationOptions defaults)
        {
            Length = Length ?? defaults.Length;
            AllowAmbiguousChar = AllowAmbiguousChar ?? defaults.AllowAmbiguousChar;
            Number = Number ?? defaults.Number;
            MinNumber = MinNumber ?? defaults.MinNumber;
            Uppercase = Uppercase ?? defaults.Uppercase;
            MinUppercase = MinUppercase ?? defaults.MinUppercase;
            Lowercase = Lowercase ?? defaults.Lowercase;
            MinLowercase = MinLowercase ?? defaults.MinLowercase;
            Special = Special ?? defaults.Special;
            MinSpecial = MinSpecial ?? defaults.MinSpecial;
            Type = Type ?? defaults.Type;
            NumWords = NumWords ?? defaults.NumWords;
            WordSeparator = WordSeparator ?? defaults.WordSeparator;
            Capitalize = Capitalize ?? defaults.Capitalize;
            IncludeNumber = IncludeNumber ?? defaults.IncludeNumber;
        }
    }
   
}
