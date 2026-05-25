using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;
using System.Threading.Tasks;

namespace Fortress.ViewModels
{
    public class HomePageViewModel : ViewModelBase, IVoiceCommandContext
    {
        #region Properties

        private bool isRefreshing;
        public bool IsRefreshing
        {
            get => isRefreshing;
            set => SetProperty(ref isRefreshing, value);
        }

        private int credentialsCount;
        public int CredentialsCount
        {
            get => credentialsCount;
            set => SetProperty(ref credentialsCount, value);
        }

        private int authenticatorsCount;
        public int AuthenticatorsCount
        {
            get => authenticatorsCount;
            set => SetProperty(ref authenticatorsCount, value);
        }

        private int weakPasswordsCount;
        public int WeakPasswordsCount
        {
            get => weakPasswordsCount;
            set => SetProperty(ref weakPasswordsCount, value);
        }

        private int reusedPasswordsCount;
        public int ReusedPasswordsCount
        {
            get => reusedPasswordsCount;
            set => SetProperty(ref reusedPasswordsCount, value);
        }

        private double passwordHealthScore = 100;
        public double PasswordHealthScore
        {
            get => passwordHealthScore;
            set => SetProperty(ref passwordHealthScore, value);
        }

        private int creditCardsCount;
        public int CreditCardsCount
        {
            get => creditCardsCount;
            set => SetProperty(ref creditCardsCount, value);
        }

        private int identitiesCount;
        public int IdentitiesCount
        {
            get => identitiesCount;
            set => SetProperty(ref identitiesCount, value);
        }

        private int secureItemsCount;
        public int SecureItemsCount
        {
            get => secureItemsCount;
            set => SetProperty(ref secureItemsCount, value);
        }

        private int passkeysCount;
        public int PasskeysCount
        {
            get => passkeysCount;
            set => SetProperty(ref passkeysCount, value);
        }

        private bool isCloudSyncEnabled;
        public bool IsCloudSyncEnabled
        {
            get => isCloudSyncEnabled;
            set => SetProperty(ref isCloudSyncEnabled, value);
        }

        // ── AI threat alert badge ─────────────────────────────────────────────

        private int _threatAlertCount;
        public int ThreatAlertCount
        {
            get => _threatAlertCount;
            set
            {
                SetProperty(ref _threatAlertCount, value);
                RaisePropertyChanged(nameof(HasUnreadThreatAlert));
            }
        }

        /// <summary>Shows the red badge on the AI Security quick-action card.</summary>
        public bool HasUnreadThreatAlert => _threatAlertCount > 0;

        private string cloudSyncProvider = string.Empty;
        public string CloudSyncProvider
        {
            get => cloudSyncProvider;
            set
            {
                SetProperty(ref cloudSyncProvider, value);
                RaisePropertyChanged(nameof(IsGoogleDriveActive));
                RaisePropertyChanged(nameof(IsDropboxActive));
                RaisePropertyChanged(nameof(IsOneDriveActive));
                RaisePropertyChanged(nameof(CloudSyncProviderDisplayName));
                RaisePropertyChanged(nameof(CloudSyncNavigationTarget));
            }
        }

        private string lastSyncDisplay = "Never";
        public string LastSyncDisplay
        {
            get => lastSyncDisplay;
            set => SetProperty(ref lastSyncDisplay, value);
        }

        private ObservableCollection<LoginItem> recentCredentials = new();
        public ObservableCollection<LoginItem> RecentCredentials
        {
            get => recentCredentials;
            set => SetProperty(ref recentCredentials, value);
        }

        private int _unreadNotificationCount;
        public int UnreadNotificationCount
        {
            get => _unreadNotificationCount;
            set
            {
                SetProperty(ref _unreadNotificationCount, value);
                RaisePropertyChanged(nameof(HasUnreadNotifications));
            }
        }

        /// <summary>True when there is at least one unread notification — controls badge visibility.</summary>
        public bool HasUnreadNotifications => _unreadNotificationCount > 0;

        // ── AI Insight Card ───────────────────────────────────────────────────

