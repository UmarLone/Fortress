namespace Fortress.Mobile.Core.Contracts
{
 /// <summary>
  /// Strongly-typed options bound from appsettings.android.json / appsettings.apple.json.
    /// Section name: "OneDrive"
  ///
    /// HOW TO GET YOUR CLIENT ID (developer setup, one-time):
    /// =======================================================
    /// 1. Go to https://portal.azure.com ? Azure Active Directory ? App registrations
    /// 2. Click "New registration"
    ///    - Name: FORTRESS Vault
    ///    - Supported account types: "Accounts in any organizational directory and personal Microsoft accounts"
    ///    - Redirect URI: Select "Public client/native (mobile &amp; desktop)"
    ///      Android: msauth://com.fortress.app/{Base64 of your SHA-1 cert hash}
  ///      iOS:     msauth.com.fortress.app://auth
    /// 3. After creation, go to "Authentication" tab and enable:
  ///      - "Allow public client flows" ? Yes
 /// 4. Go to "API Permissions" ? Add ? Microsoft Graph:
    ///      Files.ReadWrite.AppFolder, User.Read, offline_access
    /// 5. Copy the "Application (client) ID" from the Overview page
    ///    ? Paste into appsettings.android.json / appsettings.apple.json ? OneDrive:ClientId
    ///
    /// No client secret is needed – mobile uses PKCE (public client flow).
  /// End users never see or enter any of this.
    /// </summary>
    public class OneDriveOptions
    {
public const string Section = "OneDrive";

     /// <summary>Application (client) ID from the Azure portal.</summary>
        public string ClientId { get; set; } = string.Empty;

  /// <summary>
        /// Redirect URI registered in the Azure portal.
        /// Android: msauth://com.fortress.app/{base64_sha1}
  /// iOS:  msauth.com.fortress.app://auth
        /// </summary>
 public string RedirectUri { get; set; } = string.Empty;

        public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
            !ClientId.StartsWith("YOUR_") &&
         !string.IsNullOrWhiteSpace(RedirectUri);
    }
}
