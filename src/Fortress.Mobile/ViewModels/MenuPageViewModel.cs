using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Models;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views;
using Fortress.Views.PopupPages;
using MauiIcons.Material;
using Maui.Biometric;
using MauiIcons.Core;

namespace Fortress.ViewModels
{
    public class MenuPageViewModel : ViewModelBase
    {
        #region Properties

        private bool _isInitialized;
        public bool IsInitialized
        {
            get => _isInitialized;
            set => SetProperty(ref _isInitialized, value);
        }

        private TimeoutOption _selectedClipboardTimeout;
        public TimeoutOption SelectedClipboardTimeout
        {
            get => _selectedClipboardTimeout;
            set => SetProperty(ref _selectedClipboardTimeout, value);
        }

        private TimeoutOption _selectedAppLockTimeout;
        public TimeoutOption SelectedAppLockTimeout
        {
            get => _selectedAppLockTimeout;
            set => SetProperty(ref _selectedAppLockTimeout, value);
        }

        private bool _checkAutofill;
        public bool CheckAutofill
        {
            get => _checkAutofill;
            set => SetProperty(ref _checkAutofill, value);
        }

        private bool _isAutofillServiceEnabled;
        public bool IsAutofillServiceEnabled
        {
            get => _isAutofillServiceEnabled;
            set => SetProperty(ref _isAutofillServiceEnabled, value);
        }

        private bool _isUseInlineEnabled;
        public bool IsUseInlineEnabled
        {
            get => _isUseInlineEnabled;
            set => SetProperty(ref _isUseInlineEnabled, value);
        }

        private bool _isUseAccessibilityEnabled;
        public bool IsUseAccessibilityEnabled
        {
            get => _isUseAccessibilityEnabled;
            set => SetProperty(ref _isUseAccessibilityEnabled, value);
        }

        private bool _isUseDrawOverEnabled;
        public bool IsUseDrawOverEnabled
        {
            get => _isUseDrawOverEnabled;
            set => SetProperty(ref _isUseDrawOverEnabled, value);
        }

        private bool _isBiometricsAvailable;
        public bool IsBiometricsAvailable
        {
            get => _isBiometricsAvailable;
            set => SetProperty(ref _isBiometricsAvailable, value);
        }

        private bool _isBiometricUnlockEnabled;
        public bool IsBiometricUnlockEnabled
        {
            get => _isBiometricUnlockEnabled;
            set
            {
                SetProperty(ref _isBiometricUnlockEnabled, value);
                RaisePropertyChanged(nameof(IsLockFeatureEnabled));
            }
        }

        private bool _isPinUnlockEnabled;
        public bool IsPinUnlockEnabled
        {
            get => _isPinUnlockEnabled;
            set
            {
                SetProperty(ref _isPinUnlockEnabled, value);
                RaisePropertyChanged(nameof(IsLockFeatureEnabled));
            }
        }

        private bool _isPasskeyProviderEnabled;
        public bool IsPasskeyProviderEnabled
        {
            get => _isPasskeyProviderEnabled;
            set => SetProperty(ref _isPasskeyProviderEnabled, value);
        }

        /// <summary>
        /// True only on Android 14+ (API 34) where CredentialProviderService is supported.
        /// Hides the Passkey Provider toggle on older devices instead of showing a useless setting.
        /// </summary>
        public bool IsPasskeyProviderSupported
        {
            get
            {
#if ANDROID
                return (int)Android.OS.Build.VERSION.SdkInt >= 34;
#else
                return false;
#endif
            }
        }

        public bool IsLockFeatureEnabled => IsBiometricUnlockEnabled || IsPinUnlockEnabled;

        /// <summary>
        /// True only on Android 14+ (API 34). Controls visibility of all
        /// passkey-related UI — the entire PASSKEYS section only exists on
        /// devices where the CredentialProviderService API is available.
        /// </summary>
        public bool IsPasskeySupported
        {
            get
            {
#if ANDROID
                return (int)Android.OS.Build.VERSION.SdkInt >= 34;
#else
                return false;
#endif
            }
        }

        private bool _isScreenCaptureEnabled;
        public bool IsScreenCaptureEnabled
        {
            get => _isScreenCaptureEnabled;
            set => SetProperty(ref _isScreenCaptureEnabled, value);
        }

        private bool _isSavePromptDisabled;
        public bool IsSavePromptDisabled
        {
            get => _isSavePromptDisabled;
            set => SetProperty(ref _isSavePromptDisabled, value);
        }

        private bool _isLockTimeoutEnabled;
        public bool IsLockTimeoutEnabled
        {
            get => _isLockTimeoutEnabled;
            set => SetProperty(ref _isLockTimeoutEnabled, value);
        }

        private bool _isCopyTOTPOnAutofill;
        public bool IsCopyTOTPOnAutofill
        {
            get => _isCopyTOTPOnAutofill;
            set => SetProperty(ref _isCopyTOTPOnAutofill, value);
        }

        private bool _sendDiagnosticLogs;
        public bool SendDiagnosticLogs
        {
            get => _sendDiagnosticLogs;
            set => SetProperty(ref _sendDiagnosticLogs, value);
        }

        private bool _isDarkModeEnabled;
        public bool IsDarkModeEnabled
        {
            get => _isDarkModeEnabled;
            set => SetProperty(ref _isDarkModeEnabled, value);
        }

        private string _phoneName;
        public string PhoneName
        {
            get => _phoneName;
            set => SetProperty(ref _phoneName, value);
        }

        private MatchThresholdOption _selectedMatchThreshold;
        public MatchThresholdOption SelectedMatchThreshold
        {
            get => _selectedMatchThreshold;
            set => SetProperty(ref _selectedMatchThreshold, value);
        }

        private bool _isLockOnBackgroundEnabled;
        public bool IsLockOnBackgroundEnabled
        {
            get => _isLockOnBackgroundEnabled;
            set => SetProperty(ref _isLockOnBackgroundEnabled, value);
        }

        private bool _isFollowSystemThemeEnabled;
        public bool IsFollowSystemThemeEnabled
        {
            get => _isFollowSystemThemeEnabled;
            set => SetProperty(ref _isFollowSystemThemeEnabled, value);
        }

        private bool _requireAuthForPasswordFill;
        public bool RequireAuthForPasswordFill
        {
            get => _requireAuthForPasswordFill;
            set => SetProperty(ref _requireAuthForPasswordFill, value);
        }

        private bool _requireAuthForCardFill;
        public bool RequireAuthForCardFill
        {
            get => _requireAuthForCardFill;
            set => SetProperty(ref _requireAuthForCardFill, value);
        }

