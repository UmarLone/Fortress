using Fortress.Core.Contracts;

namespace Fortress.Core.Services
{
    /// <summary>
    /// Platform-agnostic preference wrapper. Uses <see cref="IPreferenceService"/>
    /// instead of Microsoft.Maui.Storage.Preferences � compiles on any TFM.
    /// </summary>
    public sealed class FortressPreferenceWrapper : BasePreferenceWrapper
    {
    // ── Autofill ──────────────────────────────────────────────────────────
        public bool IsSavePromptDisabled
        {
   get => Prefs.Get("pref_isSavePromptDisabled", false);
     set => Prefs.Set("pref_isSavePromptDisabled", value);
        }
   public bool IsCopyTOTPOnAutofill
        {
      get => Prefs.Get("pref_isCopyTOTPOnAutofill", true);
       set => Prefs.Set("pref_isCopyTOTPOnAutofill", value);
        }
        public bool IsScreenCaptureEnabled
        {
    get => Prefs.Get("pref_screenCaptureEnabled", false);
     set => Prefs.Set("pref_screenCaptureEnabled", value);
        }
        public bool AllowBiometrics
    {
  get => Prefs.Get("pref_allowBiometrics", false);
     set => Prefs.Set("pref_allowBiometrics", value);
  }
  public bool SendDiagnosticLogs
        {
      get => Prefs.Get("pref_sendDiagnosticLogsKey", true);
  set => Prefs.Set("pref_sendDiagnosticLogsKey", value);
        }

        // ── Security ──────────────────────────────────────────────────────────
        private ICryptographyService? _cryptoService;

     /// <summary>
     /// The PBKDF2 verifier used to verify the master password at unlock.
        /// Format: "v1:{saltBase64}:{hashBase64}" or legacy plaintext.
        /// Do NOT use this as the crypto key � use <see cref="MasterPasswordForCrypto"/> instead.
        /// </summary>
        public string DatabasePassword
   {
        get => Prefs.Get("pref_databasePassword", string.Empty);
   set
       {
  Prefs.Set("pref_databasePassword", value);
 try { _cryptoService?.InvalidateKeyCache(); } catch { }
     }
        }

        /// <summary>
        /// The actual master password used only for AES key derivation by CryptographyService.
        /// Stored separately from the verifier so CryptographyService always derives from the
      /// real password even after DatabasePassword is upgraded to a PBKDF2 verifier hash.
        /// </summary>
  public string MasterPasswordForCrypto
        {
        get => Prefs.Get("pref_masterKeyPlain", string.Empty);
            set
            {
       Prefs.Set("pref_masterKeyPlain", value);
  try { _cryptoService?.InvalidateKeyCache(); } catch { }
     }
        }

/// <summary>Inject after construction so the wrapper can evict the key cache on password change.</summary>
   public void SetCryptographyService(ICryptographyService svc) => _cryptoService = svc;

     public int LockTimeout
     {
get => Prefs.Get("pref_lockTimeout", 300);
 set => Prefs.Set("pref_lockTimeout", value);
        }
    public int ClearClipboardTimeout
     {
         get => Prefs.Get("pref_clearClipboardTimeout", 0);
   set => Prefs.Set("pref_clearClipboardTimeout", value);
     }
     public bool IsApplicationLocked
  {
            get => Prefs.Get("pref_applicationLocked", false);
 set => Prefs.Set("pref_applicationLocked", value);
    }
     public bool PreventLocking
  {
     get => Prefs.Get("pref_preventLocking", false);
         set => Prefs.Set("pref_preventLocking", value);
      }
     public bool IsBiometricUnlockEnabled
        {
          get => Prefs.Get("pref_isBiometricLockedKey", false);
   set => Prefs.Set("pref_isBiometricLockedKey", value);
        }
        public bool IsPinUnlockEnabled
        {
   get => Prefs.Get("pref_isPinUnlockKey", false);
   set => Prefs.Set("pref_isPinUnlockKey", value);
        }
        public string PinUnlockHash
        {
  get => Prefs.Get("pref_pinUnlockHash", string.Empty);
   set => Prefs.Set("pref_pinUnlockHash", value);
        }
    public bool IsUseInlineAutofillEnabled
      {
         get => Prefs.Get("pref_isUseInlineAutofillEnabledKey", false);
          set => Prefs.Set("pref_isUseInlineAutofillEnabledKey", value);
       }
     public int MaxFailedUnlockAttempts
        {
     get => Prefs.Get("pref_maxFailedUnlockAttempts", 5);
      set => Prefs.Set("pref_maxFailedUnlockAttempts", value);
        }
        public int FailedUnlockAttemptCount
        {
   get => Prefs.Get("pref_failedUnlockAttemptCount", 0);
set => Prefs.Set("pref_failedUnlockAttemptCount", value);
        }
        public bool RequireAuthForPasswordFill
     {
   get => Prefs.Get("pref_requireAuthForPasswordFill", false);
 set => Prefs.Set("pref_requireAuthForPasswordFill", value);
 }
        public bool RequireAuthForCardFill
        {
            get => Prefs.Get("pref_requireAuthForCardFill", false);
            set => Prefs.Set("pref_requireAuthForCardFill", value);
        }

