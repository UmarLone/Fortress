using LiteDB;

namespace Fortress.Core.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Base record shared by every vault item type.
    // ─────────────────────────────────────────────────────────────────────────
    public abstract class VaultItem
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Label { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsFavorite { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Login – websites, desktop apps, phone apps, computer logins
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class LoginItem : VaultItem
    {
        public string Url { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        /// <summary>AES-encrypted password.</summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>AES-encrypted TOTP secret. Null = no 2FA.</summary>
        public string? OtpSecret { get; set; }
        public LoginType LoginType { get; set; } = LoginType.Web;
        public int PasswordStrengthScore { get; set; }
        public int PasswordStrengthLevel { get; set; }
        /// <summary>SHA-256 hex of the plaintext password.</summary>
        public string PasswordHash { get; set; } = string.Empty;
        public bool RequireAuthBeforeFill { get; set; }
    }

    public enum LoginType
    {
        Web = 1,
        PhoneApp = 2,
        DesktopApp = 3,
        WindowsLocal = 4,
        MacLocal = 5,
        Domain = 6,
        AzureAD = 7
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Credit Card
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class CreditCardItem : VaultItem
    {
        public string CardholderName { get; set; } = string.Empty;
        /// <summary>AES-encrypted card number.</summary>
        public string Number { get; set; } = string.Empty;
        public string ExpiryMonth { get; set; } = string.Empty;
        public string ExpiryYear { get; set; } = string.Empty;
        /// <summary>AES-encrypted CVV.</summary>
        public string Cvv { get; set; } = string.Empty;
        public string CardNetwork { get; set; } = "Unknown";
        public string BillingAddress { get; set; } = string.Empty;
        public bool RequireAuthBeforeFill { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Identity / Address
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class IdentityItem : VaultItem
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Secure Note
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class SecureNoteItem : VaultItem
    {
        /// <summary>AES-encrypted note body.</summary>
        public string Content { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Secure Item – unified: ID card, Passport, DL, SSN, Tax, Wi-Fi, SSH
    // ─────────────────────────────────────────────────────────────────────────
    public enum SecureItemType
    {
        IdCard = 1,
        Passport = 2,
        DriversLicense = 3,
        SocialSecurity = 4,
        TaxNumber = 5,
        WiFi = 6,
        Ssh = 7,
        Identity = 8,
        SecureNote = 9,
    }

    public sealed class SecureItem : VaultItem
    {
        public SecureItemType ItemType { get; set; } = SecureItemType.IdCard;

        // Document fields
        public string FullName { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        /// <summary>AES-encrypted document / ID number.</summary>
        public string Number { get; set; } = string.Empty;
        public string IssuingCountry { get; set; } = string.Empty;
        public string IssuedDate { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;

        // Identity / personal profile fields
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;

        // Wi-Fi fields
        public string Ssid { get; set; } = string.Empty;
        public string WifiSecurity { get; set; } = string.Empty;
        /// <summary>AES-encrypted Wi-Fi password.</summary>
        public string Password { get; set; } = string.Empty;

        // SSH fields
        public string Host { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        /// <summary>AES-encrypted SSH password or passphrase.</summary>
        public string SshPassword { get; set; } = string.Empty;
        /// <summary>AES-encrypted private-key PEM / OpenSSH blob.</summary>
        public string PrivateKey { get; set; } = string.Empty;
        public string KeyFingerprint { get; set; } = string.Empty;

        // Secure Note
        /// <summary>AES-encrypted note body.</summary>
        public string NoteContent { get; set; } = string.Empty;
    }
}