        private FailedAttemptsOption _selectedMaxFailedAttempts;
        public FailedAttemptsOption SelectedMaxFailedAttempts
        {
            get => _selectedMaxFailedAttempts;
            set => SetProperty(ref _selectedMaxFailedAttempts, value);
        }

        private int _blockedSitesCount;
        public int BlockedSitesCount
        {
            get => _blockedSitesCount;
            set
            {
                SetProperty(ref _blockedSitesCount, value);
                RaisePropertyChanged(nameof(BlockedSitesSubtitle));
                // Refresh the DynNav "Blocked Sites" row subtitle live
                if (SettingItems.Count > 0) RefreshDynNavItems();
            }
        }

        public string BlockedSitesSubtitle =>
            BlockedSitesCount == 0
    ? "No sites blocked"
      : $"{BlockedSitesCount} site{(BlockedSitesCount == 1 ? "" : "s")} blocked";

        /// <summary>Flat list that drives the settings CollectionView.</summary>
        private ObservableCollection<SettingItem> _settingItems = new();
    public ObservableCollection<SettingItem> SettingItems
        {
            get => _settingItems;
            private set => SetProperty(ref _settingItems, value);
        }

        /// <summary>Master unfiltered list — SettingItems is rebuilt from this on visibility changes.</summary>
        private List<SettingItem> _allSettingItems = new();

        #endregion

        #region Fields

        private readonly IDeviceServices _deviceInfo;
        private readonly IUserDialogs _dialogService;
        private readonly IDataStorageService _dataStorageService;
        private readonly IEventLogProcessor _eventLogProcessor;
        private readonly ILogger<MenuPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;
        private readonly ISharedCredentialWriter? _sharedCredentialWriter;
        private readonly IEventAggregator _eventAggregator;
        private SubscriptionToken? _autofillStatusSubscription;

        private static readonly List<TimeoutOption> AppLockTimeoutOptions = new()
        {
             new TimeoutOption { Key = 60,  Value = "1 minute"  },
                new TimeoutOption { Key = 300, Value = "5 minutes" },
              new TimeoutOption { Key = 900, Value = "15 minutes" },
        };

        private static readonly List<TimeoutOption> ClipboardTimeoutOptions = new()
        {
              new TimeoutOption { Key = 0,   Value = "Never"     },
                    new TimeoutOption { Key = 60,  Value = "1 minute"  },
         new TimeoutOption { Key = 120, Value = "2 minutes" },
             new TimeoutOption { Key = 300, Value = "5 minutes" },
        };

        private static readonly List<MatchThresholdOption> MatchThresholdOptions = new()
        {
         new MatchThresholdOption { Key = 50, Value = "Broad Matching"    },
         new MatchThresholdOption { Key = 70, Value = "Standard Matching" },
                    new MatchThresholdOption { Key = 90, Value = "Strict Matching"   },
     };

        private static readonly List<FailedAttemptsOption> FailedAttemptsOptions = new()
      {
             new FailedAttemptsOption { Key = 3,  Value = "3 attempts"  },
                        new FailedAttemptsOption { Key = 5,  Value = "5 attempts"  },
                        new FailedAttemptsOption { Key = 10, Value = "10 attempts" },
        };

        #endregion

        public MenuPageViewModel(
             INavigationService navigationService,
               IDeviceServices deviceInfo,
          IUserDialogs dialogService,
       IDataStorageService dataStorageService,
        IEventLogProcessor eventLogProcessor,
         ILogger<MenuPageViewModel> logger,
           IBottomSheetService bottomSheetService,
                  IEventAggregator eventAggregator,
                  ISharedCredentialWriter? sharedCredentialWriter = null)
                  : base(navigationService)
        {
            _deviceInfo = deviceInfo;
            _dialogService = dialogService;
            _dataStorageService = dataStorageService;
            _eventLogProcessor = eventLogProcessor;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
            _sharedCredentialWriter = sharedCredentialWriter;
            _eventAggregator = eventAggregator;

            _autofillStatusSubscription = _eventAggregator
                  .GetEvent<AutofillStatusChangedEvent>()
                   .Subscribe(OnAutofillStatusChanged);
        }

        private void OnAutofillStatusChanged(bool isEnabled)
        {
            _logger.LogInformation("Autofill status changed: {IsEnabled}", isEnabled);
            IsAutofillServiceEnabled = isEnabled;
            if (isEnabled)
                _deviceInfo.Toast("Fortress AutoFill enabled");
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            CheckAutofill = false;
            base.OnNavigatedFrom(parameters);
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            IsInitialized = false;

            IsScreenCaptureEnabled = PreferenceWrapper.Instance.IsScreenCaptureEnabled;
            IsBiometricUnlockEnabled = PreferenceWrapper.Instance.IsBiometricUnlockEnabled;
            IsPinUnlockEnabled = PreferenceWrapper.Instance.IsPinUnlockEnabled;
            IsAutofillServiceEnabled = _deviceInfo.AutofillServiceEnabled(out _);
            IsUseInlineEnabled = PreferenceWrapper.Instance.IsUseInlineAutofillEnabled;
            IsUseAccessibilityEnabled = _deviceInfo.AutofillAccessibilityServiceRunning();
            IsCopyTOTPOnAutofill = PreferenceWrapper.Instance.IsCopyTOTPOnAutofill;
            IsBiometricsAvailable = await BiometricAuthentication.Current.IsAvailableAsync();
            SendDiagnosticLogs = PreferenceWrapper.Instance.SendDiagnosticLogs;
            IsDarkModeEnabled = PreferenceWrapper.Instance.AppTheme == "Dark";
            IsSavePromptDisabled = PreferenceWrapper.Instance.IsSavePromptDisabled;
            PhoneName = DeviceInfo.Name;
            IsLockOnBackgroundEnabled = PreferenceWrapper.Instance.LockOnBackground;
            IsFollowSystemThemeEnabled = PreferenceWrapper.Instance.FollowSystemTheme;
            RequireAuthForPasswordFill = PreferenceWrapper.Instance.RequireAuthForPasswordFill;
            RequireAuthForCardFill = PreferenceWrapper.Instance.RequireAuthForCardFill;

            SelectedAppLockTimeout = AppLockTimeoutOptions
                   .FirstOrDefault(x => x.Key == PreferenceWrapper.Instance.LockTimeout)
         ?? AppLockTimeoutOptions[0];
            SelectedClipboardTimeout = ClipboardTimeoutOptions
.FirstOrDefault(x => x.Key == PreferenceWrapper.Instance.ClearClipboardTimeout)
              ?? ClipboardTimeoutOptions[0];
            SelectedMatchThreshold = MatchThresholdOptions
               .FirstOrDefault(x => x.Key == PreferenceWrapper.Instance.MatchThreshold)
               ?? MatchThresholdOptions[1];
            SelectedMaxFailedAttempts = FailedAttemptsOptions
                .FirstOrDefault(x => x.Key == PreferenceWrapper.Instance.MaxFailedUnlockAttempts)
 ?? FailedAttemptsOptions[1];

            BlockedSitesCount = PreferenceWrapper.Instance.AutofillBlockedUris.Count;
            IsPasskeyProviderEnabled = PreferenceWrapper.Instance.IsPasskeyProviderEnabled;

            BuildSettingItems();

            IsInitialized = true;
        }

