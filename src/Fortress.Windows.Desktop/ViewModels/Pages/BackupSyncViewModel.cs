using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fortress.Core.Contracts;
using Fortress.Core.Models;
using Fortress.Core.Security;
using Fortress.Windows.Desktop.Services;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class BackupSyncViewModel : ObservableObject, INavigationAware
    {
        private readonly IDesktopDataService _data;
        private readonly IVaultCryptoService _crypto;
        private readonly WpfGoogleDriveSyncService _googleDrive;
        private readonly WpfDropboxSyncService _dropbox;
        private readonly WpfOneDriveSyncService _oneDrive;
        private readonly ISnackbarService _snackbar;
        private readonly IContentDialogService _dialogs;

        private ICloudSyncService? _activeService;

        private const string Tag = "[BackupSync]";
        private static void Log(string m) => System.Diagnostics.Debug.WriteLine($"{Tag} {m}");

        // ── State ─────────────────────────────────────────────────────────────
        [ObservableProperty] private string _selectedProvider = "";
        [ObservableProperty] private bool _isConnected;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _busyMessage = "";
        [ObservableProperty] private string _connectedProviderName = "";
        [ObservableProperty] private string _connectedEmail = "";
        [ObservableProperty] private string _connectedUserName = "";
        [ObservableProperty] private string _lastSyncText = "Never";
        [ObservableProperty] private bool _isExporting;
        [ObservableProperty] private string _lastExportPath = "";

        // ── Provider border helpers ───────────────────────────────────────────
        public bool IsOneDriveSelected => SelectedProvider == "onedrive";
        public bool IsGoogleDriveSelected => SelectedProvider == "googledrive";
        public bool IsDropboxSelected => SelectedProvider == "dropbox";
        public bool IsProviderSelected => !string.IsNullOrEmpty(SelectedProvider);
        public bool IsNotBusy => !IsBusy;
        public bool CanSyncNow => IsConnected && !IsBusy;

        private static SolidColorBrush Brush(string hex) =>
              new((Color)ColorConverter.ConvertFromString(hex)!);

        public SolidColorBrush OneDriveBorderBrush => IsOneDriveSelected ? Brush("#4F46E5") : new(Colors.Transparent);
        public SolidColorBrush GoogleDriveBorderBrush => IsGoogleDriveSelected ? Brush("#4285F4") : new(Colors.Transparent);
        public SolidColorBrush DropboxBorderBrush => IsDropboxSelected ? Brush("#0061FF") : new(Colors.Transparent);
        public System.Windows.Thickness OneDriveBorder => IsOneDriveSelected ? new(2) : new(0);
        public System.Windows.Thickness GoogleDriveBorder => IsGoogleDriveSelected ? new(2) : new(0);
        public System.Windows.Thickness DropboxBorder => IsDropboxSelected ? new(2) : new(0);

        // ── Commands ─────────────────────────────────────────────────────────
        public IRelayCommand<string> SelectProviderCommand { get; }
        public IAsyncRelayCommand ConnectAsyncCommand { get; }
        public IAsyncRelayCommand BackupNowAsyncCommand { get; }
        public IAsyncRelayCommand RestoreAsyncCommand { get; }
        public IRelayCommand DisconnectCommand { get; }
        public IAsyncRelayCommand ExportLocalBackupAsyncCommand { get; }

        public BackupSyncViewModel(
        IDesktopDataService data,
                  IVaultCryptoService crypto,
           WpfGoogleDriveSyncService googleDrive,
       WpfDropboxSyncService dropbox,
             WpfOneDriveSyncService oneDrive,
                  ISnackbarService snackbar,
           IContentDialogService dialogs)
        {
            _data = data;
            _crypto = crypto;
            _googleDrive = googleDrive;
            _dropbox = dropbox;
            _oneDrive = oneDrive;
            _snackbar = snackbar;
            _dialogs = dialogs;

            SelectProviderCommand = new RelayCommand<string>(SelectProvider);
            ConnectAsyncCommand = new AsyncRelayCommand(ConnectAsync, () => IsProviderSelected && !IsBusy);
            BackupNowAsyncCommand = new AsyncRelayCommand(BackupNowAsync, () => IsConnected && !IsBusy);
            RestoreAsyncCommand = new AsyncRelayCommand(RestoreAsync, () => IsConnected && !IsBusy);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected && !IsBusy);
            ExportLocalBackupAsyncCommand = new AsyncRelayCommand(ExportLocalBackupAsync, () => !IsExporting);
        }

        // ── Partial hooks ─────────────────────────────────────────────────────
        partial void OnSelectedProviderChanged(string v) { RefreshProviderBorders(); ConnectAsyncCommand.NotifyCanExecuteChanged(); }
        partial void OnIsConnectedChanged(bool v) { BackupNowAsyncCommand.NotifyCanExecuteChanged(); RestoreAsyncCommand.NotifyCanExecuteChanged(); DisconnectCommand.NotifyCanExecuteChanged(); }
        partial void OnIsBusyChanged(bool v) { ConnectAsyncCommand.NotifyCanExecuteChanged(); BackupNowAsyncCommand.NotifyCanExecuteChanged(); RestoreAsyncCommand.NotifyCanExecuteChanged(); DisconnectCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(IsNotBusy)); OnPropertyChanged(nameof(CanSyncNow)); }
        partial void OnIsExportingChanged(bool v) => ExportLocalBackupAsyncCommand.NotifyCanExecuteChanged();

        // ── Navigation ────────────────────────────────────────────────────────
        public Task OnNavigatedToAsync()
        {
            var store = VaultSettingsStore.Instance;
            IsConnected = store.IsCloudSyncEnabled && !string.IsNullOrEmpty(store.CloudSyncProvider);
            ConnectedProviderName = store.CloudSyncProvider;
            SelectedProvider = IsConnected ? store.CloudSyncProvider.ToLowerInvariant().Replace(" ", "") : "";

            if (IsConnected)
            {
                _activeService = ResolveService(SelectedProvider);
                _ = RefreshConnectedStateAsync();
            }
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task RefreshConnectedStateAsync()
        {
            if (_activeService == null) return;
            IsBusy = true;
            BusyMessage = "Checking connection...";
            try
            {
                var ok = await _activeService.IsAuthenticatedAsync();
                if (ok)
                {
                    var info = await _activeService.GetUserInfoAsync();
                    if (info.HasValue) { ConnectedEmail = info.Value.Email; ConnectedUserName = info.Value.Name; }
                    var last = await _activeService.GetLastSyncTimeAsync();
                    LastSyncText = last.HasValue
                             ? last.Value.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt")
                      : "Never";
                }
                else
                {
                    IsConnected = false;
                    VaultSettingsStore.Instance.IsCloudSyncEnabled = false;
                    VaultSettingsStore.Instance.CloudSyncProvider = "";
                }
            }
            finally { IsBusy = false; }
        }

        // ── Select provider ───────────────────────────────────────────────────
        private void SelectProvider(string? key)
        {
            if (key is null) return;
            SelectedProvider = SelectedProvider == key ? "" : key;
            RefreshProviderBorders();
        }

        private void RefreshProviderBorders()
        {
            OnPropertyChanged(nameof(IsOneDriveSelected));
            OnPropertyChanged(nameof(IsGoogleDriveSelected));
            OnPropertyChanged(nameof(IsDropboxSelected));
            OnPropertyChanged(nameof(IsProviderSelected));
            OnPropertyChanged(nameof(OneDriveBorderBrush));
            OnPropertyChanged(nameof(GoogleDriveBorderBrush));
            OnPropertyChanged(nameof(DropboxBorderBrush));
            OnPropertyChanged(nameof(OneDriveBorder));
            OnPropertyChanged(nameof(GoogleDriveBorder));
            OnPropertyChanged(nameof(DropboxBorder));
            ConnectAsyncCommand.NotifyCanExecuteChanged();
        }

        // ── Connect ───────────────────────────────────────────────────────────
        private async Task ConnectAsync()
        {
            _activeService = ResolveService(SelectedProvider);
            if (_activeService == null) { Toast("Unknown provider.", ControlAppearance.Danger); return; }

            IsBusy = true;
            BusyMessage = $"Opening {_activeService.ProviderName} sign-in...";
            try
            {
                Log($"Connect: {_activeService.ProviderName}");
                var ok = await _activeService.AuthenticateAsync();
                if (ok)
                {
                    BusyMessage = "Loading your account...";
                    var info = await _activeService.GetUserInfoAsync();
                    if (info.HasValue) { ConnectedEmail = info.Value.Email; ConnectedUserName = info.Value.Name; }
                    var last = await _activeService.GetLastSyncTimeAsync();
                    LastSyncText = last.HasValue
                        ? last.Value.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt")
                        : "Never";

                    ConnectedProviderName = _activeService.ProviderName;
                    IsConnected = true;

                    VaultSettingsStore.Instance.IsCloudSyncEnabled = true;
                    VaultSettingsStore.Instance.CloudSyncProvider = _activeService.ProviderName;

                    Toast($"{_activeService.ProviderName} connected!", ControlAppearance.Success,
                          new SymbolIcon(SymbolRegular.CloudCheckmark24));
                }
                else
                {
                    Toast("Sign-in was cancelled or failed. Please try again.", ControlAppearance.Caution,
                   new SymbolIcon(SymbolRegular.Warning24));
                }
            }
            catch (Exception ex)
            {
                Log($"Connect error: {ex}");
                var msg = ex is InvalidOperationException ? ex.Message
              : $"Could not connect to {_activeService?.ProviderName}. Check your internet connection.";
                await _dialogs.ShowAlertAsync("Connection Failed", msg, "OK");
            }
            finally { IsBusy = false; }
        }

        // ── Backup now ────────────────────────────────────────────────────────
        private async Task BackupNowAsync()
        {
            if (_activeService == null) return;
            IsBusy = true;
            BusyMessage = "Reading vault...";
            try
            {
                var credentials = await _data.GetLoginItemsAsync();
                var authenticators = await _data.GetAuthenticatorsAsync();
                var creditCards = await _data.GetCreditCardsAsync();
                var identities = await _data.GetIdentitiesAsync();

                var snapshot = new VaultBackupSnapshot
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    Credentials = credentials,
                    Authenticators = authenticators,
                    CreditCards = creditCards,
                    Identities = identities
                };

                BusyMessage = "Encrypting vault...";
                var ciphertext = await _crypto.EncryptAsync(JsonSerializer.Serialize(snapshot));

                BusyMessage = $"Uploading to {_activeService.ProviderName}...";
                var result = await _activeService.UploadBackupAsync(Encoding.UTF8.GetBytes(ciphertext));
                if (result.Success)
                {
                    var when = result.SyncTime?.ToLocalTime().ToString("MMM d 'at' h:mm tt") ?? "just now";
                    LastSyncText = result.SyncTime?.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt") ?? "Just now";
                    Toast($"Backed up to {_activeService.ProviderName} on {when}.", ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.CloudArrowUp24));
                    Log($"Backup OK — {credentials.Count} logins, {authenticators.Count} 2FA");
                }
                else
                {
                    await _dialogs.ShowAlertAsync("Backup Failed",
                 result.ErrorMessage ?? "Upload failed.", "OK");
                }
            }
            catch (Exception ex)
            {
                Log($"Backup error: {ex}");
                await _dialogs.ShowAlertAsync("Backup Failed", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        // ── Restore ───────────────────────────────────────────────────────────
        private async Task RestoreAsync()
        {
            if (_activeService == null) return;

            var confirm = await _dialogs.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
            {
                Title = "Restore Vault",
                Content = $"This will merge the backup from {_activeService.ProviderName} into your current vault. Are you sure?",
                PrimaryButtonText = "Yes, Restore",
                CloseButtonText = "Cancel"
            });
            if (confirm != ContentDialogResult.Primary) return;

            IsBusy = true;
            BusyMessage = $"Downloading from {_activeService.ProviderName}...";
            try
            {
                var result = await _activeService.DownloadBackupAsync();
                if (!result.Success || result.Data == null || result.Data.Length == 0)
                {
                    await _dialogs.ShowAlertAsync("Restore Failed",
                 result.ErrorMessage ?? "No backup found.", "OK");
                    return;
                }

                BusyMessage = "Decrypting backup...";
                string json;
                try { json = await _crypto.DecryptAsync(Encoding.UTF8.GetString(result.Data)); }
                catch
                {
                    await _dialogs.ShowAlertAsync("Restore Failed",
                  "Decryption failed — backup may be encrypted with a different master password.", "OK");
                    return;
                }

                BusyMessage = "Reading backup...";
                VaultBackupSnapshot? snapshot;
                try { snapshot = JsonSerializer.Deserialize<VaultBackupSnapshot>(json); }
                catch
                {
                    await _dialogs.ShowAlertAsync("Restore Failed",
            "Backup file is corrupted or in an unrecognised format.", "OK");
                    return;
                }

                if (snapshot == null)
                {
                    await _dialogs.ShowAlertAsync("Restore Failed", "Backup appears empty.", "OK");
                    return;
                }

                var summary = $"{snapshot.Credentials.Count} logins, {snapshot.Authenticators.Count} 2FA, {snapshot.CreditCards.Count} cards.";
                Toast($"Restore complete — {summary}", ControlAppearance.Success,
             new SymbolIcon(SymbolRegular.CloudArrowDown24));
                Log($"Restore OK — {summary}");
            }
            catch (Exception ex)
            {
                Log($"Restore error: {ex}");
                await _dialogs.ShowAlertAsync("Restore Failed", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        // ── Disconnect ────────────────────────────────────────────────────────
        private async void Disconnect()
        {
            var prev = ConnectedProviderName;
            var confirm = await _dialogs.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
            {
                Title = $"Disconnect {prev}",
                Content = $"This will remove {prev} access from Fortress. Your existing backup will remain on the cloud.",
                PrimaryButtonText = "Disconnect",
                CloseButtonText = "Cancel"
            });
            if (confirm != ContentDialogResult.Primary) return;

            await SignOutAndCleanAsync(prev);
        }

        private async Task SignOutAndCleanAsync(string providerName)
        {
            IsBusy = true;
            BusyMessage = "Disconnecting...";
            try
            {
                if (_activeService != null) await _activeService.SignOutAsync();
                VaultSettingsStore.Instance.IsCloudSyncEnabled = false;
                VaultSettingsStore.Instance.CloudSyncProvider = "";
                IsConnected = false;
                ConnectedProviderName = "";
                ConnectedEmail = "";
                ConnectedUserName = "";
                LastSyncText = "Never";
                SelectedProvider = "";
                _activeService = null;
                RefreshProviderBorders();
                Toast($"{providerName} disconnected.", ControlAppearance.Secondary,
                          new SymbolIcon(SymbolRegular.PlugDisconnected24));
            }
            finally { IsBusy = false; }
        }

        // ── Local export ──────────────────────────────────────────────────────
        private async Task ExportLocalBackupAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Encrypted Vault Backup",
                Filter = "Fortress Backup (*.fvault)|*.fvault",
                FileName = $"fortress_backup_{DateTime.Now:yyyyMMdd_HHmmss}.fvault",
                DefaultExt = ".fvault"
            };
            if (dialog.ShowDialog() != true) return;

            IsExporting = true;
            try
            {
                var credentials = await _data.GetLoginItemsAsync();
                var authenticators = await _data.GetAuthenticatorsAsync();
                var creditCards = await _data.GetCreditCardsAsync();
                var identities = await _data.GetIdentitiesAsync();

                var snapshot = new VaultBackupSnapshot
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    Credentials = credentials,
                    Authenticators = authenticators,
                    CreditCards = creditCards,
                    Identities = identities
                };

                var ciphertext = await _crypto.EncryptAsync(JsonSerializer.Serialize(snapshot));
                await System.IO.File.WriteAllTextAsync(dialog.FileName, ciphertext);

                LastExportPath = dialog.FileName;
                Toast($"Exported to {System.IO.Path.GetFileName(dialog.FileName)}",
                 ControlAppearance.Success, new SymbolIcon(SymbolRegular.SaveArrowRight24));
            }
            catch (Exception ex)
            {
                await _dialogs.ShowAlertAsync("Export Failed", ex.Message, "OK");
            }
            finally { IsExporting = false; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private ICloudSyncService? ResolveService(string key) => key switch
        {
            "onedrive" => _oneDrive,
            "googledrive" => _googleDrive,
            "dropbox" => _dropbox,
            _ => null
        };

        private void Toast(string message, ControlAppearance appearance,
           IconElement? icon = null, TimeSpan? duration = null)
        {
            _snackbar.Show(
        title: appearance == ControlAppearance.Danger ? "Error"
                  : appearance == ControlAppearance.Success ? "Success"
        : appearance == ControlAppearance.Caution ? "Warning"
      : "Fortress",
             message: message,
         appearance: appearance,
    icon: icon ?? new SymbolIcon(SymbolRegular.Info24),
          timeout: duration ?? TimeSpan.FromSeconds(4));
        }
    }
}
