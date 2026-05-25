using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Utilities;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// iOS-specific service to write credentials to the shared App Group storage
    /// for access by the Autofill extension.
    /// </summary>
    public class SharedCredentialWriter : ISharedCredentialWriter
    {
        private const string AppGroup = "group.com.fortress";
        private const string CredentialsFileName = "autofill_credentials.json";
        private const string PendingSavesFileName = "pending_save_credentials.json";
        
        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly ILogger<SharedCredentialWriter> _logger;
        private string? _containerPath;
        
        public SharedCredentialWriter(
            IDataStorageService dataStorageService,
            ICryptographyService cryptographyService,
            ILogger<SharedCredentialWriter> logger)
        {
            _dataStorageService = dataStorageService;
            _cryptographyService = cryptographyService;
            _logger = logger;
            
            InitializeContainer();
        }
        
        private void InitializeContainer()
        {
            try
            {
                var containerUrl = NSFileManager.DefaultManager.GetContainerUrl(AppGroup);
                if (containerUrl != null)
                {
                    _containerPath = containerUrl.Path;
                    _logger.LogInformation($"SharedCredentialWriter initialized. Container path: {_containerPath}");
                }
                else
                {
                    _logger.LogError($"Failed to get App Group container URL for '{AppGroup}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SharedCredentialWriter initialization failed");
            }
        }
        
        /// <summary>
        /// Gets the file path for the shared credentials file
        /// </summary>
        private string? GetCredentialsFilePath()
        {
            if (string.IsNullOrEmpty(_containerPath)) return null;
            return Path.Combine(_containerPath, CredentialsFileName);
        }
        
        /// <summary>
        /// Syncs all autofill-compatible credentials to the shared storage
        /// </summary>
        public async Task SyncCredentialsToSharedStorageAsync()
        {
            try
            {
                var filePath = GetCredentialsFilePath();
                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogError("Cannot sync credentials: Container path not available");
                    return;
                }
                
                // Get all web, app and OTP credentials that can be used for autofill
                // (same types as Android CredentialResolver)
                var credentials = await _dataStorageService.GetLoginItemsAsync(
                    c => c.LoginType == LoginType.Web ||
                         c.LoginType == LoginType.PhoneApp);

                var sharedCredentials = new List<SharedCredentialData>();

                foreach (var cred in credentials)
                {
                    try
                    {
                        // Decrypt the password - Password field is encrypted, not Data
                        string decryptedPassword = string.Empty;
                        if (!string.IsNullOrEmpty(cred.Password))
                        {
                            var result = await _cryptographyService.Decrypt(cred.Password);
                            if (result.Succeeded) decryptedPassword = result.Data;
                        }

                        bool hasOtp = !string.IsNullOrEmpty(cred.OtpSecret);
                        string? data = null;
                        if (hasOtp)
                        {
                            var decryptResult = await _cryptographyService.Decrypt(cred.OtpSecret);
                            data = decryptResult.Succeeded ? decryptResult.Data : cred.OtpSecret;
                        }

                        var sharedCred = new SharedCredentialData
                        {
                            Id  = cred.Id,
                            Domain         = cred.Url ?? cred.Label ?? string.Empty,
                            Username       = cred.Username ?? string.Empty,
                            Password       = decryptedPassword,
                            HasOtp         = hasOtp,
                            Data      = data,
                            CredentialType = cred.LoginType.ToString(),
                            IconUri        = GetIconUri(cred),
                            FallbackIcon   = GetFallbackIcon(cred)
                        };
      sharedCredentials.Add(sharedCred);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process credential: {cred.Id}");
                    }
                }
                
                // Write to shared storage
                var json = JsonConvert.SerializeObject(sharedCredentials, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.LogInformation($"Synced {sharedCredentials.Count} credentials to shared storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync credentials to shared storage");
            }
        }
        
        /// <summary>
        /// Writes the lock state to shared preferences
        /// </summary>
        public void SyncLockStateToSharedPreferences()
        {
            try
            {
                var defaults = new NSUserDefaults(AppGroup, NSUserDefaultsType.SuiteName);
                if (defaults == null)
                {
                    _logger.LogError("Failed to get App Group defaults");
                    return;
                }
                
                // Sync lock-related preferences
                defaults.SetBool(PreferenceWrapper.Instance.IsApplicationLocked, "pref_applicationLocked");
                defaults.SetBool(PreferenceWrapper.Instance.IsBiometricUnlockEnabled, "pref_isBiometricLockedKey");
                defaults.SetBool(PreferenceWrapper.Instance.IsPinUnlockEnabled, "pref_isPinUnlockKey");
                defaults.SetBool(PreferenceWrapper.Instance.HasSetupCompleted, "hasSetupCompleted");
                defaults.SetBool(PreferenceWrapper.Instance.IsCopyTOTPOnAutofill, "pref_isCopyTOTPOnAutofill");
                
                // Sync match threshold for credential matching (same as Android)
                defaults.SetInt((nint)PreferenceWrapper.Instance.MatchThreshold, "pref_matchThreshold");
                
                // Sync database password for decryption (secure since it's in App Group)
                if (!string.IsNullOrEmpty(PreferenceWrapper.Instance.DatabasePassword))
                {
                    defaults.SetString(PreferenceWrapper.Instance.DatabasePassword, "pref_databasePassword");
                }
                
                defaults.Synchronize();
                
                _logger.LogInformation("Lock state synced to shared preferences");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync lock state to shared preferences");
            }
        }
        
        /// <summary>
        /// Clears all shared data (call on logout)
        /// </summary>
        public void ClearSharedData()
        {
            try
            {
                // Clear credentials file
                var filePath = GetCredentialsFilePath();
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                // Clear shared preferences
                var defaults = new NSUserDefaults(AppGroup, NSUserDefaultsType.SuiteName);
                if (defaults != null)
                {
                    defaults.RemoveObject("pref_applicationLocked");
                    defaults.RemoveObject("pref_isBiometricLockedKey");
                    defaults.RemoveObject("pref_isPinUnlockKey");
                    defaults.RemoveObject("hasSetupCompleted");
                    defaults.RemoveObject("pref_databasePassword");
                    defaults.Synchronize();
                }
                
                _logger.LogInformation("Shared data cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear shared data");
            }
        }
        
        /// <summary>
        /// Checks if the autofill extension is enabled in iOS Settings
        /// This value is set by the extension when PrepareInterfaceForExtensionConfiguration is called
        /// </summary>
        public bool IsAutofillEnabled()
        {
            try
            {
                var defaults = new NSUserDefaults(AppGroup, NSUserDefaultsType.SuiteName);
                if (defaults == null)
                {
                    _logger.LogWarning("Failed to get App Group defaults for IsAutofillEnabled check");
                    return false;
                }
                
                // Check if the key exists first
                if (defaults["pref_isAutofillEnabled"] == null)
                    return false;
                
                return defaults.BoolForKey("pref_isAutofillEnabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if autofill is enabled");
                return false;
            }
        }
        
        private const string UsageEventsFileName = "autofill_usage_events.json";
        
        /// <summary>
        /// Gets the file path for the usage events file
        /// </summary>
        private string? GetUsageEventsFilePath()
        {
            if (string.IsNullOrEmpty(_containerPath)) return null;
            return Path.Combine(_containerPath, UsageEventsFileName);
        }
        
        /// <summary>
        /// Processes pending credential usage events written by the extension
        /// (like Android's OnCredentialUsed that logs to EventLogProcessor)
        /// </summary>
        public async Task ProcessPendingUsageEventsAsync()
        {
            try
            {
                var filePath = GetUsageEventsFilePath();
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return; // No pending events
                }
                
                var json = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }
                
                var events = JsonConvert.DeserializeObject<List<SharedCredentialUsageEvent>>(json);
                if (events == null || !events.Any())
                {
                    return;
                }
                
                _logger.LogInformation($"Processing {events.Count} pending credential usage events from extension");
                
                var eventLogProcessor = Shiny.Hosting.Host.GetService<IEventLogProcessor>();
                
                foreach (var evt in events)
                {
                    try
                    {
                        var credType = Enum.TryParse<Models.CredentialType>(evt.CredentialType, out var type) 
                            ? type : Models.CredentialType.Web;
                        
                        var eventLogType = credType == Models.CredentialType.PhoneApplication 
                            ? Models.EventLogType.PhonePasswordUsed 
                            : Models.EventLogType.WebCredentialUsed;
                        
                        await eventLogProcessor.ProcessEventLogAsync(new Models.EventLog
                        {
                            CredentialId = evt.CredentialId,
                            EventType = (int)eventLogType
                        });
                        
                        _logger.LogInformation($"Logged usage event for credential {evt.CredentialId} ({evt.Domain})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process usage event for credential {evt.CredentialId}");
                    }
                }
                
                // Clear the events file after processing
                File.Delete(filePath);
                _logger.LogInformation("Cleared processed usage events");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process pending usage events");
            }
        }
        
        /// <summary>
        /// Gets the icon URL for a credential (same logic as CredentialMapper.GetIcon)
        /// </summary>
        private string GetIconUri(LoginItem credential)
        {
            return credential.LoginType switch
     {
       LoginType.Web or LoginType.PhoneApp =>
     $"https://hubfunctions-us.azurewebsites.net/api/LogoService/Icon?domain={FixUrl(credential.Url ?? string.Empty)}&size=64",
  LoginType.DesktopApp => "applicationlogo.png",
    _ => "gklogoblue64.png"
       };
        }
        
        /// <summary>
        /// Gets the fallback icon for a credential (same logic as CredentialMapper.GetFallbackIcon)
        /// </summary>
        private string GetFallbackIcon(LoginItem credential)
        {
             return credential.LoginType switch
      {
    LoginType.Web     => "weblogo.png",
  LoginType.PhoneApp => "phoneapplogo.png",
     LoginType.DesktopApp => "applicationlogo.png",
          _ => "gklogoblue64.png"
      };
        }
        
        /// <summary>
        /// Cleans up URL to extract domain for icon lookup (same as CredentialMapper.FixUrl)
        /// </summary>
        private string FixUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;
            
            try
            {
                // Add scheme if missing
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }
                
                return url;
            }
            catch
            {
                return url;
            }
        }

        public List<Contracts.PendingSaveItem> GetPendingSaveCredentials()
        {
            try
            {
                if (string.IsNullOrEmpty(_containerPath)) return new List<Contracts.PendingSaveItem>();

                var filePath = Path.Combine(_containerPath, PendingSavesFileName);
                if (!File.Exists(filePath)) return new List<Contracts.PendingSaveItem>();

                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return new List<Contracts.PendingSaveItem>();

                // The extension writes PendingSaveCredential (lowercase JSON names).
                var raw = JsonConvert.DeserializeObject<List<PendingSaveRaw>>(json);
                return raw?
                    .Select(r => new Contracts.PendingSaveItem
                    {
                        Domain   = r.Domain ?? string.Empty,
                        Username = r.Username ?? string.Empty,
                        Password = r.Password ?? string.Empty
                    })
                    .ToList() ?? new List<Contracts.PendingSaveItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingSaveCredentials failed");
                return new List<Contracts.PendingSaveItem>();
            }
        }

        public void ClearFirstPendingSaveCredential()
        {
            try
            {
                if (string.IsNullOrEmpty(_containerPath)) return;

                var filePath = Path.Combine(_containerPath, PendingSavesFileName);
                if (!File.Exists(filePath)) return;

                var json  = File.ReadAllText(filePath);
                var items = JsonConvert.DeserializeObject<List<PendingSaveRaw>>(json);
                if (items == null || items.Count == 0)
                {
                    File.Delete(filePath);
                    return;
                }

                items.RemoveAt(0);

                if (items.Count == 0)
                    File.Delete(filePath);
                else
                    File.WriteAllText(filePath, JsonConvert.SerializeObject(items));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClearFirstPendingSaveCredential failed");
            }
        }

        // Local mirror of the extension's PendingSaveCredential JSON shape
        private sealed class PendingSaveRaw
        {
      [JsonProperty("domain")]   public string? Domain   { get; set; }
         [JsonProperty("username")] public string? Username { get; set; }
 [JsonProperty("password")] public string? Password { get; set; }
        }
    }

    /// <summary>Data model for credentials stored in the shared JSON file</summary>
    public class SharedCredentialData
    {
     public Guid    Id   { get; set; }
  public string  Domain       { get; set; } = string.Empty;
        public string  Username       { get; set; } = string.Empty;
        public string  Password       { get; set; } = string.Empty;
        public bool    HasOtp         { get; set; }
        public string? Data         { get; set; }
       public string  CredentialType { get; set; } = string.Empty;
      public string? IconUri   { get; set; }
        public string? FallbackIcon   { get; set; }
    }

    /// <summary>Data model for credential usage events written by the extension</summary>
    public class SharedCredentialUsageEvent
{
        public Guid     CredentialId   { get; set; }
        public string   CredentialType { get; set; } = string.Empty;
        public DateTime UsedAt         { get; set; }
        public string?  Domain         { get; set; }
    }
}
