using Controls.UserDialogs.Maui;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Services;
using Fortress.Views;

namespace Fortress.ViewModels
{
    public class CloudSyncPageViewModel : ViewModelBase
    {
        private readonly IUserDialogs _dialogService;
        private readonly IDataStorageService _dataStorageService;

        public CloudSyncPageViewModel(
         INavigationService navigationService,
                   IUserDialogs dialogService,
        IDataStorageService dataStorageService)
          : base(navigationService)
        {
            _dialogService = dialogService;
            _dataStorageService = dataStorageService;
        }

        // ── Bindable state ───────────────────────────────────────────────────

        /// <summary>Shows the ● ON badge on the Google Drive card.</summary>
        public bool IsGoogleDriveConnected =>
          PreferenceWrapper.Instance.IsCloudSyncEnabled &&
  PreferenceWrapper.Instance.CloudSyncProvider == "GoogleDrive";

        /// <summary>Shows the ● ON badge on the Dropbox card.</summary>
        public bool IsDropboxConnected =>
          PreferenceWrapper.Instance.IsCloudSyncEnabled &&
          PreferenceWrapper.Instance.CloudSyncProvider == "Dropbox";

        /// <summary>Shows the ● ON badge on the OneDrive card.</summary>
        public bool IsOneDriveConnected =>
      PreferenceWrapper.Instance.IsCloudSyncEnabled &&
     PreferenceWrapper.Instance.CloudSyncProvider == "OneDrive";

        // ── Lifecycle ────────────────────────────────────────────────────────

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            Title = "Backs and Sync";
            // Refresh badge in case user just connected / disconnected
            RaisePropertyChanged(nameof(IsGoogleDriveConnected));
            RaisePropertyChanged(nameof(IsDropboxConnected));
         RaisePropertyChanged(nameof(IsOneDriveConnected));
        }

        // ── Commands ─────────────────────────────────────────────────────────

        private async void ExecuteSelectProviderCommand(string provider)
        {
            switch (provider)
            {
                case "GoogleDrive":
                    await NavigationService.NavigateAsync(nameof(GoogleDriveSyncPage));
                    break;

                case "Dropbox":
                   await NavigationService.NavigateAsync(nameof(DropboxSyncPage));
  break;

     case "OneDrive":
          await NavigationService.NavigateAsync(nameof(OneDriveSyncPage));
      break;

                case "iCloud":
                case "WebDAV":
                    await _dialogService.AlertAsync(
                    $"{ProviderDisplayName(provider)} sync is coming in a future update. Stay tuned!",
           "Coming Soon", "OK");
                    break;

         case "UsbExport":
           await NavigationService.NavigateAsync(nameof(UsbExportPage));
             break;
            }
        }

        private static string ProviderDisplayName(string key) => key switch
        {
      "Dropbox" => "Dropbox",
          "OneDrive" => "OneDrive",
      "iCloud" => "iCloud Drive",
      "WebDAV" => "WebDAV / Self-Hosted",
            "UsbExport" => "USB / External Storage",
       _ => key
        };

        private DelegateCommand<string>? _selectProviderCommand;
        public DelegateCommand<string> SelectProviderCommand =>
 _selectProviderCommand ??= new DelegateCommand<string>(ExecuteSelectProviderCommand);

        private DelegateCommand? _goBackCommand;
        public DelegateCommand GoBackCommand =>
    _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());
    }
}
