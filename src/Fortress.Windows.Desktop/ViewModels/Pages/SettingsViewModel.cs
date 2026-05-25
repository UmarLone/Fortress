using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fortress.Windows.Desktop.Services;
using System.Windows.Media;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly IVaultSessionService _session;
        private readonly IBiometricService _biometric;

  // ── App info ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _appVersion = string.Empty;
        [ObservableProperty] private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        // ── Security toggles ─────────────────────────────────────────────────
        [ObservableProperty] private bool _pinEnabled;
    [ObservableProperty] private bool _biometricEnabled;
        [ObservableProperty] private bool _isBiometricAvailable;
        [ObservableProperty] private bool _lockOnMinimise;

        // ── Auto-lock ─────────────────────────────────────────────────────────
        [ObservableProperty] private int _autoLockIndex;

        // ── Clipboard ─────────────────────────────────────────────────────────
        [ObservableProperty] private bool _clearClipboard;

        // ── PIN card border ───────────────────────────────────────────────────
private static SolidColorBrush PinBorderOn  => new((Color)ColorConverter.ConvertFromString("#4F46E5")!);
   private static SolidColorBrush PinBorderOff => new(Colors.Transparent);
        public SolidColorBrush PinCardBorderBrush     => PinEnabled ? PinBorderOn : PinBorderOff;
        public System.Windows.Thickness PinCardBorderThickness => PinEnabled ? new System.Windows.Thickness(2) : new System.Windows.Thickness(0);

 // ── Biometric card border ─────────────────────────────────────────────
        private static SolidColorBrush BioBorderOn  => new((Color)ColorConverter.ConvertFromString("#7C3AED")!);
   private static SolidColorBrush BioBorderOff => new(Colors.Transparent);
   public SolidColorBrush BiometricCardBorderBrush     => BiometricEnabled ? BioBorderOn : BioBorderOff;
        public System.Windows.Thickness BiometricCardBorderThickness => BiometricEnabled ? new System.Windows.Thickness(2) : new System.Windows.Thickness(0);

        // ── Biometric status ──────────────────────────────────────────────────
        [ObservableProperty] private bool _isBiometricBusy;
        [ObservableProperty] private string _biometricStatusText = "";

        // ── Status feedback ───────────────────────────────────────────────────
[ObservableProperty] private string _statusMessage = "";
  [ObservableProperty] private bool _isStatusError;

        private static readonly int[] AutoLockSeconds = [0, 60, 300, 900, 1800, 3600];

 // ── Commands ──────────────────────────────────────────────────────────
        public IRelayCommand<string> ChangeThemeCommand       { get; }
        public IAsyncRelayCommand    ToggleBiometricAsyncCommand { get; }

        public SettingsViewModel(IVaultSessionService session, IBiometricService biometric)
        {
        _session   = session;
       _biometric = biometric;
 ChangeThemeCommand     = new RelayCommand<string>(ChangeTheme);
            ToggleBiometricAsyncCommand = new AsyncRelayCommand(ToggleBiometricAsync);
   }

   public Task OnNavigatedToAsync()
        {
            var store = VaultSettingsStore.Instance;
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
   var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.0.0";

            PinEnabled       = store.IsPinUnlockEnabled;
      BiometricEnabled = store.IsBiometricUnlockEnabled;
       LockOnMinimise   = store.LockOnMinimise;

   var idx = Array.IndexOf(AutoLockSeconds, store.LockTimeoutSeconds);
            AutoLockIndex = idx >= 0 ? idx : 2;

            ClearClipboard = true;
            StatusMessage  = "";
   RefreshPinBorders();
   RefreshBiometricBorders();
 _ = CheckBiometricAsync();
return Task.CompletedTask;
   }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task CheckBiometricAsync()
  {
  var result = await _biometric.CheckAvailabilityAsync();
 IsBiometricAvailable = result == BiometricAvailability.Available;
        }

        // ── Theme ─────────────────────────────────────────────────────────────
        private void ChangeTheme(string? parameter)
{
       if (parameter == "theme_light")
            {
   if (CurrentTheme == ApplicationTheme.Light) return;
            ApplicationThemeManager.Apply(ApplicationTheme.Light);
         CurrentTheme = ApplicationTheme.Light;
    }
      else
            {
     if (CurrentTheme == ApplicationTheme.Dark) return;
      ApplicationThemeManager.Apply(ApplicationTheme.Dark);
         CurrentTheme = ApplicationTheme.Dark;
         }
    }

        // ── Auto-lock ─────────────────────────────────────────────────────────
        partial void OnAutoLockIndexChanged(int value)
        {
    if (value >= 0 && value < AutoLockSeconds.Length)
            {
        VaultSettingsStore.Instance.LockTimeoutSeconds = AutoLockSeconds[value];
        ShowStatus("Auto-lock timeout saved.");
        }
     }

