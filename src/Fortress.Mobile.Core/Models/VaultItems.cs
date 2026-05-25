using LiteDB;

namespace Fortress.Mobile.Core.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Base record shared by every vault item type.
    // ─────────────────────────────────────────────────────────────────────────
    public abstract class VaultItem
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>User-visible label / name for this item.</summary>
        public string Label { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsFavorite { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Login — websites, desktop apps, phone apps, computer logins
    // Replaces Credential for CredentialType.Web / Application /
    // PhoneApplication / Domain / WindowsLocal / MacLocal / Otp
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class LoginItem : VaultItem
    {
        /// <summary>URL or package name (androidapp:// / iosapp://).</summary>
        public string Url { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        /// <summary>AES-encrypted password.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>AES-encrypted TOTP secret. Null = no 2FA.</summary>
        public string? OtpSecret { get; set; }

        public LoginType LoginType { get; set; } = LoginType.Web;

        // ── Health fields (populated before encryption) ────────────────────
        public int PasswordStrengthScore { get; set; }
        public int PasswordStrengthLevel { get; set; }
        /// <summary>SHA-256 hex of the plaintext password. Empty on legacy records.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// When true this item requires biometric or PIN authentication before
        /// it can be used for autofill. Checked at fill-time against
        /// <see cref="PreferenceWrapper.RequireAuthForPasswordFill"/>.
        /// </summary>
        public bool RequireAuthBeforeFill { get; set; }
    }

    /// <summary>Fine-grained login sub-type replaces the 7 overloaded CredentialType values.</summary>
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

        /// <summary>Visa, Mastercard, Amex, Discover, Unknown.</summary>
        public string CardNetwork { get; set; } = "Unknown";

        public string BillingAddress { get; set; } = string.Empty;

        /// <summary>
        /// When true this card requires biometric or PIN authentication before
        /// it can be used for autofill. Checked at fill-time against
        /// <see cref="PreferenceWrapper.RequireAuthForCardFill"/>.
        /// </summary>
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
    // Unified Secure Items  (ID card, Passport, Driver's License, SSN, Tax ID,
    //           Wi-Fi, SSH key/credential)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The kind of document / credential stored in a <see cref="SecureItem"/>
    /// Determines which fields are shown / required in the unified form.
    /// </summary>
    public enum SecureItemType
    {
        IdCard      = 1,
 Passport   = 2,
      DriversLicense = 3,
        SocialSecurity = 4,
        TaxNumber      = 5,
        WiFi = 6,
 Ssh = 7,
     Identity       = 8,
        SecureNote     = 9,
    }

    /// <summary>
    /// A single vault record that can hold any of the SecureItemType kinds.
    /// Unused fields are left as empty strings – no nullable columns needed.
    /// Sensitive fields (Number, Password, PrivateKey) are AES-encrypted.
    /// </summary>
    public sealed class SecureItem : VaultItem
    {
        public SecureItemType ItemType { get; set; } = SecureItemType.IdCard;

        // ── Document fields ───────────────────────────────────────────────────
        public string FullName { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        /// <summary>AES-encrypted document / ID number.</summary>
        public string Number { get; set; } = string.Empty;
        public string IssuingCountry { get; set; } = string.Empty;
        public string IssuedDate { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;

        // ── Identity / personal profile fields ───────────────────────────────
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

        // ── Wi-Fi fields ──────────────────────────────────────────────────────
        public string Ssid { get; set; } = string.Empty;
        public string WifiSecurity { get; set; } = string.Empty;   // WPA2, WPA3, WEP, Open …
        /// <summary>AES-encrypted Wi-Fi password.</summary>
        public string Password { get; set; } = string.Empty;

        // ── SSH fields ────────────────────────────────────────────────────────
        public string Host { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        /// <summary>AES-encrypted SSH password or passphrase.</summary>
        public string SshPassword { get; set; } = string.Empty;
        /// <summary>AES-encrypted private-key PEM / OpenSSH blob.</summary>
        public string PrivateKey { get; set; } = string.Empty;
        public string KeyFingerprint { get; set; } = string.Empty;

        // ── Secure Note field ─────────────────────────────────────────────────
        /// <summary>AES-encrypted note body.</summary>
        public string NoteContent { get; set; } = string.Empty;
    }
}