        private string _aiInsightText = string.Empty;
        public string AiInsightText
        {
            get => _aiInsightText;
            set
            {
                SetProperty(ref _aiInsightText, value);
                RaisePropertyChanged(nameof(HasAiInsight));
            }
        }

        private string _aiInsightIcon = "\ue8e8"; // lightbulb
        public string AiInsightIcon
        {
            get => _aiInsightIcon;
            set => SetProperty(ref _aiInsightIcon, value);
        }

        private string _aiInsightActionPage = "VaultHealthPage";
        public string AiInsightActionPage
        {
            get => _aiInsightActionPage;
            set => SetProperty(ref _aiInsightActionPage, value);
        }

        private string _aiInsightActionLabel = "Review";
        public string AiInsightActionLabel
        {
            get => _aiInsightActionLabel;
            set => SetProperty(ref _aiInsightActionLabel, value);
        }

        private string _aiInsightSeverityColor = "#3B82F6"; // blue default
        public string AiInsightSeverityColor
        {
            get => _aiInsightSeverityColor;
            set => SetProperty(ref _aiInsightSeverityColor, value);
        }

        public bool HasAiInsight => !string.IsNullOrEmpty(_aiInsightText);

        // ── Voice FAB Coach Mark ──────────────────────────────────────────────

private bool _showVoiceCoachMark;
        public bool ShowVoiceCoachMark
        {
get => _showVoiceCoachMark;
      set => SetProperty(ref _showVoiceCoachMark, value);
        }

        public bool IsGoogleDriveActive => CloudSyncProvider == "GoogleDrive";
 public bool IsDropboxActive => CloudSyncProvider == "Dropbox";
        public bool IsOneDriveActive => CloudSyncProvider == "OneDrive";

        /// <summary>
        /// True only on Android 14+ (API 34) where the CredentialProviderService
        /// passkey API exists. Hides passkey UI on unsupported devices/platforms.
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
        public string CloudSyncProviderDisplayName => CloudSyncProvider switch
        {
            "GoogleDrive" => "Google Drive",
            "Dropbox" => "Dropbox",
            "OneDrive" => "OneDrive",
            _ => CloudSyncProvider
        };
        public string CloudSyncNavigationTarget => CloudSyncProvider switch
        {
            "GoogleDrive" => "GoogleDriveSyncPage",
            "Dropbox" => "DropboxSyncPage",
            "OneDrive" => "OneDriveSyncPage",
            _ => "CloudSyncPage"
        };

        // ── Health Trend ──────────────────────────────────────────────────────

  private string _healthTrendText = string.Empty;
        public string HealthTrendText
        {
         get => _healthTrendText;
         set { SetProperty(ref _healthTrendText, value); RaisePropertyChanged(nameof(HasHealthTrend)); }
     }

        private string _healthTrendIcon = string.Empty;
     public string HealthTrendIcon
        {
get => _healthTrendIcon;
        set => SetProperty(ref _healthTrendIcon, value);
        }

        private string _healthTrendColor = "#64748B";
     public string HealthTrendColor
        {
     get => _healthTrendColor;
set => SetProperty(ref _healthTrendColor, value);
        }

    public bool HasHealthTrend => !string.IsNullOrEmpty(_healthTrendText);

        #endregion

        #region Fields

        private readonly ISharedCredentialWriter? _sharedCredentialWriter;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataStorageService _dataStorageService;
        private readonly IDeviceServices _deviceInfo;
        private readonly IUserDialogs _dialogService;
        private readonly ILogger<HomePageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;
        private readonly IVoiceCommandService _voiceCommandService;
        private readonly VaultHealthCalculator _healthCalculator;
        private readonly IVoiceCommandRouter _voiceCommandRouter;
        private readonly IEventLogProcessor _eventLogProcessor;
        private readonly ISpeechToTextService _speechToText;
        private readonly ICryptographyService _cryptographyService;
        private VaultHealthResult? _cachedHealth;

        #endregion

