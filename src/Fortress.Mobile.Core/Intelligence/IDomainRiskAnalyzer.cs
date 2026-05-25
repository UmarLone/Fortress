namespace Fortress.Mobile.Core.Intelligence
{
    // ── Enums ─────────────────────────────────────────────────────────────────────

    /// <summary>How closely the current domain matches the saved domain.</summary>
    public enum DomainMatchType
    {
        /// <summary>Exact eTLD+1 and subdomain match.</summary>
        Exact,
        /// <summary>Current domain is a subdomain of the saved domain (safe).</summary>
        Subdomain,
        /// <summary>Levenshtein distance ≤ 2 or homoglyph attack detected (warn).</summary>
        Similar,
        /// <summary>Completely different eTLD+1 — no relationship.</summary>
        Mismatch
    }

    /// <summary>Overall risk classification for an autofill candidate.</summary>
    public enum DomainRiskLevel
    {
        /// <summary>Domains match — safe to fill.</summary>
        Safe,
        /// <summary>Minor difference (subdomain of parent, or very similar) — confirm before fill.</summary>
        Caution,
        /// <summary>Likely typosquat or homoglyph — block until user explicitly confirms.</summary>
        High
    }

    /// <summary>What the autofill UI should do next.</summary>
    public enum AutofillSuggestedAction
    {
        /// <summary>Fill silently.</summary>
        AllowAutofill,
        /// <summary>Show a non-blocking warning before filling.</summary>
        RequireConfirm,
        /// <summary>Show a blocking warning; do not fill unless user explicitly overrides.</summary>
        BlockUntilConfirm
    }

    // ── Result DTO ────────────────────────────────────────────────────────────────

    /// <summary>Full risk assessment for one (saved domain, current domain) pair.</summary>
    public sealed class DomainRiskResult
    {
        public DomainMatchType MatchType { get; init; }
        public DomainRiskLevel RiskLevel { get; init; }
        public AutofillSuggestedAction SuggestedAction { get; init; }

        /// <summary>Human-readable explanation shown in the warning modal.</summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>Normalised eTLD+1 extracted from the saved credential domain.</summary>
        public string SavedBaseDomain { get; init; } = string.Empty;

        /// <summary>Normalised eTLD+1 extracted from the current browser/app domain.</summary>
        public string CurrentBaseDomain { get; init; } = string.Empty;

        /// <summary>
        /// ML phishing probability in [0,1]. 0 when no phishing check was run.
        /// Values ≥ 0.6 triggered a <see cref="DomainRiskLevel.High"/> result.
        /// </summary>
        public float PhishingProbability { get; init; }

        public static DomainRiskResult Safe(string saved, string current) => new()
        {
            MatchType = DomainMatchType.Exact,
            RiskLevel = DomainRiskLevel.Safe,
            SuggestedAction = AutofillSuggestedAction.AllowAutofill,
            Reason = "Domains match exactly.",
            SavedBaseDomain = saved,
            CurrentBaseDomain = current
        };
    }

    // ── Interface ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Offline phishing / domain-mismatch risk analyser.
    /// All logic is deterministic and runs on-device — no network calls.
    /// </summary>
    public interface IDomainRiskAnalyzer
    {
        /// <summary>
        /// Assess how risky it is to autofill a credential saved for
        /// <paramref name="savedDomain"/> into a page served by <paramref name="currentDomain"/>.
        /// </summary>
        DomainRiskResult GetRisk(string savedDomain, string currentDomain);
    }
}
