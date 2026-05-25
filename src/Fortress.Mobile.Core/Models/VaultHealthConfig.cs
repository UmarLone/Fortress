namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// All tunable parameters for the vault health calculation.
    /// Ships with sensible defaults; can be injected/overridden per-user.
    /// </summary>
    public sealed class VaultHealthConfig
    {
  // ── Age threshold ─────────────────────────────────────────────────────
        /// <summary>
        /// A password unchanged for longer than this many days is considered "old".
     /// Default: 365 days (1 year).
        /// </summary>
        public int MaxPasswordAgeDays { get; init; } = 365;

        // ── Minimum length thresholds ─────────────────────────────────────────
        /// <summary>Passwords shorter than this are always rated Weak.</summary>
        public int MinAcceptableLength { get; init; } = 8;

 /// <summary>Passwords at or above this length get a bonus in strength scoring.</summary>
        public int StrongLengthThreshold { get; init; } = 14;

      // ── Score deductions ──────────────────────────────────────────────────
// Each deduction is applied once per affected credential, capped at
      // MaxDeductionPerCategory so a single category cannot tank the whole score.

        public int DeductionPerWeakPassword    { get; init; } = 4;
        public int DeductionPerReusedPassword  { get; init; } = 5;
        public int DeductionPerOldPassword     { get; init; } = 2;
        public int DeductionPerMissing2FA      { get; init; } = 3;
        public int DeductionPerBreachedAccount { get; init; } = 10;
        public int DeductionPerEmptyPassword   { get; init; } = 8;
        public int DeductionPerUsernameAsPassword { get; init; } = 6;

   /// <summary>
  /// Maximum total points any single finding category can deduct.
   /// Prevents one bad category from making the score misleadingly low.
        /// </summary>
        public int MaxDeductionPerCategory { get; init; } = 30;

        // ── Score thresholds ──────────────────────────────────────────────────
        public int ExcellentThreshold { get; init; } = 90;
        public int GoodThreshold      { get; init; } = 75;
        public int AtRiskThreshold    { get; init; } = 50;
        // Below AtRiskThreshold ? Critical
    }
}
