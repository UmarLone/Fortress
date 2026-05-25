using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Microsoft.Extensions.Logging;
using Shiny.Jobs;
using System.Text;
using System.Text.Json;

namespace Fortress.Mobile.Core.Services.Jobs
{
    /// <summary>
    /// Shiny background job that encrypts and uploads the vault to whichever
    /// cloud provider the user has configured (Google Drive or Dropbox).
    /// Scheduled interval is controlled by <see cref="PreferenceWrapper.CloudSyncSchedule"/>.
    /// </summary>
    public class CloudBackupJob : IJob
    {
        private readonly IDataStorageService _storage;
        private readonly ICryptographyService _crypto;
        private readonly GoogleDriveSyncService _googleDrive;
        private readonly DropboxSyncService _dropbox;
        private readonly OneDriveSyncService _oneDrive;
        private readonly INotificationService _notifications;
        private readonly ILogger<CloudBackupJob> _logger;

        public CloudBackupJob(
            IDataStorageService storage,
            ICryptographyService crypto,
            GoogleDriveSyncService googleDrive,
            DropboxSyncService dropbox,
            INotificationService notifications,
            ILogger<CloudBackupJob> logger,
            OneDriveSyncService oneDrive)
        {
            _storage = storage;
            _crypto = crypto;
            _googleDrive = googleDrive;
            _dropbox = dropbox;
            _notifications = notifications;
            _logger = logger;
            _oneDrive = oneDrive;
        }

        public async Task Run(JobInfo jobInfo, CancellationToken cancelToken)
        {
            var provider = PreferenceWrapper.Instance.CloudSyncProvider;
            if (!PreferenceWrapper.Instance.IsCloudSyncEnabled || string.IsNullOrEmpty(provider))
            {
                _logger.LogDebug("[CloudBackupJob] Cloud sync disabled or no provider — skipping.");
                return;
            }

            // ── Enforce the user-chosen interval ─────────────────────────────
            // Shiny / WorkManager may fire this job every ~15 min regardless of
            // the desired schedule.  We read the configured interval from the
            // job parameters and compare it against the provider's last-sync
            // timestamp.  If not enough time has passed we bail out early.
            if (!ShouldRunNow(jobInfo, provider))
                return;

            _logger.LogInformation("[CloudBackupJob] Starting automatic backup for provider={Provider}", provider);
            var providerDisplay = provider == "GoogleDrive" ? "Google Drive" : provider;

            try
            {
                // 1. Collect vault data
                var snapshot = new VaultBackupSnapshot
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    Credentials = (await _storage.GetLoginItemsAsync()).ToList(),
                    Authenticators = (await _storage.GetAuthenticatorsAsync()).ToList(),
                    CreditCards = (await _storage.GetCreditCardItemsAsync()).ToList(),
                    Identities = (await _storage.GetIdentityItemsAsync()).ToList(),
                    SecureNotes = (await _storage.GetSecureNoteItemsAsync()).ToList()
                };

                // 2. Encrypt
                var json = JsonSerializer.Serialize(snapshot);
                var encResult = await _crypto.Encrypt(json);
                if (!encResult.Succeeded)
                {
                    _logger.LogWarning("[CloudBackupJob] Encryption failed: {Error}", encResult.ErrorMessage);
                    await _notifications.SaveAsync(
                        "Backup Encryption Failed",
                        $"Could not encrypt your vault for {providerDisplay} backup. {encResult.ErrorMessage}",
                        NotificationType.Error,
                        providerDisplay);
                    return;
                }

                var encryptedBytes = Encoding.UTF8.GetBytes(encResult.Data);

                // 3. Upload
                CloudSyncResult result = provider switch
                {
                    "GoogleDrive" => await _googleDrive.UploadBackupAsync(encryptedBytes),
                    "Dropbox" => await _dropbox.UploadBackupAsync(encryptedBytes),
                    "OneDrive" => await _oneDrive.UploadBackupAsync(encryptedBytes),
                    _ => new CloudSyncResult { Success = false, ErrorMessage = $"Unknown provider: {provider}" }
                };

                if (result.Success)
                {
                    _logger.LogInformation("[CloudBackupJob] Backup succeeded at {Time:O}", result.SyncTime);
                    var when = result.SyncTime?.ToLocalTime().ToString("MMM d 'at' h:mm tt") ?? "just now";

                    // Build smart notification copy with vault item counts
                    var itemParts = new List<string>();
                    if (snapshot.Credentials.Count > 0)
                        itemParts.Add($"{snapshot.Credentials.Count} login{(snapshot.Credentials.Count == 1 ? "" : "s")}");
                    if (snapshot.Authenticators.Count > 0)
                        itemParts.Add($"{snapshot.Authenticators.Count} 2FA code{(snapshot.Authenticators.Count == 1 ? "" : "s")}");
                    if (snapshot.CreditCards.Count > 0)
                        itemParts.Add($"{snapshot.CreditCards.Count} card{(snapshot.CreditCards.Count == 1 ? "" : "s")}");
                    if (snapshot.Identities.Count > 0)
                        itemParts.Add($"{snapshot.Identities.Count} identit{(snapshot.Identities.Count == 1 ? "y" : "ies")}");
                    if (snapshot.SecureNotes.Count > 0)
                        itemParts.Add($"{snapshot.SecureNotes.Count} secure note{(snapshot.SecureNotes.Count == 1 ? "" : "s")}");

                    var itemSummary = itemParts.Count > 0
                        ? $" Protected: {string.Join(", ", itemParts)}."
                        : string.Empty;

                    var schedule = PreferenceWrapper.Instance.CloudSyncSchedule;
                    var nextHint = schedule switch
                    {
                        SyncSchedule.Hourly => " Next backup in ~1 hour.",
                        SyncSchedule.Daily => " Next backup tomorrow.",
                        SyncSchedule.Weekly => " Next backup next week.",
                        SyncSchedule.Monthly => " Next backup next month.",
                        _ => string.Empty
                    };

                    await _notifications.SaveAsync(
                        $"✅ {providerDisplay} Backup Complete",
                        $"Your vault was backed up to {providerDisplay} on {when}.{itemSummary}{nextHint}",
                        NotificationType.Success,
                        providerDisplay);
                }
                else
                {
                    _logger.LogWarning("[CloudBackupJob] Backup failed: {Error}", result.ErrorMessage);

                    // Smart failure message with actionable hint
                    var hint = result.ErrorMessage switch
                    {
                        string e when e.Contains("auth", StringComparison.OrdinalIgnoreCase)
                            => " Try reconnecting your account in Settings.",
                        string e when e.Contains("quota", StringComparison.OrdinalIgnoreCase)
                             || e.Contains("storage", StringComparison.OrdinalIgnoreCase)
                            => " Your cloud storage may be full.",
                        string e when e.Contains("network", StringComparison.OrdinalIgnoreCase)
                         || e.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                            => " Check your internet connection and try again.",
                        _ => " Fortress will retry on the next scheduled interval."
                    };

                    await _notifications.SaveAsync(
                        $"⚠️ {providerDisplay} Backup Failed",
                        $"Automatic backup to {providerDisplay} failed: {result.ErrorMessage}.{hint}",
                        NotificationType.Error,
                        providerDisplay);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("[CloudBackupJob] Cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CloudBackupJob] Unhandled exception.");
                await _notifications.SaveAsync(
                    $"{providerDisplay} Backup Error",
                    $"An unexpected error occurred during automatic backup: {ex.Message}",
                    NotificationType.Error,
                    providerDisplay);
            }
        }

