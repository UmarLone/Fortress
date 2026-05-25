namespace Fortress.Core.Contracts
{
    /// <summary>
    /// How often the vault is automatically backed up to the cloud.
    /// </summary>
    public enum SyncSchedule
    {
        Manual  = 0,
        Hourly  = 1,
        Daily   = 2,
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
  _          => "Every day"
        };

        public static TimeSpan? ToInterval(this SyncSchedule s) => s switch
        {
            SyncSchedule.Hourly  => TimeSpan.FromHours(1),
            SyncSchedule.Daily   => TimeSpan.FromDays(1),
            SyncSchedule.Weekly  => TimeSpan.FromDays(7),
   SyncSchedule.Monthly => TimeSpan.FromDays(30),
            _         => null
  };
    }
}
