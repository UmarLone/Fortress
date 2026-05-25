using Android.App;
using Android.Content;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Platforms.Android;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.Button;
using Google.Android.Material.Card;
using Java.Lang;
using System.Security.Cryptography;
using System.Text;
using Resource = Microsoft.Maui.Resource;
using View = Android.Views.View;

namespace Fortress.Droid.Renderers
{
    /// <summary>
    /// Inline autofill unlock sheet.
    ///
    /// Authentication priority (highest → lowest):
    /// 1. Biometric (fingerprint/face) + device credential fallback
    /// 2. Device credential only (pattern / PIN / password)
    /// 3. Fortress app PIN entry field (4-digit)
    /// 4. Error state — nothing configured.
    /// </summary>
    public class UnlockBottomSheetFragment : BottomSheetDialogFragment
    {
        // ── Events ──────────────────────────────────────────────────────
        public event Action OnUnlocked;
        public event Action OnCancelled;

        // ── State ───────────────────────────────────────────────────────
        private readonly string _appName;

        // Resolved once in OnCreateView
        private bool _canUseBiometric;
        private bool _canUseDeviceCredential;
        private bool _canUseAppPin;

        // ── Views ───────────────────────────────────────────────────────
        private MaterialCardView _errorBanner;
        private TextView _errorText;
        private TextView _subtitle;
        private LinearLayout _pinSection;
        private EditText _pinEditText;
        private MaterialButton _unlockPinButton;
        private MaterialButton _biometricButton;
        private MaterialButton _pinButton;
        private MaterialButton _cancelButton;

        public UnlockBottomSheetFragment(string appName = null) => _appName = appName;

        // ── Sheet size ───────────────────────────────────────────────────
        public override void OnStart()
        {
            base.OnStart();
            if (Dialog is not BottomSheetDialog dlg) return;
            var bs = dlg.FindViewById<FrameLayout>(Resource.Id.design_bottom_sheet);
            if (bs == null) return;
            bs.LayoutParameters.Height = ViewGroup.LayoutParams.WrapContent;
            bs.RequestLayout();
            var beh = BottomSheetBehavior.From(bs);
            beh.State = BottomSheetBehavior.StateExpanded;
            beh.SkipCollapsed = true;
            beh.Draggable = true;
        }

        public override Dialog OnCreateDialog(Bundle savedInstanceState) =>
            new BottomSheetDialog(Context!, Resource.Style.BottomSheetDialogTheme);

        // ── Inflate ──────────────────────────────────────────────────────
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.bs_unlock, container, false);

            // Apply Audiowide font to the FORTRESS hero title
            (view.FindViewWithTag("fortressTitle") as TextView)
         ?.SetTypeface(FontHelper.Audiowide(RequireContext()), Android.Graphics.TypefaceStyle.Bold);

            _subtitle = view.FindViewById<TextView>(Resource.Id.unlockSubtitle);
            _errorBanner = view.FindViewById<MaterialCardView>(Resource.Id.errorBanner);
            _errorText = view.FindViewById<TextView>(Resource.Id.errorText);
            _pinSection = view.FindViewById<LinearLayout>(Resource.Id.pinSection);
            _pinEditText = view.FindViewById<EditText>(Resource.Id.pinEditText);
            _unlockPinButton = view.FindViewById<MaterialButton>(Resource.Id.unlockPinButton);
            _biometricButton = view.FindViewById<MaterialButton>(Resource.Id.biometricButton);
            _pinButton = view.FindViewById<MaterialButton>(Resource.Id.pinButton);
            _cancelButton = view.FindViewById<MaterialButton>(Resource.Id.cancelButton);

            if (!string.IsNullOrWhiteSpace(_appName))
                _subtitle.Text = $"Unlock to fill credentials for {_appName}.";

            ResolveAuthCapabilities();

            if (_canUseBiometric || _canUseDeviceCredential)
            {
                _biometricButton.Visibility = ViewStates.Visible;
                _biometricButton.Text = _canUseBiometric
        ? "Use Biometrics / Device Lock"
              : "Use Device Lock (Pattern / PIN / Password)";

                _pinButton.Visibility = _canUseAppPin ? ViewStates.Visible : ViewStates.Gone;
                _pinButton.Text = "Use FORTRESS PIN instead";

                _biometricButton.Click += (_, __) => TriggerSystemAuth();
                _pinButton.Click += (_, __) => ShowAppPinMode();

                TriggerSystemAuth();
            }
            else if (_canUseAppPin)
            {
                ShowAppPinMode();
            }
            else
            {
                _subtitle.Text = "No unlock method is configured. Please open FORTRESS to set one up.";
            }

            // ── PIN EditText wiring ──────────────────────────────────────
            _pinEditText.AddTextChangedListener(new PinWatcher(this));
            _pinEditText.EditorAction += (_, args) =>
            {
                if (args.ActionId == ImeAction.Done)
                    TryVerifyPin();
            };

            _unlockPinButton.Click += (_, __) => TryVerifyPin();

            _cancelButton.Click += (_, __) =>
                 {
                     OnCancelled?.Invoke();
                     Dismiss();
                 };

