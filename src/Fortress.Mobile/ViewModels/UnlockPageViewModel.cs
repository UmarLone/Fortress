using Controls.UserDialogs.Maui;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Views;
using Maui.Biometric;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.ViewModels
{
    public class UnlockPageViewModel : ViewModelBase, INavigationAware
    {
        #region Properties
        private bool canUsePin;
        public bool CanUsePin
        {
            get => canUsePin;
            set => SetProperty(ref canUsePin, value);
        }

        private bool hasError;
        public bool HasError
        {
            get => hasError;
            set => SetProperty(ref hasError, value);
        }

        private bool canUseBiometrics;
        public bool CanUseBiometrics
        {
            get => canUseBiometrics;
            set => SetProperty(ref canUseBiometrics, value);
        }

        private string pin;
        public string Pin
        {
            get => pin;
            set => SetProperty(ref pin, value);
        }

        private string errorMessage;
        public string ErrorMessage
        {
            get => errorMessage;
            set => SetProperty(ref errorMessage, value);
        }

        private bool _isUnlocking;
        public bool IsUnlocking
        {
            get => _isUnlocking;
            set => SetProperty(ref _isUnlocking, value);
        }
        #endregion

        public TaskCompletionSource<bool> UnlockResult { get; private set; }

        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISharedCredentialWriter? _sharedCredentialWriter;
        private volatile bool _biometricInProgress;

        public UnlockPageViewModel(INavigationService navigationService,
                                   IUserDialogs dialogService,
                                   IEventAggregator eventAggregator,
                                   ISharedCredentialWriter? sharedCredentialWriter = null) : base(navigationService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _sharedCredentialWriter = sharedCredentialWriter;
        }

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);

            // Re-read preferences each time we navigate here (they may have
            // changed between app sessions).
            CanUsePin = PreferenceWrapper.Instance.IsPinUnlockEnabled;
            CanUseBiometrics = PreferenceWrapper.Instance.IsBiometricUnlockEnabled;

            // Only create a new TCS if one doesn't exist or the previous one
            // was already completed (allows the UnlockService to keep its
            // reference to the same TCS across retries).
            if (UnlockResult == null || UnlockResult.Task.IsCompleted)
                UnlockResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Reset UI state for a fresh attempt
            HasError = false;
            ErrorMessage = string.Empty;
            Pin = string.Empty;
            IsUnlocking = false;

            if (CanUseBiometrics)
                _ = ExecuteBiometricCommand();
        }

        private async Task ExecuteVerifyPinCommand()
        {
            if (string.IsNullOrEmpty(Pin) || IsUnlocking)
                return;

            IsUnlocking = true;
            try
            {
                // Hash on a background thread so the UI stays responsive
                // and the activity overlay has time to render.
                var enteredPin = Pin;
                var hash = await Task.Run(() =>
                {
                    using var sha = SHA256.Create();
                    var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(enteredPin));
                    return Convert.ToBase64String(hashBytes);
                });

                if (hash == PreferenceWrapper.Instance.PinUnlockHash)
                {
                    await CompleteUnlockAsync("PIN");
                }
                else
                {
                    ErrorMessage = "Incorrect PIN. Please try again.";
                    HasError = true;

                    // Tell the code-behind to ignore the next TextChanged
                    // so it doesn't overwrite our error when we clear Pin.
                    if (Application.Current?.MainPage != null)
                    {
                        var unlockPage = FindUnlockPage();
                        unlockPage?.SuppressNextTextChanged();
                    }

                    Pin = string.Empty;
                    // Do NOT set UnlockResult to false — let the user retry.
                    _ = LogEventAsync(EventLogType.UnlockFailed, "PIN");
                }
            }
            finally
            {
                IsUnlocking = false;
            }
        }

        private async Task ExecuteBiometricCommand()
        {
            // Guard against concurrent biometric prompts (e.g. double-tap,
            // or OnNavigatedTo fires while a prompt is already showing).
            if (_biometricInProgress || IsUnlocking)
                return;

            _biometricInProgress = true;
            IsUnlocking = true;
            try
            {
                var result = await BiometricAuthentication.Current.AuthenticateAsync(
                    new AuthenticationRequest(
                         title: "FORTRESS",
                     reason: "Please unlock to proceed"));

                if (result.IsSuccessful)
                {
                    await CompleteUnlockAsync("Biometric");
                }
                else
                {
                    ErrorMessage = "Biometric authentication failed.";
                    HasError = true;
                    // Do NOT set UnlockResult to false — user can retry or use PIN.
                    _ = LogEventAsync(EventLogType.UnlockFailed, "Biometric");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Unlock] Biometric error: {ex.Message}");
                ErrorMessage = "Biometric authentication unavailable.";
                HasError = true;
            }
            finally
            {
                _biometricInProgress = false;
                IsUnlocking = false;
            }
        }

        /// <summary>
        /// Central success path — updates lock state, signals the TCS,
        /// publishes the event. Navigation is handled by the UnlockService
        /// which pops the modal to restore the previous page context.
        /// </summary>
        private async Task CompleteUnlockAsync(string method)
        {
            PreferenceWrapper.Instance.IsApplicationLocked = false;
            _sharedCredentialWriter?.SyncLockStateToSharedPreferences();

            HasError = false;
            ErrorMessage = string.Empty;

            // Signal the UnlockService that unlock succeeded.
            // The service will pop the modal and restore navigation context.
            UnlockResult?.TrySetResult(true);

            // Publish event so App.xaml.cs can run any pending actions
            // (e.g. queued autofill, push notification handling).
            _eventAggregator.GetEvent<AppUnlockedEvent>().Publish(true);

            _ = LogEventAsync(EventLogType.VaultUnlocked, method);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static async Task LogEventAsync(EventLogType type, string? detail = null)
        {
            try
            {
                var processor = Shiny.Hosting.Host.GetService<IEventLogProcessor>();
                if (processor == null) return;
                await processor.ProcessEventLogAsync(new EventLog
                {
                    EventType = (int)type,
                    Detail = detail,
                });
            }
            catch { /* fire-and-forget — never crash the unlock flow */ }
        }

        /// <summary>
        /// Finds the UnlockPage instance to call code-behind methods.
        /// </summary>
        private static UnlockPage? FindUnlockPage()
        {
            var mainPage = Application.Current?.MainPage;
            if (mainPage is null) return null;

            // Check modals first.
            if (mainPage.Navigation?.ModalStack?.Count > 0)
            {
                var topModal = mainPage.Navigation.ModalStack[^1];
                if (topModal is UnlockPage up) return up;
                if (topModal is NavigationPage np && np.CurrentPage is UnlockPage mu) return mu;
            }

            if (mainPage is NavigationPage nav && nav.CurrentPage is UnlockPage su) return su;
            if (mainPage is UnlockPage du) return du;

            return null;
        }

        #region Commands
        private AsyncCommand _verifyPinCommand;
        public ICommand VerifyPinCommand =>
            _verifyPinCommand ??= new AsyncCommand(ExecuteVerifyPinCommand);

        private AsyncCommand _verifyBiometricsCommand;
        public ICommand VerifyBiometricsCommand =>
  _verifyBiometricsCommand ??= new AsyncCommand(ExecuteBiometricCommand);
        #endregion
    }
}
