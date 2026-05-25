using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Fortress.iPhone.Autofill
{
    /// <summary>
    /// Logs credential usage events to shared storage for the main app to process.
    /// This mirrors Android's OnCredentialUsed() functionality.
    /// </summary>
    public static class CredentialUsageLogger
    {
        private const string AppGroup = "group.com.fortress.passwordmanager";
        private const string UsageEventsFileName = "autofill_usage_events.json";
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        
        /// <summary>
        /// Logs a credential usage event (like Android EventLogType.WebCredentialUsed/PhonePasswordUsed)
        /// </summary>
        public static async Task LogCredentialUsedAsync(Guid credentialId, string credentialType, string? domain)
        {
            try
            {
                var containerUrl = NSFileManager.DefaultManager.GetContainerUrl(AppGroup);
                if (containerUrl == null) return;
                
                var filePath = Path.Combine(containerUrl.Path!, UsageEventsFileName);
                
                // Read existing events
                var events = new List<SharedCredentialUsageEvent>();
                if (File.Exists(filePath))
                {
                    var existingJson = await File.ReadAllTextAsync(filePath);
                    if (!string.IsNullOrEmpty(existingJson))
                    {
                        events = JsonSerializer.Deserialize<List<SharedCredentialUsageEvent>>(existingJson, _jsonOptions) 
                            ?? new List<SharedCredentialUsageEvent>();
                    }
                }
                
                // Add new event
                events.Add(new SharedCredentialUsageEvent
                {
                    CredentialId = credentialId,
                    CredentialType = credentialType,
                    UsedAt = DateTime.UtcNow,
                    Domain = domain
                });
                
                // Write back
                var json = JsonSerializer.Serialize(events, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch
            {
                // Fire and forget - don't slow down autofill
            }
        }
    }
    
    /// <summary>
    /// Data model for credential usage events (matches main app's SharedCredentialUsageEvent)
    /// </summary>
    public class SharedCredentialUsageEvent
    {
        [JsonPropertyName("credentialId")]
        public Guid CredentialId { get; set; }
        
        [JsonPropertyName("credentialType")]
        public string CredentialType { get; set; } = string.Empty;
        
        [JsonPropertyName("usedAt")]
        public DateTime UsedAt { get; set; }
        
        [JsonPropertyName("domain")]
        public string? Domain { get; set; }
    }
}
