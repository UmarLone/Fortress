namespace Fortress.Mobile.Core.Services
{
    public sealed partial class PreferenceWrapper : BasePreferenceWrapper
    {
        #region Standard preferences

        private const string IsSavePromptDisabledKey = "pref_isSavePromptDisabled";
        private const bool IsSavePromptDisabledDefault = false;
        public bool IsSavePromptDisabled
        {
            get => Preferences.Default.Get(IsSavePromptDisabledKey, IsSavePromptDisabledDefault);
            set => Preferences.Default.Set(IsSavePromptDisabledKey, value);
        }

        private const string IsCopyTOTPOnAutofillKey = "pref_isCopyTOTPOnAutofill";
        private const bool IsCopyTOTPOnAutofillDefault = true;
        public bool IsCopyTOTPOnAutofill
        {
            get => Preferences.Default.Get(IsCopyTOTPOnAutofillKey, IsCopyTOTPOnAutofillDefault);
            set => Preferences.Default.Set(IsCopyTOTPOnAutofillKey, value);
        }

        private const string IsScreenCaptureEnabledKey = "pref_screenCaptureEnabled";
        private const bool IsScreenCaptureEnabledDefault = false;
        public bool IsScreenCaptureEnabled
        {
            get => Preferences.Default.Get(IsScreenCaptureEnabledKey, IsScreenCaptureEnabledDefault);
            set => Preferences.Default.Set(IsScreenCaptureEnabledKey, value);
        }

        private const string AllowBiometricsKey = "pref_allowBiometrics";
        private const bool AllowBiometricsDefault = false;
        public bool AllowBiometrics
        {
            get => Preferences.Default.Get(AllowBiometricsKey, AllowBiometricsDefault);
            set => Preferences.Default.Set(AllowBiometricsKey, value);
        }

        private const string SendDiagnosticLogsKey = "pref_sendDiagnosticLogsKey";
        private const bool SendDiagnosticLogsDefault = true;
        public bool SendDiagnosticLogs
        {
            get => Preferences.Default.Get(SendDiagnosticLogsKey, SendDiagnosticLogsDefault);
            set => Preferences.Default.Set(SendDiagnosticLogsKey, value);
        }

        private const string DatabasePasswordKey = "pref_databasePassword";
        private const string DatabasePasswordDefault = "";
        public string DatabasePassword
        {
            get => Preferences.Default.Get(DatabasePasswordKey, DatabasePasswordDefault);
            set
            {
                Preferences.Default.Set(DatabasePasswordKey, value);
                // Evict the cached derived key whenever the master password changes so
                // the next Encrypt/Decrypt re-derives from the new password.
                try
                {
                    Shiny.Hosting.Host.GetService<ICryptographyService>()?.InvalidateKeyCache();
                }
                catch { /* service may not be resolved yet during first-run setup */ }
            }
        }

        private const string LockTimeoutKey = "pref_lockTimeout";
        private const int LockTimeoutDefault = 300;
        public int LockTimeout
        {
            get => Preferences.Default.Get(LockTimeoutKey, LockTimeoutDefault);
            set => Preferences.Default.Set(LockTimeoutKey, value);
        }

        private const string ClearClipboardTimeoutKey = "pref_clearClipboardTimeout";
        private const int ClearClipboardTimeoutDefault = 0;
        public int ClearClipboardTimeout
        {
            get => Preferences.Default.Get(ClearClipboardTimeoutKey, ClearClipboardTimeoutDefault);
            set => Preferences.Default.Set(ClearClipboardTimeoutKey, value);
        }

        private const string IsApplicationLockedKey = "pref_applicationLocked";
        private const bool IsApplicationLockedDefault = false;
        public bool IsApplicationLocked
        {
            get => Preferences.Default.Get(IsApplicationLockedKey, IsApplicationLockedDefault);
            set => Preferences.Default.Set(IsApplicationLockedKey, value);
        }

        private const string PreventLockingKey = "pref_preventLockiing";
        private const bool PreventLockingDefault = false;
        public bool PreventLocking
        {
            get => Preferences.Default.Get(PreventLockingKey, PreventLockingDefault);
            set => Preferences.Default.Set(PreventLockingKey, value);
        }

        private const string IsBiometricUnlockEnabledKey = "pref_isBiometricLockedKey";
        private const bool IsBiometricUnlockedDefault = false;
        public bool IsBiometricUnlockEnabled
        {
            get => Preferences.Default.Get(IsBiometricUnlockEnabledKey, IsBiometricUnlockedDefault);
            set => Preferences.Default.Set(IsBiometricUnlockEnabledKey, value);
        }

        private const string IsPinUnlockEnabledKey = "pref_isPinUnlockKey";
        private const bool IsPinUnlockEnabledDefault = false;
        public bool IsPinUnlockEnabled
        {
            get => Preferences.Default.Get(IsPinUnlockEnabledKey, IsPinUnlockEnabledDefault);
            set => Preferences.Default.Set(IsPinUnlockEnabledKey, value);
        }

        private const string IsUseInlineAutofillEnabledKey = "pref_isUseInlineAutofillEnabledKey";
        private const bool IsUseInlineAutofillEnabledDefault = false;
        public bool IsUseInlineAutofillEnabled
        {
            get => Preferences.Default.Get(IsUseInlineAutofillEnabledKey, IsUseInlineAutofillEnabledDefault);
            set => Preferences.Default.Set(IsUseInlineAutofillEnabledKey, value);
        }

        private const string PinUnlockHashKey = "pref_pinUnlockHash";
        private const string PinUnlockHashDefault = "";
        public string PinUnlockHash
        {
            get => Preferences.Default.Get(PinUnlockHashKey, PinUnlockHashDefault);
            set => Preferences.Default.Set(PinUnlockHashKey, value);
        }

        private const string MatchThresholdKey = "pref_matchThreshold";
        private const double MatchThresholdDefault = 70;
        public double MatchThreshold
        {
            get => Preferences.Default.Get(MatchThresholdKey, MatchThresholdDefault);
            set => Preferences.Default.Set(MatchThresholdKey, value);
        }

        private const string AppThemeKey = "pref_appTheme";
        private const string AppThemeDefault = "Light";
        public string AppTheme
        {
            get => Preferences.Default.Get(AppThemeKey, AppThemeDefault);
            set => Preferences.Default.Set(AppThemeKey, value);
        }

        private const string CloudSyncProviderKey = "pref_cloudSyncProvider";
        private const string CloudSyncProviderDefault = "";
        public string CloudSyncProvider
        {
            get => Preferences.Default.Get(CloudSyncProviderKey, CloudSyncProviderDefault);
            set => Preferences.Default.Set(CloudSyncProviderKey, value);
        }

        private const string IsCloudSyncEnabledKey = "pref_isCloudSyncEnabled";
        private const bool IsCloudSyncEnabledDefault = false;
        public bool IsCloudSyncEnabled
        {
            get => Preferences.Default.Get(IsCloudSyncEnabledKey, IsCloudSyncEnabledDefault);
            set => Preferences.Default.Set(IsCloudSyncEnabledKey, value);
        }

        private const string CloudSyncScheduleKey = "pref_cloudSyncSchedule";
        // Default = Daily (2) — backs up once a day automatically
        private const int CloudSyncScheduleDefault = (int)Fortress.Mobile.Core.Contracts.SyncSchedule.Daily;
        public Fortress.Mobile.Core.Contracts.SyncSchedule CloudSyncSchedule
        {
            get => (Fortress.Mobile.Core.Contracts.SyncSchedule)Preferences.Default.Get(CloudSyncScheduleKey, CloudSyncScheduleDefault);
            set => Preferences.Default.Set(CloudSyncScheduleKey, (int)value);
        }

        private const string LockOnBackgroundKey = "pref_lockOnBackground";
        private const bool LockOnBackgroundDefault = false;
        public bool LockOnBackground
        {
            get => Preferences.Default.Get(LockOnBackgroundKey, LockOnBackgroundDefault);
            set => Preferences.Default.Set(LockOnBackgroundKey, value);
        }

        private const string FollowSystemThemeKey = "pref_followSystemTheme";
        private const bool FollowSystemThemeDefault = false;
        public bool FollowSystemTheme
        {
            get => Preferences.Default.Get(FollowSystemThemeKey, FollowSystemThemeDefault);
            set => Preferences.Default.Set(FollowSystemThemeKey, value);
        }

        private const string MaxFailedUnlockAttemptsKey = "pref_maxFailedUnlockAttempts";
        private const int MaxFailedUnlockAttemptsDefault = 5;
        public int MaxFailedUnlockAttempts
        {
            get => Preferences.Default.Get(MaxFailedUnlockAttemptsKey, MaxFailedUnlockAttemptsDefault);
            set => Preferences.Default.Set(MaxFailedUnlockAttemptsKey, value);
        }

        private const string FailedUnlockAttemptCountKey = "pref_failedUnlockAttemptCount";
        private const int FailedUnlockAttemptCountDefault = 0;
        public int FailedUnlockAttemptCount
        {
            get => Preferences.Default.Get(FailedUnlockAttemptCountKey, FailedUnlockAttemptCountDefault);
            set => Preferences.Default.Set(FailedUnlockAttemptCountKey, value);
        }

        private const string IsAccessibilityAutofillEnabledKey = "pref_isAccessibilityAutofillEnabled";
        private const bool IsAccessibilityAutofillEnabledDefault = false;
        /// <summary>
        /// Mirrors the user's intent for accessibility autofill.
        /// Note: the actual service state is controlled by Android system settings —
        /// this pref just tracks whether the user has ever deliberately enabled it,
        /// so we can show the correct toggle state in the UI.
        /// </summary>
        public bool IsAccessibilityAutofillEnabled
        {
            get => Preferences.Default.Get(IsAccessibilityAutofillEnabledKey, IsAccessibilityAutofillEnabledDefault);
            set => Preferences.Default.Set(IsAccessibilityAutofillEnabledKey, value);
        }

        #endregion

        #region State

        private const string FirstLaunchKey = "firstLaunch";
        private const bool FirstLaunchDefault = true;
        public bool FirstLaunch
        {
            get => Preferences.Default.Get(FirstLaunchKey, FirstLaunchDefault);
            set => Preferences.Default.Set(FirstLaunchKey, value);
        }

        private const string IsApplicationClosedKey = "isApplicationClosed";
        private const bool IsApplicationClosedDefault = false;
        public bool IsApplicationClosed
        {
            get => Preferences.Default.Get(IsApplicationClosedKey, IsApplicationClosedDefault);
            set => Preferences.Default.Set(IsApplicationClosedKey, value);
        }

        private const string HasSetupCompletedKey = "hasSetupCompleted";
        private const bool HasSetupCompletedDefault = false;
        public bool HasSetupCompleted
        {
            get => Preferences.Default.Get(HasSetupCompletedKey, HasSetupCompletedDefault);
            set => Preferences.Default.Set(HasSetupCompletedKey, value);
        }

        public void CleanAll() => Preferences.Default.Clear();

        #endregion

        // ── Autofill blocked URIs ─────────────────────────────────────────────────
        // JSON-serialised list of URI strings the user has chosen to suppress.
        // e.g. ["https://bank.com", "androidapp://com.example.app"]
        private const string AutofillBlockedUrisKey = "pref_autofillBlockedUris";
        private const string AutofillBlockedUrisDefault = "[]";

        public List<string> AutofillBlockedUris
        {
            get
            {
                var json = Preferences.Default.Get(AutofillBlockedUrisKey, AutofillBlockedUrisDefault);
                try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
                catch { return new(); }
            }
            set
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
                Preferences.Default.Set(AutofillBlockedUrisKey, json);
            }
        }

        /// <summary>Adds a URI to the user-blocked list. Returns true when newly added.</summary>
        public bool BlockAutofillUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return false;
            var list = AutofillBlockedUris;
            if (list.Contains(uri, StringComparer.OrdinalIgnoreCase)) return false;
            list.Add(uri.ToLowerInvariant());
            AutofillBlockedUris = list;
            return true;
        }

        /// <summary>Removes a URI from the user-blocked list. Returns true when removed.</summary>
        public bool UnblockAutofillUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return false;
            var list = AutofillBlockedUris;
            var removed = list.RemoveAll(u => u.Equals(uri, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) AutofillBlockedUris = list;
            return removed > 0;
        }

        /// <summary>Returns true when autofill should be suppressed for the given URI.</summary>
        public bool IsAutofillBlockedFor(string uri) =>
   !string.IsNullOrWhiteSpace(uri) &&
 AutofillBlockedUris.Any(u => u.Equals(uri, StringComparison.OrdinalIgnoreCase));

        // ── Autofill risk-accepted allowlist ──────────────────────────────────────
        // Stores "credentialId::requestingUri" pairs the user has explicitly
  // accepted the risk warning for, so they are never flagged again.
        private const string AutofillRiskAcceptedKey = "pref_autofillRiskAccepted";
        private const string AutofillRiskAcceptedDefault = "[]";

        private List<string> AutofillRiskAcceptedList
        {
         get
            {
          var json = Preferences.Default.Get(AutofillRiskAcceptedKey, AutofillRiskAcceptedDefault);
   try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
             catch { return new(); }
            }
            set
       {
       var json = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
      Preferences.Default.Set(AutofillRiskAcceptedKey, json);
            }
        }

        private static string RiskAcceptedKey(Guid credentialId, string requestingUri) =>
            credentialId.ToString();

        /// <summary>
        /// Marks a credential+URI pair as user-accepted so the ML risk gate
        /// will not block it again on future fills.
    /// </summary>
        public void AcceptAutofillRisk(Guid credentialId, string requestingUri)
        {
 var key = RiskAcceptedKey(credentialId, requestingUri);
            var list = AutofillRiskAcceptedList;
        if (!list.Contains(key, StringComparer.Ordinal))
  {
        list.Add(key);
     AutofillRiskAcceptedList = list;
       }
    }

        /// <summary>
        /// Returns true when the user has previously accepted the risk for this
        /// credential+URI pair — the ML gate should be skipped.
        /// </summary>
        public bool IsAutofillRiskAccepted(Guid credentialId, string requestingUri) =>
      AutofillRiskAcceptedList.Contains(
           RiskAcceptedKey(credentialId, requestingUri),
  StringComparer.Ordinal);

        private const string IsPasskeyProviderEnabledKey = "pref_isPasskeyProviderEnabled";
        private const bool IsPasskeyProviderEnabledDefault = false;
        public bool IsPasskeyProviderEnabled
        {
          get => Preferences.Default.Get(IsPasskeyProviderEnabledKey, IsPasskeyProviderEnabledDefault);
          set => Preferences.Default.Set(IsPasskeyProviderEnabledKey, value);
        }

     // ── Autofill authentication guards ────────────────────────────────────────

      private const string RequireAuthForPasswordFillKey = "pref_requireAuthForPasswordFill";
    private const bool RequireAuthForPasswordFillDefault = false;
   /// <summary>
    /// When true, every autofill of a password that has
        /// <see cref="LoginItem.RequireAuthBeforeFill"/> set must pass biometric/PIN first.
  /// </summary>
        public bool RequireAuthForPasswordFill
        {
            get => Preferences.Default.Get(RequireAuthForPasswordFillKey, RequireAuthForPasswordFillDefault);
   set => Preferences.Default.Set(RequireAuthForPasswordFillKey, value);
        }

        private const string RequireAuthForCardFillKey = "pref_requireAuthForCardFill";
        private const bool RequireAuthForCardFillDefault = false;
        /// <summary>
        /// When true, every autofill of a card that has
        /// <see cref="CreditCardItem.RequireAuthBeforeFill"/> set must pass biometric/PIN first.
      /// </summary>
   public bool RequireAuthForCardFill
    {
        get => Preferences.Default.Get(RequireAuthForCardFillKey, RequireAuthForCardFillDefault);
            set => Preferences.Default.Set(RequireAuthForCardFillKey, value);
        }

        private PreferenceWrapper() : base() { }

        private static readonly Lazy<PreferenceWrapper> lazy =
            new Lazy<PreferenceWrapper>(() => new PreferenceWrapper());

        public static PreferenceWrapper Instance => lazy.Value;
    }
}
