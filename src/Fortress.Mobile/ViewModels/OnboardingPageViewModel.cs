using Controls.UserDialogs.Maui;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Services;
using Fortress.Models;
using Fortress.Views;

namespace Fortress.ViewModels
{
    public class OnboardingPageViewModel : ViewModelBase, INavigationAware
    {
        #region properties
        private Onboarding selectedOnboarding;
        public Onboarding SelectedOnboarding
        {
            get => selectedOnboarding;
            set => SetProperty(ref selectedOnboarding, value);
        }
        public ObservableCollection<Onboarding> Items
        {
            get => items;
            set => SetProperty(ref items, value);
        }

        public bool LastPositionReached
        {
            get => lastPositionReached;
            set => SetProperty(ref lastPositionReached, value);
        }
        private ObservableCollection<Onboarding> items;

        private bool lastPositionReached;
        #endregion

        private readonly IUserDialogs _dialogService;

        private readonly IDeviceServices _deviceServices;
        public OnboardingPageViewModel(INavigationService navigationService, IUserDialogs dialogService, IDeviceServices deviceServices) : base(navigationService)
        {
            _dialogService = dialogService;
            _deviceServices = deviceServices;

        }

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);


        }
        public void OnBoarding()
        {
            Items = new ObservableCollection<Onboarding>
     {
  new Onboarding { Title = "Meet Fortress",        FileName = "Welcome.json",   AnimationFile = "Welcome.json" },
      new Onboarding { Title = "Password Vault",       FileName = "PasswordVault.json", AnimationFile = "PasswordVault.json" },
     new Onboarding { Title = "Built-in 2FA Codes",   FileName = "Totp.json",                AnimationFile = "Totp.json" },
 new Onboarding { Title = "Fill in One Tap",      FileName = "Autofill.json",            AnimationFile = "Autofill.json" },
     new Onboarding { Title = "Vault Health Score",   FileName = "Health.json",      AnimationFile = "Health.json" },
       new Onboarding { Title = "Voice Commands",   FileName = "Voice.json",      AnimationFile = "Voice.json" },
    new Onboarding { Title = "PIN & Biometric Lock", FileName = "AuthenticationLock.json",  AnimationFile = "AuthenticationLock.json" },
     new Onboarding { Title = "You're All Set!", FileName = "Backups.json",             AnimationFile = "Backups.json" },
            };
            SelectedOnboarding = Items[0];
        }

        private async void ExecuteFinishCommand()
        {

            using (var dlg = _dialogService?.Loading("Let's set up your account...", maskType: MaskType.Clear))
            {
                if (PreferenceWrapper.Instance != null)
                {
                    PreferenceWrapper.Instance.FirstLaunch = false;
                }
                if (NavigationService != null)
                {
                    var a = await NavigationService.NavigateAsync($"/{nameof(SetupPage)}");
                }
            }
        }
        #region Commands
        private DelegateCommand _finishCommand;
        public DelegateCommand FinishCommand => _finishCommand ??= new DelegateCommand(ExecuteFinishCommand);
        public ICommand PositionChangedCommand => new Command<int>((position) =>
        {
            LastPositionReached = position == Items.Count - 1;
            var item = Items[position];
            item.AnimationFile = item.FileName;
            SelectedOnboarding = item;
        });
        #endregion
    }

}
