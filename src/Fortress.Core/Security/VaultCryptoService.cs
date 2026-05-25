using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.Core.Security
{
    /// <summary>
    /// Vault field crypto.
    ///
    /// • KDF: Argon2id (t=3, m=64 MiB, p=4) → 32 bytes.
    /// • Cipher: AES-256-GCM (authenticated; rejects tampered ciphertext).
    /// • Wire: Base64( [magic "F2" (2)] [version (1)] [iv (12)] [ciphertext] [tag (16)] ).
    ///
    /// The wire format carries a magic + version byte so a future v3 can coexist
    /// with v2 fields in the same vault and be detected on read.
    ///
    /// Byte-for-byte identical to <c>Fortress.Mobile.Core.Services.CryptographyService</c>
    /// so a vault written on any platform decrypts on any other.
    /// </summary>
    public sealed class VaultCryptoService : IVaultCryptoService
    {
        // Magic = "F2" → first decoded byte 0x46 is distinct from any plausible
        // future version's prefix and lets format detection be a constant-time check.
        private static readonly byte[] V2_Magic = [0x46, 0x32]; // 'F', '2'
        private const byte V2_Version = 0x02;
        private const int  V2_IvSize  = 12;   // AES-GCM standard nonce size
        private const int  V2_TagSize = 16;   // AES-GCM tag size
        private const int  V2_KeyBits = 256;

        // Argon2id parameters — OWASP 2023 recommended minimum + margin.
        // Tuned so the KDF takes ~250-500 ms on a 2020+ desktop. Increase MemorySize
        // before increasing Iterations: memory hardness is what defeats GPU/ASIC.
        private const int V2_ArgonTimeCost    = 3;
        private const int V2_ArgonMemoryKiB   = 65536; // 64 MiB
        private const int V2_ArgonParallelism = 4;

        // Constant domain-separation salt. The memory hardness of Argon2id makes
        // precomputed-table attacks impractical even without per-vault salt
        // (a 64 MiB derivation costs real money per candidate password).
        // A future v3 may add per-vault random salt.
        private static readonly byte[] V2_Salt = Encoding.UTF8.GetBytes(
            "fortress.v2:argon2id-2026-vault-key-derivation");

        // ── In-memory session state ──────────────────────────────────────────
        private byte[]? _sessionKey;
        private string? _passwordVerifierHash;
        private readonly object _lock = new();

        // ── IVaultCryptoService ──────────────────────────────────────────────
        public bool IsSessionUnlocked
        {
            get { lock (_lock) return _sessionKey is not null; }
        }

        public void SetMasterPassword(string masterPassword)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(masterPassword);
            var key = DeriveKey(masterPassword);
            lock (_lock)
            {
                ClearKey();
                _sessionKey           = key;
                _passwordVerifierHash = ComputeVerifierHash(masterPassword);
            }
        }

        public bool VerifyMasterPassword(string candidate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate);
            lock (_lock)
            {
                if (_passwordVerifierHash is null) return false;
                var candidateHash = ComputeVerifierHash(candidate);
                return string.Equals(_passwordVerifierHash, candidateHash, StringComparison.Ordinal);
            }
        }

        public void LockSession()
        {
            lock (_lock) ClearKey();
        }

        public Task<string> EncryptAsync(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return Task.FromResult(plainText);

            byte[] key;
            lock (_lock)
            {
                if (_sessionKey is null)
                    throw new InvalidOperationException(
                        "Vault is locked. Call SetMasterPassword before encrypting.");
                key = (byte[])_sessionKey.Clone();
            }

            try { return Task.FromResult(Encrypt(plainText, key)); }
            finally { Array.Clear(key, 0, key.Length); }
        }

        public Task<string> DecryptAsync(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return Task.FromResult(cipherText);

            byte[] raw;
            try { raw = Convert.FromBase64String(cipherText); }
            catch (FormatException) { throw new InvalidOperationException("Ciphertext is not valid Base64."); }

            if (!IsV2(raw))
                throw new InvalidOperationException("Unrecognized ciphertext format (expected v2).");

            byte[] key;
            lock (_lock)
            {
                if (_sessionKey is null)
                    throw new InvalidOperationException(
                        "Vault is locked. Call SetMasterPassword before decrypting.");
                key = (byte[])_sessionKey.Clone();
            }
            try { return Task.FromResult(Decrypt(raw, key)); }
            finally { Array.Clear(key, 0, key.Length); }
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

        private static string Encrypt(string plainText, byte[] key)
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

        private static string Decrypt(byte[] raw, byte[] key)
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

        // ── Verifier hash ────────────────────────────────────────────────────
        // SHA-256 of (password || in-memory nonce). The nonce is process-local
        // so the verifier hash is useless to anyone reading the prefs file; it
        // exists only so VerifyMasterPassword can answer quickly without re-running
        // Argon2id on every check.
        private static readonly byte[] _verifierNonce = GenerateNonce();

        private static byte[] GenerateNonce()
        {
            var b = new byte[32];
            RandomNumberGenerator.Fill(b);
            return b;
        }

        private static string ComputeVerifierHash(string password)
        {
            var pwBytes  = Encoding.UTF8.GetBytes(password);
            var combined = new byte[pwBytes.Length + _verifierNonce.Length];
            Buffer.BlockCopy(pwBytes,        0, combined, 0,              pwBytes.Length);
            Buffer.BlockCopy(_verifierNonce, 0, combined, pwBytes.Length, _verifierNonce.Length);
            return Convert.ToHexString(SHA256.HashData(combined));
        }

        private void ClearKey()
        {
            if (_sessionKey is not null)
            {
                Array.Clear(_sessionKey, 0, _sessionKey.Length);
                _sessionKey = null;
            }
            _passwordVerifierHash = null;
        }
    }
}
