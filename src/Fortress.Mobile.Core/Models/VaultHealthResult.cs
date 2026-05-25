using System.Collections.ObjectModel;

namespace Fortress.Mobile.Core.Models
{
    // ── Enums ────────────────────────────────────────────────────────────────────
    /// <summary>Overall vault security classification.</summary>
 public enum VaultHealthStatus
    {
        /// <summary>Score 90–100. The vault is in excellent shape.</summary>
  Excellent,
        /// <summary>Score 75–89. Minor issues worth addressing.</summary>
        Good,
        /// <summary>Score 50–74. Several issues need attention.</summary>
        AtRisk,
  /// <summary>Score 0–49. Critical vulnerabilities present.</summary>
        Critical
    }

  /// <summary>Severity level for individual findings.</summary>
    public enum FindingSeverity
    {
        Info,
        Low,
        Medium,
    High,
        Critical
    }

    // ── Finding types ─────────────────────────────────────────────────────────────
    /// <summary>
    /// A single actionable security finding attached to one or more credentials.
    /// </summary>
    public sealed class VaultFinding
    {
        public FindingSeverity Severity { get; init; }

     /// <summary>Short human-readable title shown in the UI.</summary>
     public string Title { get; init; } = string.Empty;

     /// <summary>Detailed explanation of why this is a problem.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>Points deducted from the score because of this finding.</summary>
        public int PointsDeducted { get; init; }

    /// <summary>Credentials affected (by Domain / Issuer label).</summary>
        public IReadOnlyList<string> AffectedLabels { get; init; } = Array.Empty<string>();

    /// <summary>Actionable recommendation shown in the UI.</summary>
        public string Recommendation { get; init; } = string.Empty;
    }

    // ── Per-group stats ───────────────────────────────────────────────────────────
    /// <summary>Detailed password-strength breakdown for one credential.</summary>
    public sealed class CredentialHealthDetail
 {
      public Guid Id { get; init; }
        public string Label { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public bool IsWeak { get; init; }
        public bool IsReused { get; init; }
     public bool IsOld { get; init; }
        public bool HasTwoFactor { get; init; }
        public int PasswordStrengthScore { get; init; } // 0–100 per-password
   public PasswordStrengthLevel StrengthLevel { get; init; }
    }

    public enum PasswordStrengthLevel
    {
  VeryWeak,
   Weak,
    Fair,
   Strong,
        VeryStrong
    }

    // ── Top-level result ──────────────────────────────────────────────────────────
    /// <summary>
    /// Fully structured vault health snapshot.
    /// Deterministic – identical inputs always produce identical outputs.
    /// No external services, no AI randomness.
  /// </summary>
    public sealed class VaultHealthResult
    {
        // ── Score and status ──────────────────────────────────────────────────
        /// <summary>Overall vault health score from 0 (worst) to 100 (best).</summary>
   public int Score { get; init; }

/// <summary>Human-readable classification of the score.</summary>
    public VaultHealthStatus Status { get; init; }

/// <summary>Short label suitable for a headline badge (e.g. "At Risk").</summary>
   public string StatusLabel => Status switch
        {
      VaultHealthStatus.Excellent => "Excellent",
     VaultHealthStatus.Good      => "Good",
     VaultHealthStatus.AtRisk    => "At Risk",
     VaultHealthStatus.Critical  => "Critical",
    _                  => "Unknown"
  };

        /// <summary>
        /// Accent colour hex appropriate for the status – ready for UI binding.
        /// </summary>
        public string StatusColor => Status switch
      {
            VaultHealthStatus.Excellent => "#22C55E",   // green-500
            VaultHealthStatus.Good      => "#84CC16",   // lime-500
            VaultHealthStatus.AtRisk    => "#F59E0B",   // amber-500
      VaultHealthStatus.Critical  => "#EF4444",   // red-500
          _        => "#94A3B8"
        };

      // ── Headline counters ─────────────────────────────────────────────────
        public int TotalCredentials { get; init; }
    public int TotalAuthenticators { get; init; }

        /// <summary>Credentials with a password rated Weak or VeryWeak.</summary>
        public int WeakPasswordsCount { get; init; }

      /// <summary>Credentials sharing an identical password with at least one other.</summary>
        public int ReusedPasswordsCount { get; init; }