        // ── Build flat settings list ──────────────────────────────────────────────

        private void BuildSettingItems()
        {
            var items = new List<SettingItem>();

            // Capture as delegates so lambdas inside local functions can call them
            Func<string, Task> doSetting = ExecuteSettingChangedAsync;
            Func<string, Task> doNavigate = ExecuteNavigateAsync;
            Func<Task> doLock = ExecuteLockNowAsync;
            Func<Task> doRemove = ExecuteRemoveAccountAsync;
            Action<string, bool> doSync = SyncToggleToVmProperty;

            void Section(string title, bool visible = true)
                   => items.Add(new SettingItem { Kind = SettingItemKind.SectionHeader, SectionTitle = title, IsVisible = visible });

            void Spacer()
                    => items.Add(new SettingItem { Kind = SettingItemKind.Spacer });

            SettingItem Toggle(
     string title, string subtitle, string glyph,
            string iconBg, string iconFg, string switchId,
       bool isToggled, Func<bool>? visibility = null, Func<bool>? valueReader = null)
            {
                var item = new SettingItem
                {
                    Kind = SettingItemKind.Toggle,
                    Title = title,
                    Subtitle = subtitle,
                    IconGlyph = glyph,
                    IconBg = iconBg,
                    IconFg = iconFg,
                    SwitchClassId = switchId,
                    IsToggled = isToggled,
                    VisibilityCondition = visibility,
                    IsVisible = visibility?.Invoke() ?? true,
                    ToggledValueReader = valueReader,
                };
                var id = switchId;
                item.TapCommand = new DelegateCommand(async () =>
         {
             item.IsToggled = !item.IsToggled;
             doSync(id, item.IsToggled);
             await doSetting(id);
             // Pull authoritative state back — async op may have reverted it
             item.RefreshToggle();
         });
                return item;
            }

            SettingItem Nav(
          string title, string subtitle, string glyph,
                string iconBg, string iconFg, string commandParam,
                   bool isNavigate = false, Func<bool>? visibility = null)
            {
                var item = new SettingItem
                {
                    Kind = SettingItemKind.Nav,
                    Title = title,
                    Subtitle = subtitle,
                    IconGlyph = glyph,
                    IconBg = iconBg,
                    IconFg = iconFg,
                    VisibilityCondition = visibility,
                    IsVisible = visibility?.Invoke() ?? true,
                };
                var p = commandParam;
                item.TapCommand = isNavigate
                    ? new DelegateCommand(async () => await doNavigate(p))
                       : new DelegateCommand(async () => await doSetting(p));
                return item;
            }

            SettingItem DynNav(
         string title, Func<string> dynamicSubtitle, string glyph,
              string iconBg, string iconFg, string commandParam,
         Func<bool>? visibility = null)
            {
                var item = new SettingItem
                {
                    Kind = SettingItemKind.Nav,
                    Title = title,
                    DynamicSubtitle = dynamicSubtitle(),
                    IconGlyph = glyph,
                    IconBg = iconBg,
                    IconFg = iconFg,
                    SubtitleReader = dynamicSubtitle,
                    VisibilityCondition = visibility,
                    IsVisible = visibility?.Invoke() ?? true,
                };
                var p = commandParam;
                item.TapCommand = new DelegateCommand(async () =>
          {
              await doSetting(p);
              item.RefreshSubtitle();
          });
                return item;
            }

            // ════════════════ AUTO-FILL ════════════════
            Section("AUTO-FILL");
            items.Add(Toggle("Auto-Fill Service", "Enable system-wide auto-fill", "\xe32a", "#E0E7FF", "#407CCA", "AutofillService", IsAutofillServiceEnabled, valueReader: () => IsAutofillServiceEnabled));
            items.Add(Toggle("Inline Auto-Fill", "Show suggestions in the keyboard bar", "\xe94d", "#FEF3C7", "#F59E0B", "InlineAutofill", IsUseInlineEnabled, () => DeviceInfo.Platform == DevicePlatform.Android, () => IsUseInlineEnabled));
            items.Add(Toggle("Accessibility Autofill", "Notify to fill in apps that block the standard service", "\xe8f4", "#FEF3C7", "#D97706", "AccessibilityAutofill", IsUseAccessibilityEnabled, () => DeviceInfo.Platform == DevicePlatform.Android, () => IsUseAccessibilityEnabled));
            items.Add(Toggle("Disable Save Prompt", "Don't ask to save new passwords", "\xe161", "#DCFCE7", "#22C55E", "SavePromptDisabled", IsSavePromptDisabled, valueReader: () => IsSavePromptDisabled));
            items.Add(Toggle("Copy OTP on Auto-Fill", "Auto-copy TOTP code after filling", "\xe14d", "#E0E7FF", "#407CCA", "CopyTOTPOnAutofill", IsCopyTOTPOnAutofill, valueReader: () => IsCopyTOTPOnAutofill));
            items.Add(Toggle("Verify Before Password Fill", "Require biometric/PIN before filling passwords", "\xe90d", "#FCE7F3", "#EC4899", "RequireAuthForPasswordFill", RequireAuthForPasswordFill, () => IsLockFeatureEnabled, () => RequireAuthForPasswordFill));
            items.Add(Toggle("Verify Before Card Fill", "Require biometric/PIN before filling cards", "\xe90d", "#FEF3C7", "#F59E0B", "RequireAuthForCardFill", RequireAuthForCardFill, () => IsLockFeatureEnabled, () => RequireAuthForCardFill));
            items.Add(DynNav("Auto-Fill Matching", () => SelectedMatchThreshold?.Value ?? "Standard Matching", "\xe421", "#F3E8FF", "#A855F7", "MatchThreshold"));
            items.Add(DynNav("Blocked Sites", () => BlockedSitesSubtitle, "\xe14c", "#FEE2E2", "#EF4444", "ManageBlockedSites"));
            Spacer();

            // ════════════════ PASSKEYS ════════════════
            Section("PASSKEYS", IsPasskeySupported);
            if (IsPasskeySupported)
            {
                if (IsPasskeyProviderSupported)
                    items.Add(Toggle("Passkey Provider", "Use FORTRESS as your passkey provider", "\xe897", "#EDE9FE", "#7C3AED", "PasskeyProvider", IsPasskeyProviderEnabled, valueReader: () => IsPasskeyProviderEnabled));
                items.Add(Nav("Saved Passkeys", "View and manage your passkeys", "\xe73c", "#EDE9FE", "#7C3AED", "PasskeysPage", isNavigate: true));
                Spacer();
            }

            // ════════════════ SECURITY ════════════════
            Section("SECURITY");
            if (IsBiometricsAvailable)
                items.Add(Toggle("Biometric Unlock", "Use fingerprint or Face ID", "\xe90d", "#FCE7F3", "#EC4899", "BiometricUnlock", IsBiometricUnlockEnabled, valueReader: () => IsBiometricUnlockEnabled));
            items.Add(Toggle("PIN Unlock", "Unlock with a 4-digit PIN", "\xe32c", "#DBEAFE", "#3B82F6", "PinUnlock", IsPinUnlockEnabled, valueReader: () => IsPinUnlockEnabled));
            items.Add(Toggle("Allow Screen Capture", "Permit screenshots & screen recording", "\xe61b", "#FEE2E2", "#EF4444", "ScreenCapture", IsScreenCaptureEnabled, valueReader: () => IsScreenCaptureEnabled));
            items.Add(Toggle("Lock on Background", "Lock vault when app is backgrounded", "\xe897", "#FEF9C3", "#CA8A04", "LockOnBackground", IsLockOnBackgroundEnabled, () => IsLockFeatureEnabled, () => IsLockOnBackgroundEnabled));
            items.Add(DynNav("Clear Clipboard", () => SelectedClipboardTimeout?.Value ?? "Never", "\xe14e", "#CFFAFE", "#06B6D4", "ClearClipboard"));
            items.Add(DynNav("Lock Timeout", () => SelectedAppLockTimeout?.Value ?? "1 minute", "\xe425", "#FEF9C3", "#CA8A04", "LockTimeout", () => IsLockFeatureEnabled));
            items.Add(DynNav("Max Failed Attempts", () => SelectedMaxFailedAttempts?.Value ?? "5 attempts", "\xe32a", "#FEE2E2", "#EF4444", "MaxFailedAttempts", () => IsLockFeatureEnabled));
            var lockNow = new SettingItem { Kind = SettingItemKind.Nav, Title = "Lock Now", Subtitle = "Immediately lock the app", IconGlyph = "\xe899", IconBg = "#FED7AA", IconFg = "#EA580C", VisibilityCondition = () => IsLockFeatureEnabled, IsVisible = IsLockFeatureEnabled };
            lockNow.TapCommand = new DelegateCommand(async () => await doLock());
            items.Add(lockNow);
            Spacer();

            // ════════════════ ACCOUNT ════════════════
            Section("ACCOUNT");
            items.Add(Nav("Device Name", PhoneName, "\xe32c", "#E0E7FF", "#407CCA", string.Empty));
            items.Add(Nav("Backups | Sync", "Configure your backup", "\xe630", "#CCFBF1", "#14B8A6", "CloudSyncPage", isNavigate: true));
            items.Add(Nav("Change Master Password", "Update your vault encryption password", "\xe73c", "#DBEAFE", "#3B82F6", "ChangeMasterPassword"));
            items.Add(Nav("Export Vault", "Download a backup of your vault data", "\xe2c4", "#FEF9C3", "#CA8A04", "ExportVault"));
            items.Add(Nav("Import Vault", "Import from Chrome, Bitwarden, 1Password & more", "\xe2c6", "#EDE9FE", "#7C3AED", "ImportPage", isNavigate: true));
            var removeAccount = new SettingItem { Kind = SettingItemKind.Nav, Title = "Remove Account", IconGlyph = "\xe7fe", IconBg = "#FEE2E2", IconFg = "#EF4444" };
            removeAccount.TapCommand = new DelegateCommand(async () => await doRemove());
            items.Add(removeAccount);
            Spacer();

            // ════════════════ MORE ════════════════
            Section("MORE");
            items.Add(Toggle("Send Diagnostic Logs", "Help improve the app with crash reports", "\xe868", "#F1F5F9", "#64748B", "SendDiagnosticLogs", SendDiagnosticLogs, valueReader: () => SendDiagnosticLogs));
            items.Add(Nav("Local Logs", "Application logs for error tracking", "\xe873", "#F1F5F9", "#64748B", "LocalLogsPage", isNavigate: true));
            items.Add(Nav("Activity Log", "Fills, unlocks, changes and security events", "\xe616", "#E0E7FF", "#407CCA", "ActivityLogPage", isNavigate: true));
            items.Add(Nav("About", string.Empty, "\xe88e", "#F1F5F9", "#64748B", "AboutPage", isNavigate: true));
            items.Add(Nav("Help and Support", string.Empty, "\xe887", "#F1F5F9", "#64748B", "HelpPage", isNavigate: true));
            Spacer();

            // ════════════════ APPEARANCE ════════════════
            Section("APPEARANCE");
            items.Add(Toggle("Follow System Theme", "Automatically match device light/dark mode", "\xe51c", "#E0E7FF", "#407CCA", "FollowSystemTheme", IsFollowSystemThemeEnabled, valueReader: () => IsFollowSystemThemeEnabled));
            items.Add(Toggle("Dark Mode", "Switch to dark theme", "\xe51c", "#1E293B", "#F8FAFC", "DarkMode", IsDarkModeEnabled, valueReader: () => IsDarkModeEnabled));
            Spacer();

            SettingItems = new ObservableCollection<SettingItem>(items);
            _allSettingItems = items;
            RebuildVisibleItems();
        }

