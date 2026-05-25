namespace Fortress.Core.Models
{
    public enum VaultHealthStatus { Excellent, Good, AtRisk, Critical }
    public enum FindingSeverity { Info, Low, Medium, High, Critical }
    public enum PasswordStrengthLevel { VeryWeak, Weak, Fair, Strong, VeryStrong }

    public sealed class VaultFinding
    {
        public FindingSeverity Severity { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
     public int PointsDeducted { get; init; }
 public IReadOnlyList<string> AffectedLabels { get; init; } = Array.Empty<string>();
   public string Recommendation { get; init; } = string.Empty;
    }

 public sealed class CredentialHealthDetail
    {
  public Guid Id { get; init; }
        public string Label { get; init; } = string.Empty;
   public string Username { get; init; } = string.Empty;
        public bool IsWeak { get; init; }
        public bool IsReused { get; init; }
        public bool IsOld { get; init; }
        public bool HasTwoFactor { get; init; }
        public int PasswordStrengthScore { get; init; }
        public PasswordStrengthLevel StrengthLevel { get; init; }
    }

    public sealed class VaultHealthResult
    {
        public int Score { get; init; }
   public VaultHealthStatus Status { get; init; }
        public string StatusLabel => Status switch
   {
      VaultHealthStatus.Excellent => "Excellent",
      VaultHealthStatus.Good  => "Good",
            VaultHealthStatus.AtRisk => "At Risk",
        VaultHealthStatus.Critical  => "Critical",
 _          => "Unknown"
};
        public string StatusColor => Status switch
{
      VaultHealthStatus.Excellent => "#22C55E",
     VaultHealthStatus.Good      => "#84CC16",
     VaultHealthStatus.AtRisk=> "#F59E0B",
     VaultHealthStatus.Critical  => "#EF4444",
    _  => "#94A3B8"
     };

 public int TotalCredentials { get; init; }
      public int TotalAuthenticators { get; init; }
        public int WeakPasswordsCount { get; init; }
        public int ReusedPasswordsCount { get; init; }
     public int OldPasswordsCount { get; init; }
        public int Missing2FACount { get; init; }
        public int BreachedCount { get; init; }
  public int EmptyPasswordCount { get; init; }
     public int UsernameAsPasswordCount { get; init; }
        public int AttackSurfaceScore { get; init; }
 public string AttackSurfaceLabel => AttackSurfaceScore switch
        {
            <= 20 => "Contained",
  <= 45 => "Moderate",
        <= 70 => "Exposed",
_     => "Critical"
     };
        public IReadOnlyList<CredentialCluster> CredentialClusters { get; init; } = Array.Empty<CredentialCluster>();
     public int HibpBreachedEmailCount { get; init; }
        public IReadOnlyList<HibpEmailResult> HibpResults { get; init; } = Array.Empty<HibpEmailResult>();

     public double WeakPercent   => TotalCredentials == 0 ? 0 : (double)WeakPasswordsCount / TotalCredentials;
 public double ReusedPercent     => TotalCredentials == 0 ? 0 : (double)ReusedPasswordsCount / TotalCredentials;
        public double Missing2FAPercent => TotalCredentials == 0 ? 0 : (double)Missing2FACount / TotalCredentials;
     public double BreachedPercent   => TotalCredentials == 0 ? 0 : (double)BreachedCount / TotalCredentials;

    public bool AllPasswordsStrong => WeakPasswordsCount == 0 && ReusedPasswordsCount == 0;
        public bool Full2FACoverage    => Missing2FACount == 0 && TotalCredentials > 0;
        public bool NoBreachesDetected => BreachedCount == 0;

      public IReadOnlyList<CredentialHealthDetail> Details { get; init; } = Array.Empty<CredentialHealthDetail>();
    public IReadOnlyList<VaultFinding> Findings { get; init; } = Array.Empty<VaultFinding>();
        public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
        public VaultHealthConfig Config { get; init; } = new();
    }
}