// ── Lock on minimise ──────────────────────────────────────────────────
        partial void OnLockOnMinimiseChanged(bool value) =>
      VaultSettingsStore.Instance.LockOnMinimise = value;

        // ── PIN — called directly from code-behind ────────────────────────────
        /// <summary>Disable PIN unlock (called when card is clicked while PIN is on).</summary>
      public void DisablePin()
        {
     VaultSettingsStore.Instance.ClearPin();
 VaultSettingsStore.Instance.IsPinUnlockEnabled = false;
          PinEnabled = false;
            RefreshPinBorders();
            ShowStatus("PIN unlock disabled.");
        }

     /// <summary>Save PIN after the dialog confirmed it (called with the validated PIN string).</summary>
        public async Task SavePinAsync(string pin)
        {
            try
            {
          await _session.SetupPinAsync(pin);
    }
         catch (InvalidOperationException)
     {
                ShowStatus("Please unlock the vault before setting a PIN.", error: true);
  return;
            }
     VaultSettingsStore.Instance.IsPinUnlockEnabled = true;
      PinEnabled = true;
        RefreshPinBorders();
            ShowStatus("PIN unlock enabled.");
        }

        private void RefreshPinBorders()
        {
            OnPropertyChanged(nameof(PinCardBorderBrush));
          OnPropertyChanged(nameof(PinCardBorderThickness));
        }

 partial void OnPinEnabledChanged(bool value) => RefreshPinBorders();

        // ── Windows Hello ─────────────────────────────────────────────────────
        private async Task ToggleBiometricAsync()
     {
            if (BiometricEnabled)
            {
   VaultSettingsStore.Instance.IsBiometricUnlockEnabled = false;
      BiometricEnabled    = false;
          BiometricStatusText = "";
   RefreshBiometricBorders();
 ShowStatus("Windows Hello disabled.");
                return;
 }

        if (!IsBiometricAvailable)
            {
    ShowStatus("Windows Hello is not available on this device.", error: true);
        return;
            }

            IsBiometricBusy     = true;
            BiometricStatusText = "Waiting for Windows Hello...";
            try
            {
     var result = await _biometric.RequestVerificationAsync("Enable Windows Hello for Fortress");
      if (result == BiometricVerificationResult.Verified)
          {
      VaultSettingsStore.Instance.IsBiometricUnlockEnabled = true;
BiometricEnabled    = true;
    BiometricStatusText = "Windows Hello enabled!";
       RefreshBiometricBorders();
         ShowStatus("Windows Hello enabled.");
                }
         else if (result == BiometricVerificationResult.Canceled)
    {
        BiometricStatusText = "";
       }
            else
                {
   BiometricStatusText = "Verification failed — click to try again.";
     ShowStatus("Windows Hello verification failed.", error: true);
                }
            }
       finally { IsBiometricBusy = false; }
        }

  private void RefreshBiometricBorders()
  {
    OnPropertyChanged(nameof(BiometricCardBorderBrush));
  OnPropertyChanged(nameof(BiometricCardBorderThickness));
        }

    partial void OnBiometricEnabledChanged(bool value) => RefreshBiometricBorders();

        // ── Helpers ───────────────────────────────────────────────────────────
        private void ShowStatus(string message, bool error = false)
     {
     StatusMessage = message;
 IsStatusError = error;
        }
    }
}
