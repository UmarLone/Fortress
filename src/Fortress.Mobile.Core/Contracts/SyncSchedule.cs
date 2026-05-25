namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// How often the vault is automatically backed up to the cloud.
    /// Stored as an int in preferences via <see cref="SyncScheduleExtensions"/>.
    /// </summary>
    public enum SyncSchedule
    {
Manual  = 0,   // no automatic job – user taps "Backup Now" only
        Hourly  = 1,
        Daily   = 2,   // default – good balance of freshness vs battery
    Weekly  = 3,
        Monthly = 4
    }

  public static class SyncScheduleExtensions
    {
        public static string ToDisplayString(this SyncSchedule s) => s switch
        {
 SyncSchedule.Manual  => "Manual only",
    SyncSchedule.Hourly  => "Every hour",
       SyncSchedule.Daily   => "Every day",
            SyncSchedule.Weekly  => "Every week",
            SyncSchedule.Monthly => "Every month",
       _  => "Every day"
     };

        /// <summary>Interval expressed as <see cref="TimeSpan"/>; null means manual.</summary>
   public static TimeSpan? ToInterval(this SyncSchedule s) => s switch
        {
            SyncSchedule.Hourly  => TimeSpan.FromHours(1),
            SyncSchedule.Daily   => TimeSpan.FromDays(1),
   SyncSchedule.Weekly  => TimeSpan.FromDays(7),
   SyncSchedule.Monthly => TimeSpan.FromDays(30),
            _      => null   // Manual
        };
    }
}