        public HomePageViewModel(
            INavigationService navigationService,
          IEventAggregator eventAggregator,
            IDataStorageService dataStorageService,
            IDeviceServices deviceInfo,
 IUserDialogs dialogService,
          ILogger<HomePageViewModel> logger,
     IBottomSheetService bottomSheetService,
      IVoiceCommandService voiceCommandService,
     VaultHealthCalculator healthCalculator,
   IVoiceCommandRouter voiceCommandRouter,
    IEventLogProcessor eventLogProcessor,
      ISpeechToTextService speechToText,
      ICryptographyService cryptographyService,
   ISharedCredentialWriter? sharedCredentialWriter = null)
            : base(navigationService)
        {
            _sharedCredentialWriter = sharedCredentialWriter;
            _eventAggregator = eventAggregator;
            _dataStorageService = dataStorageService;
            _deviceInfo = deviceInfo;
            _dialogService = dialogService;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
            _voiceCommandService = voiceCommandService;
            _healthCalculator = healthCalculator;
            _voiceCommandRouter = voiceCommandRouter;
            _eventLogProcessor = eventLogProcessor;
            _speechToText = speechToText;
            _cryptographyService = cryptographyService;

            _eventAggregator.GetEvent<RefreshProfileEvent>().Subscribe(RefreshProfileAction);
            _eventAggregator.GetEvent<AuthenticatorAddedEvent>().Subscribe(OnAuthenticatorAdded);
            _eventAggregator.GetEvent<AuthenticatorDeletedEvent>().Subscribe(OnAuthenticatorDeleted);
            _eventAggregator.GetEvent<RefreshNotificationsEvent>().Subscribe(_ => { var __ = RefreshUnreadCountAsync(); });
            _eventAggregator.GetEvent<AutofillPasswordsEvent>().Subscribe(OnAutofillIntent);

            _logger.LogInformation("HomePageViewModel initialized");
        }

        // ── Autofill add-new / save intent handler ────────────────────────────────

        private async void OnAutofillIntent(RequestingApplication request)
        {
            if (request == null) return;

            // Only handle the "add new" flow here — the fill flow is handled by AutofillPage
            if (!request.IsAddOrSaveContext) return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
      {
          try
          {
              // Route to the correct Add page based on fill type
              var targetPage = request.FillType switch
              {
                  CipherType.Card => nameof(Views.AddEditCreditCardPage),
                  CipherType.Identity => nameof(Views.AddEditIdentityPage),
                  _ => nameof(Views.AddEditCredentialPage), // Login / default
              };

              var navParams = new NavigationParameters();

              // Pre-populate domain from the requesting app
              if (!string.IsNullOrWhiteSpace(request.Name))
                  navParams.Add("domain", request.Name);
              if (!string.IsNullOrWhiteSpace(request.Package))
                  navParams.Add("uri", request.Package);

              _logger.LogInformation("AutofillIntent: routing to {Page} for domain={D}", targetPage, request.Name);
              var result = await NavigationService.NavigateAsync(targetPage, navParams);
              if (!result.Success)
                  _logger.LogError(result.Exception, "AutofillIntent: navigation to {Page} failed", targetPage);
          }
          catch (Exception ex)
          {
              _logger.LogError(ex, "AutofillIntent: navigation threw");
          }
      });
        }

        #region IVoiceCommandContext

