using System;

namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// Persisted vault activity record stored in SQLite on-device.
    /// </summary>
    public class EventLog
    {
        /// <summary>Row PK — set by LiteDB/SQLite on insert.</summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>UTC timestamp of the event.</summary>
        public DateTime DateTime { get; set; } = DateTime.UtcNow;

        /// <summary>The category of activity — maps to <see cref="EventLogType"/>.</summary>
        public int EventType { get; set; }

        /// <summary>
        /// Optional — the credential this event relates to (fill, view, edit, delete).
        /// Empty for events that are not credential-specific.
        /// </summary>
        public Guid? CredentialId { get; set; }

        /// <summary>
        /// Human-readable label for the credential (domain or app name).
        /// Captured at log time so the log remains readable after deletion.
        /// </summary>
        public string? CredentialLabel { get; set; }

        /// <summary>
        /// Free-text detail — e.g. the requesting URI on an autofill event,
        /// the risk level on a blocked fill, or the sync error message.
        /// </summary>
        public string? Detail { get; set; }

        /// <summary>Device identifier for multi-device correlation.</summary>
        public string? DeviceId { get; set; } = DeviceInfo.Idiom.ToString();
    }
}