        private void SyncToggleToVmProperty(string switchId, bool value)
        {
            switch (switchId)
            {
                case "AutofillService": IsAutofillServiceEnabled = value; break;
                case "InlineAutofill": IsUseInlineEnabled = value; break;
                case "AccessibilityAutofill": IsUseAccessibilityEnabled = value; break;
                case "SavePromptDisabled": IsSavePromptDisabled = value; break;
                case "CopyTOTPOnAutofill": IsCopyTOTPOnAutofill = value; break;
                case "RequireAuthForPasswordFill": RequireAuthForPasswordFill = value; break;
                case "RequireAuthForCardFill": RequireAuthForCardFill = value; break;
                case "PasskeyProvider": IsPasskeyProviderEnabled = value; break;
                case "BiometricUnlock": IsBiometricUnlockEnabled = value; break;
                case "PinUnlock": IsPinUnlockEnabled = value; break;
                case "ScreenCapture": IsScreenCaptureEnabled = value; break;
                case "LockOnBackground": IsLockOnBackgroundEnabled = value; break;
                case "SendDiagnosticLogs": SendDiagnosticLogs = value; break;
                case "FollowSystemTheme": IsFollowSystemThemeEnabled = value; break;
                case "DarkMode": IsDarkModeEnabled = value; break;
            }
            if (switchId is "BiometricUnlock" or "PinUnlock")
            {
                foreach (var item in SettingItems)
                    item.RefreshVisibility();
            }
        }