        async Task IVoiceCommandContext.NavigateAsync(string page, IDictionary<string, object>? parameters)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
   {
       if (parameters is null)
       {
           await NavigationService.NavigateAsync(page);
       }
       else
       {
           var nav = new NavigationParameters();
           foreach (var kv in parameters) nav.Add(kv.Key, kv.Value);
           await NavigationService.NavigateAsync(page, nav);
       }
   });
        }

        async Task<VaultHealthResult> IVoiceCommandContext.GetVaultHealthAsync()
        {
            if (_cachedHealth is not null) return _cachedHealth;
            var credentials = (await _dataStorageService.GetLoginItemsAsync()).ToList();
            var authenticators = (await _dataStorageService.GetAuthenticatorsAsync()).ToList();
            _cachedHealth = await Task.Run(() => _healthCalculator.Calculate(credentials, authenticators));
            return _cachedHealth;
        }

        async Task<int> IVoiceCommandContext.GetPasswordCountAsync()
        {
            try { return await _dataStorageService.GetLoginItemsCountAsync(); }
            catch { return CredentialsCount; }
        }

        async Task<int> IVoiceCommandContext.GetCardCountAsync()
        {
            try { return await _dataStorageService.GetCreditCardItemsCountAsync(); }
            catch { return CreditCardsCount; }
        }

        async Task IVoiceCommandContext.LockAsync()
        {
            PreferenceWrapper.Instance.IsApplicationLocked = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
               {
                   await NavigationService.NavigateAsync($"/NavigationPage/{nameof(Views.UnlockPage)}");
               });
        }

        void IVoiceCommandContext.ShowToast(string message) =>
       _dialogService.ShowToast(message);

        async Task IVoiceCommandContext.TriggerSyncAsync()
        {
            _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
            await Task.CompletedTask;
        }

        async Task IVoiceCommandContext.SpeakAsync(string text)
        {
            try
            {
                await TextToSpeech.Default.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TTS failed");
            }
        }

        bool IVoiceCommandContext.IsLockConfigured =>
     PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
            PreferenceWrapper.Instance.IsPinUnlockEnabled;

        async Task<string?> IVoiceCommandContext.ListenForDictationAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Reuse the platform STT service in raw dictation mode (no intent parsing).
                return await _speechToText.ListenAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Voice journal dictation failed");
                return null;
            }
        }

        async Task<Guid> IVoiceCommandContext.SaveJournalEntryAsync(string content)
        {
            // Build a human-friendly title with date/time
  var now = DateTime.Now;
  var label = $"🎙️ Journal — {now:MMM d, yyyy h:mm tt}";

        // Encrypt the content before storing
      var encResult = await _cryptographyService.Encrypt(content);
     var encryptedContent = encResult.Succeeded ? encResult.Data ?? content : content;

            // Save as a SecureItem with SecureNote type — this is the model that
       // SecureItemsPage actually loads and displays.
        var item = new SecureItem
    {
      Id = Guid.NewGuid(),
        ItemType = SecureItemType.SecureNote,
         Label = label,
           NoteContent = encryptedContent,
        Notes = "Voice journal entry",
       IsFavorite = false,
        CreatedAt = DateTime.UtcNow,
   UpdatedAt = DateTime.UtcNow,
   };

    await _dataStorageService.SaveSecureItemAsync(item);
            _logger.LogInformation("Voice journal saved: {Id} — {Label}", item.Id, item.Label);

     // Refresh the home page counts so the secure-items tile updates
    _ = RefreshProfileAsync();

return item.Id;
        }

        #endregion

        #region Navigation & lifecycle

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
   await Application.Current.Dispatcher.DispatchAsync(() => IsRefreshing = true);

            if (parameters.ContainsKey("AutoFillCheck"))
            CheckAutoFillService();

            // Show voice coach mark for the first 3 app opens, then auto-dismiss
     if (!Preferences.Default.Get("VoiceCoachMarkDismissed", false))
      {
      int openCount = Preferences.Default.Get("VoiceCoachMarkShownCount", 0);
      if (openCount < 3)
  {
       ShowVoiceCoachMark = true;
 Preferences.Default.Set("VoiceCoachMarkShownCount", openCount + 1);
    }
    else
  {
        Preferences.Default.Set("VoiceCoachMarkDismissed", true);
ShowVoiceCoachMark = false;
         }
  }

   RefreshProfileAction(null);
          await RefreshUnreadCountAsync();

            await Application.Current.Dispatcher.DispatchAsync(() => IsRefreshing = false);
        }

        private void OnAuthenticatorDeleted(Authenticator auth) => _ = RefreshProfileAsync();
        private void OnAuthenticatorAdded(Authenticator auth) => _ = RefreshProfileAsync();

        #endregion

        #region Data loading

        // Event-aggregator subscriber (sync signature required by Prism) — fire-and-forget safely
        private void RefreshProfileAction(string obj) => _ = RefreshProfileAsync();

        private async Task RefreshProfileAsync()
        {
            try
            {
                // ── Run all independent count queries in parallel ──────────────────
                var credCountTask = _dataStorageService.GetLoginItemsCountAsync();
                var cardCountTask = _dataStorageService.GetCreditCardItemsCountAsync();
                var identityCountTask = _dataStorageService.GetIdentityItemsCountAsync();
                var secureItemsTask = _dataStorageService.GetSecureItemsAsync();
                var authTask = _dataStorageService.GetAuthenticatorsAsync();
                var passkeyTask = _dataStorageService.GetPasskeyItemsAsync();
                var credentialsTask = _dataStorageService.GetLoginItemsAsync();

                await Task.WhenAll(credCountTask, cardCountTask, identityCountTask,
               secureItemsTask, authTask, passkeyTask, credentialsTask);

                CredentialsCount = credCountTask.Result;
                CreditCardsCount = cardCountTask.Result;
                IdentitiesCount = identityCountTask.Result;
                SecureItemsCount = secureItemsTask.Result.Count();
                AuthenticatorsCount = authTask.Result.Count();
                PasskeysCount = passkeyTask.Result.Count();

                var credentials = credentialsTask.Result.ToList();
                var authenticators = authTask.Result.ToList();

                await CalculatePasswordHealthAsync(credentials, authenticators);
                await LoadRecentLoginsAsync(credentials);

                IsCloudSyncEnabled = PreferenceWrapper.Instance.IsCloudSyncEnabled;
                CloudSyncProvider = PreferenceWrapper.Instance.CloudSyncProvider;

                var syncPrefKey = CloudSyncProvider switch
                {
                    "Dropbox" => Fortress.Mobile.Core.Contracts.DropboxConstants.PrefLastSyncTime,
                    "GoogleDrive" => Fortress.Mobile.Core.Contracts.GoogleDriveConstants.PrefLastSyncTime,
                    "OneDrive" => Fortress.Mobile.Core.Contracts.OneDriveConstants.PrefLastSyncTime,
                    _ => string.Empty
                };

                var rawSync = string.IsNullOrEmpty(syncPrefKey)
             ? string.Empty
                   : Preferences.Default.Get(syncPrefKey, string.Empty);

                if (!string.IsNullOrEmpty(rawSync) &&
              DateTime.TryParse(rawSync, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var lastSync))
                {
                    var ago = DateTime.UtcNow - lastSync;
                    LastSyncDisplay = ago.TotalMinutes < 1 ? "Just now"
                  : ago.TotalMinutes < 60 ? $"{(int)ago.TotalMinutes}m ago"
                 : ago.TotalHours < 24 ? $"{(int)ago.TotalHours}h ago"
                  : $"{(int)ago.TotalDays}d ago";
                }
                else
                {
                    LastSyncDisplay = "Never";
                }

                _ = SyncCredentialsToAutofillAsync();
                _ = RefreshThreatAlertCountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh home page");
            }
        }

        /// <summary>
        /// Counts blocked + warned autofill events in the last 7 days and
        /// surfaces the total as a badge on the AI Security quick-action card.
        /// </summary>
        private async Task RefreshThreatAlertCountAsync()
        {
            try
            {
                var since = DateTime.UtcNow.AddDays(-7);
                var logs = await _eventLogProcessor.GetLocalLogsAsync(
               new List<int>
                           {
              (int)EventLogType.AutofillBlockedRisk,
           (int)EventLogType.AutofillWarnedRisk,
           },
               since, DateTime.UtcNow, recordCount: 200);

                ThreatAlertCount = logs.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh threat alert count");
            }
        }

        /// <summary>
        /// Runs the CPU-heavy vault health calculation on the thread pool so
        /// the UI thread is never blocked — critical for 3,500+ item vaults.
        /// Accepts the already-loaded lists so we don't hit storage twice.
        /// </summary>
        private async Task CalculatePasswordHealthAsync(
                List<LoginItem> credentials,
           List<Authenticator> authenticators)
        {
            try
            {
                var result = await Task.Run(() => _healthCalculator.Calculate(credentials, authenticators));

                _cachedHealth = result;
                PasswordHealthScore = result.Score;
                WeakPasswordsCount = result.WeakPasswordsCount;
                ReusedPasswordsCount = result.ReusedPasswordsCount;

   // Generate proactive AI insight from computed health data
                await GenerateAiInsightAsync(result);

  // Compute health trend from historical snapshots
    _ = ComputeHealthTrendAsync(result.Score);
            }
  catch (Exception ex)
 {
        _logger.LogError(ex, "Failed to calculate password health");
         PasswordHealthScore = 0;
     }
   }

        /// <summary>
        /// Picks the highest-priority, most relevant AI insight from existing
        /// computed vault health data and threat logs. No new AI models needed —
        /// this is pure prioritised template selection from data already computed.
        /// </summary>
        private async Task GenerateAiInsightAsync(VaultHealthResult health)
        {
            try
      {
           // Priority 1: Breached passwords (critical)
   if (health.BreachedCount > 0)
     {
 AiInsightIcon = "\ue002";  // warning
       AiInsightSeverityColor = "#EF4444"; // red
          AiInsightText = health.BreachedCount == 1
     ? "1 password was found in a known breach database. Change it now to stay safe."
        : $"{health.BreachedCount} passwords found in breach databases. Change them before attackers try them.";
        AiInsightActionPage = "VaultHealthPage";
      AiInsightActionLabel = "Fix now";
    return;
     }

     // Priority 2: Active threat blocks this week
       if (ThreatAlertCount > 0)
   {
   AiInsightIcon = "\ue32a";  // shield
  AiInsightSeverityColor = "#F59E0B"; // amber
        AiInsightText = ThreatAlertCount == 1
     ? "Fortress blocked 1 suspicious autofill attempt this week. Your AI is working."
            : $"Fortress blocked {ThreatAlertCount} suspicious autofill attempts this week. Tap to review.";
   AiInsightActionPage = "SecurityInsightsPage";
  AiInsightActionLabel = "View details";
 return;
            }

       // Priority 3: Credential clusters (blast radius / compromise chains)
  if (health.CredentialClusters.Count > 0)
      {
      var biggest = health.CredentialClusters[0];
        AiInsightIcon = "\ue002";  // warning
   AiInsightSeverityColor = "#F59E0B"; // amber
  AiInsightText = $"Compromise chain detected: {biggest.RiskNarrative}";
   AiInsightActionPage = "VaultHealthPage";
     AiInsightActionLabel = "Fix chain";
    return;
        }

            // Priority 4: Reused passwords
            if (health.ReusedPasswordsCount > 0)
    {
          AiInsightIcon = "\ue14d";  // content_copy
          AiInsightSeverityColor = "#F59E0B"; // amber
   var potentialScore = Math.Min(100, health.Score + (health.ReusedPasswordsCount * 3));
    AiInsightText = health.ReusedPasswordsCount == 1
  ? $"1 password is reused across accounts. Fixing it could raise your score to {potentialScore}%."
          : $"{health.ReusedPasswordsCount} passwords are reused. Fixing them could raise your score to {potentialScore}%.";
AiInsightActionPage = "VaultHealthPage";
 AiInsightActionLabel = "Fix reuse";
        return;
           }

       // Priority 5: Weak passwords
    if (health.WeakPasswordsCount > 0)
  {
  AiInsightIcon = "\ue002";  // warning
     AiInsightSeverityColor = "#F59E0B"; // amber
             AiInsightText = health.WeakPasswordsCount == 1
     ? "1 password is weak and could be cracked quickly. Use the generator to strengthen it."
         : $"{health.WeakPasswordsCount} passwords are weak and could be cracked quickly. Strengthen them.";
           AiInsightActionPage = "VaultHealthPage";
  AiInsightActionLabel = "Strengthen";
       return;
     }

  // Priority 6: Missing 2FA
 if (health.Missing2FACount > 0)
   {
AiInsightIcon = "\ue8e8";  // lightbulb
      AiInsightSeverityColor = "#3B82F6"; // blue (info)
 var coveragePercent = health.TotalCredentials > 0
          ? (int)(((double)(health.TotalCredentials - health.Missing2FACount) / health.TotalCredentials) * 100)
: 0;
        AiInsightText = $"{health.Missing2FACount} accounts are missing two-factor authentication. Your 2FA coverage is {coveragePercent}%.";
       AiInsightActionPage = "VaultHealthPage";
        AiInsightActionLabel = "Add 2FA";
 return;
    }

        // Priority 7: Old passwords
     if (health.OldPasswordsCount > 0)
                {
 AiInsightIcon = "\ue8e8";  // lightbulb
         AiInsightSeverityColor = "#3B82F6"; // blue
          AiInsightText = $"{health.OldPasswordsCount} passwords haven't been changed in over {health.Config.MaxPasswordAgeDays} days. Consider rotating them.";
      AiInsightActionPage = "VaultHealthPage";
 AiInsightActionLabel = "Review";
       return;
       }

       // Priority 8: All clear — vault is healthy
                if (health.TotalCredentials > 0)
{
    AiInsightIcon = "\ue86c";// check_circle
  AiInsightSeverityColor = "#22C55E"; // green
        AiInsightText = "All clear — every password is unique and strong. Your vault is in excellent shape.";
     AiInsightActionPage = string.Empty;
     AiInsightActionLabel = string.Empty;
            return;
      }

                // Empty vault — welcome message
 AiInsightIcon = "\ue8e8";  // lightbulb
         AiInsightSeverityColor = "#3B82F6"; // blue
      AiInsightText = "Your vault is empty. Add your first login to get started with AI-powered security analysis.";
                AiInsightActionPage = "AddEditCredentialPage";
AiInsightActionLabel = "Add login";
         }
         catch (Exception ex)
      {
      _logger.LogError(ex, "Failed to generate AI insight");
         AiInsightText = string.Empty;
            }
        }

        /// <summary>
    /// Computes a simple health trend from the last 7 days of snapshots.
  /// Shows "↑ +5 this week" or "↓ -3 this week" or "— Stable" on the hero.
        /// </summary>
      private async Task ComputeHealthTrendAsync(int currentScore)
   {
try
     {
        var history = await _dataStorageService.GetHealthHistoryAsync(7);
 if (history == null || history.Count < 2)
 {
       HealthTrendText = string.Empty;
      return;
   }

      // Compare current score vs oldest snapshot in the window
   var oldest = history.OrderBy(h => h.RecordedDate).First();
 var delta = currentScore - oldest.Score;

       if (delta > 0)
    {
         HealthTrendIcon = "\ue5d8";  // arrow_upward
     HealthTrendColor = "#22C55E"; // green
       HealthTrendText = $"+{delta} this week";
    }
     else if (delta < 0)
   {
        HealthTrendIcon = "\ue5db";  // arrow_downward
        HealthTrendColor = "#EF4444"; // red
        HealthTrendText = $"{delta} this week";
    }
     else
  {
     HealthTrendIcon = "\ue15b";  // remove (dash)
    HealthTrendColor = "#64748B"; // gray
       HealthTrendText = "Stable this week";
   }
   }
   catch (Exception ex)
      {
   _logger.LogError(ex, "Failed to compute health trend");
     HealthTrendText = string.Empty;
    }
   }

        private Task LoadRecentLoginsAsync(List<LoginItem> credentials)
        {
            try
            {
                var recent = credentials
          .OrderByDescending(c => c.UpdatedAt)
          .Take(5)
            .ToList();
                RecentCredentials = new ObservableCollection<LoginItem>(recent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load recent logins");
            }
            return Task.CompletedTask;
        }

        private async Task SyncCredentialsToAutofillAsync()
        {
            try
            {
#if IOS
      if (_sharedCredentialWriter != null)
    {
        await _sharedCredentialWriter.SyncCredentialsToSharedStorageAsync();
             _logger.LogDebug("Credentials synced to autofill storage");
            }
#endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync credentials to autofill storage");
            }
        }

        private async Task CheckAutoFillService()
        {
            if (!_deviceInfo.AutofillServiceEnabled(out var isPackageNameCorrect) && !isPackageNameCorrect)
            {
                var result = await _dialogService.ConfirmAsync("To use autofill, please enable the autofill service in your device settings.",
                    "Autofill", "Enable");
                if (result)
                    _deviceInfo.OpenAutofillSettings();
            }
        }

        private async Task RefreshUnreadCountAsync()
        {
            try
            {
                var all = await _dataStorageService.GetNotificationsAsync();
                UnreadNotificationCount = all.Count(n => !n.IsSeen && !n.IsExpired);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh unread notification count");
            }
        }

        #endregion

        #region Commands

        private AsyncCommand<string> _navigateCommand;
        public ICommand NavigateCommand =>
  _navigateCommand ??= new AsyncCommand<string>(ExecuteNavigateAsync);

        private AsyncCommand<string> _navigateWithTypeCommand;
        public ICommand NavigateWithTypeCommand =>
                _navigateWithTypeCommand ??= new AsyncCommand<string>(ExecuteNavigateWithTypeAsync);

        private AsyncCommand _quickBackupCommand;
        public ICommand QuickBackupCommand =>
                 _quickBackupCommand ??= new AsyncCommand(() => ExecuteNavigateAsync(CloudSyncNavigationTarget));

        private AsyncCommand _voiceFabCommand;
        public ICommand VoiceFabCommand =>
       _voiceFabCommand ??= new AsyncCommand(ExecuteVoiceFabAsync);

        private AsyncCommand<string> _aiInsightTapCommand;
        public ICommand AiInsightTapCommand =>
        _aiInsightTapCommand ??= new AsyncCommand<string>(ExecuteAiInsightTapAsync);

        private DelegateCommand _dismissCoachMarkCommand;
      public ICommand DismissCoachMarkCommand =>
            _dismissCoachMarkCommand ??= new DelegateCommand(ExecuteDismissCoachMark);

        private async Task ExecuteNavigateAsync(string page)
        {
            if (string.IsNullOrEmpty(page)) return;
            try
            {
                _logger.LogInformation("NavigateCommand -> {Page}", page);
                var result = await NavigationService.NavigateAsync(page);
                if (!result.Success)
                    _logger.LogError(result.Exception, "Navigation to {Page} failed", page);
            }
            catch (Exception ex) { _logger.LogError(ex, "Navigation to {Page} threw", page); }
        }

        private async Task ExecuteNavigateWithTypeAsync(string typeParam)
        {
            if (string.IsNullOrEmpty(typeParam)) return;
            try
            {
                var parts = typeParam.Split('|');
                if (parts.Length == 2)
                {
                    var navParams = new NavigationParameters { { "type", parts[1] } };
                    var result = await NavigationService.NavigateAsync(parts[0], navParams);
                    if (!result.Success)
                        _logger.LogError(result.Exception, "Navigation to {Page} with type failed", parts[0]);
                }
                else
                {
                    var result = await NavigationService.NavigateAsync(typeParam);
                    if (!result.Success)
                        _logger.LogError(result.Exception, "Navigation to {Page} failed", typeParam);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "NavigateWithType threw for param {Param}", typeParam); }
        }

        private async Task ExecuteAiInsightTapAsync(string page)
       {
  if (!string.IsNullOrEmpty(page))
     await ExecuteNavigateAsync(page);
        }

        private void ExecuteDismissCoachMark()
        {
     ShowVoiceCoachMark = false;
         Preferences.Default.Set("VoiceCoachMarkDismissed", true);
    }

        private async Task ExecuteVoiceFabAsync()
        {
            // Dismiss coach mark on first use
            if (ShowVoiceCoachMark)
{
                ShowVoiceCoachMark = false;
         Preferences.Default.Set("VoiceCoachMarkDismissed", true);
     }

            try
            {
     if (!await _voiceCommandService.RequestPermissionAsync())
                {
           _dialogService.ShowToast("Microphone permission is required for voice commands.");
           return;
       }
await Task.Delay(500);
          var result = await _bottomSheetService
        .ShowAsync<VoiceListeningSheet, VoiceListeningSheetViewModel, VoiceCommandResult>(
        null, "Listening\u2026");

   if (result == null) return;

                // Route both recognised AND unrecognised (Unknown) intents to the
     // router. For recognised intents the router executes the action.
           // For Unknown intents the router speaks a help message so the user
       // knows what they can say instead of silent dismissal.
  await _voiceCommandRouter.RouteAsync(result, this);
      }
catch (Exception ex)
          {
           _dialogService.Alert($"An error occurred while processing voice command. Please try again. {ex.Message}", "Error", "OK");
                _logger.LogError(ex, "VoiceFab failed");
}
        }

        public ISharedCredentialWriter? SharedCredentialWriter => _sharedCredentialWriter;

        #endregion
    }
}
