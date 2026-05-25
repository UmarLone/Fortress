using Fortress.Mobile.Core.Models;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Vault field crypto.
    ///
    /// Byte-for-byte identical to <c>Fortress.Core.Security.VaultCryptoService</c>
    /// so a vault written on any platform decrypts on any other. See that file
    /// for the wire-format spec.
    ///
    /// Reads the master password from <c>PreferenceWrapper.Instance.DatabasePassword</c>;
    /// the derived key is cached and invalidated via <see cref="InvalidateKeyCache"/>.
    /// </summary>
    public class CryptographyService : ICryptographyService
    {
        // Magic = "F2" → first decoded byte 0x46 is distinct from any plausible
        // future version's prefix.
        private static readonly byte[] V2_Magic = [0x46, 0x32]; // 'F', '2'
        private const byte V2_Version          = 0x02;
        private const int  V2_IvSize           = 12;
        private const int  V2_TagSize          = 16;
        private const int  V2_KeyBits          = 256;

        // Argon2id parameters — must match Fortress.Core/Security/VaultCryptoService.
        private const int V2_ArgonTimeCost    = 3;
        private const int V2_ArgonMemoryKiB   = 65536; // 64 MiB
        private const int V2_ArgonParallelism = 4;

        private static readonly byte[] V2_Salt = Encoding.UTF8.GetBytes(
            "fortress.v2:argon2id-2026-vault-key-derivation");

        private readonly ILogger<CryptographyService> _logger;

        // Argon2id with 64 MiB takes a few hundred ms; the cache avoids paying
        // that on the per-field hot path. Invalidated when the password changes.
        private byte[]? _cachedKey;
        private string? _cachedPassword;
        private readonly object _keyLock = new();

        public CryptographyService(ILogger<CryptographyService> logger)
        {
            _logger = logger;
        }

        public void InvalidateKeyCache()
        {
            lock (_keyLock)
            {
                if (_cachedKey is not null) Array.Clear(_cachedKey, 0, _cachedKey.Length);
                _cachedKey      = null;
                _cachedPassword = null;
            }
        }

        // ── Key access ───────────────────────────────────────────────────────

        private byte[] GetKey()
        {
            var password = PreferenceWrapper.Instance.DatabasePassword;
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException(
                    "Master password is not set. Ensure the vault is unlocked before encrypting.");

            lock (_keyLock)
            {
                if (_cachedKey is not null &&
                    string.Equals(_cachedPassword, password, StringComparison.Ordinal))
                {
                    return _cachedKey;
                }

                _cachedKey      = DeriveKey(password);
                _cachedPassword = password;
                return _cachedKey;
            }
        }

        // ── Public Encrypt / Decrypt ─────────────────────────────────────────

        public Task<CommandResult<string>> Encrypt(string plainText)
        {
            var result = new CommandResult<string>();
            if (string.IsNullOrEmpty(plainText))
            {
                result.ErrorMessage = "string is null or empty";
                result.Data         = plainText;
                return Task.FromResult(result);
            }
            try
            {
                result.Data      = EncryptV2(plainText, GetKey());
                result.Succeeded = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex.Message);
            }
            return Task.FromResult(result);
        }

        public Task<CommandResult<string>> Decrypt(string cipherText)
        {
            var result = new CommandResult<string>();
            if (string.IsNullOrEmpty(cipherText))
            {
                result.ErrorMessage = "string is null or empty";
                result.Data         = cipherText;
                return Task.FromResult(result);
            }
            try
            {
                byte[] raw;
                try { raw = Convert.FromBase64String(cipherText); }
                catch (FormatException) { throw new InvalidOperationException("Ciphertext is not valid Base64."); }

                if (!IsV2(raw))
                    throw new InvalidOperationException("Unrecognized ciphertext format (expected v2).");

                result.Data      = DecryptV2(raw, GetKey());
                result.Succeeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                result.ErrorMessage = ex.Message;
            }
            return Task.FromResult(result);
        }

        // ── Argon2id KDF ─────────────────────────────────────────────────────

        private static byte[] DeriveKey(string password)
        {
            var pwBytes = Encoding.UTF8.GetBytes(password);
            using var argon2 = new Argon2id(pwBytes)
            {
                Salt                = V2_Salt,
                Iterations          = V2_ArgonTimeCost,
                MemorySize          = V2_ArgonMemoryKiB,   // KiB
                DegreeOfParallelism = V2_ArgonParallelism,
            };
            return argon2.GetBytes(V2_KeyBits / 8);
        }

        // ── AES-GCM encrypt / decrypt ────────────────────────────────────────

        private static bool IsV2(byte[] raw) =>
            raw.Length >= V2_Magic.Length + 1
            && raw[0] == V2_Magic[0]
            && raw[1] == V2_Magic[1]
            && raw[2] == V2_Version;

        private static string EncryptV2(string plainText, byte[] key)
        {
            var iv         = RandomNumberGenerator.GetBytes(V2_IvSize);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipher     = new byte[plainBytes.Length];
            var tag        = new byte[V2_TagSize];

            using (var aes = new AesGcm(key, V2_TagSize))
                aes.Encrypt(iv, plainBytes, cipher, tag);

            // [Magic(2)] [Version(1)] [IV(12)] [Cipher(var)] [Tag(16)]
            var packed = new byte[V2_Magic.Length + 1 + V2_IvSize + cipher.Length + V2_TagSize];
            int idx = 0;
            Array.Copy(V2_Magic, 0, packed, idx, V2_Magic.Length); idx += V2_Magic.Length;
            packed[idx++] = V2_Version;
            Array.Copy(iv,     0, packed, idx, V2_IvSize);     idx += V2_IvSize;
            Array.Copy(cipher, 0, packed, idx, cipher.Length); idx += cipher.Length;
            Array.Copy(tag,    0, packed, idx, V2_TagSize);
            return Convert.ToBase64String(packed);
        }

        private static string DecryptV2(byte[] raw, byte[] key)
        {
            int idx = V2_Magic.Length + 1; // skip magic + version
            if (raw.Length < idx + V2_IvSize + V2_TagSize)
                throw new InvalidOperationException("v2 ciphertext truncated.");

            var iv = new byte[V2_IvSize];
            Array.Copy(raw, idx, iv, 0, V2_IvSize);
            idx += V2_IvSize;

            int cipherLen = raw.Length - idx - V2_TagSize;
            var cipher = new byte[cipherLen];
            Array.Copy(raw, idx, cipher, 0, cipherLen);
            idx += cipherLen;

            var tag = new byte[V2_TagSize];
            Array.Copy(raw, idx, tag, 0, V2_TagSize);

            var plain = new byte[cipherLen];
            using (var aes = new AesGcm(key, V2_TagSize))
                aes.Decrypt(iv, cipher, tag, plain); // throws CryptographicException if tag mismatches
            return Encoding.UTF8.GetString(plain);
        }
    }
}
