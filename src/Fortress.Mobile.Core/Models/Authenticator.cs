using LiteDB;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fortress.Mobile.Core.Models
{
    public class Authenticator : BindableBase
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();

        public AuthenticatorType Type { get; set; } = AuthenticatorType.Totp;

        public string Username { get; set; } = string.Empty;

        private string iconUri = string.Empty;
        public string IconUri
        {
            get => $"https://hubfunctions-us.azurewebsites.net/api/LogoService/Icon?domain={Issuer}&size=64&type=authenticator://";
            set => SetProperty(ref iconUri, value);
        }

        public string FallbackIcon { get; set; } = "authenticatorlogo.png";

        public string Issuer { get; set; } = string.Empty;

        private int digits = 6;
        public int Digits
        {
            get => digits;
            set => SetProperty(ref digits, value);
        }
        public long Counter { get; set; }
        public string Secret { get; set; } = string.Empty;


        private string code = string.Empty;

        public string Code
        {
            get => code;
            set => SetProperty(ref code, value);
        }

        private double progress;
        public double Progress
        {
            get => progress;
            set => SetProperty(ref progress, value);
        }

        private int duration = 30;
        public int Duration
        {
            get => duration;
            set => SetProperty(ref duration, value);
        }

        private int period = 30;
        public int Period
        {
            get => period;
            set => SetProperty(ref period, value);
        }

        /// <summary>Hash algorithm used for OTP generation. Stored as int — matches Fortress.Core.HashAlgorithmType values.</summary>
        public HashAlgorithm Algorithm { get; set; } = HashAlgorithm.Sha1;

        // ── Metadata ─────────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsFavorite { get; set; }
        public string Notes { get; set; } = string.Empty;

        // ── Multi-select support ─────────────────────────────────────────────
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public enum AuthenticatorType
    {
        [Display(Name = "Counter based (HOTP)")]
        Hotp = 1,
        [Display(Name = "Time based (TOTP)")]
        Totp = 2,
        [Display(Name = "Mobile OTP (mOPT)")]
        MobileOtp = 3,
        [Display(Name = "Steam")]
        SteamOtp = 4
    }


    public enum HashAlgorithm
    {
        Sha1 = 0,
        Sha256 = 1,
        Sha512 = 2
    }


}
