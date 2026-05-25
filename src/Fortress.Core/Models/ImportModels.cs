namespace Fortress.Core.Models
{
    public enum ImportSourceFormat
    {
        Unknown = 0, ChromeCSV = 1, EdgeCSV = 2, FirefoxCSV = 3, SafariCSV = 4,
  BitwardenJSON = 5, BitwardenCSV = 6, OnePasswordCSV = 7, OnePasswordJSON = 8,
        DashlaneCSV = 9, DashlaneJSON = 10, LastPassCSV = 11, GenericCSV = 12,
    }

    public enum DuplicateMergeOption { KeepExisting, ReplaceWithImported, ImportAsDuplicate }

    public sealed class CanonicalUrl
    {
        public string OriginalUrl { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
        public string RegistrableDomain { get; init; } = string.Empty;
  public string StorageUrl { get; init; } = string.Empty;
  public bool IsEmpty => string.IsNullOrWhiteSpace(OriginalUrl);
    public override string ToString() => StorageUrl;
    }

    public sealed class ImportCandidate
    {
      public string Label { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
     public List<CanonicalUrl> Urls { get; set; } = new();
      public CanonicalUrl PrimaryUrl => Urls.Count > 0 ? Urls[0] : new CanonicalUrl();
    public string? TotpRaw { get; set; }
    public bool HasTotp => !string.IsNullOrWhiteSpace(TotpRaw);
    public Dictionary<string, string> ImportedFields { get; set; } = new();
public int PasswordStrengthScore { get; set; }
  public PasswordStrengthLevel PasswordStrengthLevel { get; set; }
      public bool IsWeak => PasswordStrengthLevel is PasswordStrengthLevel.VeryWeak or PasswordStrengthLevel.Weak;
public bool IsDuplicate { get; set; }
     public Guid? DuplicateOfId { get; set; }
        public string DuplicateReason { get; set; } = string.Empty;
        public bool IsReused { get; set; }
        public int SourceRowIndex { get; set; }
    }

    public sealed class ImportPreview
    {
   public ImportSourceFormat DetectedFormat { get; set; }
   public int TotalRows { get; set; }
     public int ValidRows { get; set; }
     public int SkippedRows { get; set; }
        public int DuplicateCount { get; set; }
  public int WeakCount { get; set; }
        public int ReusedCount { get; set; }
      public int StrongCount { get; set; }
        public int TotpCount { get; set; }
     public IReadOnlyList<ImportCandidate> Candidates { get; set; } = Array.Empty<ImportCandidate>();
public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    public sealed class ImportResult
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public int Replaced { get; set; }
      public int Failed { get; set; }
     public bool Success => Failed == 0;
   public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }

    public sealed class ImportAuditRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
  public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
     public string SourceFileName { get; set; } = string.Empty;
 public ImportSourceFormat Format { get; set; }
public int TotalParsed { get; set; }
  public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Replaced { get; set; }
     public int Failed { get; set; }
        public int WeakPasswords { get; set; }
   public int ReusedPasswords { get; set; }
     public int Duplicates { get; set; }
     public int TotpEntries { get; set; }
        public DuplicateMergeOption MergeOption { get; set; }
        public bool WasCommitted { get; set; }
    }

    public enum AuthenticatorImportFormat
    {
        Unknown = 0, AegisJson = 1, RaivoJson = 2, TwoFasJson = 3,
    OtpAuthUriList = 4, GoogleMigration = 5,
    }

    public sealed class AuthenticatorCandidate
    {
        public string Issuer { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
 public string Secret { get; set; } = string.Empty;
  public string Algorithm { get; set; } = "SHA1";
        public int Digits { get; set; } = 6;
   public int Period { get; set; } = 30;
     public string Type { get; set; } = "TOTP";
        public bool IsDuplicate { get; set; }
     public int SourceRowIndex { get; set; }
    }

    public sealed class AuthenticatorImportPreview
    {
        public AuthenticatorImportFormat DetectedFormat { get; set; }
        public int TotalParsed { get; set; }
   public int ValidCount { get; set; }
    public int DuplicateCount { get; set; }
        public IReadOnlyList<AuthenticatorCandidate> Candidates { get; set; } = Array.Empty<AuthenticatorCandidate>();
       public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    public sealed class AuthenticatorImportResult
    {
     public int Imported { get; set; }
      public int Skipped { get; set; }
   public int Failed { get; set; }
  public bool Success => Failed == 0;
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }
}
