using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fortress.Windows.Desktop.Services;

namespace Fortress.Windows.Desktop.ViewModels.Windows
{
    /// <summary>
    /// Drives the lock screen. Supports three unlock methods:
    ///   1. 4-digit PIN (shown first if enabled)
    ///   2. Windows Hello biometrics (shown if enabled)
    /// 3. Master password (always available as fallback)
    /// </summary>
    public partial class LockScreenViewModel : ObservableObject
    {
        private readonly IVaultSessionService _session;
        private readonly IBiometricService _biometric;

        [ObservableProperty] private string _password = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool _isBusy = false;
        [ObservableProperty] private bool _showPinPad = false;
        [ObservableProperty] private bool _showPasswordBox = true;
        [ObservableProperty] private bool _showBiometricButton = false;

        // ── PIN dot brushes ───────────────────────────────────────────────────
        private static System.Windows.Media.SolidColorBrush DotFilled => new(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4F46E5")!);
        private static System.Windows.Media.SolidColorBrush DotEmpty => new(
   (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5E7EB")!);

        private string _pinBuffer = "";
        public System.Windows.Media.Brush PinDot1 => _pinBuffer.Length >= 1 ? DotFilled : DotEmpty;
        public System.Windows.Media.Brush PinDot2 => _pinBuffer.Length >= 2 ? DotFilled : DotEmpty;
        public System.Windows.Media.Brush PinDot3 => _pinBuffer.Length >= 3 ? DotFilled : DotEmpty;
        public System.Windows.Media.Brush PinDot4 => _pinBuffer.Length >= 4 ? DotFilled : DotEmpty;

     public bool PinEnabled  => VaultSettingsStore.Instance.IsPinUnlockEnabled;
        public bool HasBiometric => VaultSettingsStore.Instance.IsBiometricUnlockEnabled;

     // ?? Manually declared commands (source generator unreliable on .NET 10 preview SDK) ??
        public IAsyncRelayCommand UnlockWithPasswordAsyncCommand   { get; }
        public IAsyncRelayCommand UnlockWithBiometricAsyncCommand  { get; }
        public IRelayCommand<string> PinDigitCommand      { get; }
        public IRelayCommand         PinBackspaceCommand           { get; }
     public IRelayCommand         SwitchToPasswordCommand{ get; }
        public IRelayCommand         SwitchToPinCommand     { get; }

        public LockScreenViewModel(IVaultSessionService session, IBiometricService biometric)
        {
  _session   = session;
            _biometric = biometric;

    UnlockWithPasswordAsyncCommand  = new AsyncRelayCommand(UnlockWithPasswordAsync);
            UnlockWithBiometricAsyncCommand = new AsyncRelayCommand(UnlockWithBiometricAsync);
PinDigitCommand               = new RelayCommand<string>(PinDigit);
    PinBackspaceCommand  = new RelayCommand(PinBackspace);
            SwitchToPasswordCommand       = new RelayCommand(SwitchToPassword);
  SwitchToPinCommand         = new RelayCommand(SwitchToPin);

     if (PinEnabled)  { ShowPinPad = true; ShowPasswordBox = false; }
          if (HasBiometric)  ShowBiometricButton = true;
      }

 // ── Master-password unlock ────────────────────────────────────────────
        private async Task UnlockWithPasswordAsync()
        {
          if (IsBusy) return;
        IsBusy = true;
      ErrorMessage = "";
   try
        {
                var ok = await _session.UnlockWithPasswordAsync(Password);
                if (ok) { Password = ""; OnUnlocked?.Invoke(this, EventArgs.Empty); }
       else    { ErrorMessage = "Incorrect master password. Please try again."; Password = ""; }
}
            finally { IsBusy = false; }
        }

  // ── PIN unlock ────────────────────────────────────────────────────────
        private void PinDigit(string? digit)
        {
  if (digit is null || _pinBuffer.Length >= 4) return;
 _pinBuffer += digit;
        RefreshDots();
   if (_pinBuffer.Length == 4) TryUnlockWithPinAsync();
  }

      private void PinBackspace()
  {
   if (_pinBuffer.Length == 0) return;
        _pinBuffer = _pinBuffer[..^1];
            RefreshDots();
        }

        private void RefreshDots()
    {
          OnPropertyChanged(nameof(PinDot1));
            OnPropertyChanged(nameof(PinDot2));
            OnPropertyChanged(nameof(PinDot3));
            OnPropertyChanged(nameof(PinDot4));
        }

        private async void TryUnlockWithPinAsync()
        {
    IsBusy = true;
            ErrorMessage = "";
     try
    {
   var ok = await _session.UnlockWithPinAsync(_pinBuffer);
    _pinBuffer = "";
      RefreshDots();
       if (ok) OnUnlocked?.Invoke(this, EventArgs.Empty);
           else    ErrorMessage = "Incorrect PIN. Please try again.";
            }
            finally { IsBusy = false; }
      }

   // ── Windows Hello unlock ──────────────────────────────────────────────
        private async Task UnlockWithBiometricAsync()
        {
            if (IsBusy) return;
          IsBusy = true;
            ErrorMessage = "";
            try
            {
    var availability = await _biometric.CheckAvailabilityAsync();
      if (availability != BiometricAvailability.Available)
            {
       ErrorMessage = "Windows Hello is not available on this device.";
           return;
     }

  var result = await _biometric.RequestVerificationAsync("Unlock your Fortress vault");

       if (result == BiometricVerificationResult.Verified)
        {
           var masterPw = VaultSettingsStore.Instance.GetPinProtectedMasterPassword();
               if (masterPw != null && await _session.UnlockWithPasswordAsync(masterPw))
               OnUnlocked?.Invoke(this, EventArgs.Empty);
     else
     ErrorMessage = "Biometric verified but vault key could not be restored. Use master password.";
     }
  else if (result != BiometricVerificationResult.Canceled)
    {
          ErrorMessage = "Windows Hello verification failed. Try PIN or master password.";
                }
            }
            catch (Exception ex)
   {
    ErrorMessage = $"Windows Hello error: {ex.Message}";
            }
         finally { IsBusy = false; }
    }

        // ── View switching ────────────────────────────────────────────────────
        private void SwitchToPassword()
    {
     ShowPinPad      = false;
  ShowPasswordBox = true;
   ErrorMessage    = "";
      }

private void SwitchToPin()
        {
            if (!PinEnabled) return;
   ShowPasswordBox = false;
   ShowPinPad      = true;
     ErrorMessage    = "";
 }

        public event EventHandler? OnUnlocked;
    }
}
