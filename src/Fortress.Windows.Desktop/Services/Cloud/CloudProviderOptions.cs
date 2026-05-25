namespace Fortress.Windows.Desktop.Services.Cloud
{
    /// <summary>
    /// Strongly-typed options bound from appsettings.json, section "GoogleDrive".
    /// Mirrors Fortress.Mobile.Core GoogleDriveOptions.
    /// </summary>
    public class GoogleDriveOptions
    {
        public const string Section = "GoogleDrive";

        /// <summary>OAuth 2.0 Client ID from Google Cloud Console.</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Loopback redirect URI registered in Google Cloud Console.
        /// Format: http://localhost:{port}/
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) &&
      !ClientId.StartsWith("YOUR_") &&
   !string.IsNullOrWhiteSpace(RedirectUri);
    }

    /// <summary>
    /// Strongly-typed options bound from appsettings.json, section "Dropbox".
    /// Mirrors Fortress.Mobile.Core DropboxOptions.
    /// </summary>
  public class DropboxOptions
    {
 public const string Section = "Dropbox";

        /// <summary>App key from the Dropbox App Console.</summary>
    public string AppKey { get; set; } = string.Empty;

        /// <summary>
        /// Loopback redirect URI registered in the Dropbox App Console.
        /// Format: http://localhost:{port}/
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        public bool IsConfigured =>
  !string.IsNullOrWhiteSpace(AppKey) &&
            !AppKey.StartsWith("YOUR_") &&
            !string.IsNullOrWhiteSpace(RedirectUri);
    }

    /// <summary>
    /// Strongly-typed options bound from appsettings.json, section "OneDrive".
    /// Mirrors Fortress.Mobile.Core OneDriveOptions.
    /// </summary>
    public class OneDriveOptions
    {
        public const string Section = "OneDrive";

     /// <summary>Application (client) ID from the Azure portal.</summary>
  public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Native-client redirect URI. Use the pre-registered Microsoft value:
    /// https://login.microsoftonline.com/common/oauth2/nativeclient
        /// This avoids the loopback block on the Entra public-client flow.
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

   public bool IsConfigured =>
     !string.IsNullOrWhiteSpace(ClientId) &&
       !ClientId.StartsWith("YOUR_") &&
     !string.IsNullOrWhiteSpace(RedirectUri);
    }
}
