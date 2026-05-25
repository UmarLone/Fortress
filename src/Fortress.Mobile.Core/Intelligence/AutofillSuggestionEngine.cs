using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fortress.Mobile.Core.Intelligence
{
    /// <summary>
/// Builds rich autofill suggestions for a requesting app/website.
 /// Covers passwords, credit cards, and identities – not just strict domain matches.
    /// All logic is offline and deterministic.
    /// </summary>
 public sealed class AutofillSuggestionEngine
    {
        private readonly IDomainRiskAnalyzer _riskAnalyzer;
     private readonly ILogger<AutofillSuggestionEngine>? _logger;

        public AutofillSuggestionEngine(
    IDomainRiskAnalyzer riskAnalyzer,
      ILogger<AutofillSuggestionEngine>? logger = null)
        {
  _riskAnalyzer = riskAnalyzer;
       _logger = logger;
        }

      // ── Password / Login suggestions ─────────────────────────────────────────
        /// <summary>
        /// Returns ranked login suggestions for <paramref name="requestingDomain"/>.
        /// Includes exact matches, subdomain matches, similar domains (with risk level attached),
      /// and recently-used credentials as a fallback.
     /// </summary>
    public IReadOnlyList<AutofillSuggestion> GetLoginSuggestions(
      IEnumerable<Credential> allCredentials,
      string requestingDomain,
    int maxResults = 10)
    {
         if (string.IsNullOrWhiteSpace(requestingDomain))
     return Array.Empty<AutofillSuggestion>();

   var results = new List<AutofillSuggestion>();

  foreach (var cred in allCredentials)
          {
      if (string.IsNullOrWhiteSpace(cred.Domain)) continue;

            var risk = _riskAnalyzer.GetRisk(cred.Domain, requestingDomain);

      int score = risk.MatchType switch
    {
           DomainMatchType.Exact     => 100,
       DomainMatchType.Subdomain => 85,
           DomainMatchType.Similar=> risk.RiskLevel == DomainRiskLevel.Caution ? 50 : 30,
        DomainMatchType.Mismatch  => 0,
      _     => 0
                };

     if (score == 0) continue;   // complete mismatch – skip

       results.Add(new AutofillSuggestion
                {
     CredentialId = cred.Id,
              Domain     = cred.Domain,
             Username     = cred.Username ?? string.Empty,
            MatchScore   = score,
    MatchType    = risk.MatchType,
     MatchReason  = risk.Reason,
  });
      }

            return results
                .OrderByDescending(s => s.MatchScore)
.Take(maxResults)
         .ToList()
            .AsReadOnly();
        }

        // ── Credit-card suggestions ───────────────────────────────────────────────
   /// <summary>
        /// Returns all credit cards as suggestions, ranked by most recently updated.
        /// Cards are always surfaced for checkout/payment pages.
        /// </summary>
   public IReadOnlyList<AutofillSuggestion> GetCreditCardSuggestions(
            IEnumerable<CreditCardItem> cards,
   int maxResults = 5)
     {
   return cards
   .OrderByDescending(c => c.UpdatedAt)
  .Take(maxResults)
                .Select(c => new AutofillSuggestion
         {
      CreditCardId = c.Id,
CardLabel    = string.IsNullOrWhiteSpace(c.Label)
     ? MaskCardNumber(c.Number)
 : c.Label,
    MatchScore   = 90,
       MatchType = DomainMatchType.Exact,
     MatchReason  = "Credit card available for payment fields.",
                })
         .ToList()
  .AsReadOnly();
        }

    // ── Identity suggestions ──────────────────────────────────────────────────
        /// <summary>
 /// Returns identity items for form-fill suggestions (name, address, email, phone).
        /// </summary>
        public IReadOnlyList<AutofillSuggestion> GetIdentitySuggestions(
          IEnumerable<IdentityItem> identities,
            int maxResults = 3)
        {
return identities
           .OrderByDescending(i => i.UpdatedAt)
   .Take(maxResults)
    .Select(i => new AutofillSuggestion
        {
          IdentityId    = i.Id,
            IdentityLabel = $"{i.FirstName} {i.LastName}".Trim(),
     MatchScore    = 80,
          MatchType   = DomainMatchType.Exact,
  MatchReason   = "Identity available for form fields.",
      })
 .ToList()
          .AsReadOnly();
     }

    // ── Combined suggestions (used by the autofill service) ───────────────────
        /// <summary>
        /// Returns a combined ranked list of all suggestion types
        /// (logins first, then cards, then identities).
        /// </summary>
        public CombinedAutofillSuggestions GetAllSuggestions(
  IEnumerable<Credential> credentials,
            IEnumerable<CreditCardItem> cards,
 IEnumerable<IdentityItem> identities,
        string requestingDomain)
        {
 return new CombinedAutofillSuggestions
 {
          LoginSuggestions      = GetLoginSuggestions(credentials, requestingDomain),
          CreditCardSuggestions = GetCreditCardSuggestions(cards),
     IdentitySuggestions   = GetIdentitySuggestions(identities),
    };
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
private static string MaskCardNumber(string number)
        {
    if (string.IsNullOrWhiteSpace(number)) return "Card";
        var digits = new string(number.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? $"•••• {digits[^4..]}" : "Card";
        }
    }

    /// <summary>Combined result from <see cref="AutofillSuggestionEngine.GetAllSuggestions"/>.</summary>
    public sealed class CombinedAutofillSuggestions
    {
 public IReadOnlyList<AutofillSuggestion> LoginSuggestions      { get; init; } = Array.Empty<AutofillSuggestion>();
   public IReadOnlyList<AutofillSuggestion> CreditCardSuggestions { get; init; } = Array.Empty<AutofillSuggestion>();
   public IReadOnlyList<AutofillSuggestion> IdentitySuggestions   { get; init; } = Array.Empty<AutofillSuggestion>();

        public bool HasAny => LoginSuggestions.Count > 0
      || CreditCardSuggestions.Count > 0
    || IdentitySuggestions.Count > 0;
    }
}
