namespace Fortress.Core.Models
{
  /// <summary>
    /// Persisted vault activity record. No MAUI dependency — DeviceId is
    /// injected via constructor / set externally rather than from DeviceInfo.
    /// </summary>
    public class EventLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
      public DateTime DateTime { get; set; } = DateTime.UtcNow;
      public int EventType { get; set; }
        public Guid? CredentialId { get; set; }
      public string? CredentialLabel { get; set; }
 public string? Detail { get; set; }
     public string? DeviceId { get; set; }
    }
}
