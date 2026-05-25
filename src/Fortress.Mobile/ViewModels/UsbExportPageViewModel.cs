using Controls.UserDialogs.Maui;
using CommunityToolkit.Maui.Storage;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Services;
using System.Text;
using System.Text.Json;

namespace Fortress.ViewModels
{
    /// <summary>
    /// Handles exporting an encrypted vault backup to a user-chosen file location
    /// (USB thumb drive, SD card, local Downloads folder, etc.) via the platform
    /// file-picker / file-save API, and restoring from such a file.
    /// </summary>
    public class UsbExportPageViewModel : ViewModelBase
    {
        private readonly IDataStorageService _storage;
        private readonly ICryptographyService _crypto;
        private readonly IUserDialogs _dialogs;
        private readonly INotificationService _notifications;
        private readonly IBottomSheetService _bottomSheetService;

        private const string BackupFileName = "fortress_vault_backup.fvb";
        private const string Tag = "[UsbExport]";
        private static void Log(string msg) =>
            System.Diagnostics.Debug.WriteLine($"{Tag} {msg}");

        // ── State ────────────────────────────────────────────────────────────
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyMessage = string.Empty;
        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        // ── Constructor ──────────────────────────────────────────────────────
        public UsbExportPageViewModel(
  INavigationService navigationService,
            IDataStorageService storage,
            ICryptographyService crypto,
      IUserDialogs dialogs,
         INotificationService notifications,
            IBottomSheetService bottomSheetService)
  : base(navigationService)
        {
            _storage = storage;
            _crypto = crypto;
            _dialogs = dialogs;
            _notifications = notifications;
            _bottomSheetService = bottomSheetService;
        }

        // ── Navigation ───────────────────────────────────────────────────────
        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            Title = "USB / External Storage";
        }

        // ── Export Command ───────────────────────────────────────────────────
        private DelegateCommand? _exportCommand;
        public DelegateCommand ExportCommand =>
      _exportCommand ??= new DelegateCommand(ExecuteExport);

        private async void ExecuteExport()
        {
            IsBusy = true;
            BusyMessage = "Reading vault…";
            try
            {
                // 1. Build snapshot
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

                // 2. Encrypt
                BusyMessage = "Encrypting vault…";
                var encResult = await _crypto.Encrypt(JsonSerializer.Serialize(snapshot));
                if (!encResult.Succeeded)
                {
                    await _dialogs.AlertAsync(
                     encResult.ErrorMessage ?? "Encryption failed.",
                   "Export Failed", "OK");
                    return;
                }

                var fileBytes = Encoding.UTF8.GetBytes(encResult.Data);

                // 3. Let the user pick a save location via the platform share/save sheet.
                //    FileSaver.Default is available in MAUI Community Toolkit 9+.
                BusyMessage = "Opening file picker…";
                using var stream = new MemoryStream(fileBytes);
                var saveResult = await FileSaver.Default.SaveAsync(
                  BackupFileName,
                 stream,
               CancellationToken.None);

                if (saveResult.IsSuccessful)
                {
                    var when = DateTime.Now.ToString("MMM d 'at' h:mm tt");
                    _dialogs.ShowToast($"Backup saved successfully ({when}).");
                    await _notifications.SaveAsync(
                         "USB Backup Successful",
                $"Your encrypted vault was exported on {when}. " +
               $"{credentials.Count} logins, {authenticators.Count} 2FA codes.",
                                NotificationType.Success, "USB");
                    Log($"Export saved to: {saveResult.FilePath}");
                }
                else if (saveResult.Exception is not null &&
                    saveResult.Exception is not OperationCanceledException)
                {
                    Log($"Save failed: {saveResult.Exception.Message}");
                    await _dialogs.AlertAsync(
                      $"Could not save the file: {saveResult.Exception.Message}",
                     "Export Failed", "OK");
                }
                // else: user cancelled – do nothing
            }
            catch (Exception ex)
            {
                Log($"Export error: {ex}");
                await _dialogs.AlertAsync(ex.Message, "Export Failed", "OK");
            }
            finally { IsBusy = false; }
        }

