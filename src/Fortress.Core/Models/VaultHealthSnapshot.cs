using LiteDB;

namespace Fortress.Core.Models
{
    public sealed class VaultHealthSnapshot
    {
  [BsonId]
  public Guid Id { get; set; } = Guid.NewGuid();
  public DateTime RecordedDate { get; set; }
  public int Score { get; set; }
  public VaultHealthStatus Status { get; set; }
        public int WeakCount { get; set; }
     public int ReusedCount { get; set; }
        public int BreachedCount { get; set; }
    public int Missing2FACount { get; set; }
        public int TotalCredentials { get; set; }
        public int AttackSurfaceScore { get; set; }
    }

    public sealed class CredentialCluster
    {
        public string SharedPasswordHash { get; init; } = string.Empty;
        public IReadOnlyList<CredentialHealthDetail> Members { get; init; } = Array.Empty<CredentialHealthDetail>();

        public string RiskNarrative
   {
      get
   {
         if (Members.Count < 2) return string.Empty;
         var first = Members[0].Label;
    var rest = Members.Count - 1;
    return $"If {first} is breached, {rest} other account{(rest == 1 ? "" : "s")} " +
            $"using the same password ({string.Join(", ", Members.Skip(1).Select(m => m.Label))}) " +
       $"are immediately exposed.";
          }
    }
    }
}