            return view;
        }

        // ── Capability resolution ─────────────────────────────────────────
        private void ResolveAuthCapabilities()
        {
            var manager = BiometricManager.From(RequireContext());

            _canUseBiometric =
        PreferenceWrapper.Instance.IsBiometricUnlockEnabled &&
   manager.CanAuthenticate(BiometricManager.Authenticators.BiometricWeak)
    == BiometricManager.BiometricSuccess;

            _canUseDeviceCredential =
      PreferenceWrapper.Instance.IsBiometricUnlockEnabled &&
        manager.CanAuthenticate(BiometricManager.Authenticators.DeviceCredential)
              == BiometricManager.BiometricSuccess;

            _canUseAppPin =
                PreferenceWrapper.Instance.IsPinUnlockEnabled &&
                         !string.IsNullOrEmpty(PreferenceWrapper.Instance.PinUnlockHash);
        }

        // ── System auth prompt ────────────────────────────────────────────
        private void TriggerSystemAuth()
        {
            HideError();

            int allowedAuthenticators = _canUseBiometric
       ? BiometricManager.Authenticators.BiometricWeak |
         BiometricManager.Authenticators.DeviceCredential
      : BiometricManager.Authenticators.DeviceCredential;

            var promptInfo = new BiometricPrompt.PromptInfo.Builder()
                   .SetTitle("FORTRESS")
                  .SetSubtitle(string.IsNullOrWhiteSpace(_appName)
                   ? "Authenticate to fill your credentials."
                     : $"Filling for {_appName}")
            .SetAllowedAuthenticators(allowedAuthenticators)
               .Build();

            new BiometricPrompt(this,
               ContextCompat.GetMainExecutor(RequireContext()),
                       new BiometricCallback(this))
                 .Authenticate(promptInfo);
        }

        // ── App-level PIN entry ───────────────────────────────────────────
        private void ShowAppPinMode()
        {
            _biometricButton.Visibility = ViewStates.Gone;
            _pinButton.Visibility = ViewStates.Gone;
            _pinSection.Visibility = ViewStates.Visible;
            _unlockPinButton.Visibility = ViewStates.Visible;
            _unlockPinButton.Enabled = false;

            _pinEditText.Text = string.Empty;
            _pinEditText.RequestFocus();

            // Show the soft keyboard so the user can type their PIN
            var imm = Activity?.GetSystemService(Context.InputMethodService) as InputMethodManager;
            imm?.ShowSoftInput(_pinEditText, ShowFlags.Implicit);
        }

        private void TryVerifyPin()
        {
            var pin = _pinEditText.Text?.Trim() ?? string.Empty;
            if (pin.Length != 4) return;
            VerifyAppPin(pin);
        }

        // ── App PIN verification ──────────────────────────────────────────
        private void VerifyAppPin(string pin)
        {
            using var sha = SHA256.Create();
            var hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(pin)));

            if (hash == PreferenceWrapper.Instance.PinUnlockHash)
            {
                PreferenceWrapper.Instance.IsApplicationLocked = false;
                HideError();
                // Hide keyboard before dismissing
                var imm = Activity?.GetSystemService(Context.InputMethodService) as InputMethodManager;
                imm?.HideSoftInputFromWindow(_pinEditText.WindowToken, HideSoftInputFlags.None);
                Dismiss();
                OnUnlocked?.Invoke();
            }
            else
            {
                _pinEditText.Text = string.Empty;
                _unlockPinButton.Enabled = false;
                ShowError("Incorrect PIN. Please try again.");
            }
        }

        // ── Error helpers ────────────────────────────────────────────────
        private void ShowError(string message)
        {
            _errorText.Text = message;
            _errorBanner.Visibility = ViewStates.Visible;
        }

        private void HideError() => _errorBanner.Visibility = ViewStates.Gone;

        // ── PIN text watcher — enables unlock button at exactly 4 digits ──
        private class PinWatcher : Java.Lang.Object, ITextWatcher
        {
            private readonly UnlockBottomSheetFragment _parent;
            public PinWatcher(UnlockBottomSheetFragment parent) => _parent = parent;

            public void AfterTextChanged(IEditable s)
            {
                var len = s?.Length() ?? 0;
                _parent._unlockPinButton.Enabled = len == 4;
                _parent.HideError();

                // Auto-verify as soon as 4 digits are entered
                if (len == 4)
                    _parent.VerifyAppPin(s!.ToString());
            }

            public void BeforeTextChanged(ICharSequence s, int start, int count, int after) { }
            public void OnTextChanged(ICharSequence s, int start, int before, int count) { }
        }

        // ── System auth callback ──────────────────────────────────────────
        private class BiometricCallback : BiometricPrompt.AuthenticationCallback
        {
            private readonly UnlockBottomSheetFragment _parent;
            public BiometricCallback(UnlockBottomSheetFragment parent) => _parent = parent;

            public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
            {
                base.OnAuthenticationSucceeded(result);
                PreferenceWrapper.Instance.IsApplicationLocked = false;
                _parent.HideError();
                _parent.Dismiss();
                _parent.OnUnlocked?.Invoke();
            }

            public override void OnAuthenticationError(int errorCode, ICharSequence errString)
            {
                base.OnAuthenticationError(errorCode, errString);

                if (errorCode == BiometricPrompt.ErrorUserCanceled ||
                  errorCode == BiometricPrompt.ErrorNegativeButton ||
                  errorCode == BiometricPrompt.ErrorCanceled)
                {
                    if (_parent._canUseAppPin)
                    {
                        _parent.Activity?.RunOnUiThread(_parent.ShowAppPinMode);
                        return;
                    }
                    _parent.Activity?.RunOnUiThread(() =>
                     {
                         _parent.OnCancelled?.Invoke();
                         _parent.Dismiss();
                     });
                    return;
                }

                if (errorCode == BiometricPrompt.ErrorLockout ||
               errorCode == BiometricPrompt.ErrorLockoutPermanent)
                {
                    _parent.Activity?.RunOnUiThread(() =>
                 _parent.ShowError("Too many attempts. Please wait and try again."));
                    return;
                }

                _parent.Activity?.RunOnUiThread(() =>
         _parent.ShowError(errString?.ToString() ?? "Authentication failed."));
            }

            public override void OnAuthenticationFailed() => base.OnAuthenticationFailed();
        }
    }
}
