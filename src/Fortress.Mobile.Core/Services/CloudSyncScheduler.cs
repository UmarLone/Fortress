using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Services;
using Microsoft.Extensions.Logging;
using Shiny.Jobs;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Manages registering and cancelling the <see cref="Jobs.CloudBackupJob"/>
    /// Shiny background job based on the user's chosen <see cref="SyncSchedule"/>.
    /// 
    /// Shiny 3.x on Android uses WorkManager which fires jobs at ~15 min intervals
    /// regardless of the desired schedule. The actual interval enforcement is done
    /// inside <see cref="Jobs.CloudBackupJob.ShouldRunNow"/> by comparing the
 /// elapsed time since the last backup against the interval stored in
    /// <see cref="JobInfo.Parameters"/>.
    /// 
    /// We also persist the interval to <see cref="Preferences"/> as a secondary
  /// source so that if Shiny serialization drops the Parameters dictionary,
    /// the job can still read the correct interval.
    /// </summary>
    public class CloudSyncScheduler
  {
        private readonly IJobManager _jobs;
        private readonly ILogger<CloudSyncScheduler> _logger;

        // Key used to store the interval (minutes) in JobInfo.Parameters
internal const string IntervalMinutesKey = "interval_minutes";

        // Secondary preference key – backup in case JobInfo.Parameters is lost
        internal const string PrefIntervalMinutesKey = "CloudBackupIntervalMinutes";

        public CloudSyncScheduler(IJobManager jobs, ILogger<CloudSyncScheduler> logger)
        {
            _jobs = jobs;
            _logger = logger;
 }

        /// <summary>
        /// Cancels any existing cloud-backup job, persists the schedule choice,
        /// then re-registers the job unless the schedule is Manual.
        /// </summary>
        public void ApplySchedule(SyncSchedule schedule)
     {
       Cancel();

            PreferenceWrapper.Instance.CloudSyncSchedule = schedule;

     var interval = schedule.ToInterval();
            if (interval is null)
            {
                _logger.LogInformation("[CloudSyncScheduler] Manual mode – no job registered.");
      Preferences.Default.Remove(PrefIntervalMinutesKey);
                return;
          }

 var intervalMinutes = (int)interval.Value.TotalMinutes;

  // Persist to Preferences as a fallback – Shiny may drop Parameters
            // during serialization/deserialization across app restarts.
        Preferences.Default.Set(PrefIntervalMinutesKey, intervalMinutes);

            var jobInfo = new JobInfo(JobConstants.CloudBackupJob, typeof(Jobs.CloudBackupJob))
          {
 RequiredInternetAccess = InternetAccess.Any,
      RunOnForeground = false,
     Parameters = new Dictionary<string, string>
      {
     [IntervalMinutesKey] = intervalMinutes.ToString()
                }
 };

            _jobs.Register(jobInfo);
        _logger.LogInformation(
                "[CloudSyncScheduler] Job registered, schedule={Schedule}, interval={IntervalMin}min",
     schedule, intervalMinutes);
        }

    /// <summary>Async wrapper so ViewModels can await without blocking UI.</summary>
  public Task ApplyScheduleAsync(SyncSchedule schedule)
        {
        ApplySchedule(schedule);
            return Task.CompletedTask;
        }

        /// <summary>Cancels the cloud-backup job without changing the persisted schedule.</summary>
        public void Cancel()
  {
   try { _jobs.Cancel(JobConstants.CloudBackupJob); }
            catch (Exception ex)
      {
      _logger.LogDebug("[CloudSyncScheduler] Cancel (job may not exist): {Message}", ex.Message);
   }
   }

        public Task CancelAsync()
        {
            Cancel();
            return Task.CompletedTask;
        }

        /// <summary>Re-applies whatever schedule is persisted. Call this at app startup.</summary>
        public Task RestoreScheduleAsync()
 => ApplyScheduleAsync(PreferenceWrapper.Instance.CloudSyncSchedule);
    }
}
