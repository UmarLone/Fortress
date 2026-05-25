namespace Fortress.Mobile.Core.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Import engine data models  (v2 – hardened)
  // ─────────────────────────────────────────────────────────────────────────
    /// <summary>The source manager format detected from the file.</summary>
    public enum ImportSourceFormat
    {
        Unknown      = 0,
     ChromeCSV    = 1,
 EdgeCSV    = 2,
        FirefoxCSV   = 3,
  SafariCSV    = 4,
        BitwardenJSON  = 5,
        BitwardenCSV   = 6,
        OnePasswordCSV = 7,
 OnePasswordJSON= 8,
 DashlaneCSV    = 9,
 DashlaneJSON   = 10,
      LastPassCSV    = 11,
        GenericCSV   = 12,
    }

    /// <summary>What to do when a duplicate is found during import.</summary>
    public enum DuplicateMergeOption
    {
        /// <summary>Skip the incoming item – keep what is already in the vault.</summary>
 KeepExisting,
        /// <summary>Overwrite the vault item with the incoming one.</summary>
        ReplaceWithImported,
        /// <summary>Import as a brand-new item even if a duplicate exists.</summary>
        ImportAsDuplicate,
    }

    /// <summary>
    /// Result of canonicalising a single raw URL string.
    /// Stores three complementary representations needed for matching and storage.
    /// </summary>
    public sealed class CanonicalUrl
    {
        /// <summary>
        /// The raw value supplied by the import source (preserved verbatim).
        /// May be empty for app-scheme URIs (androidapp://, iosapp://).
        /// </summary>
        public string OriginalUrl { get; init; } = string.Empty;

        /// <summary>
        /// Lowercase host without leading "www.", e.g. "accounts.google.com".
        /// Empty when the input is not a web URL.
 /// </summary>
        public string Host { get; init; } = string.Empty;

        /// <summary>
        /// eTLD+1 registrable domain, e.g. "google.com".
  /// Computed without an external PSL lookup (see <c>DomainCanonicaliser</c>).
        /// Used for broad duplicate/autofill matching.
        /// </summary>
    public string RegistrableDomain { get; init; } = string.Empty;

  /// <summary>
        /// Best single URL to store in the vault: scheme-normalised, no credentials,
        /// no fragment, no trailing slash.  Falls back to OriginalUrl when parsing fails.
        /// </summary>
        public string StorageUrl { get; init; } = string.Empty;

        public bool IsEmpty => string.IsNullOrWhiteSpace(OriginalUrl);

        public override string ToString() => StorageUrl;
    }

    /// <summary>
    /// A single candidate item produced by the parser before any DB interaction.
    /// All credential fields are plaintext here – encryption happens on save.
    /// </summary>
 public sealed class ImportCandidate
    {
   // ── Core fields ───────────────────────────────────────────────────────
  public string Label    { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Notes    { get; set; } = string.Empty;

        // ── Multi-URL support (Bitwarden / 1Password style) ───────────────────
  /// <summary>
      /// All URLs associated with this entry.  The primary URL (first element)
    /// is stored in <see cref="LoginItem.Url"/>.  Additional URLs are
        /// serialised into <see cref="ImportedFields"/>.
  /// </summary>
      public List<CanonicalUrl> Urls { get; set; } = new();

        /// <summary>Primary URL – first entry in <see cref="Urls"/>, or empty.</summary>
 public CanonicalUrl PrimaryUrl => Urls.Count > 0 ? Urls[0] : new CanonicalUrl();

     // ── TOTP ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Raw otpauth:// URI or bare TOTP secret extracted from the import source.
    /// Never logged.  Encrypted and stored in <see cref="LoginItem.OtpSecret"/>.
        /// </summary>
        public string? TotpRaw { get; set; }

     public bool HasTotp => !string.IsNullOrWhiteSpace(TotpRaw);

        // ── Unknown / extra columns ───────────────────────────────────────────
        /// <summary>
        /// Any column in the import file that did not map to a known field.
        /// Stored as key?plaintext pairs, encrypted before persisting to the vault.
     /// Prevents data loss from exotic export formats.
    /// </summary>
        public Dictionary<string, string> ImportedFields { get; set; } = new();

        // ── Password strength (computed by engine) ────────────────────────────
 public int PasswordStrengthScore { get; set; }
        public PasswordStrengthLevel PasswordStrengthLevel { get; set; }
        public bool IsWeak => PasswordStrengthLevel is PasswordStrengthLevel.VeryWeak
       or PasswordStrengthLevel.Weak;

      // ── Duplicate detection ───────────────────────────────────────────────
        public bool  IsDuplicate    { get; set; }
        public Guid? DuplicateOfId  { get; set; }
        /// <summary>Human-readable reason this candidate was flagged as a duplicate.</summary>
 public string DuplicateReason { get; set; } = string.Empty;

        // ── Reuse detection (within batch) ────────────────────────────────────
        public bool IsReused { get; set; }

        // ── Diagnostics (non-sensitive) ───────────────────────────────────────
        public int SourceRowIndex { get; set; }
    }

    /// <summary>
    /// Summary produced after parsing – shown to the user before they confirm import.
    /// </summary>
    public sealed class ImportPreview
  {
        public ImportSourceFormat DetectedFormat { get; set; }
   public int           TotalRows      { get; set; }
        public int          ValidRows      { get; set; }
        public int   SkippedRows    { get; set; }
        public int   DuplicateCount { get; set; }
        public int      WeakCount      { get; set; }
        public int       ReusedCount    { get; set; }
        public int               StrongCount    { get; set; }
        public int   TotpCount      { get; set; }
        public IReadOnlyList<ImportCandidate> Candidates  { get; set; } = Array.Empty<ImportCandidate>();
        public IReadOnlyList<string>       Warnings  { get; set; } = Array.Empty<string>();
    }

    /// <summary>Final result returned after committing the import.</summary>
    public sealed class ImportResult
    {
        public int  Imported { get; set; }
        public int  Skipped  { get; set; }
     public int  Replaced { get; set; }
      public int  Failed   { get; set; }
        public bool Success  => Failed == 0;
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Non-sensitive import audit record
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Persisted after each import run for user-visible history.
    /// Contains NO credential data – only aggregate statistics and metadata.
    /// </summary>
    public sealed class ImportAuditRecord
    {
        public Guid   Id     { get; set; } = Guid.NewGuid();
  public DateTime OccurredAt  { get; set; } = DateTime.UtcNow;

        /// <summary>File name only (no path).  Stored for user recognition.</summary>
  public string SourceFileName { get; set; } = string.Empty;

    public ImportSourceFormat Format { get; set; }

        // ── Aggregate counts only – no credentials ────────────────────────────
        public int TotalParsed    { get; set; }
        public int Imported { get; set; }
        public int Skipped    { get; set; }
      public int Replaced    { get; set; }
     public int Failed         { get; set; }
        public int WeakPasswords  { get; set; }
        public int ReusedPasswords{ get; set; }
        public int Duplicates  { get; set; }
    public int TotpEntries { get; set; }

        public DuplicateMergeOption MergeOption { get; set; }
 public bool WasCommitted  { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Authenticator import models
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Source format of an authenticator backup file.</summary>
    public enum AuthenticatorImportFormat
  {
        Unknown        = 0,
        AegisJson      = 1,   // Aegis Authenticator JSON export
 RaivoJson    = 2,   // Raivo OTP JSON export
      TwoFasJson     = 3,   // 2FAS Authenticator JSON export
        OtpAuthUriList = 4,   // Plain text file, one otpauth:// URI per line
        GoogleMigration= 5,   // otpauth-migration:// QR code payload
    }

    /// <summary>One authenticator account parsed from a backup file.</summary>
    public sealed class AuthenticatorCandidate
    {
        public string Issuer      { get; set; } = string.Empty;
    public string Account   { get; set; } = string.Empty;
        public string Secret      { get; set; } = string.Empty;   // Base32, never logged
  public string Algorithm   { get; set; } = "SHA1";
        public int    Digits      { get; set; } = 6;
 public int    Period      { get; set; } = 30;
        public string Type        { get; set; } = "TOTP";
        public bool   IsDuplicate { get; set; }
      public int    SourceRowIndex { get; set; }
    }

    /// <summary>Preview shown to the user before confirming authenticator import.</summary>
    public sealed class AuthenticatorImportPreview
    {
        public AuthenticatorImportFormat DetectedFormat { get; set; }
    public int TotalParsed      { get; set; }
 public int ValidCount       { get; set; }
        public int DuplicateCount   { get; set; }
        public IReadOnlyList<AuthenticatorCandidate> Candidates { get; set; }
     = Array.Empty<AuthenticatorCandidate>();
   public IReadOnlyList<string> Warnings { get; set; }
    = Array.Empty<string>();
    }

    /// <summary>Final result of committing an authenticator import.</summary>
    public sealed class AuthenticatorImportResult
    {
        public int Imported  { get; set; }
      public int Skipped   { get; set; }
    public int Failed    { get; set; }
        public bool Success  => Failed == 0;
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }
}
