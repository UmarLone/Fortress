using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fortress.Windows.Desktop.Services;
using System.Windows;
using System.Windows.Media;

namespace Fortress.Windows.Desktop.ViewModels.Windows
{
    /// <summary>
    /// Drives SetupWindow.
    ///
    /// Steps:
    ///   0  Master Password   (required)
    ///   1  App Lock   (PIN + optional Windows Hello — skippable)
    ///   2  Cloud Backup      (OneDrive / Google Drive / Dropbox — skippable)
    ///   3  Finish
    /// </summary>
    public partial class SetupViewModel : ObservableObject
    {
        private const int TotalSteps = 4;
        private readonly IVaultSessionService _session;
        private readonly IBiometricService _biometric;

        // ── Step state ────────────────────────────────────────────────────────
        [ObservableProperty] private int _currentStep = 0;
        [ObservableProperty] private bool _canGoNext = false;
        [ObservableProperty] private bool _isCreating = false;
        [ObservableProperty] private bool _showSkipButton = false;
        [ObservableProperty] private string _nextButtonText = "Continue";

        // ── Header pills ──────────────────────────────────────────────────────
        public double Step0PillWidth => 50;
        public double Step1PillWidth => CurrentStep >= 1 ? 50 : 40;
        public double Step2PillWidth => CurrentStep >= 2 ? 50 : 40;
        public double Step3PillWidth => CurrentStep >= 3 ? 50 : 40;
        public double Step1PillOpacity => CurrentStep >= 1 ? 1.0 : 0.4;
        public double Step2PillOpacity => CurrentStep >= 2 ? 1.0 : 0.4;
        public double Step3PillOpacity => CurrentStep >= 3 ? 1.0 : 0.4;

        // ── Header icon / title / subtitle ────────────────────────────────────
        public string StepIcon => CurrentStep switch
        {
            0 => "LockOpen24",
            1 => "Fingerprint24",
            2 => "Cloud24",
            _ => "CheckmarkCircle24"
        };

        public string StepTitle => CurrentStep switch
        {
            0 => "Create Your Master Password",
            1 => "Set Up Quick Unlock",
            2 => "Cloud Backup (Optional)",
            _ => "You're All Set!"
        };

        public string StepSubtitle => CurrentStep switch
        {
            0 => "This is the one password that unlocks everything",
            1 => "Use a PIN or Windows Hello to open Fortress quickly",
            2 => "Back up your encrypted vault to the cloud",
            _ => "Your vault is ready to use"
        };

        // ── Footer layout ─────────────────────────────────────────────────────
        public int ContinueButtonColumn => ShowSkipButton ? 1 : 0;
        public int ContinueButtonColumnSpan => ShowSkipButton ? 1 : 2;
        public string ContinueButtonMargin => ShowSkipButton ? "6,0,0,0" : "0";

        // ── Shared brush/thickness helpers ────────────────────────────────────
        private static SolidColorBrush Brush(string hex) =>
            new(ColorConverter.ConvertFromString(hex) is Color c ? c : Colors.Transparent);
        private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);
        private static readonly Thickness BorderOn = new(2);
        private static readonly Thickness BorderOff = new(0);

        // ════════════════════════════════════════════════════════════════════
        // STEP 0 — MASTER PASSWORD
        // ════════════════════════════════════════════════════════════════════
        [ObservableProperty] private string _masterPassword = "";
        [ObservableProperty] private string _masterPasswordConfirm = "";
        [ObservableProperty] private string _masterPasswordError = "";
        [ObservableProperty] private int _passwordStrength = 0;

        public string StrengthLabel => PasswordStrength switch
        {
            0 => "",
            1 => "Weak",
            2 => "Fair",
            3 => "Good",
            _ => "Strong"
        };
        public string StrengthColor => PasswordStrength switch
        {
            1 => "#EF4444",
            2 => "#F97316",
            3 => "#EAB308",
            _ => "#22C55E"
        };
        public Brush StrengthBrush => PasswordStrength switch
        {
            1 => Brush("#EF4444"),
            2 => Brush("#F97316"),
            3 => Brush("#EAB308"),
            _ => Brush("#22C55E")
        };
        public double StrengthBarWidth => PasswordStrength * (460.0 / 4);

