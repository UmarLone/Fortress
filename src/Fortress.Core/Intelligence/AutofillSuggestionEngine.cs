using Fortress.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fortress.Core.Intelligence
{
    /// <summary>
    /// Builds ranked autofill suggestions for logins, credit cards, and identities.
    /// Fully offline and deterministic.
    /// </summary>
    public sealed class AutofillSuggestionEngine
    {
    private readonly IDomainRiskAnalyzer _riskAnalyzer;
        private readonly ILogger<AutofillSuggestionEngine>? _logger;

      public AutofillSuggestionEngine(IDomainRiskAnalyzer riskAnalyzer,
     ILogger<AutofillSuggestionEngine>? logger = null)
        {
        _riskAnalyzer = riskAnalyzer;
         _logger = logger;
  }

        // ── Login suggestions ─────────────────────────────────────────────────
      public IReadOnlyList<AutofillSuggestion> GetLoginSuggestions(
   IEnumerable<LoginItem> allCredentials,
    string requestingDomain,
    int maxResults = 10)
  {
  if (string.IsNullOrWhiteSpace(requestingDomain))
     return Array.Empty<AutofillSuggestion>();

    var results = new List<AutofillSuggestion>();
        foreach (var cred in allCredentials)
 {
      if (string.IsNullOrWhiteSpace(cred.Url)) continue;
 var risk = _riskAnalyzer.GetRisk(cred.Url, requestingDomain);
   int score = risk.MatchType switch
   {
  DomainMatchType.Exact => 100,
     DomainMatchType.Subdomain => 85,
   DomainMatchType.Similar   => risk.RiskLevel == DomainRiskLevel.Caution ? 50 : 30,
    _          => 0,
   };
   if (score == 0) continue;
  results.Add(new AutofillSuggestion
          {
   CredentialId = cred.Id, Domain = cred.Url, Username = cred.Username ?? string.Empty,
  MatchScore = score, MatchType = risk.MatchType, MatchReason = risk.Reason,
         });
  }
      return results.OrderByDescending(s => s.MatchScore).Take(maxResults).ToList().AsReadOnly();
     }

        // ── Credit card suggestions ───────────────────────────────────────────
   public IReadOnlyList<AutofillSuggestion> GetCreditCardSuggestions(
     IEnumerable<CreditCardItem> cards, int maxResults = 5) =>
     cards.OrderByDescending(c => c.UpdatedAt).Take(maxResults)
   .Select(c => new AutofillSuggestion
     {
        CreditCardId = c.Id,
        CardLabel = string.IsNullOrWhiteSpace(c.Label) ? MaskCardNumber(c.Number) : c.Label,
    MatchScore = 90, MatchType = DomainMatchType.Exact,
     MatchReason = "Credit card available for payment fields.",
          }).ToList().AsReadOnly();

// ── Identity suggestions ──────────────────────────────────────────────
      public IReadOnlyList<AutofillSuggestion> GetIdentitySuggestions(
     IEnumerable<IdentityItem> identities, int maxResults = 3) =>
  identities.OrderByDescending(i => i.UpdatedAt).Take(maxResults)
               .Select(i => new AutofillSuggestion
    {
      IdentityId = i.Id,
       IdentityLabel = $"{i.FirstName} {i.LastName}".Trim(),
 MatchScore = 80, MatchType = DomainMatchType.Exact,
MatchReason = "Identity available for form fields.",
         }).ToList().AsReadOnly();

  // ── Combined ──────────────────────────────────────────────────────────
        public CombinedAutofillSuggestions GetAllSuggestions(
         IEnumerable<LoginItem> credentials,
         IEnumerable<CreditCardItem> cards,
  IEnumerable<IdentityItem> identities,
            string requestingDomain) => new()
  {
   LoginSuggestions      = GetLoginSuggestions(credentials, requestingDomain),
   CreditCardSuggestions = GetCreditCardSuggestions(cards),
   IdentitySuggestions   = GetIdentitySuggestions(identities),
 };

        private static string MaskCardNumber(string number)
  {
   if (string.IsNullOrWhiteSpace(number)) return "Card";
    var digits = new string(number.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? $"���� {digits[^4..]}" : "Card";
}
    }

   // ── DTOs ──────────────────────────────────────────────────────────────────
    public sealed class AutofillSuggestion
    {
   public Guid? CredentialId { get; init; }
 public Guid? CreditCardId { get; init; }
 public Guid? IdentityId { get; init; }
   public string Domain { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
public string? CardLabel { get; init; }
       public string? IdentityLabel { get; init; }
        public int MatchScore { get; init; }
       public DomainMatchType MatchType { get; init; }
   public string MatchReason { get; init; } = string.Empty;
    }

public sealed class CombinedAutofillSuggestions
    {
   public IReadOnlyList<AutofillSuggestion> LoginSuggestions { get; init; } = Array.Empty<AutofillSuggestion>();
  public IReadOnlyList<AutofillSuggestion> CreditCardSuggestions { get; init; } = Array.Empty<AutofillSuggestion>();
  public IReadOnlyList<AutofillSuggestion> IdentitySuggestions { get; init; } = Array.Empty<AutofillSuggestion>();
    public bool HasAny => LoginSuggestions.Count > 0 || CreditCardSuggestions.Count > 0 || IdentitySuggestions.Count > 0;
    }
}