    /// <summary>
        /// Credentials whose password has not been changed for longer than
     /// <see cref="VaultHealthConfig.MaxPasswordAgeDays"/>.
     /// (Requires <see cref="Credential"/> to carry a <c>UpdatedAt</c> timestamp.
      ///  When unavailable this counter is always 0.)
      /// </summary>
 public int OldPasswordsCount { get; init; }

/// <summary>
        /// Credentials that have no matching <see cref="Authenticator"/> entry
        /// (matched by domain / issuer).
        /// </summary>
        public int Missing2FACount { get; init; }

        /// <summary>
        /// Credentials flagged as breached via the built-in common-breach wordlist.
        /// No network call is made – purely local pattern matching.
        /// </summary>
    public int BreachedCount { get; init; }

        /// <summary>Credentials with an empty or whitespace-only password.</summary>
   public int EmptyPasswordCount { get; init; }

        /// <summary>Credentials where username is also used as password.</summary>
        public int UsernameAsPasswordCount { get; init; }

        // ── Attack surface ────────────────────────────────────────────────────
        /// <summary>
        /// Blast-radius score from 0 (minimal exposure) to 100 (maximally exposed).
        /// Combines: critical accounts missing 2FA, reuse chains touching critical
      /// accounts, single email used everywhere, no lock configured.
/// Higher = worse.
        /// </summary>
        public int AttackSurfaceScore { get; init; }

        /// <summary>Short label for the attack surface score.</summary>
        public string AttackSurfaceLabel => AttackSurfaceScore switch
  {
   <= 20  => "Contained",
            <= 45  => "Moderate",
            <= 70  => "Exposed",
            _ => "Critical"
        };

      /// <summary>
        /// Groups of credentials that share the same password.
  /// Each cluster represents a compromise chain.
        /// </summary>
    public IReadOnlyList<CredentialCluster> CredentialClusters { get; init; }
   = Array.Empty<CredentialCluster>();

        /// <summary>
        /// Number of email addresses found in HIBP data breaches.
        /// 0 until an online check has been performed (requires internet + user consent).
     /// </summary>
   public int HibpBreachedEmailCount { get; init; }

    /// <summary>Per-email HIBP results (populated after online check).</summary>
        public IReadOnlyList<HibpEmailResult> HibpResults { get; init; }
         = Array.Empty<HibpEmailResult>();

        // ── Percentage helpers for UI progress bars ───────────────────────────
        public double WeakPercent      => TotalCredentials == 0 ? 0 : (double)WeakPasswordsCount / TotalCredentials;
        public double ReusedPercent    => TotalCredentials == 0 ? 0 : (double)ReusedPasswordsCount    / TotalCredentials;
        public double Missing2FAPercent => TotalCredentials == 0 ? 0 : (double)Missing2FACount        / TotalCredentials;
        public double BreachedPercent   => TotalCredentials == 0 ? 0 : (double)BreachedCount          / TotalCredentials;

  // ── Achievements / positive indicators ───────────────────────────────
        /// <summary>True when every credential has a strong, unique password.</summary>
        public bool AllPasswordsStrong  => WeakPasswordsCount == 0 && ReusedPasswordsCount == 0;

        /// <summary>True when 2FA is configured for every credential.</summary>
        public bool Full2FACoverage     => Missing2FACount == 0 && TotalCredentials > 0;

 /// <summary>True when no password appears in the breach wordlist.</summary>
        public bool NoBreachesDetected  => BreachedCount == 0;

        // ── Per-credential detail ─────────────────────────────────────────────
public IReadOnlyList<CredentialHealthDetail> Details { get; init; }
            = Array.Empty<CredentialHealthDetail>();

      // ── Actionable findings ───────────────────────────────────────────────
    /// <summary>
        /// Ordered list of findings from most to least severe.
        /// Each finding carries the deducted points so the UI can explain the score.
        /// </summary>
  public IReadOnlyList<VaultFinding> Findings { get; init; }
         = Array.Empty<VaultFinding>();

        // ── Metadata ──────────────────────────────────────────────────────────
        public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;

     /// <summary>Configuration snapshot used for this calculation.</summary>
        public VaultHealthConfig Config { get; init; } = new();
    }
}
