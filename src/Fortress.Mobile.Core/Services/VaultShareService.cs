using Fortress.Mobile.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Encrypts/decrypts vault items for peer-to-peer file sharing.
    /// Uses AES-256-GCM with a key derived from a one-time passphrase via
    /// PBKDF2-SHA256 (600 000 iterations).
    /// 
    /// The flow:
    ///   Sender:  item ? JSON ? AES-256-GCM encrypt(passphrase) ? SharedItemPayload ? .fortress file
    ///   Receiver: .fortress file ? SharedItemPayload ? AES-256-GCM decrypt(passphrase) ? JSON ? item
    /// </summary>
    public sealed class VaultShareService
    {
        private const int SaltSize = 16;
        private const int NonceSize = 12;     // AES-GCM standard
        private const int TagSize = 16;       // 128-bit auth tag
        private const int KeySize = 32;       // 256 bits
        private const int Pbkdf2Iterations = 600_000;

        // ── Public API ───────────────────────────────────────────────────────
        /// <summary>
        /// Encrypt a vault item into a <see cref="SharedItemPayload"/>
        /// ready to be serialised to a .fortress file.
        /// </summary>
        public SharedItemPayload Encrypt(object vaultItem, string itemType, string label, string passphrase)
        {
            var plainJson = JsonSerializer.Serialize(vaultItem, vaultItem.GetType(), JsonOptions);
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var key = DeriveKey(passphrase, salt);

            var plainBytes = Encoding.UTF8.GetBytes(plainJson);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Concatenate ciphertext + tag for storage
            var combined = new byte[cipherBytes.Length + tag.Length];
            Buffer.BlockCopy(cipherBytes, 0, combined, 0, cipherBytes.Length);
            Buffer.BlockCopy(tag, 0, combined, cipherBytes.Length, tag.Length);

            return new SharedItemPayload
            {
                Version = 1,
                ItemType = itemType,
                Label = label,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Data = Convert.ToBase64String(combined),
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Decrypt a <see cref="SharedItemPayload"/> back to a vault item.
        /// Returns null if the passphrase is wrong or data is corrupt.
        /// </summary>
        public T? Decrypt<T>(SharedItemPayload payload, string passphrase) where T : class
        {
            try
            {
                var salt = Convert.FromBase64String(payload.Salt);
                var nonce = Convert.FromBase64String(payload.Nonce);
                var combined = Convert.FromBase64String(payload.Data);
                var key = DeriveKey(passphrase, salt);

                if (combined.Length < TagSize)
                    return null;

                var cipherBytes = new byte[combined.Length - TagSize];
                var tag = new byte[TagSize];
                Buffer.BlockCopy(combined, 0, cipherBytes, 0, cipherBytes.Length);
                Buffer.BlockCopy(combined, cipherBytes.Length, tag, 0, TagSize);

                var plainBytes = new byte[cipherBytes.Length];
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (CryptographicException)
            {
                // Wrong passphrase or tampered data
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decrypt the raw JSON string from a payload (for previewing heterogeneous types).
        /// Returns null if the passphrase is wrong.
        /// </summary>
        public string? DecryptToJson(SharedItemPayload payload, string passphrase)
        {
            try
            {
                var salt = Convert.FromBase64String(payload.Salt);
                var nonce = Convert.FromBase64String(payload.Nonce);
                var combined = Convert.FromBase64String(payload.Data);
                var key = DeriveKey(passphrase, salt);

                if (combined.Length < TagSize)
                    return null;

                var cipherBytes = new byte[combined.Length - TagSize];
                var tag = new byte[TagSize];
                Buffer.BlockCopy(combined, 0, cipherBytes, 0, cipherBytes.Length);
                Buffer.BlockCopy(combined, cipherBytes.Length, tag, 0, TagSize);

                var plainBytes = new byte[cipherBytes.Length];
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Serialise a <see cref="SharedItemPayload"/> to .fortress file bytes.
        /// </summary>
        public byte[] SerializeToFile(SharedItemPayload payload)
               => JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);

        /// <summary>
        /// Deserialise .fortress file bytes into a <see cref="SharedItemPayload"/>.
        /// </summary>
        public SharedItemPayload? DeserializeFromFile(byte[] fileBytes)
        {
            try
            {
                return JsonSerializer.Deserialize<SharedItemPayload>(fileBytes, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Generate a random 4-word passphrase from the EFF short word list.
        /// </summary>
        public static string GeneratePassphrase(int wordCount = 4)
        {
            var words = Fortress.Mobile.Core.Utilities.EEFLongWordList.Instance.List;
            var result = new string[wordCount];
            for (int i = 0; i < wordCount; i++)
                result[i] = words[RandomNumberGenerator.GetInt32(words.Count)];
            return string.Join("-", result);
        }
        // ── Private helpers ──────────────────────────────────────────────────
        private static byte[] DeriveKey(string passphrase, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(
         passphrase,
           salt,
           Pbkdf2Iterations,
               HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(KeySize);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
