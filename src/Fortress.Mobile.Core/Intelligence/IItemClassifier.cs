using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Intelligence
{
 // ── DTOs ──────────────────────────────────────────────────────────────────────
    /// <summary>Suggested classification produced for a new or edited vault item.</summary>
    public sealed class ItemClassification
    {
  /// <summary>
        /// Whether this item should be flagged as critical
        /// (email, banking, crypto, admin accounts).
   /// </summary>
 public bool IsCritical { get; init; }

     /// <summary>Suggested category icon key (matches ResourceDictionary icon names).</summary>
        public string SuggestedIconKey { get; init; } = "icon_key";

     /// <summary>Suggested tags inferred from the title / domain.</summary>
     public IReadOnlyList<string> SuggestedTags { get; init; } = Array.Empty<string>();

   /// <summary>Human-readable reason why IsCritical was set (shown as hint in UI).</summary>
    public string? CriticalReason { get; init; }

        /// <summary>Suggested item type refinement (e.g. Web, PhoneApp).</summary>
  public LoginType? SuggestedLoginType { get; init; }
}

    /// <summary>Autofill suggestion for showing in a fill picker.</summary>
    public sealed class AutofillSuggestion
    {
        public Guid CredentialId { get; init; }
        public string Domain     { get; init; } = string.Empty;
        public string Username   { get; init; } = string.Empty;
        /// <summary>Confidence 0–100. Higher = show first in list.</summary>
    public int MatchScore { get; init; }
        public DomainMatchType MatchType { get; init; }
public string MatchReason { get; init; } = string.Empty;

        // New vault-item types
        public Guid? CreditCardId  { get; init; }
        public string? CardLabel   { get; init; }
        public Guid? IdentityId    { get; init; }
        public string? IdentityLabel { get; init; }
    }

    // ── Interface ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Auto-classifies vault items using offline heuristics.
 /// No external API calls – purely local rule evaluation.
    /// </summary>
    public interface IItemClassifier
    {
        /// <summary>Classify a Login item by its domain and title.</summary>
     ItemClassification Classify(string domain, string title);

        /// <summary>Classify a Credit Card item by its label/cardholder name.</summary>
  ItemClassification ClassifyCard(string label);

        /// <summary>Classify an Identity item by its name / email.</summary>
        ItemClassification ClassifyIdentity(string firstName, string lastName, string email);
    }
}
