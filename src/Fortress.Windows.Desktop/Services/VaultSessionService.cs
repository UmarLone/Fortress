using Fortress.Core.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fortress.Windows.Desktop.Services
{
    public enum UnlockMethod { MasterPassword, Pin, Biometric }

    public interface IVaultSessionService
 {
        bool IsUnlocked { get; }
        bool IsSetupComplete { get; }
        Task<bool> SetupMasterPasswordAsync(string password, string confirmPassword);
        Task<bool> UnlockWithPasswordAsync(string password);
        Task<bool> UnlockWithPinAsync(string pin);
        Task SetupPinAsync(string pin);
      void SetBiometricEnabled(bool enabled);
        void Lock();
        void RecordFailedAttempt();
        void ResetFailedAttempts();
  event EventHandler<bool> LockStateChanged;
    }

    /// <summary>
    /// Desktop vault session service.
    ///
    /// All unlock / lock / status calls go through the named pipe to
    /// Fortress.Service � the service owns credential verification and issues
  /// the session token.  The desktop only keeps the token in memory and
    /// writes a minimal local DPAPI store for fast offline checks (e.g. to
    /// decide which unlock methods to show on the lock screen without a round-trip).
    ///
  /// Setup (first-run) is the one exception: the desktop writes the master
    /// password verifier and PIN hash into the shared preferences file that the
    /// service reads, so the service can verify on the next unlock.
    /// </summary>
    public sealed class VaultSessionService : IVaultSessionService
    {
        private readonly PipeClient _pipe;
   private readonly IDesktopSessionStore _store;
      private readonly VaultSettingsStore   _settings = VaultSettingsStore.Instance;

     private static readonly JsonSerializerOptions _json = new()
        {
PropertyNameCaseInsensitive = true,
         PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        };

   public bool IsUnlocked     => _store.IsUnlocked;
        public bool IsSetupComplete => _settings.HasSetupCompleted;

        public event EventHandler<bool>? LockStateChanged;

        public VaultSessionService(PipeClient pipe, IDesktopSessionStore store)
        {
  _pipe  = pipe;
            _store = store;
 }

        // ── Setup ─────────────────────────────────────────────────────────────
        // Setup does NOT go through the pipe � the service isn't set up yet.
        // We write the verifier/hash into the shared prefs file directly so
        // the service can read them on the very next unlock call.

        public async Task<bool> SetupMasterPasswordAsync(string password, string confirm)
        {
            if (string.IsNullOrWhiteSpace(password) || password != confirm)
 return false;

      // ── Desktop-local DPAPI store ─────────────────────────────────────
       var verifier = ComputeVerifier(password);
      _settings.SetMasterPasswordVerifier(verifier);
            _settings.SetPinProtectedMasterPassword(password);
          _settings.HasSetupCompleted  = true;
_settings.FailedAttemptCount = 0;

            // ?? Shared service preferences (service reads these on GetStatus) ?
            // Write directly to the shared prefs JSON file.
            WriteSharedPrefs(password);

   // ── Issue a real service unlock right away ────────────────────────
 // The service will have loaded the prefs file already, so unlock
    // should succeed immediately.
       var unlocked = await UnlockWithPasswordAsync(password);
    return unlocked;
     }

        // ── Unlock via pipe ───────────────────────────────────────────────────
        public async Task<bool> UnlockWithPasswordAsync(string password)
        {
if (string.IsNullOrWhiteSpace(password)) return false;

            var response = await _pipe.SendAsync(new IpcRequest
        {
          Method  = "UnlockWithPassword",
  Payload = JsonSerializer.Serialize(
           new VaultActionRequest { Credential = password }, _json),
            });

  if (!response.Success) return false;

            return ApplyToken(response.Payload);
        }

   public async Task<bool> UnlockWithPinAsync(string pin)
        {
var response = await _pipe.SendAsync(new IpcRequest
     {
                Method  = "UnlockWithPin",
        Payload = JsonSerializer.Serialize(
           new VaultActionRequest { Credential = pin }, _json),
            });

        if (!response.Success) return false;

            return ApplyToken(response.Payload);
        }

        // ── PIN setup ─────────────────────────────────────────────────────────
    public Task SetupPinAsync(string pin)
        {
            if (!IsUnlocked)
                throw new InvalidOperationException("Vault must be unlocked to set up PIN.");
            if (pin.Length != 4 || !pin.All(char.IsAsciiDigit))
     throw new ArgumentException("PIN must be exactly 4 digits.");

  var hash = HashPin(pin);

   // Write into the DPAPI store for offline lock-screen display logic.
            _settings.SetPinHash(hash);
       _settings.IsPinUnlockEnabled = true;

            // Write into the shared service prefs so the service can verify.
            WriteSharedPref("pref_isPinUnlockKey",  true);
         WriteSharedPref("pref_pinUnlockHash",   hash);

            return Task.CompletedTask;
    }

   public void SetBiometricEnabled(bool enabled)
        {
   _settings.IsBiometricUnlockEnabled = enabled;
            WriteSharedPref("pref_isBiometricLockedKey", enabled);
        }

        // ── Lock ──────────────────────────────────────────────────────────────
     public void Lock()
        {
     _store.ClearToken();
         RaiseLockStateChanged(false);
      }

      // ── Failed attempts ───────────────────────────────────────────────────
public void RecordFailedAttempt()
        {
            _settings.FailedAttemptCount++;
     if (_settings.FailedAttemptCount >= _settings.MaxFailedAttempts)
            {
             _settings.ClearPin();
 _settings.FailedAttemptCount = 0;
    WriteSharedPref("pref_isPinUnlockKey",          false);
     WriteSharedPref("pref_pinUnlockHash",           string.Empty);
           WriteSharedPref("pref_failedUnlockAttemptCount", 0);
      }
        }

        public void ResetFailedAttempts()
        {
            _settings.FailedAttemptCount = 0;
            WriteSharedPref("pref_failedUnlockAttemptCount", 0);
        }

 // ── Helpers ───────────────────────────────────────────────────────────
        private bool ApplyToken(string payload)
        {
            try
         {
    using var doc = JsonDocument.Parse(payload);
             if (doc.RootElement.TryGetProperty("token", out var t))
  {
     var token = t.GetString();
        if (!string.IsNullOrEmpty(token))
      {
       _store.SetToken(token);
       RaiseLockStateChanged(true);
           return true;
              }
  }
     }
            catch { /* malformed payload */ }
            return false;
     }

     // ?? SHA-256 PIN hash � must match Fortress.Service.VaultSessionService.HashPin ??
        private static string HashPin(string pin)
        {
var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
     return Convert.ToHexString(bytes).ToLowerInvariant();
     }

    private static string ComputeVerifier(string password)
     {
         var salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
   using var pbkdf2 = new Rfc2898DeriveBytes(
          Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256);
 var hash = pbkdf2.GetBytes(32);
            return $"v1:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

        // ── Shared prefs helpers ──────────────────────────────────────────────
        // Writes individual keys into the service's preferences.json file so
        // Fortress.Service reads up-to-date values without requiring a restart.

      private static readonly string SharedPrefsPath = System.IO.Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
"Fortress", "Service", "preferences.json");

        private static readonly object SharedPrefsLock = new();

private static void WriteSharedPrefs(string masterPassword)
    {
         var salt = new byte[16];
      RandomNumberGenerator.Fill(salt);
            using var pbkdf2 = new Rfc2898DeriveBytes(
     Encoding.UTF8.GetBytes(masterPassword), salt, 100_000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);
var verifier = $"v1:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";

          PatchSharedPrefs(prefs =>
          {
     prefs["pref_databasePassword"]     = JsonSerializer.SerializeToElement(verifier);
     prefs["pref_masterKeyPlain"]         = JsonSerializer.SerializeToElement(masterPassword);
            prefs["hasSetupCompleted"]     = JsonSerializer.SerializeToElement(true);
       prefs["pref_failedUnlockAttemptCount"] = JsonSerializer.SerializeToElement(0);
            });
        }

        private static void WriteSharedPref<T>(string key, T value)
        {
          PatchSharedPrefs(prefs =>
     prefs[key] = JsonSerializer.SerializeToElement(value));
        }

        private static void PatchSharedPrefs(Action<Dictionary<string, JsonElement>> patch)
 {
      lock (SharedPrefsLock)
   {
        try
{
          var dir = System.IO.Path.GetDirectoryName(SharedPrefsPath)!;
       System.IO.Directory.CreateDirectory(dir);

    Dictionary<string, JsonElement> prefs;
         if (System.IO.File.Exists(SharedPrefsPath))
      {
          try
            {
       using var doc = JsonDocument.Parse(
          System.IO.File.ReadAllText(SharedPrefsPath));
  prefs = doc.RootElement
      .EnumerateObject()
         .ToDictionary(p => p.Name, p => p.Value.Clone());
          }
     catch { prefs = new(); }
           }
        else { prefs = new(); }

            patch(prefs);

            var json = JsonSerializer.Serialize(prefs,
      new JsonSerializerOptions { WriteIndented = true });
                    var tmp = SharedPrefsPath + ".tmp";
       System.IO.File.WriteAllText(tmp, json);
           System.IO.File.Move(tmp, SharedPrefsPath, overwrite: true);
    }
 catch { /* best-effort � service will fall back to its own defaults */ }
            }
        }

        private void RaiseLockStateChanged(bool isUnlocked)
      => LockStateChanged?.Invoke(this, isUnlocked);
    }
}