        /// <summary>Refreshes all DynNav subtitle labels and conditional-visibility items.</summary>
        private void RefreshDynNavItems()
        {
            foreach (var item in _allSettingItems)
            {
                item.RefreshSubtitle();
                item.RefreshVisibility();
            }
            RebuildVisibleItems();
        }

        /// <summary>
        /// Rebuilds the <see cref="SettingItems"/> collection from <see cref="_allSettingItems"/>,
        /// filtering out items where <c>IsVisible == false</c> and removing orphaned spacers/headers
        /// whose section has no visible content rows.
        /// </summary>
        private void RebuildVisibleItems()
        {
            // Refresh visibility on all items first
            foreach (var item in _allSettingItems)
                item.RefreshVisibility();

            var visible = new List<SettingItem>();
            for (int i = 0; i < _allSettingItems.Count; i++)
            {
                var item = _allSettingItems[i];

                if (!item.IsVisible)
                    continue;

                // For SectionHeader: only include if there's at least one visible Toggle/Nav before the next Spacer
                if (item.Kind == SettingItemKind.SectionHeader)
                {
                    bool hasContent = false;
                    for (int j = i + 1; j < _allSettingItems.Count; j++)
                    {
                        var next = _allSettingItems[j];
                        if (next.Kind == SettingItemKind.Spacer) break;
                        if (next.Kind == SettingItemKind.SectionHeader) break;
                        if (next.IsVisible && (next.Kind is SettingItemKind.Toggle or SettingItemKind.Nav))
                        { hasContent = true; break; }
                    }
                    if (!hasContent) continue;
                }

                // For Spacer: only include if the preceding visible item is a Toggle/Nav
                if (item.Kind == SettingItemKind.Spacer)
                {
                    var lastVisible = visible.LastOrDefault();
                    if (lastVisible == null || lastVisible.Kind is not (SettingItemKind.Toggle or SettingItemKind.Nav))
                        continue;
                }

                visible.Add(item);
            }

            SettingItems = new ObservableCollection<SettingItem>(visible);
        }

        // ── ExecuteSettingChangedAsync ────────────────────────────────────────────

        private async Task ExecuteSettingChangedAsync(string settingType)
        {
            if (settingType == "PasskeyProvider")
            {
                PreferenceWrapper.Instance.IsPasskeyProviderEnabled = IsPasskeyProviderEnabled;
#if ANDROID
                if (IsPasskeyProviderEnabled)
                    _deviceInfo.OpenCredentialProviderSettings();
#endif
                _dialogService.ShowToast(IsPasskeyProviderEnabled
         ? "FORTRESS is now your passkey provider"
                 : "Passkey provider disabled");
                return;
            }

            if (settingType == "DarkMode")
            {
                if (IsFollowSystemThemeEnabled)
                {
                    _dialogService.ShowToast("Disable 'Follow System Theme' to manually set dark mode");
                    IsDarkModeEnabled = PreferenceWrapper.Instance.AppTheme == "Dark";
                    // Refresh the toggle back to the authoritative state
                    foreach (var item in SettingItems.Where(i => i.SwitchClassId == "DarkMode"))
                        item.RefreshToggle();
                    return;
                }
                if (IsDarkModeEnabled) { Fortress.Mobile.App.SetTheme("Dark"); _dialogService.ShowToast("Dark mode enabled"); }
                else { Fortress.Mobile.App.SetTheme("Light"); _dialogService.ShowToast("Light mode enabled"); }
                return;
            }

            if (settingType == "AutofillService")
            {
                if (IsAutofillServiceEnabled)
                {
                    _deviceInfo.OpenAutofillSettings();
                    CheckAutofill = true;
                    var timer = Application.Current.Dispatcher.CreateTimer();
                    timer.Interval = TimeSpan.FromSeconds(1);
                    timer.Tick += (s, e) =>
                   {
                       IsAutofillServiceEnabled = _deviceInfo.AutofillServiceEnabled(out _);
                       if (IsAutofillServiceEnabled) _deviceInfo.Toast("Fortress Auto-Fill enabled");
                       if (!CheckAutofill || IsAutofillServiceEnabled) timer.Stop();
                   };
                    timer.Start();
                }
                else
                {
                    _deviceInfo.DisableAutofillService();
                    _dialogService.ShowToast("Auto-Fill disabled");
                    IsAutofillServiceEnabled = false;
                }
                return;
            }

            if (settingType == "InlineAutofill")
            {
                PreferenceWrapper.Instance.IsUseInlineAutofillEnabled = IsUseInlineEnabled;
                _dialogService.ShowToast(IsUseInlineEnabled ? "Inline Auto-Fill enabled" : "Inline Auto-Fill disabled");
                return;
            }

            if (settingType == "AccessibilityAutofill")
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
                _deviceInfo.OpenAccessibilitySettings();
                PreferenceWrapper.Instance.IsAccessibilityAutofillEnabled = IsUseAccessibilityEnabled;
                _dialogService.ShowToast(
      "Find 'FORTRESS Autofill' in the Accessibility list and switch it " +
       (IsUseAccessibilityEnabled ? "ON" : "OFF"));
                var timer = Application.Current.Dispatcher.CreateTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, e) =>
                       {
                           var live = _deviceInfo.AutofillAccessibilityServiceRunning();
                           IsUseAccessibilityEnabled = live;
                           if (live == PreferenceWrapper.Instance.IsAccessibilityAutofillEnabled) timer.Stop();
                       };
                timer.Start();
                return;
            }