      // ── Matching ──────────────────────────────────────────────────────────
  public double MatchThreshold
        {
 get => Prefs.Get("pref_matchThreshold", 70.0);
   set => Prefs.Set("pref_matchThreshold", value);
    }

     // ── Theme ─────────────────────────────────────────────────────────────
        public string AppTheme
     {
    get => Prefs.Get("pref_appTheme", "Light");
     set => Prefs.Set("pref_appTheme", value);
        }
     public bool FollowSystemTheme
  {
         get => Prefs.Get("pref_followSystemTheme", false);
  set => Prefs.Set("pref_followSystemTheme", value);
     }
        public int ColorSetIndex
        {
    get => Prefs.Get("pref_colorSetIndex", 0);
      set => Prefs.Set("pref_colorSetIndex", value);
      }

     // ── Cloud sync ────────────────────────────────────────────────────────
      public string CloudSyncProvider
        {
   get => Prefs.Get("pref_cloudSyncProvider", string.Empty);
      set => Prefs.Set("pref_cloudSyncProvider", value);
        }
 public bool IsCloudSyncEnabled
       {
     get => Prefs.Get("pref_isCloudSyncEnabled", false);
       set => Prefs.Set("pref_isCloudSyncEnabled", value);
      }
        public SyncSchedule CloudSyncSchedule
        {
    get => (SyncSchedule)Prefs.Get("pref_cloudSyncSchedule", (int)SyncSchedule.Daily);
       set => Prefs.Set("pref_cloudSyncSchedule", (int)value);
       }
        public bool LockOnBackground
        {
   get => Prefs.Get("pref_lockOnBackground", false);
          set => Prefs.Set("pref_lockOnBackground", value);
      }

     // ── State ─────────────────────────────────────────────────────────────
     public bool FirstLaunch
   {
          get => Prefs.Get("firstLaunch", true);
     set => Prefs.Set("firstLaunch", value);
  }
        public bool IsApplicationClosed
     {
          get => Prefs.Get("isApplicationClosed", false);
    set => Prefs.Set("isApplicationClosed", value);
        }
   public bool HasSetupCompleted
        {
    get => Prefs.Get("hasSetupCompleted", false);
            set => Prefs.Set("hasSetupCompleted", value);
}
      public bool IsPasskeyProviderEnabled
        {
          get => Prefs.Get("pref_isPasskeyProviderEnabled", false);
          set => Prefs.Set("pref_isPasskeyProviderEnabled", value);
        }
  public bool IsAccessibilityAutofillEnabled
        {
       get => Prefs.Get("pref_isAccessibilityAutofillEnabled", false);
            set => Prefs.Set("pref_isAccessibilityAutofillEnabled", value);
    }

  // ── Autofill blocked URIs ─────────────────────────────────────────────
        public List<string> AutofillBlockedUris
        {
            get
 {
     var json = Prefs.Get("pref_autofillBlockedUris", "[]");
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
       catch { return new(); }
      }
    set => Prefs.Set("pref_autofillBlockedUris", System.Text.Json.JsonSerializer.Serialize(value ?? new()));
       }
      public bool BlockAutofillUri(string uri)
        {
        if (string.IsNullOrWhiteSpace(uri)) return false;
            var list = AutofillBlockedUris;
         if (list.Contains(uri, StringComparer.OrdinalIgnoreCase)) return false;
          list.Add(uri.ToLowerInvariant());
    AutofillBlockedUris = list;
  return true;
        }
    public bool UnblockAutofillUri(string uri)
        {
    if (string.IsNullOrWhiteSpace(uri)) return false;
        var list = AutofillBlockedUris;
   var removed = list.RemoveAll(u => u.Equals(uri, StringComparison.OrdinalIgnoreCase));
if (removed > 0) AutofillBlockedUris = list;
      return removed > 0;
        }
       public bool IsAutofillBlockedFor(string uri) =>
  !string.IsNullOrWhiteSpace(uri) &&
   AutofillBlockedUris.Any(u => u.Equals(uri, StringComparison.OrdinalIgnoreCase));

       // ── Autofill risk-accepted allowlist ──────────────────────────────────
 private List<string> AutofillRiskAcceptedList
        {
         get
            {
             var json = Prefs.Get("pref_autofillRiskAccepted", "[]");
 try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
   }
   set => Prefs.Set("pref_autofillRiskAccepted", System.Text.Json.JsonSerializer.Serialize(value ?? new()));
        }
        public void AcceptAutofillRisk(Guid credentialId, string requestingUri)
  {
      var key = credentialId.ToString();
     var list = AutofillRiskAcceptedList;
          if (!list.Contains(key, StringComparer.Ordinal)) { list.Add(key); AutofillRiskAcceptedList = list; }
        }
       public bool IsAutofillRiskAccepted(Guid credentialId, string requestingUri) =>
       AutofillRiskAcceptedList.Contains(credentialId.ToString(), StringComparer.Ordinal);

     public void CleanAll() => Prefs.Clear();

        public FortressPreferenceWrapper(IPreferenceService prefs) : base(prefs) { }
  }
}
