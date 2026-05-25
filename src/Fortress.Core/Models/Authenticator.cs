using System.ComponentModel.DataAnnotations;

namespace Fortress.Core.Models
{
    /// <summary>
    /// TOTP/HOTP authenticator entry. Pure POCO — no Prism/MAUI dependency.
    /// UI-bindable properties (Code, Progress) are updated by the ViewModel layer.
    /// </summary>
    public class Authenticator
    {
        public Guid Id { get; set; }
        public AuthenticatorType Type { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public int Digits { get; set; } = 6;
        public int Period { get; set; } = 30;
        public int Duration { get; set; } = 30;
        public long Counter { get; set; }
        public HashAlgorithmType Algorithm { get; set; }
        public string FallbackIcon { get; set; } = "authenticatorlogo.png";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsFavorite { get; set; }
        public string Notes { get; set; } = string.Empty;

        public string IconUri =>
          $"https://hubfunctions-us.azurewebsites.net/api/LogoService/Icon?domain={Issuer}&size=64&type=authenticator://";
    }

    public enum AuthenticatorType
    {
        [Display(Name = "Counter based (HOTP)")]
        Hotp = 1,
        [Display(Name = "Time based (TOTP)")]
        Totp = 2,
        [Display(Name = "Mobile OTP (mOTP)")]
        MobileOtp = 3,
        [Display(Name = "Steam")]
        SteamOtp = 4
    }

    public enum HashAlgorithmType
    {
        Sha1 = 0,
        Sha256 = 1,
        Sha512 = 2
    }
}