            if (settingType == "BiometricUnlock")
            {
                if (IsBiometricUnlockEnabled)
                {
                    var result = await BiometricAuthentication.Current.AuthenticateAsync(
               new AuthenticationRequest(title: "FORTRESS", reason: "Validate Biometrics"));
                    if (result.IsSuccessful)
                    {
                        PreferenceWrapper.Instance.IsBiometricUnlockEnabled = true;
                        PreferenceWrapper.Instance.PinUnlockHash = string.Empty;
                        PreferenceWrapper.Instance.IsPinUnlockEnabled = false;
                        IsPinUnlockEnabled = false;
                        _dialogService.ShowToast("Biometric unlock enabled");
                        _ = LogEventAsync(EventLogType.BiometricEnabled);
                        // Refresh PIN toggle + lock-dependent rows
                        foreach (var item in SettingItems.Where(i => i.SwitchClassId == "PinUnlock"))
                            item.RefreshToggle();
                        RefreshDynNavItems();
                    }
                    else { IsBiometricUnlockEnabled = false; }
                }
                else
                {
                    PreferenceWrapper.Instance.IsBiometricUnlockEnabled = false;
                    IsBiometricUnlockEnabled = false;
                    _dialogService.ShowToast("Biometric unlock disabled");
                    _ = LogEventAsync(EventLogType.BiometricDisabled);
                    RefreshDynNavItems();
                }
                return;
            }

            if (settingType == "ScreenCapture")
            {
                PreferenceWrapper.Instance.IsScreenCaptureEnabled = IsScreenCaptureEnabled;
                _deviceInfo.SetScreenCaptureAllowed(IsScreenCaptureEnabled);
                _dialogService.ShowToast(IsScreenCaptureEnabled ? "Screen capture enabled" : "Screen capture disabled");
                return;
            }

            if (settingType == "PinUnlock")
            {
                if (IsPinUnlockEnabled)
                {
                    var result = await _bottomSheetService.ShowAsync<SetUnlockPINSheet, SetUnlockPINSheetViewModel, bool>();
                    if (result) _dialogService.ShowToast("PIN unlock saved");
                    _ = LogEventAsync(EventLogType.PinEnabled);
                    IsPinUnlockEnabled = result;
                }
                else
                {
                    PreferenceWrapper.Instance.IsPinUnlockEnabled = false;
                    PreferenceWrapper.Instance.PinUnlockHash = string.Empty;
                    IsPinUnlockEnabled = false;
                    _dialogService.ShowToast("PIN unlock disabled");
                    _ = LogEventAsync(EventLogType.PinDisabled);
                }
                RefreshDynNavItems();
                return;
            }

            if (settingType == "DrawOver") { _dialogService.ShowToast(IsUseDrawOverEnabled ? "Draw-Over enabled" : "Draw-Over disabled"); return; }
            if (settingType == "LockTimeout") { await ShowLockTimeoutSheetAsync(); return; }
            if (settingType == "MatchThreshold") { await ShowMatchThresholdSheetAsync(); return; }

            if (settingType == "SavePromptDisabled")
            {
                PreferenceWrapper.Instance.IsSavePromptDisabled = IsSavePromptDisabled;
                _dialogService.ShowToast(IsSavePromptDisabled ? "Save Prompt disabled" : "Save Prompt enabled");
                return;
            }

            if (settingType == "RequireAuthForPasswordFill")
            {
                PreferenceWrapper.Instance.RequireAuthForPasswordFill = RequireAuthForPasswordFill;
                _dialogService.ShowToast(RequireAuthForPasswordFill
                   ? "Password fills now require authentication"
               : "Password fills no longer require authentication");
                return;
            }

            if (settingType == "RequireAuthForCardFill")
            {
                PreferenceWrapper.Instance.RequireAuthForCardFill = RequireAuthForCardFill;
                _dialogService.ShowToast(RequireAuthForCardFill
          ? "Card fills now require authentication"
     : "Card fills no longer require authentication");
                return;
            }

            if (settingType == "CopyTOTPOnAutofill")
            {
                PreferenceWrapper.Instance.IsCopyTOTPOnAutofill = IsCopyTOTPOnAutofill;
                _dialogService.ShowToast(IsCopyTOTPOnAutofill
     ? "OTP will be auto-copied to clipboard after filling"
       : "Auto-copy OTP disabled");
                return;
            }

            if (settingType == "ClearClipboard") { await ShowClearClipboardSheetAsync(); return; }
            if (settingType == "MaxFailedAttempts") { await ShowMaxFailedAttemptsSheetAsync(); return; }
            if (settingType == "ManageBlockedSites") { await ShowManageBlockedSitesAsync(); return; }
            if (settingType == "ChangeMasterPassword") { await ExecuteChangeMasterPasswordAsync(); return; }
            if (settingType == "ExportVault") { await ExecuteExportVaultAsync(); return; }

            if (settingType == "SendDiagnosticLogs")
            {
                if (SendDiagnosticLogs)
                {
                    PreferenceWrapper.Instance.SendDiagnosticLogs = true;
                    _dialogService.ShowToast("Diagnostic Logging enabled.");
                }
                else
                {
                    if (await _bottomSheetService.ConfirmAsync("Diagnostic Logging",
                   "Logs allow FORTRESS to track the errors, issues and improve quality of the application. Are you sure you want to disable this?",
                 "Yes", "Cancel"))
                    {
                        PreferenceWrapper.Instance.SendDiagnosticLogs = false;
                        _dialogService.ShowToast("Diagnostic Logging disabled.");
                    }
                    else
                    {
                        SendDiagnosticLogs = true; // revert VM state
                    }
                }
                // Always sync toggle + Local Logs row visibility back to truth
                RefreshDynNavItems();
                foreach (var item in SettingItems.Where(i => i.SwitchClassId == "SendDiagnosticLogs"))
                    item.RefreshToggle();
                return;
            }

            if (settingType == "LockOnBackground")
            {
                PreferenceWrapper.Instance.LockOnBackground = IsLockOnBackgroundEnabled;
                _dialogService.ShowToast(IsLockOnBackgroundEnabled
                   ? "Vault will lock when the app goes to background"
                  : "Background lock disabled");
                return;
            }