        // ── Import Command ───────────────────────────────────────────────────
        private DelegateCommand? _importCommand;
        public DelegateCommand ImportCommand =>
          _importCommand ??= new DelegateCommand(ExecuteImport);

        private async void ExecuteImport()
        {
            var confirm = await _bottomSheetService.ConfirmAsync(
           "Restore from File",
                       "This will merge the backup into your current vault. " +
              "Existing entries with the same ID will be overwritten. Continue?",
                   "Yes, Restore", "Cancel");
            if (!confirm) return;

            IsBusy = true;
            BusyMessage = "Picking file…";
            try
            {
                // Allow .fvb (custom) and common backup extensions
                var customType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
       { DevicePlatform.Android, new[] { "application/octet-stream", "*/*" } },
                 { DevicePlatform.iOS,     new[] { "public.data" } },
        { DevicePlatform.WinUI,   new[] { ".fvb", ".bak" } },
              { DevicePlatform.macOS,   new[] { "public.data" } },
     });

                var pickResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a FORTRESS backup file (.fvb)",
                    FileTypes = customType
                });

                if (pickResult is null)
                    return; // user cancelled

                BusyMessage = "Reading file…";
                byte[] fileBytes;
                using (var stream = await pickResult.OpenReadAsync())
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                if (fileBytes.Length == 0)
                {
                    await _dialogs.AlertAsync("The selected file is empty.", "Import Failed", "OK");
                    return;
                }

                // 2. Decrypt
                BusyMessage = "Decrypting backup…";
                var decResult = await _crypto.Decrypt(Encoding.UTF8.GetString(fileBytes));
                if (!decResult.Succeeded || string.IsNullOrEmpty(decResult.Data))
                {
                    await _dialogs.AlertAsync(
             decResult.ErrorMessage ??
             "Decryption failed – the file may be corrupt or encrypted with a different master password.",
              "Import Failed", "OK");
                    return;
                }

                // 3. Deserialise
                BusyMessage = "Reading backup…";
                VaultBackupSnapshot? snapshot;
                try { snapshot = JsonSerializer.Deserialize<VaultBackupSnapshot>(decResult.Data); }
                catch
                {
                    await _dialogs.AlertAsync(
                     "The backup file is corrupted or in an unrecognised format.",
                 "Import Failed", "OK");
                    return;
                }

                if (snapshot is null)
                {
                    await _dialogs.AlertAsync("The backup appears to be empty.", "Import Failed", "OK");
                    return;
                }

                // 4. Restore
                BusyMessage = "Restoring vault…";
                if (snapshot.Credentials.Count > 0)
                    foreach (var item in snapshot.Credentials)
                        await _storage.SaveLoginItemAsync(item);
                if (snapshot.Authenticators.Count > 0)
                    await _storage.AddOrUpdateAuthenticatorsAsync(snapshot.Authenticators);
                foreach (var card in snapshot.CreditCards)
                    await _storage.SaveCreditCardItemAsync(card);
                foreach (var identity in snapshot.Identities)
                    await _storage.SaveIdentityItemAsync(identity);
                foreach (var note in snapshot.SecureNotes)
                    await _storage.SaveSecureNoteItemAsync(note);

                var summary =
        $"{snapshot.Credentials.Count} logins, " +
               $"{snapshot.Authenticators.Count} 2FA, " +
              $"{snapshot.CreditCards.Count} cards restored.";

                _dialogs.ShowToast($"Restore complete! {summary}");
                await _notifications.SaveAsync(
               "USB Restore Successful",
        $"Vault restored from file. {summary}",
               NotificationType.Success, "USB");
            }
            catch (Exception ex)
            {
                Log($"Import error: {ex}");
                await _dialogs.AlertAsync(ex.Message, "Import Failed", "OK");
            }
            finally { IsBusy = false; }
        }

        // ── Back ─────────────────────────────────────────────────────────────
        private DelegateCommand? _goBackCommand;
        public DelegateCommand GoBackCommand =>
            _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());
    }
}
