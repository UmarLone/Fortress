using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Fortress.iPhone.Autofill
{
    /// <summary>
    /// Provides access to shared credential storage between the main app and the autofill extension.
    /// Uses App Group container for file-based storage.
    /// Optimized for fast startup in iOS extensions.
    /// </summary>
    public sealed class SharedCredentialStorage
    {
        private const string AppGroup = "group.com.fortress.passwordmanager";
        private const string CredentialsFileName = "autofill_credentials.json";
        
        private static SharedCredentialStorage? _instance;
        private static readonly object _lock = new object();
        
        public static SharedCredentialStorage Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SharedCredentialStorage();
                    }
                }
                return _instance;
            }
        }
        
        private string? _containerPath;
        private List<AutofillCredential>? _cachedCredentials;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        
        private SharedCredentialStorage()
        {
            var containerUrl = NSFileManager.DefaultManager.GetContainerUrl(AppGroup);
            _containerPath = containerUrl?.Path;
        }
        
        /// <summary>
        /// Loads all credentials from the shared storage (synchronous for speed)
        /// </summary>
        public List<AutofillCredential> LoadCredentials()
        {
            // Return cached if available
            if (_cachedCredentials != null)
                return _cachedCredentials;
            
            try
            {
                if (string.IsNullOrEmpty(_containerPath))
                    return new List<AutofillCredential>();
                
                var filePath = Path.Combine(_containerPath, CredentialsFileName);
                
                if (!File.Exists(filePath))
                    return new List<AutofillCredential>();
                
                // Synchronous read is faster for extension cold start
                var json = File.ReadAllText(filePath);
                var credentials = JsonSerializer.Deserialize<List<SharedCredentialData>>(json, _jsonOptions);
                
                if (credentials == null)
                    return new List<AutofillCredential>();
                
                // Map to AutofillCredential
                _cachedCredentials = credentials.Select(c => new AutofillCredential(c.Domain, c.Username, c.Password)
                {
                    Id = c.Id,
                    HasOtp = c.HasOtp,
                    Data = c.Data ?? string.Empty,
                    IconUri = c.IconUri,
                    FallbackIcon = c.FallbackIcon,
                    CredentialType = c.CredentialType ?? string.Empty
                }).ToList();
                
                return _cachedCredentials;
            }
            catch
            {
                return new List<AutofillCredential>();
            }
        }
        
        /// <summary>
        /// Async wrapper for compatibility
        /// </summary>
        public Task<List<AutofillCredential>> LoadCredentialsAsync()
        {
            return Task.FromResult(LoadCredentials());
        }
        
        /// <summary>
        /// Gets credentials matching a specific domain/URL using simple matching
        /// </summary>
        public Task<List<AutofillCredential>> GetMatchingCredentialsAsync(string domain)
        {
            return Task.FromResult(GetMatchingCredentials(domain));
        }
        
        /// <summary>
        /// Gets credentials matching a specific domain (synchronous)
        /// </summary>
        public List<AutofillCredential> GetMatchingCredentials(string domain)
        {
            var allCredentials = LoadCredentials();
            
            if (string.IsNullOrWhiteSpace(domain) || allCredentials.Count == 0)
                return allCredentials;
            
            // Extract the main domain name for matching
            var searchTerms = ExtractSearchTerms(domain);
            if (searchTerms.Count == 0)
                return allCredentials;
            
            // Simple contains-based matching (much faster than N-Gram)
            var matched = new List<(AutofillCredential cred, int score)>();
            
            foreach (var cred in allCredentials)
            {
                var credDomain = cred.Domain?.ToLowerInvariant() ?? "";
                var score = 0;
                
                // Exact match gets highest score
                foreach (var term in searchTerms)
                {
                    if (credDomain.Contains(term))
                    {
                        // Longer matches = higher score
                        score += term.Length * 10;
                        
                        // Bonus for exact domain match
                        if (credDomain == term || credDomain.StartsWith(term + ".") || credDomain.EndsWith("." + term))
                            score += 50;
                    }
                }
                
                if (score > 0)
                    matched.Add((cred, score));
            }
            
            // Return matched credentials sorted by score, or empty list if no matches
            if (matched.Count > 0)
                return matched.OrderByDescending(m => m.score).Select(m => m.cred).ToList();
            
            return new List<AutofillCredential>();
        }
        
        /// <summary>
        /// Extract search terms from URL/domain
        /// </summary>
        private List<string> ExtractSearchTerms(string url)
        {
            var terms = new List<string>();
            
            try
            {
                var cleaned = url.ToLowerInvariant()
                    .Replace("https://", "")
                    .Replace("http://", "")
                    .Replace("www.", "");
                
                // Remove path
                var slashIndex = cleaned.IndexOf('/');
                if (slashIndex > 0)
                    cleaned = cleaned.Substring(0, slashIndex);
                
                // Split by dots and filter common TLDs
                var parts = cleaned.Split('.');
                foreach (var part in parts)
                {
                    if (!string.IsNullOrWhiteSpace(part) && part.Length > 2 && !IsCommonTLD(part))
                        terms.Add(part);
                }
                
                // Also add full domain for exact matching
                if (!string.IsNullOrWhiteSpace(cleaned))
                    terms.Add(cleaned);
            }
            catch { }
            
            return terms;
        }
        
        private static readonly HashSet<string> CommonTLDs = new(StringComparer.OrdinalIgnoreCase)
        {
            "com", "net", "org", "io", "co", "uk", "au", "nz", "fr", "de", 
            "tv", "info", "app", "eu", "me", "dev", "jp", "www", "https", "http"
        };
        
        private bool IsCommonTLD(string part) => CommonTLDs.Contains(part);
        
        #region Save Credential Queue
        
        private const string PendingSavesFileName = "pending_save_credentials.json";
        
        /// <summary>
        /// Queues a credential to be saved by the main app.
        /// The main app will process this queue on next launch.
        /// </summary>
        public void QueueCredentialForSave(PendingSaveCredential credential)
        {
            try
            {
                if (string.IsNullOrEmpty(_containerPath))
                {
                    ErrorLogger.LogError("Container path is null - cannot queue credential");
                    return;
                }
                
                var filePath = Path.Combine(_containerPath, PendingSavesFileName);
                
                // Load existing queue
                var pendingCredentials = new List<PendingSaveCredential>();
                if (File.Exists(filePath))
                {
                    var existingJson = File.ReadAllText(filePath);
                    if (!string.IsNullOrEmpty(existingJson))
                    {
                        pendingCredentials = JsonSerializer.Deserialize<List<PendingSaveCredential>>(existingJson, _jsonOptions) 
                            ?? new List<PendingSaveCredential>();
                    }
                }
                
                // Add new credential (avoid duplicates for same domain/username)
                pendingCredentials.RemoveAll(c => 
                    c.Domain == credential.Domain && c.Username == credential.Username);
                pendingCredentials.Add(credential);
                
                // Save back
                var json = JsonSerializer.Serialize(pendingCredentials, _jsonOptions);
                File.WriteAllText(filePath, json);
                
                ErrorLogger.LogInfo($"Queued credential for save: {credential.Domain}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to queue credential for save", ex);
            }
        }
        
        /// <summary>
        /// Gets all pending credentials to be saved.
        /// Called by the main app to process the queue.
        /// </summary>
        public List<PendingSaveCredential> GetPendingSaveCredentials()
        {
            try
            {
                if (string.IsNullOrEmpty(_containerPath))
                    return new List<PendingSaveCredential>();
                
                var filePath = Path.Combine(_containerPath, PendingSavesFileName);
                if (!File.Exists(filePath))
                    return new List<PendingSaveCredential>();
                
                var json = File.ReadAllText(filePath);
                if (string.IsNullOrEmpty(json))
                    return new List<PendingSaveCredential>();
                
                return JsonSerializer.Deserialize<List<PendingSaveCredential>>(json, _jsonOptions) 
                    ?? new List<PendingSaveCredential>();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to get pending save credentials", ex);
                return new List<PendingSaveCredential>();
            }
        }
        
        /// <summary>
        /// Clears the pending save queue after processing.
        /// </summary>
        public void ClearPendingSaveCredentials()
        {
            try
            {
                if (string.IsNullOrEmpty(_containerPath))
                    return;
                
                var filePath = Path.Combine(_containerPath, PendingSavesFileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    ErrorLogger.LogInfo("Cleared pending save credentials");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to clear pending save credentials", ex);
            }
        }
        
        /// <summary>
        /// Removes a specific credential from the pending queue.
        /// </summary>
        public void RemovePendingSaveCredential(string domain, string username)
        {
            try
            {
                if (string.IsNullOrEmpty(_containerPath))
                    return;
                
                var filePath = Path.Combine(_containerPath, PendingSavesFileName);
                if (!File.Exists(filePath))
                    return;
                
                var json = File.ReadAllText(filePath);
                var pendingCredentials = JsonSerializer.Deserialize<List<PendingSaveCredential>>(json, _jsonOptions) 
                    ?? new List<PendingSaveCredential>();
                
                pendingCredentials.RemoveAll(c => c.Domain == domain && c.Username == username);
                
                if (pendingCredentials.Count == 0)
                {
                    File.Delete(filePath);
                }
                else
                {
                    var updatedJson = JsonSerializer.Serialize(pendingCredentials, _jsonOptions);
                    File.WriteAllText(filePath, updatedJson);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to remove pending save credential", ex);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Represents a credential pending to be saved by the main app.
    /// </summary>
    public class PendingSaveCredential
    {
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;
        
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
        
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }
    
    /// <summary>
    /// Data model for credentials stored in the shared JSON file.
    /// </summary>
    public class SharedCredentialData
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;
        
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
        
        [JsonPropertyName("hasOtp")]
        public bool HasOtp { get; set; }
        
        [JsonPropertyName("data")]
        public string? Data { get; set; }
        
        [JsonPropertyName("credentialType")]
        public string CredentialType { get; set; } = string.Empty;
        
        [JsonPropertyName("iconUri")]
        public string? IconUri { get; set; }
        
        [JsonPropertyName("fallbackIcon")]
        public string? FallbackIcon { get; set; }
    }
}
