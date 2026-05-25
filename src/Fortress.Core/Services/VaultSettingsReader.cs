using System.Text.Json;

namespace Fortress.Core.Services
{
    /// <summary>
    /// Reads the subset of <c>vault.settings.json</c> that the
    /// Fortress.Service needs at runtime (e.g. HasSetupCompleted).
    ///
    /// This file is written exclusively by <c>Fortress.Windows.Desktop</c>
    /// via <c>VaultSettingsStore</c> and lives at:
    ///   <c>%LOCALAPPDATA%\Fortress\vault.settings.json</c>
    ///
  /// The service must not write to this file — it is owned by the desktop app.
    /// </summary>
    public sealed class VaultSettingsReader
    {
        private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Fortress",
  "vault.settings.json");

        private static readonly JsonSerializerOptions _jsonOpts = new()
     {
        PropertyNameCaseInsensitive = true,
 };

        /// <summary>
  /// Returns true when the desktop app has completed the first-run setup wizard.
        /// Falls back to false if the file does not exist or cannot be parsed.
        /// </summary>
 public bool HasSetupCompleted
        {
            get
          {
           try
        {
         if (!File.Exists(SettingsFile)) return false;
         using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile), new JsonDocumentOptions { AllowTrailingCommas = true });
    if (doc.RootElement.TryGetProperty("HasSetupCompleted", out var el))
  return el.GetBoolean();
         }
         catch { /* file locked or corrupt — treat as not set up */ }
    return false;
            }
  }

        /// <summary>
        /// Returns true when Windows Hello / biometric unlock was enabled during setup.
        /// </summary>
      public bool IsBiometricUnlockEnabled
   {
  get
            {
              try
    {
      if (!File.Exists(SettingsFile)) return false;
      using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile), new JsonDocumentOptions { AllowTrailingCommas = true });
     if (doc.RootElement.TryGetProperty("IsBiometricUnlockEnabled", out var el))
    return el.GetBoolean();
     }
         catch { }
             return false;
       }
        }

   /// <summary>
        /// Returns true when PIN unlock was enabled during setup.
        /// </summary>
        public bool IsPinUnlockEnabled
        {
        get
{
      try
                {
 if (!File.Exists(SettingsFile)) return false;
          using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile), new JsonDocumentOptions { AllowTrailingCommas = true });
       if (doc.RootElement.TryGetProperty("IsPinUnlockEnabled", out var el))
      return el.GetBoolean();
       }
     catch { }
   return false;
            }
    }
    }
}
