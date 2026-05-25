using Fortress.Core.Contracts;
using Fortress.Core.Models;
using Fortress.Core.Services;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.Service.Infrastructure
{
    /// <summary>
    /// Manages the vault lock/unlock lifecycle for the Windows Service.
/// Holds the in-memory session token that all IPC callers must present.
    /// Supports: master password, PIN, and Windows Hello (future).
    /// </summary>
    public sealed class VaultSessionService
    {
     private readonly FortressPreferenceWrapper _prefs;
  private readonly ILogger<VaultSessionService> _logger;
  private readonly VaultSettingsReader _vaultSettings;

        // In-memory session token – regenerated on every unlock, wiped on lock.
  // Never persisted to disk.
      private string? _sessionToken;
     private readonly object _tokenLock = new();

        // Automatic lock timer
        private Timer? _lockTimer;
        private readonly object _timerLock = new();

  public VaultSessionService(
       FortressPreferenceWrapper prefs,
            ILogger<VaultSessionService> logger,
            VaultSettingsReader vaultSettings)
  {
   _prefs = prefs;
 _logger = logger;
            _vaultSettings = vaultSettings;
  }

        // ── State ─────────────────────────────────────────────────────────────
  public bool IsUnlocked
  {
 get { lock (_tokenLock) return _sessionToken != null; }
        }

  /// <summary>Validates a session token presented by an IPC caller.</summary>
        public bool ValidateToken(string? token)
  {
    if (string.IsNullOrEmpty(token)) return false;
   lock (_tokenLock) return string.Equals(_sessionToken, token, StringComparison.Ordinal);
     }

     public VaultSessionInfo GetSessionInfo() => new()
  {
     IsUnlocked     = IsUnlocked,
  DeviceId         = Environment.MachineName,
   UnlockedAt       = IsUnlocked ? DateTime.UtcNow : null,
  // Setup state lives in two places: vault.settings.json (written by the
  // desktop app) and the service's own preferences (written by CompleteSetup
  // when the browser extension drives setup with no desktop present). Either
  // source is authoritative — whichever was written most recently wins.
      IsSetupComplete  = _vaultSettings.HasSetupCompleted || _prefs.HasSetupCompleted,
        IsPinEnabled     = _vaultSettings.IsPinUnlockEnabled || _prefs.IsPinUnlockEnabled,
        IsBiometricEnabled = _vaultSettings.IsBiometricUnlockEnabled || _prefs.IsBiometricUnlockEnabled,
      IsPasswordEnabled  = !string.IsNullOrEmpty(_prefs.DatabasePassword),
    };

  // ── Unlock ────────────────────────────────────────────────────────────
    /// <summary>
     /// Attempts to unlock the vault with a master password.
     /// Returns a fresh session token on success, null on failure.
     /// </summary>
        public CommandResult<string> UnlockWithPassword(string password)
        {
     if (string.IsNullOrEmpty(password))
    return Fail("Password cannot be empty.");

      if (!VerifyMasterPassword(password))
       {
     IncrementFailedAttempts();
    return Fail($"Incorrect password. Attempt {_prefs.FailedUnlockAttemptCount}/{_prefs.MaxFailedUnlockAttempts}.");
     }

          // Store the real password for AES key derivation (separate from the verifier)
 _prefs.MasterPasswordForCrypto = password;
   return IssueToken();
  }

        /// <summary>Attempts to unlock the vault with a PIN.</summary>
  public CommandResult<string> UnlockWithPin(string pin)
  {
    if (!_prefs.IsPinUnlockEnabled)
      return Fail("PIN unlock is not enabled.");

  if (string.IsNullOrEmpty(pin))
    return Fail("PIN cannot be empty.");

   var expectedHash = _prefs.PinUnlockHash;
     var actualHash   = HashPin(pin);
      if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
      {
    IncrementFailedAttempts();
    return Fail("Incorrect PIN.");
            }

      return IssueToken();
     }

  // ── Setup (extension-driven first run) ─────────────────────────────────
  /// <summary>
  /// Persists the master-password verifier, the optional PIN hash, and the
  /// "setup complete" flag from a browser-extension-driven first-run wizard.
  /// Fails if the service is already set up — the caller must reset first.
  /// </summary>
  public CommandResult<string> CompleteSetup(string masterPassword, string? pin)
  {
      if (_vaultSettings.HasSetupCompleted || _prefs.HasSetupCompleted)
          return Fail("Setup is already complete. Reset the vault before running setup again.");

      if (string.IsNullOrEmpty(masterPassword))
          return Fail("Master password cannot be empty.");

      // Write the PBKDF2 verifier — same v1 format VerifyMasterPassword expects.
      var saltBytes = new byte[16];
      RandomNumberGenerator.Fill(saltBytes);
      using var kdf = new System.Security.Cryptography.Rfc2898DeriveBytes(
          System.Text.Encoding.UTF8.GetBytes(masterPassword),
          saltBytes, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256);
      var hash = kdf.GetBytes(32);
      _prefs.DatabasePassword =
          $"v1:{Convert.ToBase64String(saltBytes)}:{Convert.ToBase64String(hash)}";

      // CryptographyService derives AES keys from the plaintext, not the verifier.
      _prefs.MasterPasswordForCrypto = masterPassword;

      // Optional PIN.
      if (!string.IsNullOrEmpty(pin))
      {
          _prefs.PinUnlockHash = HashPin(pin);
          _prefs.IsPinUnlockEnabled = true;
      }
      else
      {
          _prefs.IsPinUnlockEnabled = false;
          _prefs.PinUnlockHash = string.Empty;
      }

      _prefs.HasSetupCompleted = true;
      _logger.LogInformation("[VaultSession] Setup completed via extension. PIN enabled: {Pin}", _prefs.IsPinUnlockEnabled);

      // Issue a session token so the caller is unlocked immediately and doesn't
      // have to re-enter the password they just chose.
      return IssueToken();
  }

  // ── Lock ──────────────────────────────────────────────────────────────
public void Lock()
  {
     lock (_tokenLock) _sessionToken = null;
  StopLockTimer();
 _prefs.IsApplicationLocked = true;
    _logger.LogInformation("[VaultSession] Vault locked.");
 }

 // ── Helpers ───────────────────────────────────────────────────────────
        private CommandResult<string> IssueToken()
        {
 var token = GenerateToken();
     lock (_tokenLock) _sessionToken = token;
       _prefs.FailedUnlockAttemptCount = 0;
  _prefs.IsApplicationLocked = false;
  StartLockTimer();
  _logger.LogInformation("[VaultSession] Vault unlocked. Token issued.");
      return new CommandResult<string>(token) { Succeeded = true };
   }

    private bool VerifyMasterPassword(string password)
    {
      var stored = _prefs.DatabasePassword;
      if (string.IsNullOrEmpty(stored)) return false;

        // Support both plain-text (legacy first-run) and PBKDF2 verifier format "v1:{salt}:{hash}"
      if (stored.StartsWith("v1:", StringComparison.Ordinal))
        {
            var parts = stored.Split(':');
 if (parts.Length != 3) return false;
     var salt     = Convert.FromBase64String(parts[1]);
      var expected = Convert.FromBase64String(parts[2]);
            using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
           System.Text.Encoding.UTF8.GetBytes(password),
                salt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256);
         var actual = pbkdf2.GetBytes(32);
         return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actual, expected);
}

      // Legacy plain-text compare – upgrade to verifier hash on success
        if (!string.Equals(stored, password, StringComparison.Ordinal))
        return false;

      // Upgrade: replace plaintext with PBKDF2 verifier
        var saltBytes = new byte[16];
 System.Security.Cryptography.RandomNumberGenerator.Fill(saltBytes);
        using var kdf = new System.Security.Cryptography.Rfc2898DeriveBytes(
      System.Text.Encoding.UTF8.GetBytes(password),
     saltBytes, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256);
   var hash = kdf.GetBytes(32);
        _prefs.DatabasePassword =
    $"v1:{Convert.ToBase64String(saltBytes)}:{Convert.ToBase64String(hash)}";
        _logger.LogInformation("[VaultSession] Master password upgraded to PBKDF2 verifier.");
        return true;
    }

        private void IncrementFailedAttempts()
  {
       _prefs.FailedUnlockAttemptCount++;
     if (_prefs.FailedUnlockAttemptCount >= _prefs.MaxFailedUnlockAttempts)
     {
     _logger.LogWarning("[VaultSession] Max failed attempts reached – locking vault.");
  Lock();
  }
    }

        private static string GenerateToken()
   {
     var bytes = RandomNumberGenerator.GetBytes(32);
      return Convert.ToBase64String(bytes);
     }

   private static string HashPin(string pin)
   {
     var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
 return Convert.ToHexString(bytes).ToLowerInvariant();
     }

        private static CommandResult<string> Fail(string message) =>
  new() { Succeeded = false, ErrorMessage = message };

        // ── Auto-lock timer ───────────────────────────────────────────────────
        private void StartLockTimer()
        {
  int seconds = _prefs.LockTimeout;
   if (seconds <= 0) return;   // 0 = never auto-lock

     lock (_timerLock)
      {
   _lockTimer?.Dispose();
    _lockTimer = new Timer(
        _ => { _logger.LogInformation("[VaultSession] Auto-lock triggered."); Lock(); },
    null,
    TimeSpan.FromSeconds(seconds),
      Timeout.InfiniteTimeSpan);
  }
        }

        private void StopLockTimer()
        {
lock (_timerLock) { _lockTimer?.Dispose(); _lockTimer = null; }
        }
    }
}
