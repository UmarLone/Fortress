using System.Text.Json.Serialization;

namespace Fortress.Mobile.Core.Models
{
    /// <summary>
    /// JSON envelope written to / read from Google Drive.
    /// All credential/note/card fields are stored as-is (already encrypted by
    /// ICryptographyService before they were persisted to the local DB).
    /// The outer envelope itself is encrypted again by the ViewModel before upload.
    /// </summary>
    public class VaultBackupSnapshot
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("credentials")]
        public List<LoginItem> Credentials { get; set; } = [];

        [JsonPropertyName("authenticators")]
        public List<Authenticator> Authenticators { get; set; } = [];

        [JsonPropertyName("creditCards")]
        public List<CreditCardItem> CreditCards { get; set; } = [];

        [JsonPropertyName("identities")]
        public List<IdentityItem> Identities { get; set; } = [];

        [JsonPropertyName("secureNotes")]
        public List<SecureNoteItem> SecureNotes { get; set; } = [];
    }
}
