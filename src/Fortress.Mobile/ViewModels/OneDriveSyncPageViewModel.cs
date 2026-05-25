using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace Fortress.ViewModels
{
    public class OneDriveSyncPageViewModel : ViewModelBase
    {
        private readonly OneDriveSyncService _oneDrive;
        private readonly IUserDialogs _dialogs;
        private readonly IDataStorageService _storage;
        private readonly ICryptographyService _crypto;
        private readonly CloudSyncScheduler _scheduler;
        private readonly INotificationService _notifications;
        private readonly IBottomSheetService _bottomSheetService;

        private const string Tag = "[OneDrive]";
        private static void Log(string msg) =>
               System.Diagnostics.Debug.WriteLine($"{Tag} {msg}");

        // ── State ────────────────────────────────────────────────────────────
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetProperty(ref _isBusy, value);
                RaisePropertyChanged(nameof(IsNotConnected));
                RaisePropertyChanged(nameof(IsConnected));
            }
        }

        private string _busyMessage = "Connecting to Microsoft…";
        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected && !_isBusy;
            set
            {
                SetProperty(ref _isConnected, value);
                RaisePropertyChanged(nameof(IsNotConnected));
            }
        }

        public bool IsNotConnected => !_isConnected && !_isBusy;

        // ── User info ────────────────────────────────────────────────────────
        private string _userEmail = "";
        public string UserEmail
        {
            get => _userEmail;
            set => SetProperty(ref _userEmail, value);
        }

        private string _userName = "";
        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public string UserInitial =>
               string.IsNullOrEmpty(_userName) ? "M" : _userName[0].ToString().ToUpperInvariant();

        private string _lastSyncDisplay = "Never";
        public string LastSyncDisplay
        {
            get => _lastSyncDisplay;
            set => SetProperty(ref _lastSyncDisplay, value);
        }

        // ── Schedule ─────────────────────────────────────────────────────────
        public ObservableCollection<SyncScheduleOption> ScheduleOptions { get; } = new(
       Enum.GetValues<SyncSchedule>().Select(s => new SyncScheduleOption(s)));

        private SyncScheduleOption _selectedSchedule = null!;
        public SyncScheduleOption SelectedSchedule
        {
            get => _selectedSchedule;
            private set => SetProperty(ref _selectedSchedule, value);
        }

        // ────────────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────────────────────────────────────────
        public OneDriveSyncPageViewModel(
            INavigationService navigationService,
         OneDriveSyncService oneDrive,
  IUserDialogs dialogs,
  IDataStorageService storage,
       ICryptographyService crypto,
            CloudSyncScheduler scheduler,
            INotificationService notifications,
            IBottomSheetService bottomSheetService)
            : base(navigationService)
        {
            _oneDrive = oneDrive;
            _dialogs = dialogs;
            _storage = storage;
            _crypto = crypto;
            _scheduler = scheduler;
            _notifications = notifications;
            _bottomSheetService = bottomSheetService;

            SetSchedule(PreferenceWrapper.Instance.CloudSyncSchedule);
        }

        // ────────────────────────────────────────────────────────────────────
        // NAVIGATION
        // ────────────────────────────────────────────────────────────────────
        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            Title = "OneDrive";
            await RefreshStateAsync();
        }

        private async Task RefreshStateAsync()
        {
            IsBusy = true;
            BusyMessage = "Checking connection…";
            try
            {
                var authenticated = await _oneDrive.IsAuthenticatedAsync();
                Log($"IsAuthenticated = {authenticated}");
                if (authenticated)
                {
                    await LoadUserInfoAsync();
                    await LoadLastSyncAsync();
                    IsConnected = true;
                }
                else
                {
                    IsConnected = false;
                }
            }
            finally { IsBusy = false; }
        }

        // ────────────────────────────────────────────────────────────────────
        // COMMANDS
        // ────────────────────────────────────────────────────────────────────
        // ── Connect ──────────────────────────────────────────────────────────
        private DelegateCommand? _connectCommand;
        public DelegateCommand ConnectCommand =>
         _connectCommand ??= new DelegateCommand(ExecuteConnect);

        private async void ExecuteConnect()
        {
            IsBusy = true;
            BusyMessage = "Opening Microsoft sign-in…";
            try
            {
                Log("Authenticate started");
                var success = await _oneDrive.AuthenticateAsync();
                Log($"Authenticate result = {success}");

                if (success)
                {
                    BusyMessage = "Loading your account…";
                    await LoadUserInfoAsync();
                    await LoadLastSyncAsync();

                    PreferenceWrapper.Instance.IsCloudSyncEnabled = true;
                    PreferenceWrapper.Instance.CloudSyncProvider = "OneDrive";

                    await _scheduler.ApplyScheduleAsync(SelectedSchedule.Value);

                    IsConnected = true;
                    _dialogs.ShowToast("OneDrive connected successfully!");
                }
                else
                {
                    _dialogs.ShowToast("Sign-in was cancelled or failed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                Log($"Connect error: {ex}");
                var msg = ex is InvalidOperationException ? ex.Message
                    : "Could not connect to OneDrive. Please check your internet connection and try again.";
                await _dialogs.AlertAsync(msg, "Connection Failed", "OK");
            }
            finally { IsBusy = false; }
        }

        // ── Backup Now ───────────────────────────────────────────────────────
        private DelegateCommand? _backupNowCommand;
        public DelegateCommand BackupNowCommand =>
            _backupNowCommand ??= new DelegateCommand(ExecuteBackupNow);

        private async void ExecuteBackupNow()
        {
            IsBusy = true;
            BusyMessage = "Reading vault…";
            try
            {
                var credentials = (await _storage.GetLoginItemsAsync()).ToList();
                var authenticators = (await _storage.GetAuthenticatorsAsync()).ToList();
                var creditCards = (await _storage.GetCreditCardItemsAsync()).ToList();
                var identities = (await _storage.GetIdentityItemsAsync()).ToList();
                var secureNotes = (await _storage.GetSecureNoteItemsAsync()).ToList();

                var snapshot = new VaultBackupSnapshot
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    Credentials = credentials,
                    Authenticators = authenticators,
                    CreditCards = creditCards,
                    Identities = identities,
                    SecureNotes = secureNotes
                };

                BusyMessage = "Encrypting vault…";
                var encResult = await _crypto.Encrypt(JsonSerializer.Serialize(snapshot));
                if (!encResult.Succeeded)
                {
                    await _dialogs.AlertAsync(encResult.ErrorMessage ?? "Encryption failed.", "Backup Failed", "OK");
                    await _notifications.SaveAsync(
                 "OneDrive Backup Failed",
                  $"Could not encrypt vault: {encResult.ErrorMessage}",
                      NotificationType.Error, "OneDrive");
                    return;
                }

                BusyMessage = "Uploading to OneDrive…";
                var result = await _oneDrive.UploadBackupAsync(Encoding.UTF8.GetBytes(encResult.Data));
                if (result.Success)
                {
                    await LoadLastSyncAsync();
                    _dialogs.ShowToast("Backup uploaded successfully!");
                    var when = result.SyncTime?.ToLocalTime().ToString("MMM d 'at' h:mm tt") ?? "just now";
                    await _notifications.SaveAsync(
                "OneDrive Backup Successful",
          $"Your vault was backed up to OneDrive on {when}. {credentials.Count} logins, {authenticators.Count} 2FA codes.",
                   NotificationType.Success, "OneDrive");
                }
                else
                {
                    await _dialogs.AlertAsync(result.ErrorMessage ?? "Upload failed.", "Backup Failed", "OK");
                    await _notifications.SaveAsync(
                           "OneDrive Backup Failed",
                              $"Upload failed: {result.ErrorMessage}",
                        NotificationType.Error, "OneDrive");
                }
            }
            catch (Exception ex)
            {
                Log($"Backup error: {ex}");
                await _dialogs.AlertAsync(ex.Message, "Backup Failed", "OK");
                await _notifications.SaveAsync(
                "OneDrive Backup Error",
                 $"An unexpected error occurred: {ex.Message}",
                NotificationType.Error, "OneDrive");
            }
            finally { IsBusy = false; }
        }

        // ── Restore ──────────────────────────────────────────────────────────
        private DelegateCommand? _restoreCommand;
        public DelegateCommand RestoreCommand =>
     _restoreCommand ??= new DelegateCommand(ExecuteRestore);

        private async void ExecuteRestore()
        {
            var confirm = await _bottomSheetService.ConfirmAsync(
       "Restore Vault",
         "This will replace your current vault with the backup from OneDrive. Are you sure?",
                "Yes", "No");
            if (!confirm) return;

            IsBusy = true;
            BusyMessage = "Downloading from OneDrive…";
            try
            {
                var result = await _oneDrive.DownloadBackupAsync();
                if (!result.Success || result.Data == null || result.Data.Length == 0)
                {
                    await _dialogs.AlertAsync(result.ErrorMessage ?? "No backup found.", "Restore Failed", "OK");
                    await _notifications.SaveAsync(
                     "OneDrive Restore Failed",
                $"Download failed: {result.ErrorMessage ?? "No backup found."}",
                    NotificationType.Error, "OneDrive");
                    return;
                }

                BusyMessage = "Decrypting backup…";
                var decResult = await _crypto.Decrypt(Encoding.UTF8.GetString(result.Data));
                if (!decResult.Succeeded || string.IsNullOrEmpty(decResult.Data))
                {
                    await _dialogs.AlertAsync(
                   decResult.ErrorMessage ?? "Decryption failed – backup may be corrupt or encrypted with a different master password.",
       "Restore Failed", "OK");
                    await _notifications.SaveAsync(
                             "OneDrive Restore Failed",
                          $"Decryption failed: {decResult.ErrorMessage}",
                             NotificationType.Error, "OneDrive");
                    return;
                }

                BusyMessage = "Reading backup…";
                VaultBackupSnapshot? snapshot;
                try { snapshot = JsonSerializer.Deserialize<VaultBackupSnapshot>(decResult.Data); }
                catch
                {
                    await _dialogs.AlertAsync("Backup file is corrupted or in an unrecognised format.", "Restore Failed", "OK");
                    await _notifications.SaveAsync("OneDrive Restore Failed", "Backup file is corrupted.", NotificationType.Error, "OneDrive");
                    return;
                }

                if (snapshot == null)
                {
                    await _dialogs.AlertAsync("Backup appears empty.", "Restore Failed", "OK");
                    return;
                }

                BusyMessage = "Restoring vault…";
                if (snapshot.Credentials.Count > 0)
                    foreach (var item in snapshot.Credentials) await _storage.SaveLoginItemAsync(item);
                if (snapshot.Authenticators.Count > 0)
                    await _storage.AddOrUpdateAuthenticatorsAsync(snapshot.Authenticators);
                foreach (var card in snapshot.CreditCards) await _storage.SaveCreditCardItemAsync(card);
                foreach (var identity in snapshot.Identities) await _storage.SaveIdentityItemAsync(identity);
                foreach (var note in snapshot.SecureNotes) await _storage.SaveSecureNoteItemAsync(note);

                var summary = $"{snapshot.Credentials.Count} logins, {snapshot.Authenticators.Count} 2FA, {snapshot.CreditCards.Count} cards restored.";
                _dialogs.ShowToast($"Restore complete! {summary}");
                await _notifications.SaveAsync(
                       "OneDrive Restore Successful",
              $"Vault restored from OneDrive. {summary}",
                      NotificationType.Success, "OneDrive");
            }
            catch (Exception ex)
            {
                Log($"Restore error: {ex}");
                await _dialogs.AlertAsync(ex.Message, "Restore Failed", "OK");
                await _notifications.SaveAsync(
      "OneDrive Restore Error",
      $"An unexpected error occurred: {ex.Message}",
                 NotificationType.Error, "OneDrive");
            }
            finally { IsBusy = false; }
        }

        // ── Disconnect ───────────────────────────────────────────────────────
        private DelegateCommand? _disconnectCommand;
        public DelegateCommand DisconnectCommand =>
                    _disconnectCommand ??= new DelegateCommand(ExecuteDisconnect);

        private async void ExecuteDisconnect()
        {
            var result = await _bottomSheetService.ConfirmAsync(
 "Disconnect OneDrive",
      "This will remove OneDrive access from FORTRESS. Your existing backup will remain on OneDrive.",
                "Yes", "No");
            if (!result) return;

            IsBusy = true;
            BusyMessage = "Disconnecting…";
            try
            {
                await _oneDrive.SignOutAsync();
                await _scheduler.CancelAsync();

                PreferenceWrapper.Instance.IsCloudSyncEnabled = false;
                PreferenceWrapper.Instance.CloudSyncProvider = "";

                UserEmail = "";
                UserName = "";
                LastSyncDisplay = "Never";
                IsConnected = false;

                _dialogs.ShowToast("OneDrive disconnected.");
            }
            finally { IsBusy = false; }
        }

        // ── Schedule selection ────────────────────────────────────────────────
        private DelegateCommand<SyncScheduleOption>? _selectScheduleCommand;
        public DelegateCommand<SyncScheduleOption> SelectScheduleCommand =>
            _selectScheduleCommand ??= new DelegateCommand<SyncScheduleOption>(opt =>
    {
        if (opt is null) return;
        SetSchedule(opt.Value);
        if (IsConnected)
            _ = _scheduler.ApplyScheduleAsync(opt.Value);
    });

        private void SetSchedule(SyncSchedule schedule)
        {
            foreach (var o in ScheduleOptions) o.IsSelected = o.Value == schedule;
            SelectedSchedule = ScheduleOptions.First(o => o.Value == schedule);
        }

        // ── Back ─────────────────────────────────────────────────────────────
        private DelegateCommand? _goBackCommand;
        public DelegateCommand GoBackCommand =>
     _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());

        // ────────────────────────────────────────────────────────────────────
        // HELPERS
        // ────────────────────────────────────────────────────────────────────
        private async Task LoadUserInfoAsync()
        {
            var info = await _oneDrive.GetUserInfoAsync();
            Log($"User info: email={info?.Email}, name={info?.Name}");
            if (info is not null)
            {
                UserEmail = info.Value.Email;
                UserName = info.Value.Name;
                RaisePropertyChanged(nameof(UserInitial));
            }
        }

        private async Task LoadLastSyncAsync()
        {
            var lastSync = await _oneDrive.GetLastSyncTimeAsync();
            Log($"Last sync: {lastSync?.ToString("O") ?? "never"}");
            LastSyncDisplay = lastSync.HasValue
                      ? lastSync.Value.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt")
        : "Never";
        }
    }
}
