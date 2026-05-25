using Prism.Mvvm;

namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// View/display model for a single vault activity entry shown in the Activity Log page.
    /// </summary>
    public class AuditLog
    {
        /// <summary>UTC date/time of the event, formatted for display.</summary>
        public string DateTime { get; set; } = string.Empty;

        /// <summary>The raw UTC <see cref="System.DateTime"/> — used for sorting.</summary>
        public System.DateTime DateTimeRaw { get; set; }

        /// <summary>Localised display name from <see cref="EventLogType"/> Display attribute.</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Integer value of <see cref="EventLogType"/> — used for colour/icon mapping.</summary>
        public int EventTypeId { get; set; }

        /// <summary>Domain, app name, or other credential label. Empty for non-credential events.</summary>
        public string? CredentialLabel { get; set; }

        /// <summary>Extra context (URI, risk score, error message, etc.).</summary>
        public string? Detail { get; set; }

        // ── Derived display helpers ──────────────────────────────────────

        /// <summary>Material icon glyph for this event category.</summary>
        public string IconGlyph => EventTypeId switch
        {
            >= 1 and <= 3   => "\ue897",   // lock / shield
            >= 10 and <= 13  => "\ue0be",   // key / credential
            >= 20 and <= 24  => "\ue32a",   // autofill
            >= 30 and <= 32  => "\ue897",   // passkey
            >= 40 and <= 41  => "\ue2bd",   // cloud
            >= 50 and <= 54  => "\ue90d",   // security settings
            >= 60 and <= 62  => "\ue2c4",   // storage/export
             _    => "\ue868",   // generic info
        };

        /// <summary>Hex colour for the icon badge background.</summary>
        public string BadgeColor => EventTypeId switch
        {
            3 or 23 or 41    => "#FEE2E2",  // danger — failed/blocked
            24      => "#FEF3C7",  // warning
            1 or 22 or 31    => "#DCFCE7",  // success green
            >= 20 and <= 24  => "#E0E7FF",  // autofill indigo
            >= 30 and <= 32  => "#EDE9FE",  // passkey purple
            >= 40 and <= 41  => "#CCFBF1",  // cloud teal
             _        => "#E0E7FF",  // default blue
     };

        /// <summary>Hex colour for the icon itself.</summary>
        public string IconColor => EventTypeId switch
        {
            3 or 23 or 41    => "#EF4444",
            24   => "#D97706",
            1 or 22 or 31    => "#16A34A",
            >= 20 and <= 24  => "#4F46E5",
            >= 30 and <= 32  => "#7C3AED",
            >= 40 and <= 41  => "#0D9488",
             _    => "#3B82F6",
     };
    }
}
