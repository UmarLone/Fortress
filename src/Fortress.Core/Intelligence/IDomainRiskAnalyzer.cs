namespace Fortress.Core.Intelligence
{
    public enum DomainMatchType { Exact, Subdomain, Similar, Mismatch }
    public enum DomainRiskLevel { Safe, Caution, High }
    public enum AutofillSuggestedAction { AllowAutofill, RequireConfirm, BlockUntilConfirm }

    public sealed class DomainRiskResult
    {
        public DomainMatchType MatchType { get; init; }
     public DomainRiskLevel RiskLevel { get; init; }
        public AutofillSuggestedAction SuggestedAction { get; init; }
 public string Reason { get; init; } = string.Empty;
        public string SavedBaseDomain { get; init; } = string.Empty;
  public string CurrentBaseDomain { get; init; } = string.Empty;
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

    public interface IDomainRiskAnalyzer
    {
        DomainRiskResult GetRisk(string savedDomain, string currentDomain);
    }
}
