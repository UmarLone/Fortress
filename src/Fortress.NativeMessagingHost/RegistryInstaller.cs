using Microsoft.Win32;
using System.Reflection;
using System.Text.Json;

namespace Fortress.NativeMessagingHost
{
    /// <summary>
    /// Writes / removes the Windows registry keys that tell Chromium browsers
    /// where to find the Fortress Native Messaging Host manifest.
    ///
    /// Chrome  : HKCU\Software\Google\Chrome\NativeMessagingHosts\com.fortress.vault
    /// Edge    : HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.fortress.vault
    /// Brave   : HKCU\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.fortress.vault
    /// Opera   : HKCU\Software\Opera Software\NativeMessagingHosts\com.fortress.vault
    /// Vivaldi : HKCU\Software\Vivaldi\NativeMessagingHosts\com.fortress.vault
    ///
    /// The registry value contains the absolute path to the JSON manifest file.
    /// </summary>
    internal static class RegistryInstaller
    {
        private const string HostName = "com.fortress.vault";

        /// <summary>
        /// Fallback extension ID used when the user does not pass one explicitly to --install.
        /// This is the ID Chrome assigns when the extension is published to the Chrome Web Store
        /// under the Fortress team's account; for an unpacked dev build, pass the runtime ID
        /// from chrome://extensions as the second argument to --install.
        /// </summary>
        private const string DefaultExtensionId = "ahgmepibgeeifbncphhhjogbcaeabaej";

        private static readonly string[] BrowserRegistryPaths =
        {
            @"Software\Google\Chrome\NativeMessagingHosts",
            @"Software\Microsoft\Edge\NativeMessagingHosts",
            @"Software\Chromium\NativeMessagingHosts",
            @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts",
            @"Software\Opera Software\NativeMessagingHosts",
            @"Software\Vivaldi\NativeMessagingHosts"
        };

        private static string ExeDir =>
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;

        private static string ManifestPath =>
            Path.Combine(ExeDir, $"{HostName}.json");

        private static string ExePath =>
            Path.Combine(ExeDir, "Fortress.NativeMessagingHost.exe");

        // ------------------------------------------------------------------
        // Install
        // ------------------------------------------------------------------

        public static void Install(string? extensionId = null)
        {
            WriteManifest(extensionId ?? DefaultExtensionId);

            foreach (var regPath in BrowserRegistryPaths)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(
                        $@"{regPath}\{HostName}",
                        writable: true);

                    key?.SetValue("", ManifestPath);
                }
                catch
                {
                    // Browser may not exist – ignore.
                }
            }
        }

        // ------------------------------------------------------------------
        // Uninstall
        // ------------------------------------------------------------------

        public static void Uninstall()
        {
            foreach (var regPath in BrowserRegistryPaths)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKey(
                        $@"{regPath}\{HostName}",
                        throwOnMissingSubKey: false);
                }
                catch
                {
                    // Ignore
                }
            }

            try
            {
                if (File.Exists(ManifestPath))
                    File.Delete(ManifestPath);
            }
            catch
            {
                // Ignore
            }
        }

        // ------------------------------------------------------------------
        // Manifest Creation
        // ------------------------------------------------------------------

        private static void WriteManifest(string extensionId)
        {
            var manifest = new
            {
                name = HostName,
                description = "Fortress Password Manager – Native Messaging Bridge",
                path = ExePath,
                type = "stdio",
                allowed_origins = new[]
                {
                    $"chrome-extension://{extensionId}/"
                }
            };

            var json = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            Directory.CreateDirectory(ExeDir);
            File.WriteAllText(ManifestPath, json);
        }
    }
}