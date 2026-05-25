using LiteDB;

namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// One daily snapshot of the vault health score stored in LiteDB.
 /// Enables the health trending sparkline – shows whether the vault
  /// is getting safer or more exposed over time.
    /// </summary>
    public sealed class VaultHealthSnapshot
    {
        [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>UTC date this snapshot was recorded (time part zeroed – one per day).</summary>
        public DateTime RecordedDate { get; set; }

  public int Score { get; set; }
     public VaultHealthStatus Status { get; set; }
        public int WeakCount { get; set; }
        public int ReusedCount { get; set; }
        public int BreachedCount { get; set; }
        public int Missing2FACount { get; set; }
        public int TotalCredentials { get; set; }

        /// <summary>Attack surface score (0 = minimal exposure, 100 = fully exposed).</summary>
     public int AttackSurfaceScore { get; set; }
    }

    /// <summary>
    /// A group of credentials that all share the same password hash.
    /// If one member account is breached, every other member is immediately exposed.
    /// </summary>
    public sealed class CredentialCluster
    {
        /// <summary>The shared password hash (SHA-256 hex) that links these accounts.</summary>
        public string SharedPasswordHash { get; init; } = string.Empty;

 /// <summary>The credentials in this compromise chain.</summary>
  public IReadOnlyList<CredentialHealthDetail> Members { get; init; }
            = Array.Empty<CredentialHealthDetail>();

        /// <summary>
        /// Human-readable narrative explaining the blast radius.
        /// Safe for TTS – never contains the actual password.
        /// </summary>
        public string RiskNarrative
        {
  get
     {
       if (Members.Count < 2) return string.Empty;
          var first = Members[0].Label;
       var rest  = Members.Count - 1;
     return $"If {first} is breached, {rest} other account{(rest == 1 ? "" : "s")} " +
            $"using the same password ({string.Join(", ", Members.Skip(1).Select(m => m.Label))}) " +
    $"are immediately exposed.";
         }
  }
    }
}
