namespace Fortress.Core.Security
{
    /// <summary>
    /// Encrypts and decrypts vault field values using the current master password.
    /// The algorithm must be identical on Windows and MAUI so that a vault backup
  /// created on one platform can be fully decrypted on the other.
    ///
    /// Algorithm: AES-256-CBC
    ///   Key derivation : PasswordDeriveBytes(masterPassword, salt, "SHA1", 3) ? 32 bytes
    ///   IV    : 16 cryptographically random bytes prepended to ciphertext
    ///   Wire format    : Base64( [ivSize(1)] [tagSize(1)] [iv(16)] [ciphertext] )
    ///               (same PackCipherData format as MAUI CryptographyService)
    /// </summary>
    public interface IVaultCryptoService
    {
        /// <summary>Encrypt a plaintext string. Returns Base64 ciphertext.</summary>
 Task<string> EncryptAsync(string plainText);

        /// <summary>Decrypt a Base64 ciphertext string. Returns plaintext.</summary>
        Task<string> DecryptAsync(string cipherText);

        /// <summary>
        /// Set a new master password. Derives and caches the key immediately.
        /// Call this after the user creates or changes their master password.
        /// </summary>
     void SetMasterPassword(string masterPassword);

        /// <summary>
        /// Verify that a given password produces the same key as the stored
    /// master password verifier. Used on the unlock/login screen.
   /// Returns true when the password is correct.
        /// </summary>
        bool VerifyMasterPassword(string candidate);

        /// <summary>True once a master password has been set for this session.</summary>
        bool IsSessionUnlocked { get; }

    /// <summary>Drop the in-memory key. Call this to lock the vault.</summary>
        void LockSession();
    }
}
