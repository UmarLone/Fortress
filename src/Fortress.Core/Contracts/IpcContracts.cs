namespace Fortress.Core.Contracts
{
    // ── Session ───────────────────────────────────────────────────────────────
    public sealed class VaultSessionInfo
    {
        public bool IsUnlocked { get; init; }
        public string? DeviceId { get; init; }
        public DateTime? UnlockedAt { get; init; }

     // ── Setup / onboarding ────────────────────────────────────────────────
        /// <summary>
        /// True once the user has completed first-run setup on the desktop app.
        /// Clients (extension, etc.) must gate their UI on this flag and show
        /// a "please complete setup on the desktop app" message when false.
        /// </summary>
        public bool IsSetupComplete { get; init; }

// ── Enabled lock methods ──────────────────────────────────────────────
 /// <summary>True when PIN unlock has been configured on the service.</summary>
        public bool IsPinEnabled { get; init; }
        /// <summary>True when Windows Hello / biometric unlock has been configured.</summary>
   public bool IsBiometricEnabled { get; init; }
        /// <summary>True when master-password unlock is available (always true once setup is done).</summary>
        public bool IsPasswordEnabled { get; init; }
    }

    // ── Autofill IPC ──────────────────────────────────────────────────────────
    public sealed class GetCredentialsRequest
    {
        public string Url { get; init; } = string.Empty;
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class GetCredentialsResponse
    {
        public bool IsVaultLocked { get; init; }
        public IReadOnlyList<AutofillSuggestionDto> Suggestions { get; init; } = Array.Empty<AutofillSuggestionDto>();
    }

    public sealed class AutofillSuggestionDto
    {
        public string Id { get; init; } = string.Empty;
        public string Type { get; init; } = "Login";   // Login | CreditCard | Identity
        public string Label { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string? IconUri { get; init; }
        public int MatchScore { get; init; }
        public string MatchReason { get; init; } = string.Empty;
        public bool RequireAuthBeforeFill { get; init; }
    }

    public sealed class FillConfirmedRequest
    {
        public string CredentialId { get; init; } = string.Empty;
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class FillConfirmedResponse
    {
        public bool Success { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public string? Totp { get; init; }
        public string? CardNumber { get; init; }
        public string? CardExpiry { get; init; }
        public string? CardCvv { get; init; }
        public string? CardholderName { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public sealed class SaveNewLoginRequest
    {
        public string Url { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string SessionToken { get; init; } = string.Empty;
    }

    // ── Setup (extension-mode first-run) ──────────────────────────────────────
    /// <summary>
    /// First-run setup written by the browser extension when no desktop app is
    /// present to own <c>vault.settings.json</c>. The service persists the
    /// master-password verifier, the optional PIN hash, and a "setup complete"
    /// flag in its preferences so subsequent restarts skip the wizard.
    ///
    /// Returns 409 (Conflict) if the service is already set up — callers must
    /// use <c>ResetVault</c> first if they want to start over.
    /// </summary>
    public sealed class CompleteSetupRequest
    {
        public string MasterPassword { get; init; } = string.Empty;
        /// <summary>Optional 4-digit PIN. Empty / null = no PIN unlock.</summary>
        public string? Pin { get; init; }
    }

    // ── Vault action IPC ──────────────────────────────────────────────────────
    public enum VaultAction { Lock, Unlock, Sync, GetStatus }

    public sealed class VaultActionRequest
    {
        public VaultAction Action { get; init; }
        public string SessionToken { get; init; } = string.Empty;
        /// <summary>PIN or password for Unlock action.</summary>
        public string? Credential { get; init; }
    }

    public sealed class VaultActionResponse
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public VaultSessionInfo? SessionInfo { get; init; }
    }

    // ── Generic IPC envelope ──────────────────────────────────────────────────
    public sealed class IpcRequest
    {
        public string Method { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;   // JSON
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class IpcResponse
    {
        public bool Success { get; init; }
        public string Payload { get; init; } = string.Empty;   // JSON
        public string? ErrorMessage { get; init; }
        public int StatusCode { get; init; } = 200;
    }

    // ── Vault CRUD ──────────────────────────────────────────────────────────
    // Login items — nested Item form (used for full round-trip management)
    public sealed class GetLoginItemsRequest
    {
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class SaveLoginItemRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Models.LoginItem Item { get; init; } = new();
        /// <summary>
        /// When true the service encrypts Password and OtpSecret before persisting.
        /// Set to false when re-saving an item whose sensitive fields are already encrypted.
        /// </summary>
        public bool EncryptSensitiveFields { get; init; }
    }

    public sealed class DeleteItemRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid Id { get; init; }
    }

    // Authenticators — flat fields matching browser-extension payload
    public sealed class GetAuthenticatorsRequest
    {
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class SaveAuthenticatorRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;      // maps to Issuer
        public string Username { get; init; } = string.Empty;
        public string Secret { get; init; } = string.Empty;
        public string AuthType { get; init; } = "TOTP";         // "TOTP"|"HOTP"|"mOTP"|"Steam"
        public int Digits { get; init; } = 6;
        public int Period { get; init; } = 30;
        public string Algorithm { get; init; } = "SHA1";        // "SHA1"|"SHA256"|"SHA512"
        public bool IsFavorite { get; init; }
    }

    // Credit cards — flat fields matching browser-extension payload
    public sealed class GetCreditCardsRequest
    {
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class SaveCreditCardRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string CardholderName { get; init; } = string.Empty;
        /// <summary>Plaintext card number — service encrypts before persisting.</summary>
        public string Number { get; init; } = string.Empty;
        /// <summary>Combined expiry in "MM/YY" or "MM/YYYY" format.</summary>
        public string Expiry { get; init; } = string.Empty;
        /// <summary>Plaintext CVV — service encrypts before persisting.</summary>
        public string Cvv { get; init; } = string.Empty;
        public bool IsFavorite { get; init; }
        public string Notes { get; init; } = string.Empty;
    }

    // Secure items — flat fields matching browser-extension payload
    public sealed class GetSecureItemsRequest
    {
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class SaveSecureItemRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public string ItemType { get; init; } = string.Empty;   // "WiFi"|"Ssh"|"IdCard"|etc.
        public string Label { get; init; } = string.Empty;
        public bool IsFavorite { get; init; }
        public string Notes { get; init; } = string.Empty;

        // Document fields
        public string FullName { get; init; } = string.Empty;
        public string DateOfBirth { get; init; } = string.Empty;
        public string Nationality { get; init; } = string.Empty;
        /// <summary>Plaintext document / ID number — service encrypts before persisting.</summary>
        public string Number { get; init; } = string.Empty;
        public string IssuingCountry { get; init; } = string.Empty;
        public string IssuedDate { get; init; } = string.Empty;
        public string ExpiryDate { get; init; } = string.Empty;

        // Identity / personal profile fields
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string AddressLine1 { get; init; } = string.Empty;
        public string AddressLine2 { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;

        // Wi-Fi fields
        public string Ssid { get; init; } = string.Empty;
        public string WifiSecurity { get; init; } = string.Empty;
        /// <summary>Plaintext Wi-Fi password — service encrypts before persisting.</summary>
        public string Password { get; init; } = string.Empty;

        // SSH fields
        public string Host { get; init; } = string.Empty;
        public string Port { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        /// <summary>Plaintext SSH password — service encrypts before persisting.</summary>
        public string SshPassword { get; init; } = string.Empty;
        /// <summary>Plaintext private key — service encrypts before persisting.</summary>
        public string PrivateKey { get; init; } = string.Empty;
        public string KeyFingerprint { get; init; } = string.Empty;

        // Secure Note
        /// <summary>Plaintext note body — service encrypts before persisting.</summary>
        public string NoteContent { get; init; } = string.Empty;
    }

    // Identities — flat fields matching browser-extension payload
    public sealed class GetIdentitiesRequest
    {
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class SaveIdentityRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string AddressLine1 { get; init; } = string.Empty;
        public string AddressLine2 { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
        public bool IsFavorite { get; init; }
        public string Notes { get; init; } = string.Empty;
    }

    // Secure Notes (separate lightweight collection)
    public sealed class GetSecureNotesRequest
    {
        public string SessionToken { get; init; } = string.Empty;
    }

    public sealed class SaveSecureNoteRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        /// <summary>Plaintext note body — service encrypts before persisting.</summary>
        public string Content { get; init; } = string.Empty;
        public bool IsFavorite { get; init; }
    }

    // ── Update requests ──────────────────────────────────────────────────────
    // Null / empty-string sensitive fields mean "keep existing encrypted value".

    public sealed class UpdateLoginRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid Id { get; init; }
        public string? Label { get; init; }
        public string? Url { get; init; }
        public string? Username { get; init; }
        /// <summary>Null or empty = keep existing encrypted password.</summary>
        public string? Password { get; init; }
        /// <summary>Null or empty = keep existing encrypted TOTP secret.</summary>
        public string? TotpSecret { get; init; }
        public string? Notes { get; init; }
        public bool? IsFavorite { get; init; }
    }

    public sealed class UpdateCreditCardRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid Id { get; init; }
        public string? Label { get; init; }
        public string? CardholderName { get; init; }
        /// <summary>Null or empty = keep existing encrypted number.</summary>
        public string? Number { get; init; }
        /// <summary>Combined expiry "MM/YY" or "MM/YYYY". Null = keep existing.</summary>
        public string? Expiry { get; init; }
        /// <summary>Null or empty = keep existing encrypted CVV.</summary>
        public string? Cvv { get; init; }
        public bool? IsFavorite { get; init; }
        public string? Notes { get; init; }
    }

    public sealed class UpdateIdentityRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid Id { get; init; }
        public string? Label { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? Company { get; init; }
        public string? AddressLine1 { get; init; }
        public string? AddressLine2 { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public string? Country { get; init; }
        public string? PostalCode { get; init; }
        public bool? IsFavorite { get; init; }
        public string? Notes { get; init; }
    }

    public sealed class UpdateSecureNoteRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid Id { get; init; }
        public string? Label { get; init; }
        /// <summary>Null or empty = keep existing encrypted content.</summary>
        public string? Content { get; init; }
        public bool? IsFavorite { get; init; }
    }

    public sealed class UpdateAuthenticatorRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid Id { get; init; }
        public string? Label { get; init; }
        public string? Username { get; init; }
        public string? Secret { get; init; }
        public string? AuthType { get; init; }
        public int? Digits { get; init; }
        public int? Period { get; init; }
        public string? Algorithm { get; init; }
        public bool? IsFavorite { get; init; }
    }

    public sealed class UpdateSecureItemRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid Id { get; init; }
        public string? Label { get; init; }
        public bool? IsFavorite { get; init; }
        public string? Notes { get; init; }

        // Document fields
        public string? FullName { get; init; }
        public string? DateOfBirth { get; init; }
        public string? Nationality { get; init; }
        /// <summary>Null or empty = keep existing encrypted value.</summary>
        public string? Number { get; init; }
        public string? IssuingCountry { get; init; }
        public string? IssuedDate { get; init; }
        public string? ExpiryDate { get; init; }

        // Identity fields
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? Company { get; init; }
        public string? AddressLine1 { get; init; }
        public string? AddressLine2 { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public string? Country { get; init; }
        public string? PostalCode { get; init; }

        // Wi-Fi fields
        public string? Ssid { get; init; }
        public string? WifiSecurity { get; init; }
        /// <summary>Null or empty = keep existing encrypted value.</summary>
        public string? Password { get; init; }

        // SSH fields
        public string? Host { get; init; }
        public string? Port { get; init; }
        public string? Username { get; init; }
        /// <summary>Null or empty = keep existing encrypted value.</summary>
        public string? SshPassword { get; init; }
        /// <summary>Null or empty = keep existing encrypted value.</summary>
        public string? PrivateKey { get; init; }
        public string? KeyFingerprint { get; init; }

        // Secure Note
        /// <summary>Null or empty = keep existing encrypted value.</summary>
        public string? NoteContent { get; init; }
    }

    // ── Generic operations ───────────────────────────────────────────────────
    /// <summary>
    /// Generic delete used by the browser extension for items in the main vault list
    /// (Logins, CreditCards, Authenticators, Identities, SecureNotes).
    /// The service probes each collection until it finds and removes the item.
    /// </summary>
    public sealed class GenericDeleteRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid CredentialId { get; init; }
    }

    public sealed class ToggleFavoriteRequest
    {
        public string SessionToken { get; init; } = string.Empty;
        public Guid CredentialId { get; init; }
        public bool Value { get; init; }
    }
}
