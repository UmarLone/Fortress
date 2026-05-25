using System.ComponentModel.DataAnnotations;

namespace Fortress.Mobile.Core.Models
{
    /// <summary>
  /// Vault activity events recorded locally on the device.
    /// These are displayed in the Activity Log page and are completely
    /// separate from the file-based diagnostic (Local Logs) output.
    /// </summary>
    public enum EventLogType
    {
        // ── Vault unlock / lock ────────────────────────────────────────────
        [Display(Name = "Vault Unlocked")]
        VaultUnlocked = 1,

        [Display(Name = "Vault Locked")]
        VaultLocked = 2,

        [Display(Name = "Unlock Failed")]
        UnlockFailed = 3,

        // ── Credential CRUD ────────────────────────────────────────────────
        [Display(Name = "Credential Added")]
     CredentialAdded = 10,

        [Display(Name = "Credential Updated")]
        CredentialUpdated = 11,

     [Display(Name = "Credential Deleted")]
        CredentialDeleted = 12,

        [Display(Name = "Credential Viewed")]
     CredentialViewed = 13,

        // ── Autofill ───────────────────────────────────────────────────────
        [Display(Name = "Password Filled")]
      WebCredentialUsed = 20,

     [Display(Name = "App Password Filled")]
        PhonePasswordUsed = 21,

    [Display(Name = "OTP Copied")]
        OtpCopied = 22,

        [Display(Name = "Autofill Blocked (Risk)")]
        AutofillBlockedRisk = 23,

        [Display(Name = "Autofill Warned (Risk)")]
        AutofillWarnedRisk = 24,

        // ── Passkeys ───────────────────────────────────────────────────────
        [Display(Name = "Passkey Registered")]
 PasskeyRegistered = 30,

        [Display(Name = "Passkey Used")]
  PasskeyUsed = 31,

        [Display(Name = "Passkey Deleted")]
        PasskeyDeleted = 32,

        // ── Cloud sync ────────────────────────────────────────────────────
  [Display(Name = "Cloud Sync Success")]
     CloudSyncSuccess = 40,

        [Display(Name = "Cloud Sync Failed")]
        CloudSyncFailed = 41,

        // ── Security settings ─────────────────────────────────────────────
        [Display(Name = "Master Password Changed")]
    MasterPasswordChanged = 50,

        [Display(Name = "Biometric Enabled")]
        BiometricEnabled = 51,

        [Display(Name = "Biometric Disabled")]
    BiometricDisabled = 52,

        [Display(Name = "PIN Enabled")]
        PinEnabled = 53,

  [Display(Name = "PIN Disabled")]
        PinDisabled = 54,

        // ── Vault data ────────────────────────────────────────────────────
        [Display(Name = "Vault Exported")]
        VaultExported = 60,

        [Display(Name = "Vault Imported")]
        VaultImported = 61,

        [Display(Name = "Account Removed")]
   AccountRemoved = 62,
    }
}
