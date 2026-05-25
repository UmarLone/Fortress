using System.Text.Json.Serialization;

namespace Fortress.Mobile.Core.Models
{
    /// <summary>
 /// Encrypted envelope for sharing vault items between FORTRESS users.
    /// Written to a .fortress file and decrypted with a one-time passphrase.
    /// </summary>
    public sealed class SharedItemPayload
    {
        /// <summary>Format version – bump when the schema changes.</summary>
        [JsonPropertyName("v")]
    public int Version { get; set; } = 1;

   /// <summary>
        /// The kind of item inside: "login", "authenticator", "creditcard",
        /// "identity", "securenote", "secureitem".
        /// </summary>
     [JsonPropertyName("type")]
        public string ItemType { get; set; } = string.Empty;

        /// <summary>Human-readable label shown in the "receive" preview.</summary>
 [JsonPropertyName("label")]
public string Label { get; set; } = string.Empty;

        /// <summary>16-byte random salt, Base64-encoded.</summary>
        [JsonPropertyName("salt")]
        public string Salt { get; set; } = string.Empty;

        /// <summary>12-byte nonce/IV, Base64-encoded.</summary>
        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;

        /// <summary>
    /// AES-256-GCM ciphertext + 16-byte auth tag, Base64-encoded.
        /// The plaintext is the JSON-serialised vault item.
      /// </summary>
  [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        /// <summary>UTC timestamp of when the share was created.</summary>
    [JsonPropertyName("ts")]
 public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
