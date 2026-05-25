namespace Fortress.Mobile.Core.Models
{
    public static class ConstantKeys
    {
        public const string AndroidAppProtocol = "androidapp://";
        public const string iOSAppProtocol = "iosapp://";
        public static string SyncOnRefreshKey = "syncOnRefresh";
        public static string VaultTimeoutKey = "lockOption";
        public static string VaultTimeoutActionKey = "vaultTimeoutAction";
        public static string LastActiveTimeKey = "lastActiveTime";
        public static string BiometricUnlockKey = "fingerprintUnlock";
        public static string ProtectedPin = "protectedPin";
        public static string PinProtectedKey = "pinProtectedKey";
        public static string DefaultUriMatch = "defaultUriMatch";
        public static string DisableAutoTotpCopyKey = "disableAutoTotpCopy";
        public static string EnvironmentUrlsKey = "environmentUrls";
        public static string LastFileCacheClearKey = "lastFileCacheClear";
        public static string AutofillDisableSavePromptKey = "autofillDisableSavePrompt";
        public static string AutofillBlacklistedUrisKey = "autofillBlacklistedUris";
        public static string AutofillTileAdded = "autofillTileAdded";
        public static string DisableFaviconKey = "disableFavicon";
        public static string PushRegisteredTokenKey = "pushRegisteredToken";
        public static string PushCurrentTokenKey = "pushCurrentToken";
        public static string PushLastRegistrationDateKey = "pushLastRegistrationDate";
        public static string PushInitialPromptShownKey = "pushInitialPromptShown";
        public static string ThemeKey = "theme";
        public static string ClearClipboardKey = "clearClipboard";
        public static string LastBuildKey = "lastBuild";
        public static string OldUserIdKey = "userId";
        public static string AddSitePromptShownKey = "addSitePromptShown";
        public static string ClearCiphersCacheKey = "clearCiphersCache";
        public static string BiometricIntegrityKey = "biometricIntegrityState";
        public static string iOSAutoFillClearCiphersCacheKey = "iOSAutoFillClearCiphersCache";
        public static string iOSAutoFillBiometricIntegrityKey = "iOSAutoFillBiometricIntegrityState";
        public static string iOSExtensionClearCiphersCacheKey = "iOSExtensionClearCiphersCache";
        public static string iOSExtensionBiometricIntegrityKey = "iOSExtensionBiometricIntegrityState";
        public static string MigratedFromV1 = "migratedFromV1";
        public static string MigratedFromV1AutofillPromptShown = "migratedV1AutofillPromptShown";
        public static string TriedV1Resync = "triedV1Resync";
        public static string EventCollectionKey = "eventCollection";
        public static string PreviousPageKey = "previousPage";
        public static string InlineAutofillEnabledKey = "inlineAutofillEnabled";
        public static string InvalidUnlockAttempts = "invalidUnlockAttempts";
        public static string PasswordRepromptAutofillKey = "passwordRepromptAutofillKey";
        public static string PasswordVerifiedAutofillKey = "passwordVerifiedAutofillKey";
        public const int SelectFileRequestCode = 42;
        public const int SelectFilePermissionRequestCode = 43;
        public const int SaveFileRequestCode = 44;

        public static readonly string[] AndroidAllClearCipherCacheKeys =
        {
            ClearCiphersCacheKey
        };

        public static readonly string[] iOSAllClearCipherCacheKeys =
        {
            ClearCiphersCacheKey,
            iOSAutoFillClearCiphersCacheKey,
            iOSExtensionClearCiphersCacheKey
        };
    }
    public static class Constants
    {
         public static string LastActiveTime = "lastActiveTime";
        public static string VaultTimeoutKey = "lockOption";
        public static string VaultTimeoutActionKey = "vaultTimeoutAction";
        public const int MaxAccounts = 5;
        public const string AndroidAppProtocol = "androidapp://";
        public const string iOSAppProtocol = "iosapp://";
        public static string StateVersionKey = "stateVersion";
        public static string StateKey = "state";
        public static string PreAuthEnvironmentUrlsKey = "preAuthEnvironmentUrls";
        public static string LastFileCacheClearKey = "lastFileCacheClear";
        public static string AutofillTileAdded = "autofillTileAdded";
        public static string PushRegisteredTokenKey = "pushRegisteredToken";
        public static string PushCurrentTokenKey = "pushCurrentToken";
        public static string PushLastRegistrationDateKey = "pushLastRegistrationDate";
        public static string PushInitialPromptShownKey = "pushInitialPromptShown";
        public static string PushInstallationRegistrationErrorKey = "pushInstallationRegistrationError";
        public static string LastBuildKey = "lastBuild";
        public static string AddSitePromptShownKey = "addSitePromptShown";
        public static string ClearCiphersCacheKey = "clearCiphersCache";
        public static string BiometricIntegrityKey = "biometricIntegrityState";
        public static string iOSAutoFillClearCiphersCacheKey = "iOSAutoFillClearCiphersCache";
        public static string iOSAutoFillBiometricIntegrityKey = "iOSAutoFillBiometricIntegrityState";
        public static string iOSExtensionClearCiphersCacheKey = "iOSExtensionClearCiphersCache";
        public static string iOSExtensionBiometricIntegrityKey = "iOSExtensionBiometricIntegrityState";
        public static string iOSShareExtensionClearCiphersCacheKey = "iOSShareExtensionClearCiphersCache";
        public static string iOSShareExtensionBiometricIntegrityKey = "iOSShareExtensionBiometricIntegrityState";
        public static string EventCollectionKey = "eventCollection";
        public static string RememberedEmailKey = "rememberedEmail";
        public static string RememberedOrgIdentifierKey = "rememberedOrgIdentifier";
        public const int SelectFileRequestCode = 42;
        public const int SelectFilePermissionRequestCode = 43;
        public const int SaveFileRequestCode = 44;

        public static readonly string[] AndroidAllClearCipherCacheKeys =
        {
            ClearCiphersCacheKey
        };

        public static readonly string[] iOSAllClearCipherCacheKeys =
        {
            ClearCiphersCacheKey,
            iOSAutoFillClearCiphersCacheKey,
            iOSExtensionClearCiphersCacheKey,
            iOSShareExtensionClearCiphersCacheKey
        };

        public static string CiphersKey(string userId) => $"ciphers_{userId}";
        public static string FoldersKey(string userId) => $"folders_{userId}";
        public static string CollectionsKey(string userId) => $"collections_{userId}";
        public static string OrganizationsKey(string userId) => $"organizations_{userId}";
        public static string LocalDataKey(string userId) => $"ciphersLocalData_{userId}";
        public static string NeverDomainsKey(string userId) => $"neverDomains_{userId}";
        public static string SendsKey(string userId) => $"sends_{userId}";
        public static string PoliciesKey(string userId) => $"policies_{userId}";
        public static string KeyKey(string userId) => $"key_{userId}";
        public static string EncOrgKeysKey(string userId) => $"encOrgKeys_{userId}";
        public static string EncPrivateKeyKey(string userId) => $"encPrivateKey_{userId}";
        public static string EncKeyKey(string userId) => $"encKey_{userId}";
        public static string KeyHashKey(string userId) => $"keyHash_{userId}";
        public static string PinProtectedKey(string userId) => $"pinProtectedKey_{userId}";
        public static string PassGenOptionsKey(string userId) => $"passwordGenerationOptions_{userId}";
        public static string PassGenHistoryKey(string userId) => $"generatedPasswordHistory_{userId}";
        public static string TwoFactorTokenKey(string email) => $"twoFactorToken_{email}";
        public static string LastActiveTimeKey(string userId) => $"lastActiveTime_{userId}";
        public static string InvalidUnlockAttemptsKey(string userId) => $"invalidUnlockAttempts_{userId}";
        public static string InlineAutofillEnabledKey(string userId) => $"inlineAutofillEnabled_{userId}";
        public static string AutofillDisableSavePromptKey(string userId) => $"autofillDisableSavePrompt_{userId}";
        public static string AutofillBlacklistedUrisKey(string userId) => $"autofillBlacklistedUris_{userId}";
        public static string ClearClipboardKey(string userId) => $"clearClipboard_{userId}";
        public static string SyncOnRefreshKey(string userId) => $"syncOnRefresh_{userId}";
        public static string DisableFaviconKey(string userId) => $"disableFavicon_{userId}";
        public static string DefaultUriMatchKey(string userId) => $"defaultUriMatch_{userId}";
        public static string ThemeKey(string userId) => $"theme_{userId}";
        public static string DisableAutoTotpCopyKey(string userId) => $"disableAutoTotpCopy_{userId}";
        public static string PreviousPageKey(string userId) => $"previousPage_{userId}";
        public static string PasswordRepromptAutofillKey(string userId) => $"passwordRepromptAutofillKey_{userId}";
        public static string PasswordVerifiedAutofillKey(string userId) => $"passwordVerifiedAutofillKey_{userId}";
        public static string SettingsKey(string userId) => $"settings_{userId}";
        public static string UsesKeyConnectorKey(string userId) => $"usesKeyConnector_{userId}";
        public static string ProtectedPinKey(string userId) => $"protectedPin_{userId}";
        public static string LastSyncKey(string userId) => $"lastSync_{userId}";
        public static string BiometricUnlockKey(string userId) => $"biometricUnlock_{userId}";
    }
}
