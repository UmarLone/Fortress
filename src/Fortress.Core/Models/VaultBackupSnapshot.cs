using Fortress.Core.Models;
using System.Text.Json.Serialization;

namespace Fortress.Core.Models
{
    /// <summary>
    /// JSON envelope written to and read from cloud backup providers.
    /// All fields are already encrypted by IVaultCryptoService before serialisation.
    /// The entire envelope is encrypted again before upload (double-wrap).
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