        public string Req10Chars => MasterPassword.Length >= 10 ? "CheckmarkCircle24" : "Circle24";
        public string ReqNumber => MasterPassword.Any(char.IsDigit) ? "CheckmarkCircle24" : "Circle24";
        public string ReqSymbol => MasterPassword.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)) ? "CheckmarkCircle24" : "Circle24";
        public string ReqLower => MasterPassword.Any(char.IsLower) ? "CheckmarkCircle24" : "Circle24";
        public string ReqUpper => MasterPassword.Any(char.IsUpper) ? "CheckmarkCircle24" : "Circle24";
        public string Req10CharsColor => MasterPassword.Length >= 10 ? "#22C55E" : "#9CA3AF";
        public string ReqNumberColor => MasterPassword.Any(char.IsDigit) ? "#22C55E" : "#9CA3AF";
        public string ReqSymbolColor => MasterPassword.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)) ? "#22C55E" : "#9CA3AF";
        public string ReqLowerColor => MasterPassword.Any(char.IsLower) ? "#22C55E" : "#9CA3AF";
        public string ReqUpperColor => MasterPassword.Any(char.IsUpper) ? "#22C55E" : "#9CA3AF";

        partial void OnMasterPasswordChanged(string value)
        {
            PasswordStrength = ComputeStrength(value);
            OnPropertyChanged(nameof(StrengthLabel));
            OnPropertyChanged(nameof(StrengthBrush));
            OnPropertyChanged(nameof(StrengthColor));
            OnPropertyChanged(nameof(StrengthBarWidth));
            OnPropertyChanged(nameof(Req10Chars)); OnPropertyChanged(nameof(Req10CharsColor));
            OnPropertyChanged(nameof(ReqNumber)); OnPropertyChanged(nameof(ReqNumberColor));
            OnPropertyChanged(nameof(ReqSymbol)); OnPropertyChanged(nameof(ReqSymbolColor));
            OnPropertyChanged(nameof(ReqLower)); OnPropertyChanged(nameof(ReqLowerColor));
            OnPropertyChanged(nameof(ReqUpper)); OnPropertyChanged(nameof(ReqUpperColor));
            ValidatePasswordStep();
        }
        partial void OnMasterPasswordConfirmChanged(string _) => ValidatePasswordStep();

        private void ValidatePasswordStep()
        {
            MasterPasswordError = "";
            var ok = MasterPassword.Length >= 10
       && MasterPassword.Any(char.IsDigit)
                  && MasterPassword.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
               && MasterPassword.Any(char.IsLower)
  && MasterPassword.Any(char.IsUpper)
   && MasterPassword == MasterPasswordConfirm;
            if (CurrentStep == 0) CanGoNext = ok;
        }

        private static int ComputeStrength(string pw)
        {
            if (string.IsNullOrEmpty(pw)) return 0;
            int s = 0;
            if (pw.Length >= 10) s++;
            if (pw.Any(char.IsDigit)) s++;
            if (pw.Any(c => !char.IsLetterOrDigit(c))) s++;
            if (pw.Any(char.IsLower) && pw.Any(char.IsUpper)) s++;
            return Math.Clamp(s, 0, 4);
        }

        // ════════════════════════════════════════════════════════════════════
        // STEP 1 — APP LOCK  (PIN + Biometric)
        // ════════════════════════════════════════════════════════════════════
        [ObservableProperty] private bool _setupPin = false;
        [ObservableProperty] private bool _isPinEntryVisible = false;
        [ObservableProperty] private bool _isConfirmingPin = false;
        [ObservableProperty] private string _pinEntryText = "";
        [ObservableProperty] private bool _hasPinError = false;
        [ObservableProperty] private string _pinErrorMessage = "";
        [ObservableProperty] private bool _isPinSaved = false;

        // Biometric card
        [ObservableProperty] private bool _setupBiometric = false;
        [ObservableProperty] private bool _isBiometricAvailable = false;
        [ObservableProperty] private bool _isBiometricSaved = false;
        [ObservableProperty] private bool _isBiometricChecking = false;
        [ObservableProperty] private string _biometricStatusText = "";

        private string _pinBuffer = "";
        private string _pinConfirm = "";
        private string _savedPin = "";   // holds the confirmed PIN until NextAsync persists it

        public string PinPromptText => IsConfirmingPin ? "Enter it again to confirm" : "Enter your 4-digit PIN";

        // PIN dot brushes
        public Brush PinDot1Brush => _pinBuffer.Length >= 1 ? Brush("#4F46E5") : Brush("#E5E7EB");
        public Brush PinDot2Brush => _pinBuffer.Length >= 2 ? Brush("#4F46E5") : Brush("#E5E7EB");
        public Brush PinDot3Brush => _pinBuffer.Length >= 3 ? Brush("#4F46E5") : Brush("#E5E7EB");
        public Brush PinDot4Brush => _pinBuffer.Length >= 4 ? Brush("#4F46E5") : Brush("#E5E7EB");

        // PIN card border
        public Thickness PinCardBorder => SetupPin ? BorderOn : BorderOff;
        public SolidColorBrush PinCardBorderBrush => SetupPin ? Brush("#4F46E5") : TransparentBrush;
        public string PinCardBorderColor => SetupPin ? "#4F46E5" : "Transparent";

        // Biometric card border
        public Thickness BiometricCardBorder => SetupBiometric ? BorderOn : BorderOff;
        public SolidColorBrush BiometricCardBorderBrush => SetupBiometric ? Brush("#7C3AED") : TransparentBrush;
        public string BiometricCardBorderColor => SetupBiometric ? "#7C3AED" : "Transparent";

        private async Task CheckBiometricAvailabilityAsync()
        {
            var result = await _biometric.CheckAvailabilityAsync();
            IsBiometricAvailable = result == BiometricAvailability.Available;
        }

        /// <summary>Toggles the PIN card selection and shows/hides the inline PIN entry.</summary>
        [RelayCommand]
        private void SelectPin()
        {
            SetupPin = !SetupPin;
            IsPinEntryVisible = SetupPin;
            OnPropertyChanged(nameof(PinCardBorder));
            OnPropertyChanged(nameof(PinCardBorderBrush));
            OnPropertyChanged(nameof(PinCardBorderColor));

            if (!SetupPin)
                ResetPinState();
            else
                PinEntryText = "";
        }

        [RelayCommand]
        private async Task SelectBiometricAsync()
        {
            if (!IsBiometricAvailable) return;
            SetupBiometric = !SetupBiometric;
            OnPropertyChanged(nameof(BiometricCardBorder));
            OnPropertyChanged(nameof(BiometricCardBorderBrush));
            OnPropertyChanged(nameof(BiometricCardBorderColor));

            if (SetupBiometric)
            {
                IsBiometricChecking = true;
                BiometricStatusText = "";
                try
                {
                    var result = await _biometric.RequestVerificationAsync(
                        "Register Windows Hello for Fortress unlock");
                    if (result == BiometricVerificationResult.Verified)
                    {
                        IsBiometricSaved = true;
                        BiometricStatusText = "Windows Hello registered!";
                    }
                    else if (result == BiometricVerificationResult.NotAvailable)
                    {
                        SetupBiometric = false;
                        IsBiometricSaved = false;
                        BiometricStatusText = "Windows Hello is not available on this device.";
                        RefreshBiometricBorders();
                    }
                    else
                    {
                        SetupBiometric = false;
                        IsBiometricSaved = false;
                        BiometricStatusText = result == BiometricVerificationResult.Canceled
                         ? "" : "Verification failed — tap to try again.";
                        RefreshBiometricBorders();
                    }
                }
                finally { IsBiometricChecking = false; }
            }
            else
            {
                IsBiometricSaved = false;
                BiometricStatusText = "";
            }
        }

        private void RefreshBiometricBorders()
        {
            OnPropertyChanged(nameof(BiometricCardBorder));
            OnPropertyChanged(nameof(BiometricCardBorderBrush));
            OnPropertyChanged(nameof(BiometricCardBorderColor));
        }

        partial void OnPinEntryTextChanged(string value)
        {
            var digits = new string(value.Where(char.IsAsciiDigit).Take(4).ToArray());
            if (digits != value) { PinEntryText = digits; return; }

            HasPinError = false;
            PinErrorMessage = "";

            if (!IsConfirmingPin)
            {
                _pinBuffer = digits;
                RefreshDots();
                if (_pinBuffer.Length == 4)
                {
                    IsConfirmingPin = true;
                    PinEntryText = "";
                    _pinConfirm = "";
                    OnPropertyChanged(nameof(PinPromptText));
                }
            }
            else
            {
                _pinConfirm = digits;
                RefreshDots();
                if (_pinConfirm.Length == 4) ValidatePin();
            }
        }

        private void RefreshDots()
        {
            OnPropertyChanged(nameof(PinDot1Brush));
            OnPropertyChanged(nameof(PinDot2Brush));
            OnPropertyChanged(nameof(PinDot3Brush));
            OnPropertyChanged(nameof(PinDot4Brush));
        }

        private void ValidatePin()
        {
            if (_pinBuffer == _pinConfirm)
            {
                _savedPin = _pinBuffer;   // keep a copy before ResetPinState wipes _pinBuffer
                IsPinSaved = true;
                HasPinError = false;
            }
            else
            {
                HasPinError = true;
                PinErrorMessage = "PINs don't match — please try again";
                ResetPinState();
                PinEntryText = "";
            }
        }

        private void ResetPinState()
        {
            _pinBuffer = "";
            _pinConfirm = "";
            IsConfirmingPin = false;
            IsPinSaved = false;
            HasPinError = false;
            PinErrorMessage = "";
            OnPropertyChanged(nameof(PinPromptText));
            RefreshDots();
        }

        // ════════════════════════════════════════════════════════════════════
        // STEP 2 — CLOUD SYNC
        // ════════════════════════════════════════════════════════════════════
        [ObservableProperty] private string _selectedCloudProvider = "";
        [ObservableProperty] private bool _isCloudConnecting = false;
        [ObservableProperty] private bool _isCloudConnected = false;
        [ObservableProperty] private string _cloudConnectedEmail = "";
        [ObservableProperty] private string _cloudConnectedProviderName = "";

        public bool IsOneDriveSelected => SelectedCloudProvider == "onedrive";
        public bool IsGoogleDriveSelected => SelectedCloudProvider == "googledrive";
        public bool IsDropboxSelected => SelectedCloudProvider == "dropbox";
        public bool IsProviderSelected => !string.IsNullOrEmpty(SelectedCloudProvider);

        // OneDrive borders
        public Thickness OneDriveBorder => IsOneDriveSelected ? BorderOn : BorderOff;
        public SolidColorBrush OneDriveBorderBrush => IsOneDriveSelected ? Brush("#4F46E5") : TransparentBrush;
        public string OneDriveBorderColor => IsOneDriveSelected ? "#4F46E5" : "Transparent";

        // Google Drive borders
        public Thickness GoogleDriveBorder => IsGoogleDriveSelected ? BorderOn : BorderOff;
        public SolidColorBrush GoogleDriveBorderBrush => IsGoogleDriveSelected ? Brush("#4285F4") : TransparentBrush;
        public string GoogleDriveBorderColor => IsGoogleDriveSelected ? "#4285F4" : "Transparent";

        // Dropbox borders
        public Thickness DropboxBorder => IsDropboxSelected ? BorderOn : BorderOff;
        public SolidColorBrush DropboxBorderBrush => IsDropboxSelected ? Brush("#0061FF") : TransparentBrush;
        public string DropboxBorderColor => IsDropboxSelected ? "#0061FF" : "Transparent";

        public string ConnectButtonText => IsCloudConnecting ? "Connecting..." : "Connect and Back Up";

        partial void OnSelectedCloudProviderChanged(string _)
        {
            OnPropertyChanged(nameof(IsOneDriveSelected));
            OnPropertyChanged(nameof(IsGoogleDriveSelected));
            OnPropertyChanged(nameof(IsDropboxSelected));
            OnPropertyChanged(nameof(IsProviderSelected));
            OnPropertyChanged(nameof(OneDriveBorder)); OnPropertyChanged(nameof(OneDriveBorderBrush)); OnPropertyChanged(nameof(OneDriveBorderColor));
            OnPropertyChanged(nameof(GoogleDriveBorder)); OnPropertyChanged(nameof(GoogleDriveBorderBrush)); OnPropertyChanged(nameof(GoogleDriveBorderColor));
            OnPropertyChanged(nameof(DropboxBorder)); OnPropertyChanged(nameof(DropboxBorderBrush)); OnPropertyChanged(nameof(DropboxBorderColor));
        }

        [RelayCommand]
        private void SelectProvider(string key) => SelectedCloudProvider = SelectedCloudProvider == key ? "" : key;

        [RelayCommand]
        private async Task ConnectCloudAsync()
        {
            if (!IsProviderSelected) return;
            IsCloudConnecting = true;
            OnPropertyChanged(nameof(ConnectButtonText));
            try
            {
                await Task.Delay(1200);
                CloudConnectedProviderName = SelectedCloudProvider switch
                {
                    "onedrive" => "OneDrive",
                    "googledrive" => "Google Drive",
                    "dropbox" => "Dropbox",
                    _ => SelectedCloudProvider
                };
                CloudConnectedEmail = "";
                IsCloudConnected = true;
                VaultSettingsStore.Instance.IsCloudSyncEnabled = true;
                VaultSettingsStore.Instance.CloudSyncProvider = CloudConnectedProviderName;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SetupViewModel] Cloud connect error: {ex.Message}"); }
            finally { IsCloudConnecting = false; OnPropertyChanged(nameof(ConnectButtonText)); }
        }

        [RelayCommand]
        private void DisconnectCloud()
        {
            IsCloudConnected = false; CloudConnectedEmail = ""; CloudConnectedProviderName = ""; SelectedCloudProvider = "";
            VaultSettingsStore.Instance.IsCloudSyncEnabled = false;
            VaultSettingsStore.Instance.CloudSyncProvider = "";
        }

        // ════════════════════════════════════════════════════════════════════
        // NAVIGATION
        // ════════════════════════════════════════════════════════════════════
        public SetupViewModel(IVaultSessionService session, IBiometricService biometric)
        {
            _session = session;
            _biometric = biometric;
            UpdateStepState();
            _ = CheckBiometricAvailabilityAsync();
        }

        [RelayCommand]
        private async Task NextAsync()
        {
            if (CurrentStep == 0)
            {
                var ok = await _session.SetupMasterPasswordAsync(MasterPassword, MasterPasswordConfirm);
                if (!ok) { MasterPasswordError = "Passwords do not match or do not meet requirements."; return; }
                CurrentStep = 1;
            }
            else if (CurrentStep == 1)
            {
                if (SetupPin && IsPinSaved)
                    await _session.SetupPinAsync(_savedPin);
                else if (SetupPin && !IsPinSaved)
                {
                    HasPinError = true;
                    PinErrorMessage = "Please complete your PIN entry before continuing.";
                    return;
                }
                if (SetupBiometric && IsBiometricSaved)
                {
                    VaultSettingsStore.Instance.IsBiometricUnlockEnabled = true;
                    // Mirror to shared service prefs
                    _session.SetBiometricEnabled(true);
                }
                CurrentStep = 2;
            }
            else if (CurrentStep == 2)
                CurrentStep = 3;
            else if (CurrentStep == 3)
                await FinishAsync();
        }

        [RelayCommand]
        private async Task SkipAsync()
        {
            if (CurrentStep < 3) CurrentStep++;
            else await FinishAsync();
        }

        private async Task FinishAsync()
        {
            IsCreating = true;
            try
            {
                VaultSettingsStore.Instance.HasSetupCompleted = true;
                await Task.Delay(350);
                OnSetupComplete?.Invoke(this, EventArgs.Empty);
            }
            finally { IsCreating = false; }
        }

        partial void OnCurrentStepChanged(int value) => UpdateStepState();

        private void UpdateStepState()
        {
            ShowSkipButton = CurrentStep is 1 or 2;
            NextButtonText = CurrentStep == 3 ? "Open Fortress" : "Continue";
            CanGoNext = CurrentStep != 0;

            OnPropertyChanged(nameof(StepIcon)); OnPropertyChanged(nameof(StepTitle)); OnPropertyChanged(nameof(StepSubtitle));
            OnPropertyChanged(nameof(Step0PillWidth)); OnPropertyChanged(nameof(Step1PillWidth));
            OnPropertyChanged(nameof(Step2PillWidth)); OnPropertyChanged(nameof(Step3PillWidth));
            OnPropertyChanged(nameof(Step1PillOpacity)); OnPropertyChanged(nameof(Step2PillOpacity)); OnPropertyChanged(nameof(Step3PillOpacity));
            OnPropertyChanged(nameof(ContinueButtonColumn)); OnPropertyChanged(nameof(ContinueButtonColumnSpan)); OnPropertyChanged(nameof(ContinueButtonMargin));

            if (CurrentStep == 0) ValidatePasswordStep();
        }

        public event EventHandler? OnSetupComplete;
    }
}