            if (settingType == "FollowSystemTheme")
            {
                PreferenceWrapper.Instance.FollowSystemTheme = IsFollowSystemThemeEnabled;
                if (IsFollowSystemThemeEnabled)
                {
                    Fortress.Mobile.App.ApplySystemTheme();
                    IsDarkModeEnabled = Application.Current?.RequestedTheme == AppTheme.Dark;
                    _dialogService.ShowToast("Theme will follow your device setting");
                }
                else { _dialogService.ShowToast("System theme sync disabled"); }
                return;
            }
        }

        private async Task ExecuteNavigateAsync(string pageName)
        {
            await NavigationService.NavigateAsync(
                   $"/{nameof(NavigationPage)}/{nameof(HomePage)}/{pageName}");
        }

        private async Task ExecuteRemoveAccountAsync()
        {
            var result = await _bottomSheetService.ConfirmAsync(
      "Delete Account",
     "Are you sure you want remove your account from the app? You will not be able to access your data from the app.",
  "Yes", "No");
            if (!result) return;

            using (_dialogService.Loading("Removing your account from this app...", maskType: MaskType.Gradient))
            {
                await LogEventAsync(EventLogType.AccountRemoved);
                await Task.Delay(1000);
                await ClearUserDataFromStorage();
                await NavigationService.NavigateAsync($"/{nameof(OnboardingPage)}");
                _deviceInfo.CloseApplication();
            }
        }

        private async Task ExecuteLockNowAsync()
        {
            if (PreferenceWrapper.Instance.IsBiometricUnlockEnabled || PreferenceWrapper.Instance.IsPinUnlockEnabled)
            {
                PreferenceWrapper.Instance.IsApplicationLocked = true;
                _sharedCredentialWriter?.SyncLockStateToSharedPreferences();
                _ = LogEventAsync(EventLogType.VaultLocked, "Lock Now");

                // Request unlock via the service — it pushes UnlockPage as a modal
                // overlay, preserving the current navigation context. When the user
                // unlocks, the modal is popped and they return to this page.
                var unlockService = Shiny.Hosting.Host.GetService<IUnlockService>();
                await unlockService.RequestUnlockAsync();
            }
        }

        // ── Bottom-sheet option pickers ───────────────────────────────────────────

        private async Task ShowLockTimeoutSheetAsync()
        {
            var current = PreferenceWrapper.Instance.LockTimeout;
            var options = AppLockTimeoutOptions.Select(opt => new BottomSheetOption
            {
                Title = opt.Value,
                Icon = new MauiIcon().Icon(MaterialIcons.Timer),
                IsSelected = opt.Key == current,
                Action = () =>
                 {
                     SelectedAppLockTimeout = opt;
                     PreferenceWrapper.Instance.LockTimeout = opt.Key;
                     _dialogService.ShowToast("Application Lock Timeout Saved");
                 }
            }).ToList();

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(options, "Lock Timeout");
        }

        private async Task ShowClearClipboardSheetAsync()
        {
            var current = PreferenceWrapper.Instance.ClearClipboardTimeout;
            var options = ClipboardTimeoutOptions.Select(opt => new BottomSheetOption
            {
                Title = opt.Value,
                Icon = new MauiIcon().Icon(MaterialIcons.ContentCut),
                IsSelected = opt.Key == current,
                Action = async () =>
                 {
                     if (opt.Key != 0 && !await _deviceInfo.VerifyAlarmPermissions())
                         return;

                     SelectedClipboardTimeout = opt;
                     PreferenceWrapper.Instance.ClearClipboardTimeout = opt.Key;
                     _dialogService.ShowToast("Clipboard Timeout Saved");
                 }
            }).ToList();

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(options, "Clear Clipboard");
        }

        private async Task ShowMatchThresholdSheetAsync()
        {
            var current = PreferenceWrapper.Instance.MatchThreshold;
            var options = MatchThresholdOptions.Select(opt => new BottomSheetOption
            {
                Title = opt.Value,
                Icon = new MauiIcon().Icon(MaterialIcons.Tune),
                IsSelected = opt.Key == current,
                Action = () =>
          {
              SelectedMatchThreshold = opt;
              PreferenceWrapper.Instance.MatchThreshold = opt.Key;
              _dialogService.ShowToast("Match Threshold Saved");
          }
            }).ToList();

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(options, "Auto-Fill Matching");
        }

        private async Task ShowMaxFailedAttemptsSheetAsync()
        {
            var current = PreferenceWrapper.Instance.MaxFailedUnlockAttempts;
            var options = FailedAttemptsOptions.Select(opt => new BottomSheetOption
            {
                Title = opt.Value,
                Icon = new MauiIcon().Icon(MaterialIcons.Shield),
                IsSelected = opt.Key == current,
                Action = () =>
         {
             SelectedMaxFailedAttempts = opt;
             PreferenceWrapper.Instance.MaxFailedUnlockAttempts = opt.Key;
             _dialogService.ShowToast("Failed attempts limit saved");
         }
            }).ToList();

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(options, "Max Failed Attempts");
        }

        private async Task ExecuteChangeMasterPasswordAsync()
        {
            var currentPassword = await _bottomSheetService.PromptAsync(
     "Change Master Password",
                       "Enter your current master password to continue.",
        placeholder: "Current password",
       confirmText: "Continue",
    cancelText: "Cancel");

            if (string.IsNullOrEmpty(currentPassword)) return;

            if (currentPassword != PreferenceWrapper.Instance.DatabasePassword)
            {
                await _bottomSheetService.AlertAsync("Incorrect Password",
           "The password you entered does not match your current master password.");
                return;
            }

            var newPassword = await _bottomSheetService.PromptAsync(
        "New Master Password",
      "Choose a strong new master password.",
          placeholder: "New password",
     confirmText: "Next",
         cancelText: "Cancel");

            if (string.IsNullOrEmpty(newPassword)) return;

            var confirmPassword = await _bottomSheetService.PromptAsync(
                  "Confirm Password",
               "Re-enter your new master password to confirm.",
                  placeholder: "Confirm password",
               confirmText: "Save",
                  cancelText: "Cancel");

            if (confirmPassword != newPassword)
            {
                await _bottomSheetService.AlertAsync("Passwords Don't Match",
    "The passwords you entered don't match. Please try again.");
                return;
            }

            using (_dialogService.Loading("Updating your vault...", maskType: MaskType.Gradient))
            {
                await Task.Delay(500);
                PreferenceWrapper.Instance.DatabasePassword = newPassword;
                _dialogService.ShowToast("Master password updated successfully");
                _ = LogEventAsync(EventLogType.MasterPasswordChanged);
            }
        }

        private async Task ExecuteExportVaultAsync()
        {
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
   "Export Vault",
          "Your vault will be exported as an unencrypted JSON file. Store it securely and delete it when no longer needed.",
    destructiveText: "Export Anyway",
    cancelText: "Cancel");

            if (!confirmed) return;

            var enteredPassword = await _bottomSheetService.PromptAsync(
           "Verify Identity",
               "Enter your master password to authorise the export.",
     placeholder: "Master password",
         confirmText: "Export",
             cancelText: "Cancel");

            if (string.IsNullOrEmpty(enteredPassword)) return;

            if (enteredPassword != PreferenceWrapper.Instance.DatabasePassword)
            {
                await _bottomSheetService.AlertAsync("Incorrect Password",
                 "Export cancelled — password did not match.");
                return;
            }

            using (_dialogService.Loading("Preparing export...", maskType: MaskType.Gradient))
            {
                try
                {
                    var credentials = await _dataStorageService.GetLoginItemsAsync();
                    var json = System.Text.Json.JsonSerializer.Serialize(credentials,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                    var fileName = $"fortress_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                    await File.WriteAllTextAsync(filePath, json);

                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = "Export Vault",
                        File = new ShareFile(filePath)
                    });
                    _ = LogEventAsync(EventLogType.VaultExported,
                       $"{credentials.Count()} items exported");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Vault export failed");
                    _dialogService.ShowToast("Export failed. Please try again.");
                }
            }
        }

        private async Task ShowManageBlockedSitesAsync()
        {
            var blocked = PreferenceWrapper.Instance.AutofillBlockedUris;

            if (!blocked.Any())
            {
                var newUri = await _bottomSheetService.PromptAsync(
          "Block a Site or an App",
              "Enter a site URL or app package URI to suppress autofill.\n\nExamples:\n  https://example.com\n  androidapp://com.example.app",
       placeholder: "https://example.com",
                confirmText: "Block",
            cancelText: "Cancel");

                if (!string.IsNullOrWhiteSpace(newUri))
                {
                    var normalised = NormaliseUri(newUri);
                    if (PreferenceWrapper.Instance.BlockAutofillUri(normalised))
                    {
                        BlockedSitesCount = PreferenceWrapper.Instance.AutofillBlockedUris.Count;
                        _dialogService.ShowToast($"Autofill blocked for {normalised}");
                    }
                }
                return;
            }

            var options = new List<BottomSheetOption>();
            foreach (var uri in blocked.ToList())
            {
                var capturedUri = uri;
                options.Add(new BottomSheetOption
                {
                    Title = capturedUri,
                    IconGlyph = "\ue14c",
                    IconBadgeColor = Color.FromArgb("#FEE2E2"),
                    IconColor = Color.FromArgb("#EF4444"),
                    IsSelected = false,
                    Action = async () =>
                                 {
                                     if (await _bottomSheetService.ConfirmAsync(
                          "Unblock Site",
                                   $"Allow FORTRESS to autofill on \"{capturedUri}\"?",
                             "Unblock", "Cancel"))
                                     {
                                         PreferenceWrapper.Instance.UnblockAutofillUri(capturedUri);
                                         BlockedSitesCount = PreferenceWrapper.Instance.AutofillBlockedUris.Count;
                                         _dialogService.ShowToast($"Autofill restored for {capturedUri}");
                                     }
                                 }
                });
            }

            options.Add(new BottomSheetOption
            {
                Title = "Add a site…",
                IconGlyph = "\ue145",
                IconBadgeColor = Color.FromArgb("#DCFCE7"),
                IconColor = Color.FromArgb("#16A34A"),
                IsSelected = false,
                Action = async () =>
             {
                 var newUri = await _bottomSheetService.PromptAsync(
              "Block a Site or an App",
            "Enter the site URL or app package URI.",
          placeholder: "https://example.com",
                  confirmText: "Block",
              cancelText: "Cancel");

                 if (!string.IsNullOrWhiteSpace(newUri))
                 {
                     var normalised = NormaliseUri(newUri);
                     if (PreferenceWrapper.Instance.BlockAutofillUri(normalised))
                     {
                         BlockedSitesCount = PreferenceWrapper.Instance.AutofillBlockedUris.Count;
                         _dialogService.ShowToast($"Autofill blocked for {normalised}");
                     }
                 }
             }
            });

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(options, "Blocked Sites");
        }

        private static string NormaliseUri(string raw)
        {
            var u = raw.Trim().ToLowerInvariant().TrimEnd('/');
            if (!u.StartsWith("http") && !u.StartsWith("androidapp://"))
                u = "https://" + u;
            return u;
        }

        public async Task ClearUserDataFromStorage()
        {
            await _dataStorageService.DeleteStorage();
            _deviceInfo.DisableAutofillService();
            _sharedCredentialWriter?.ClearSharedData();
            var dbFileKey = Preferences.Default.Get("pref_dbFileKey", string.Empty);
            var schemaVersion = Preferences.Default.Get("pref_dbSchemaVersion", 0);
            PreferenceWrapper.Instance.CleanAll();
            if (!string.IsNullOrEmpty(dbFileKey))
                Preferences.Default.Set("pref_dbFileKey", dbFileKey);
            Preferences.Default.Set("pref_dbSchemaVersion", schemaVersion);
            Preferences.Default.Remove(GroupsPageViewModel.SeedDonePrefKeyPublic);
        }

        private async Task LogEventAsync(EventLogType type, string? detail = null)
        {
            try { await _eventLogProcessor.ProcessEventLogAsync(new EventLog { EventType = (int)type, Detail = detail }); }
            catch (Exception ex) { _logger.LogWarning(ex, "[Menu] Failed to log {Type}", type); }
        }

        #region Commands

        private AsyncCommand<string> _settingChangedCommand;
        public ICommand SettingChangedCommand =>
_settingChangedCommand ??= new AsyncCommand<string>(ExecuteSettingChangedAsync);

        private AsyncCommand<string> _navigateCommand;
        public ICommand NavigateCommand =>
        _navigateCommand ??= new AsyncCommand<string>(ExecuteNavigateAsync);

        private AsyncCommand _lockNowCommand;
        public ICommand LockNowCommand =>
       _lockNowCommand ??= new AsyncCommand(ExecuteLockNowAsync);

        private AsyncCommand _removeAccountCommand;
        public ICommand RemoveAccountCommand =>
           _removeAccountCommand ??= new AsyncCommand(ExecuteRemoveAccountAsync);

        private AsyncCommand? _menuGoBackCommand;
        public new ICommand GoBackCommand =>
     _menuGoBackCommand ??= new AsyncCommand(async () =>
              await NavigationService.NavigateAsync($"/{nameof(NavigationPage)}/{nameof(HomePage)}"));

        #endregion
    }
}
