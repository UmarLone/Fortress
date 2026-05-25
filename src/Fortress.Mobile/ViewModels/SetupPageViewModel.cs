using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Views;
using Fortress.Mobile.Adapters;
using Maui.Biometric;
using Fortress.Extensions;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.ViewModels
{
    public class SetupPageViewModel : ViewModelBase
    {
        #region Properties

        // ── Platform ─────────────────────────────────────────────────────────
        public bool IsAndroid => DeviceInfo.Current.Platform == DevicePlatform.Android;
 public bool IsIOS   => DeviceInfo.Current.Platform == DevicePlatform.iOS;

        // ── Password step ─────────────────────────────────────────────────────
        private UserPassword userPassword = new UserPassword();
        public UserPassword UserPassword
        {
    get => userPassword;
        set => SetProperty(ref userPassword, value);
        }

        private bool canSetPassword;
        public bool CanSetPassword
        {
 get => canSetPassword;
            set
 {
          SetProperty(ref canSetPassword, value);
         if (CurrentStep == 0) CanGoNext = value;
      }
  }

        // Password strength (0–4) computed in code-behind, exposed for UI
        private int passwordStrength;
  public int PasswordStrength
      {
       get => passwordStrength;
            set
  {
                SetProperty(ref passwordStrength, value);
 RaisePropertyChanged(nameof(StrengthLabel));
      RaisePropertyChanged(nameof(StrengthColor));
        RaisePropertyChanged(nameof(StrengthWidth));
            }
        }
        public string StrengthLabel => PasswordStrength switch
        {
            0 => string.Empty,
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
        public double StrengthWidth => PasswordStrength * 25.0; // 0–100 %

        private bool isCreating;
 public bool IsCreating
        {
            get => isCreating;
            set => SetProperty(ref isCreating, value);
        }

        private int currentStep;
        public int CurrentStep
        {
get => currentStep;
            set { SetProperty(ref currentStep, value); UpdateStepVisibility(); }
        }

        // ── Step visibility ───────────────────────────────────────────────────
        private bool isPasswordStepVisible = true;
  public bool IsPasswordStepVisible { get => isPasswordStepVisible; set => SetProperty(ref isPasswordStepVisible, value); }

        private bool isLockStepVisible;
     public bool IsLockStepVisible { get => isLockStepVisible; set => SetProperty(ref isLockStepVisible, value); }

        private bool isAutofillStepVisible;
public bool IsAutofillStepVisible { get => isAutofillStepVisible; set => SetProperty(ref isAutofillStepVisible, value); }

        private bool isCloudSyncStepVisible;
        public bool IsCloudSyncStepVisible { get => isCloudSyncStepVisible; set => SetProperty(ref isCloudSyncStepVisible, value); }

        // ── Navigation ────────────────────────────────────────────────────────
     private string nextButtonText = "Continue";
        public string NextButtonText { get => nextButtonText; set => SetProperty(ref nextButtonText, value); }

        private bool canGoNext = true;
        public bool CanGoNext { get => canGoNext; set => SetProperty(ref canGoNext, value); }

private bool showSkipButton;
        public bool ShowSkipButton { get => showSkipButton; set => SetProperty(ref showSkipButton, value); }

        // ── Lock step ─────────────────────────────────────────────────────────
        private bool isBiometricsAvailable;
        public bool IsBiometricsAvailable { get => isBiometricsAvailable; set => SetProperty(ref isBiometricsAvailable, value); }

    private bool setupBiometric;
  public bool SetupBiometric
    {
            get => setupBiometric;
            set
            {
      SetProperty(ref setupBiometric, value);
           if (value) SetupPin = false;
            }
   }

    private bool setupPin;
        public bool SetupPin
        {
            get => setupPin;
       set
         {
          SetProperty(ref setupPin, value);
    if (value) SetupBiometric = false;
       RaisePropertyChanged(nameof(IsPinEntryVisible));
        // Reset PIN state when deselected
     if (!value) ResetPinState();
  }
        }

   // ── Inline PIN ────────────────────────────────────────────────────────
        public bool IsPinEntryVisible => SetupPin;

        private string pinEntry = string.Empty;
        public string PinEntry
    {
   get => pinEntry;
            set
   {
    SetProperty(ref pinEntry, value);
                RaisePropertyChanged(nameof(PinDot1));
        RaisePropertyChanged(nameof(PinDot2));
                RaisePropertyChanged(nameof(PinDot3));
           RaisePropertyChanged(nameof(PinDot4));
 }
        }

        private string pinConfirm = string.Empty;
        public string PinConfirm
        {
            get => pinConfirm;
   set
            {
     SetProperty(ref pinConfirm, value);
     RaisePropertyChanged(nameof(ConfirmDot1));
      RaisePropertyChanged(nameof(ConfirmDot2));
 RaisePropertyChanged(nameof(ConfirmDot3));
       RaisePropertyChanged(nameof(ConfirmDot4));
    }
        }

  private bool isConfirmingPin;
        public bool IsConfirmingPin { get => isConfirmingPin; set => SetProperty(ref isConfirmingPin, value); }

   public bool PinDot1 => PinEntry.Length >= 1;
        public bool PinDot2 => PinEntry.Length >= 2;
      public bool PinDot3 => PinEntry.Length >= 3;
     public bool PinDot4 => PinEntry.Length >= 4;
      public bool ConfirmDot1 => PinConfirm.Length >= 1;
        public bool ConfirmDot2 => PinConfirm.Length >= 2;
        public bool ConfirmDot3 => PinConfirm.Length >= 3;
        public bool ConfirmDot4 => PinConfirm.Length >= 4;

        private string pinErrorMessage = string.Empty;
     public string PinErrorMessage { get => pinErrorMessage; set => SetProperty(ref pinErrorMessage, value); }

     private bool hasPinError;
        public bool HasPinError { get => hasPinError; set => SetProperty(ref hasPinError, value); }

        private bool isPinSaved;
     public bool IsPinSaved { get => isPinSaved; set => SetProperty(ref isPinSaved, value); }

     // ── Autofill step ─────────────────────────────────────────────────────
        private bool isAutofillEnabled;
        public bool IsAutofillEnabled
  {
     get => isAutofillEnabled;
   set
         {
                SetProperty(ref isAutofillEnabled, value);
                RaisePropertyChanged(nameof(IsAutofillNotEnabled));
            }
        }
        public bool IsAutofillNotEnabled => !IsAutofillEnabled;

        // ── Cloud sync step ───────────────────────────────────────────────────
        private bool setupCloudSync;
        public bool SetupCloudSync { get => setupCloudSync; set => SetProperty(ref setupCloudSync, value); }

     // Selected provider: "googledrive" | "icloud" | "dropbox" | "onedrive" | ""
   private string selectedCloudProvider = string.Empty;
  public string SelectedCloudProvider
    {
            get => selectedCloudProvider;
set
 {
         SetProperty(ref selectedCloudProvider, value);
   RaisePropertyChanged(nameof(IsGoogleDriveSelected));
    RaisePropertyChanged(nameof(IsICloudSelected));
  RaisePropertyChanged(nameof(IsDropboxSelected));
                RaisePropertyChanged(nameof(IsOneDriveSelected));
   RaisePropertyChanged(nameof(IsProviderSelected));
      RaisePropertyChanged(nameof(IsProviderNotSelected));
     }
        }
    public bool IsGoogleDriveSelected => SelectedCloudProvider == "googledrive";
  public bool IsICloudSelected      => SelectedCloudProvider == "icloud";
     public bool IsDropboxSelected     => SelectedCloudProvider == "dropbox";
   public bool IsOneDriveSelected    => SelectedCloudProvider == "onedrive";
        public bool IsProviderSelected => !string.IsNullOrEmpty(SelectedCloudProvider);
   public bool IsProviderNotSelected => string.IsNullOrEmpty(SelectedCloudProvider);

        private bool isCloudConnecting;
        public bool IsCloudConnecting { get => isCloudConnecting; set => SetProperty(ref isCloudConnecting, value); }

        private bool isCloudConnected;
  public bool IsCloudConnected
        {
         get => isCloudConnected;
   set
            {
    SetProperty(ref isCloudConnected, value);
  RaisePropertyChanged(nameof(IsCloudNotConnected));
    }
        }
        public bool IsCloudNotConnected => !IsCloudConnected;

        private string cloudConnectedEmail = string.Empty;
        public string CloudConnectedEmail { get => cloudConnectedEmail; set => SetProperty(ref cloudConnectedEmail, value); }

        private string cloudConnectedProviderName = string.Empty;
        public string CloudConnectedProviderName { get => cloudConnectedProviderName; set => SetProperty(ref cloudConnectedProviderName, value); }

        #endregion

        private readonly IDataStorageService _dataStorageService;
        private readonly IDeviceServices _deviceService;
        private readonly IBottomSheetService _bottomSheetService;
 private readonly GoogleDriveSyncService _googleDrive;
   private readonly DropboxSyncService _dropbox;
        private readonly OneDriveSyncService _oneDrive;

    // Autofill polling (Android only — iOS cannot detect programmatically)
        private System.Timers.Timer? _autofillPollTimer;

        public SetupPageViewModel(
   INavigationService navigationService,
      IDataStorageService dataStorageService,
      IDeviceServices deviceService,
  IBottomSheetService bottomSheetService,
   GoogleDriveSyncService googleDrive,
   DropboxSyncService dropbox,
      OneDriveSyncService oneDrive) : base(navigationService)
        {
          _dataStorageService = dataStorageService;
            _deviceService = deviceService;
 _bottomSheetService = bottomSheetService;
 _googleDrive = googleDrive;
      _dropbox = dropbox;
         _oneDrive = oneDrive;
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
{
            PreferenceWrapper.Instance.PreventLocking = true;
            CurrentStep = 0;
    IsBiometricsAvailable = await BiometricAuthentication.Current.IsAvailableAsync();
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
      {
      StopAutofillPolling();
     PreferenceWrapper.Instance.PreventLocking = false;
        }

        private void UpdateStepVisibility()
        {
            IsPasswordStepVisible  = CurrentStep == 0;
            IsLockStepVisible      = CurrentStep == 1;
       IsAutofillStepVisible  = CurrentStep == 2;
   IsCloudSyncStepVisible = CurrentStep == 3;

     ShowSkipButton = CurrentStep > 0;
       NextButtonText = CurrentStep == 3 ? "Finish" : "Continue";
        CanGoNext = CurrentStep == 0 ? CanSetPassword : true;

        // Start/stop autofill polling when entering/leaving step 2
      if (CurrentStep == 2 && IsAndroid)
           StartAutofillPolling();
          else
            StopAutofillPolling();
        }

    // ── Autofill polling (Android) ────────────────────────────────────────
        private void StartAutofillPolling()
      {
       StopAutofillPolling();
 RefreshAutofillStatus();
            _autofillPollTimer = new System.Timers.Timer(2000) { AutoReset = true };
            _autofillPollTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshAutofillStatus);
            _autofillPollTimer.Start();
  }

        private void StopAutofillPolling()
        {
            _autofillPollTimer?.Stop();
   _autofillPollTimer?.Dispose();
            _autofillPollTimer = null;
  }

 public void RefreshAutofillStatus()
        {
  // On iOS the system doesn't expose a programmatic check — we leave it as-is
            if (IsAndroid)
           IsAutofillEnabled = _deviceService.AutofillServiceEnabled(out _);
      }

        // ── PIN numpad ────────────────────────────────────────────────────────
        private DelegateCommand<string>? _pinKeyCommand;
        public DelegateCommand<string> PinKeyCommand =>
        _pinKeyCommand ??= new DelegateCommand<string>(OnPinKey);

        private void OnPinKey(string key)
        {
            HasPinError = false;
            PinErrorMessage = string.Empty;

         if (key == "⌫")
            {
            if (!IsConfirmingPin && PinEntry.Length > 0)
            PinEntry = PinEntry[..^1];
     else if (IsConfirmingPin && PinConfirm.Length > 0)
      PinConfirm = PinConfirm[..^1];
      return;
       }

            if (!IsConfirmingPin)
            {
         if (PinEntry.Length < 4)
      {
         PinEntry += key;
                 if (PinEntry.Length == 4)
             IsConfirmingPin = true;
     }
   }
 else
            {
     if (PinConfirm.Length < 4)
  {
  PinConfirm += key;
       if (PinConfirm.Length == 4)
   ValidatePin();
        }
            }
        }

      private void ValidatePin()
        {
      if (PinEntry == PinConfirm)
          {
   SavePinHash(PinEntry);
      IsPinSaved = true;
          }
 else
            {
        HasPinError = true;
          PinErrorMessage = "PINs don't match — please try again";
  ResetPinState();
    }
        }

        // Called from code-behind when using the system-keyboard Entry
        public void ValidatePinFromView() => ValidatePin();

        private void ResetPinState()
        {
            PinEntry = string.Empty;
         PinConfirm = string.Empty;
            IsConfirmingPin = false;
          IsPinSaved = false;
          HasPinError = false;
  PinErrorMessage = string.Empty;
 }

        private void SavePinHash(string pin)
        {
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(pin));
            PreferenceWrapper.Instance.PinUnlockHash = Convert.ToBase64String(hashBytes);
            PreferenceWrapper.Instance.IsPinUnlockEnabled = true;
         PreferenceWrapper.Instance.IsBiometricUnlockEnabled = false;
        }

        // ── Cloud provider selection ──────────────────────────────────────────
        private DelegateCommand<string>? _selectProviderCommand;
        public DelegateCommand<string> SelectProviderCommand =>
            _selectProviderCommand ??= new DelegateCommand<string>(key =>
       {
                // Toggle off if tapped again
           SelectedCloudProvider = SelectedCloudProvider == key ? string.Empty : key;
      });

        // ── Cloud connect ─────────────────────────────────────────────────────
        private AsyncCommand? _connectCloudCommand;
   public ICommand ConnectCloudCommand =>
_connectCloudCommand ??= new AsyncCommand(ExecuteConnectCloudAsync);

        private async Task ExecuteConnectCloudAsync()
        {
   if (!IsProviderSelected) return;
            IsCloudConnecting = true;
       try
   {
  if (SelectedCloudProvider == "googledrive")
          {
var success = await _googleDrive.AuthenticateAsync();
if (success)
    {
  var info = await _googleDrive.GetUserInfoAsync();
    CloudConnectedEmail        = info?.Email ?? string.Empty;
      CloudConnectedProviderName = "Google Drive";
  IsCloudConnected = true; SetupCloudSync = true;
    PreferenceWrapper.Instance.IsCloudSyncEnabled  = true;
PreferenceWrapper.Instance.CloudSyncProvider   = "GoogleDrive";
   }
   }
         else if (SelectedCloudProvider == "dropbox")
   {
    var success = await _dropbox.AuthenticateAsync();
   if (success)
     {
     var info = await _dropbox.GetUserInfoAsync();
 CloudConnectedEmail      = info?.Email ?? string.Empty;
     CloudConnectedProviderName = "Dropbox";
        IsCloudConnected = true; SetupCloudSync = true;
           PreferenceWrapper.Instance.IsCloudSyncEnabled  = true;
    PreferenceWrapper.Instance.CloudSyncProvider   = "Dropbox";
          }
            }
            else if (SelectedCloudProvider == "onedrive")
            {
var success = await _oneDrive.AuthenticateAsync();
       if (success)
  {
         var info = await _oneDrive.GetUserInfoAsync();
CloudConnectedEmail        = info?.Email ?? string.Empty;
  CloudConnectedProviderName = "OneDrive";
      IsCloudConnected = true; SetupCloudSync = true;
          PreferenceWrapper.Instance.IsCloudSyncEnabled  = true;
        PreferenceWrapper.Instance.CloudSyncProvider   = "OneDrive";
          }
 }
            else if (SelectedCloudProvider == "icloud")
  {
     CloudConnectedProviderName = "iCloud";
     CloudConnectedEmail        = "iCloud account";
  IsCloudConnected = true; SetupCloudSync = true;
  PreferenceWrapper.Instance.IsCloudSyncEnabled  = true;
     PreferenceWrapper.Instance.CloudSyncProvider   = "iCloud";
   }
        }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Cloud connect error: {ex.Message}"); }
       finally { IsCloudConnecting = false; }
        }

     // ── Cloud disconnect ──────────────────────────────────────────────────
        private AsyncCommand? _disconnectCloudCommand;
        public ICommand DisconnectCloudCommand =>
      _disconnectCloudCommand ??= new AsyncCommand(ExecuteDisconnectCloudAsync);

        private async Task ExecuteDisconnectCloudAsync()
   {
     if (SelectedCloudProvider == "googledrive") await _googleDrive.SignOutAsync();
       else if (SelectedCloudProvider == "dropbox")  await _dropbox.SignOutAsync();
       else if (SelectedCloudProvider == "onedrive") await _oneDrive.SignOutAsync();
  IsCloudConnected = false;
     CloudConnectedEmail = CloudConnectedProviderName = string.Empty;
SetupCloudSync = false;
          SelectedCloudProvider = string.Empty;
     PreferenceWrapper.Instance.IsCloudSyncEnabled = false;
     PreferenceWrapper.Instance.CloudSyncProvider  = string.Empty;
        }

        // ── Autofill settings ─────────────────────────────────────────────────
        private AsyncCommand? _openAutofillSettingsCommand;
        public ICommand OpenAutofillSettingsCommand =>
     _openAutofillSettingsCommand ??= new AsyncCommand(ExecuteOpenAutofillSettingsAsync);

        private async Task ExecuteOpenAutofillSettingsAsync()
        {
    _deviceService.OpenAutofillSettings();
            if (IsAndroid)
           await Task.Delay(1500);
        }

        // ── Flow ──────────────────────────────────────────────────────────────
        private async Task ExecuteNextAsync()
     {
   if (CurrentStep == 0)
            {
       await SaveMasterPassword();
            CurrentStep = 1;
}
      else if (CurrentStep == 1)
{
      var ok = await ApplyLockStep();
   if (!ok) return;
    CurrentStep = 2;
         }
        else if (CurrentStep == 2)
        {
    StopAutofillPolling();
    CurrentStep = 3;
        }
   else if (CurrentStep == 3)
     {
       await FinishSetup();
      }
      }

        private async Task ExecuteSkipAsync()
        {
   if (CurrentStep == 2) StopAutofillPolling();
     if (CurrentStep < 3) CurrentStep++;
     else await FinishSetup();
        }

        private Task SaveMasterPassword()
        {
         PreferenceWrapper.Instance.DatabasePassword = UserPassword.Password;
         return Task.CompletedTask;
  }

    private async Task<bool> ApplyLockStep()
        {
            if (SetupBiometric && IsBiometricsAvailable)
            {
 var result = await BiometricAuthentication.Current.AuthenticateAsync(
   new AuthenticationRequest("FORTRESS", "Enable biometric unlock"));

                if (result.IsSuccessful)
                {
          PreferenceWrapper.Instance.IsBiometricUnlockEnabled = true;
    PreferenceWrapper.Instance.IsPinUnlockEnabled = false;
     }
   else
          {
     return false;
      }
     }
      else if (SetupPin && !IsPinSaved)
            {
            // PIN was selected but not completed
          return false;
       }
    return true;
        }

        private async Task FinishSetup()
        {
    IsCreating = true;
        try
            {
             PreferenceWrapper.Instance.HasSetupCompleted = true;
              PreferenceWrapper.Instance.FirstLaunch = false;
         PreferenceWrapper.Instance.PreventLocking = false;
            await Task.Delay(300);
         await NavigationService.NavigateAsync($"/{nameof(NavigationPage)}/{nameof(HomePage)}");
     }
            catch (Exception ex)
   {
                System.Diagnostics.Debug.WriteLine($"FinishSetup error: {ex.Message}");
            }
finally
{
      IsCreating = false;
   }
   }

        #region Commands

        private AsyncCommand? _nextCommand;
  public ICommand NextCommand =>
          _nextCommand ??= new AsyncCommand(ExecuteNextAsync);

        private AsyncCommand? _skipCommand;
 public ICommand SkipCommand =>
          _skipCommand ??= new AsyncCommand(ExecuteSkipAsync);

   private AsyncCommand? _setPasswordCommand;
        public ICommand SetPasswordCommand =>
       _setPasswordCommand ??= new AsyncCommand(ExecuteNextAsync);

      #endregion
    }
}
