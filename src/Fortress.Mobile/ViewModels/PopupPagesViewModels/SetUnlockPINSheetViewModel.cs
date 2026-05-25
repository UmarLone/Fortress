using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Services;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.ViewModels
{
    public class SetUnlockPINSheetViewModel  :BottomSheetViewModelBase
    {
        #region Properties
        private bool canExecute;
        public bool CanExecute
        {
            get { return canExecute; }
            set { SetProperty(ref canExecute, value); }
        }
        
        private string pin;
        public string Pin
        {
            get { return !string.IsNullOrEmpty(pin) ? pin.Trim() : pin; }
            set { SetProperty(ref pin, !string.IsNullOrEmpty(value) ? value.Trim() : value); }
        }
        private string confirmPin;
        public string ConfirmPin
        {
            get { return confirmPin; }
            set { SetProperty(ref confirmPin, value); }
        }

        #endregion

        public SetUnlockPINSheetViewModel()
        {
        }
        public override Task InitializeAsync(object args, string title)
        {

            return Task.CompletedTask;
        }
        private async void ExecuteSaveCommand()
        {
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Pin));
            PreferenceWrapper.Instance.PinUnlockHash = Convert.ToBase64String(hashBytes);
            PreferenceWrapper.Instance.IsPinUnlockEnabled = true;
            PreferenceWrapper.Instance.IsBiometricUnlockEnabled = false;
            ReturnResult?.Invoke(true);
            DismissAction?.Invoke();
        }
        private async void ExecuteDisableCommand()
        {
            PreferenceWrapper.Instance.PinUnlockHash = string.Empty;
            PreferenceWrapper.Instance.IsPinUnlockEnabled = false;
            PreferenceWrapper.Instance.IsBiometricUnlockEnabled = false;
            ReturnResult?.Invoke(true);
            DismissAction?.Invoke();
        }
        #region Commands
        private DelegateCommand _saveCommand;
        public DelegateCommand SaveCommand => _saveCommand ??= new DelegateCommand(ExecuteSaveCommand).ObservesCanExecute(() => CanExecute);
        private DelegateCommand _disableCommand;
        public DelegateCommand DisableCommand => _disableCommand ??= new DelegateCommand(ExecuteDisableCommand).ObservesCanExecute(() => CanExecute);
        #endregion
    }
}