        /// <summary>
        /// Returns <c>true</c> only when enough time has elapsed since the last
        /// successful backup for the active provider, according to the interval
        /// stored in <see cref="JobInfo.Parameters"/>.
        /// </summary>
        private bool ShouldRunNow(JobInfo jobInfo, string provider)
        {
            // 1. Read the desired interval (minutes) from job parameters.
            //    Shiny on Android may lose the Parameters dictionary across
            //    app restarts / WorkManager re-scheduling, so we fall back to
            //    the Preferences-persisted value set by CloudSyncScheduler.
            int intervalMinutes = 0;

            if (jobInfo.Parameters is not null
             && jobInfo.Parameters.TryGetValue(CloudSyncScheduler.IntervalMinutesKey, out var raw)
       && int.TryParse(raw, out var parsed)
                && parsed > 0)
            {
  intervalMinutes = parsed;
            }
   else
            {
           // Fallback 1: Read from Preferences (set by CloudSyncScheduler)
    intervalMinutes = Preferences.Default.Get(CloudSyncScheduler.PrefIntervalMinutesKey, 0);
 }

            if (intervalMinutes <= 0)
 {
       // Fallback 2: Derive from the persisted SyncSchedule enum
           var schedule = PreferenceWrapper.Instance.CloudSyncSchedule;
         var interval = schedule.ToInterval();
                if (interval is not null)
          intervalMinutes = (int)interval.Value.TotalMinutes;
       }

     if (intervalMinutes <= 0)
    {
       // No valid interval anywhere — treat as manual-only, skip.
     _logger.LogDebug("[CloudBackupJob] No interval found in params, prefs, or schedule — skipping (manual mode).");
                return false;
          }

   // 2. Read the last-sync timestamp for the active provider.
            var prefKey = provider switch
            {
                "GoogleDrive" => GoogleDriveConstants.PrefLastSyncTime,
                "Dropbox" => DropboxConstants.PrefLastSyncTime,
                "OneDrive" => OneDriveConstants.PrefLastSyncTime,
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(prefKey))
            {
                _logger.LogDebug("[CloudBackupJob] Unknown provider '{Provider}' — running immediately.", provider);
                return true;
            }

            var lastSyncRaw = Preferences.Default.Get(prefKey, string.Empty);
            if (string.IsNullOrEmpty(lastSyncRaw))
            {
                // Never synced before → run now.
                _logger.LogDebug("[CloudBackupJob] No previous sync for {Provider} — running now.", provider);
                return true;
            }

            if (!DateTime.TryParse(lastSyncRaw, null,
               System.Globalization.DateTimeStyles.RoundtripKind, out var lastSync))
            {
                _logger.LogDebug("[CloudBackupJob] Could not parse last sync time — running now.");
                return true;
            }

            // 3. Compare elapsed time vs desired interval.
            var elapsed = DateTime.UtcNow - lastSync;
            var requiredInterval = TimeSpan.FromMinutes(intervalMinutes);

            if (elapsed < requiredInterval)
            {
                _logger.LogDebug(
                 "[CloudBackupJob] Skipping — last backup was {Elapsed:g} ago, interval is {Interval:g}.",
                    elapsed, requiredInterval);
                return false;
            }

            _logger.LogDebug(
             "[CloudBackupJob] Interval reached ({Elapsed:g} >= {Interval:g}) — proceeding with backup.",
                elapsed, requiredInterval);
            return true;
        }
    }
}
