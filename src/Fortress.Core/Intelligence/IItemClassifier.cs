using Fortress.Core.Models;

namespace Fortress.Core.Intelligence
{
  public sealed class ItemClassification
  {
        public bool IsCritical { get; init; }
     public string SuggestedIconKey { get; init; } = "icon_key";
  public IReadOnlyList<string> SuggestedTags { get; init; } = Array.Empty<string>();
        public string? CriticalReason { get; init; }
     public LoginType? SuggestedLoginType { get; init; }
    }

    public interface IItemClassifier
    {
        ItemClassification Classify(string domain, string title);
       ItemClassification ClassifyCard(string label);
 ItemClassification ClassifyIdentity(string firstName, string lastName, string email);
    }
}
