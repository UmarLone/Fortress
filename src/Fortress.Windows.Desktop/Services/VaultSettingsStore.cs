using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// Stores all security-sensitive vault settings for the Windows app.
    ///
    /// Sensitive values (PIN hash, master-password verifier, DB file key) are
    /// encrypted with DPAPI (ProtectedData.CurrentUser scope) before being written
    /// to a JSON file in %LOCALAPPDATA%.  Non-sensitive settings (lock timeout,
    /// first-run flag, etc.) are stored in plain JSON alongside them.
    ///
    /// DPAPI scope = CurrentUser, which means:
    ///   � Data is only readable by the same Windows user account
    ///   � Survives reboots
    ///   � Does NOT survive a full OS re-install without a user profile backup
    /// </summary>
    public sealed class VaultSettingsStore
    {
        private static readonly string SettingsDir =
              Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
              "Fortress");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "vault.settings.json");

        private VaultSettings _settings;
        private readonly object _fileLock = new();

        // ── Singleton ─────────────────────────────────────────────────────────
        private static readonly Lazy<VaultSettingsStore> _lazy =
         new(() => new VaultSettingsStore());
        public static VaultSettingsStore Instance => _lazy.Value;

        private VaultSettingsStore()
        {
            _settings = Load();
        }

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>True if the user has completed the first-run setup wizard.</summary>
        public bool HasSetupCompleted
        {
            get => _settings.HasSetupCompleted;
            set { _settings.HasSetupCompleted = value; Save(); }
        }

        /// <summary>Whether to lock the vault when the app loses focus.</summary>
        public bool LockOnMinimise
        {
            get => _settings.LockOnMinimise;
            set { _settings.LockOnMinimise = value; Save(); }
        }

        /// <summary>Seconds of inactivity before auto-lock. 0 = never.</summary>
        public int LockTimeoutSeconds
        {
            get => _settings.LockTimeoutSeconds;
            set { _settings.LockTimeoutSeconds = value; Save(); }
        }

        /// <summary>Whether Windows Hello biometric unlock is enabled.</summary>
        public bool IsBiometricUnlockEnabled
        {
            get => _settings.IsBiometricUnlockEnabled;
            set { _settings.IsBiometricUnlockEnabled = value; Save(); }
        }

        /// <summary>Whether 4-digit PIN unlock is enabled.</summary>
        public bool IsPinUnlockEnabled
        {
            get => _settings.IsPinUnlockEnabled;
            set { _settings.IsPinUnlockEnabled = value; Save(); }
        }

        /// <summary>Cloud sync provider name, e.g. "GoogleDrive", "OneDrive", "".</summary>
        public string CloudSyncProvider
        {
            get => _settings.CloudSyncProvider;
            set { _settings.CloudSyncProvider = value; Save(); }
        }

        public bool IsCloudSyncEnabled
        {
            get => _settings.IsCloudSyncEnabled;
            set { _settings.IsCloudSyncEnabled = value; Save(); }
        }

        public int MaxFailedAttempts
        {
            get => _settings.MaxFailedAttempts;
            set { _settings.MaxFailedAttempts = value; Save(); }
        }

        public int FailedAttemptCount
        {
            get => _settings.FailedAttemptCount;
            set { _settings.FailedAttemptCount = value; Save(); }
        }

        // ── DPAPI-protected values ─────────────────────────────────────────
        /// <summary>
        /// DPAPI-encrypted PBKDF2 verifier for the master password.
        /// Written once during setup. Used on every unlock to verify the entered
        /// master password before handing the derived key to VaultCryptoService.
        /// </summary>
        public void SetMasterPasswordVerifier(string verifierBase64)
        {
            _settings.MasterPasswordVerifierDpapi =
               Protect(Encoding.UTF8.GetBytes(verifierBase64));
            Save();
        }

        public string? GetMasterPasswordVerifier()
        {
            if (string.IsNullOrEmpty(_settings.MasterPasswordVerifierDpapi))
                return null;
            var raw = Unprotect(_settings.MasterPasswordVerifierDpapi);
            return raw is null ? null : Encoding.UTF8.GetString(raw);
        }

        public bool HasMasterPassword =>
     !string.IsNullOrEmpty(_settings.MasterPasswordVerifierDpapi);

        /// <summary>DPAPI-encrypted SHA-256 hash of the 4-digit PIN.</summary>
        public void SetPinHash(string pinHashBase64)
        {
            _settings.PinHashDpapi = Protect(Encoding.UTF8.GetBytes(pinHashBase64));
            Save();
        }

        public string? GetPinHash()
        {
            if (string.IsNullOrEmpty(_settings.PinHashDpapi)) return null;
            var raw = Unprotect(_settings.PinHashDpapi);
            return raw is null ? null : Encoding.UTF8.GetString(raw);
        }

        public void ClearPin()
        {
            _settings.PinHashDpapi = null;
            _settings.PinMasterPasswordDpapi = null;
            _settings.IsPinUnlockEnabled = false;
            Save();
        }

        /// <summary>
        /// Stores the master password encrypted under DPAPI so PIN unlock
        /// can re-derive the AES session key without asking for the full password.
        /// This is protected by Windows user-account scope (same level as the PIN hash).
        /// </summary>
        public void SetPinProtectedMasterPassword(string masterPassword)
        {
            _settings.PinMasterPasswordDpapi = Protect(Encoding.UTF8.GetBytes(masterPassword));
            Save();
        }

        public string? GetPinProtectedMasterPassword()
        {
            if (string.IsNullOrEmpty(_settings.PinMasterPasswordDpapi)) return null;
            var raw = Unprotect(_settings.PinMasterPasswordDpapi);
            return raw is null ? null : Encoding.UTF8.GetString(raw);
        }

        /// <summary>
        /// DPAPI-encrypted random key used to open the LiteDB file.
        /// Separate from the master password � changing the master password
        /// does NOT change this key, so the DB file stays readable.
        /// </summary>
        public string GetOrCreateDbFileKey()
        {
            if (!string.IsNullOrEmpty(_settings.DbFileKeyDpapi))
            {
                var existing = Unprotect(_settings.DbFileKeyDpapi);
                if (existing is not null)
                    return Encoding.UTF8.GetString(existing);
            }

            // Generate a new random key
            var keyBytes = new byte[32];
            RandomNumberGenerator.Fill(keyBytes);
            var key = Convert.ToBase64String(keyBytes);
            _settings.DbFileKeyDpapi = Protect(Encoding.UTF8.GetBytes(key));
            Save();
            return key;
        }

        // ── DPAPI helpers ──────────────────────────────────────────────────
        private static string Protect(byte[] data)
        {
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static byte[]? Unprotect(string base64)
        {
            try
            {
                var encrypted = Convert.FromBase64String(base64);
                return ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        // ── Persistence ────────────────────────────────────────────────────
        private VaultSettings Load()
        {
            if (!File.Exists(SettingsFile))
                return new VaultSettings();
            try
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<VaultSettings>(json) ?? new VaultSettings();
            }
            catch { return new VaultSettings(); }
        }

        private void Save()
        {
            lock (_fileLock)
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(_settings,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
        }

        // ── Settings DTO ───────────────────────────────────────────────────
        private sealed class VaultSettings
        {
            public bool HasSetupCompleted { get; set; } = false;
            public bool LockOnMinimise { get; set; } = false;
            public int LockTimeoutSeconds { get; set; } = 300;
            public bool IsBiometricUnlockEnabled { get; set; } = false;
            public bool IsPinUnlockEnabled { get; set; } = false;
            public string CloudSyncProvider { get; set; } = "";
            public bool IsCloudSyncEnabled { get; set; } = false;
            public int MaxFailedAttempts { get; set; } = 5;
            public int FailedAttemptCount { get; set; } = 0;

            // DPAPI-encrypted blobs (Base64 strings)
            public string? MasterPasswordVerifierDpapi { get; set; }
            public string? PinHashDpapi { get; set; }
            public string? PinMasterPasswordDpapi { get; set; }
            public string? DbFileKeyDpapi { get; set; }
        }
    }
}
