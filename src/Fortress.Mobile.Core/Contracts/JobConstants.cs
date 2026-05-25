namespace Fortress.Mobile.Core.Contracts
{
    public static class JobConstants
    {
        // Hub-related jobs removed - will add cloud sync jobs in future
        public const string EventLogProcessorJob = "EventLogProcessorJob";

        // ── Cloud backup job ─────────────────────────────────────────────────
        /// <summary>Shiny background-job identifier for the automatic vault cloud backup.</summary>
        public const string CloudBackupJob = "CloudBackupJob";
    }
}
