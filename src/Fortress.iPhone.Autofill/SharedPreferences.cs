using Foundation;
using System;

namespace Fortress.iPhone.Autofill
{
    /// <summary>
    /// Provides access to shared preferences between the main app and the autofill extension
    /// using App Groups (NSUserDefaults with suite name)
    /// </summary>
    public sealed class SharedPreferences
    {
        private const string AppGroup = "group.com.fortress.passwordmanager";
        
        private static readonly Lazy<SharedPreferences> _instance = 
            new Lazy<SharedPreferences>(() => new SharedPreferences());
        
        public static SharedPreferences Instance => _instance.Value;
        
        private NSUserDefaults? _defaults;
        
        private SharedPreferences()
        {
            try
            {
                _defaults = new NSUserDefaults(AppGroup, NSUserDefaultsType.SuiteName);
                if (_defaults == null)
                {
                    ErrorLogger.LogError("Failed to initialize App Group defaults", new Exception($"App Group '{AppGroup}' not available"));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SharedPreferences initialization failed", ex);
            }
        }
        
        // Lock status keys (matching PreferenceWrapper from main app)
        private const string IsApplicationLockedKey = "pref_applicationLocked";
        private const string IsBiometricUnlockEnabledKey = "pref_isBiometricLockedKey";
        private const string IsPinUnlockEnabledKey = "pref_isPinUnlockKey";
        private const string HasSetupCompletedKey = "hasSetupCompleted";
        private const string DatabasePasswordKey = "pref_databasePassword";
        private const string IsCopyTOTPOnAutofillKey = "pref_isCopyTOTPOnAutofill";
        
        // Autofill extension status key
        private const string IsAutofillEnabledKey = "pref_isAutofillEnabled";
        
        public bool IsApplicationLocked => GetBool(IsApplicationLockedKey, false);
        public bool IsBiometricUnlockEnabled => GetBool(IsBiometricUnlockEnabledKey, false);
        public bool IsPinUnlockEnabled => GetBool(IsPinUnlockEnabledKey, false);
        public bool HasSetupCompleted => GetBool(HasSetupCompletedKey, false);
        public string DatabasePassword => GetString(DatabasePasswordKey, string.Empty);
        public bool IsCopyTOTPOnAutofill => GetBool(IsCopyTOTPOnAutofillKey, true);
        
        /// <summary>
        /// Whether the autofill extension is enabled in iOS Settings
        /// </summary>
        public bool IsAutofillEnabled => GetBool(IsAutofillEnabledKey, false);
        
        /// <summary>
        /// Sets the autofill enabled status (called when extension is configured)
        /// </summary>
        public void SetAutofillEnabled(bool enabled)
        {
            SetBool(IsAutofillEnabledKey, enabled);
            ErrorLogger.LogInfo($"Autofill enabled status set to: {enabled}");
        }
        
        private void SetBool(string key, bool value)
        {
            try
            {
                if (_defaults == null) return;
                _defaults.SetBool(value, key);
                _defaults.Synchronize();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"SetBool failed for key: {key}", ex);
            }
        }
        
        /// <summary>
        /// Determines if the app is locked and requires authentication
        /// </summary>
        public bool IsLocked()
        {
            try
            {
                // Sync once to get latest values from main app
                _defaults?.Synchronize();
                
                var isLocked = IsApplicationLocked;
                var hasBiometric = IsBiometricUnlockEnabled;
                var hasPin = IsPinUnlockEnabled;
                
                // If app is explicitly marked as unlocked, allow access
                if (!isLocked)
                {
                    return false;
                }
                
                // App is locked AND has an unlock method configured
                if (hasBiometric || hasPin)
                {
                    return true;
                }
                
                // No unlock method configured, treat as unlocked
                return false;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("IsLocked check failed", ex);
                return true; // Fail safe - assume locked
            }
        }
        
        private bool GetBool(string key, bool defaultValue)
        {
            try
            {
                if (_defaults == null) return defaultValue;
                
                // Note: Don't call Synchronize() here - call it once before reading multiple values
                // Check if key exists
                if (_defaults[key] == null)
                {
                    return defaultValue;
                }
                
                return _defaults.BoolForKey(key);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"GetBool failed for key: {key}", ex);
                return defaultValue;
            }
        }
        
        private string GetString(string key, string defaultValue)
        {
            try
            {
                if (_defaults == null) return defaultValue;
                return _defaults.StringForKey(key) ?? defaultValue;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"GetString failed for key: {key}", ex);
                return defaultValue;
            }
        }
        
        private int GetInt(string key, int defaultValue)
        {
            try
            {
                if (_defaults == null) return defaultValue;
                
                if (_defaults[key] == null)
                    return defaultValue;
                
                return (int)_defaults.IntForKey(key);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"GetInt failed for key: {key}", ex);
                return defaultValue;
            }
        }
    }
}
