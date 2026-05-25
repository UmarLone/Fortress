using LiteDB;

namespace Fortress.Core.Models
{
    public sealed class PasskeyItem
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RpId { get; set; } = string.Empty;
        public string RpName { get; set; } = string.Empty;
        public string UserHandle { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserDisplayName { get; set; } = string.Empty;
        public string CredentialId { get; set; } = string.Empty;
        /// <summary>AES-encrypted private key (PKCS#8 DER, ES256).</summary>
        public string EncryptedPrivateKey { get; set; } = string.Empty;
        public string PublicKeyCose { get; set; } = string.Empty;
        public int Algorithm { get; set; } = -7;
        public uint SignCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? IconUri { get; set; }
    }
}
