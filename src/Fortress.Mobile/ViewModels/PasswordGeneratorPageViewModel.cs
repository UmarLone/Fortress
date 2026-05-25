using Fortress.Helpers;
namespace Fortress.ViewModels
{
    public class PasswordGeneratorPageViewModel : ViewModelBase
    {
        #region Properties
        private string _generatedPassword;
        public string GeneratedPassword
        {
            get { return _generatedPassword; }
            set { SetProperty(ref _generatedPassword, value); }
        }
        private bool _includeUppercase = true;
        private bool _includeLowercase = true;
        private bool _includeNumbers = true;
        private bool _includeSymbols = true;
        private int _numberOfLetters = 15;
        private int _score;
        public bool IncludeUppercase
        {
            get => _includeUppercase;
            set { SetProperty(ref _includeUppercase, value); GeneratePassword(); }
        }

        public bool IncludeLowercase
        {
            get => _includeLowercase;
            set { SetProperty(ref _includeLowercase, value); GeneratePassword(); }
        }

        public bool IncludeNumbers
        {
            get => _includeNumbers;
            set { SetProperty(ref _includeNumbers, value); GeneratePassword(); }
        }

        public bool IncludeSymbols
        {
            get => _includeSymbols;
            set { SetProperty(ref _includeSymbols, value); GeneratePassword(); }
        }

        public int NumberOfLetters
        {
            get => _numberOfLetters;
            set { SetProperty(ref _numberOfLetters, value); GeneratePassword(); }
        }
        public int Score
        {
            get => _score;
            set => SetProperty(ref _score, value);
        }
        #endregion

        public PasswordGeneratorPageViewModel(INavigationService navigationService)
          : base(navigationService)
        {
        }
        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            GeneratePassword();
        }

        private async void ExecuteCopyOtpToClipboardCommand()
        {

            if (!string.IsNullOrEmpty(GeneratedPassword))
                await Clipboard.SetTextAsync(GeneratedPassword);
        }
        private void ExecuteGenerateCommand()
        {
            GeneratePassword();
        }

        private async void ExecuteCloseCommand()
        {
            await NavigationService.GoBackAsync();
        }
        private void GeneratePassword()
        {
            GeneratedPassword = PasswordHelper.GeneratePassword(IncludeUppercase,IncludeLowercase,IncludeNumbers,IncludeSymbols,NumberOfLetters);
           var strength= PasswordHelper.GetPasswordStrength(GeneratedPassword);
           Score = ConvertToIntValue(strength);
        }
       
         
        public static int ConvertToIntValue(PasswordStrength strength)
        {
            switch (strength)
            {
                case PasswordStrength.VeryWeak:
                    return 20;
                case PasswordStrength.Weak:
                    return 40;
                case PasswordStrength.Medium:
                    return 60;
                case PasswordStrength.Strong:
                    return 80;
                case PasswordStrength.VeryStrong:
                    return 100;
                default:
                    return 0;
            }
        }
       


        #region Commands
        private DelegateCommand _generateCommand;
        public DelegateCommand GenerateCommand => _generateCommand ??= new DelegateCommand(ExecuteGenerateCommand);

        private DelegateCommand _closeCommand;
        public DelegateCommand CloseCommand => _closeCommand ??= new DelegateCommand(ExecuteCloseCommand);
        private DelegateCommand _copyToClipboardCommand;

        public DelegateCommand CopyToClipboardCommand => _copyToClipboardCommand ??= new DelegateCommand(ExecuteCopyOtpToClipboardCommand);

        private DelegateCommand _increaseLengthCommand;
        public DelegateCommand IncreaseLengthCommand => _increaseLengthCommand ??= new DelegateCommand(() =>
        {
            if (NumberOfLetters < 64) { NumberOfLetters++; GeneratePassword(); }
        });

        private DelegateCommand _decreaseLengthCommand;
        public DelegateCommand DecreaseLengthCommand => _decreaseLengthCommand ??= new DelegateCommand(() =>
        {
            if (NumberOfLetters > 6) { NumberOfLetters--; GeneratePassword(); }
        });
        private DelegateCommand _goBackCommand;
        public DelegateCommand GoBackCommand =>
      _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());
        #endregion
    }
}
