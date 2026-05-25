using Controls.UserDialogs.Maui;
using Fortress.Mobile.Core.Contracts;

namespace Fortress.ViewModels
{
    public class AboutPageViewModel : ViewModelBase
    {
        private string version;
        public string CopyrightText =>
          $"© {DateTime.Now.Year} Fortress Password Manager";
        public string Version
        {
            get { return version; }
            set { SetProperty(ref version, value); }
        }
        private readonly IDeviceServices _deviceServices;
        private readonly ILogger<AboutPageViewModel> _logger;
        private readonly IUserDialogs _dialogService;
        private readonly IAppInfo _appInfo;
        public AboutPageViewModel(INavigationService navigationService, IDeviceServices deviceServices,
            ILogger<AboutPageViewModel> logger, IUserDialogs dialogService, IAppInfo appInfo)
          : base(navigationService)
        {
            _deviceServices = deviceServices;
            _logger = logger;
            _dialogService = dialogService;
            _appInfo = appInfo;
        }
        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            Version = _appInfo.VersionString;

        }
        private async void ExecuteCheckUpdateCommand()
        {
            try
            {
                using (var dlg = _dialogService.Loading("Checking for updates..."))
                {
                    await Task.Delay(2000);

                    // Check app store directly without Hub
                    await _deviceServices.OpenAppStore();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while checking for update. Reason: {ex.Message}");
            }
        }
        private DelegateCommand _checkUpdateCommand;
        public DelegateCommand CheckUpdateCommand => _checkUpdateCommand ??= new DelegateCommand(ExecuteCheckUpdateCommand);

        private DelegateCommand _goBackCommand;
        public DelegateCommand GoBackCommand => _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());
    }
}
