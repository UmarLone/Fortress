namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Strongly-typed options bound from appsettings.android.json / appsettings.apple.json.
 /// Section name: "GoogleDrive"
    ///
    /// HOW TO GET YOUR CLIENT IDs (developer setup, one-time):
    /// =========================================================
    /// 1. Go to https://console.cloud.google.com
    /// 2. Select project "fortress-488504"
    /// 3. APIs and Services ? Library ? enable "Google Drive API"
 /// 4. APIs and Services ? Credentials ? Create Credentials ? OAuth 2.0 Client ID
    ///
    ///    For ANDROID:
    ///      - Application type : Android
    ///      - Package name     : com.fortress.app
    ///      - SHA-1 fingerprint: run `keytool -list -v -keystore ~/.android/debug.keystore`
    /// (use your release keystore for production builds)
    ///      ? Copy the generated client ID (ends in .apps.googleusercontent.com)
    ///      ? Paste into appsettings.android.json ? GoogleDrive:ClientId
    ///  ? Set RedirectUri to: com.googleusercontent.apps.{numeric_id}:/oauth2redirect
    ///
    ///    For iOS:
    ///    - Application type : iOS
    ///      - Bundle ID        : com.fortress.app
    ///      ? Copy the generated client ID
    ///      ? Paste into appsettings.apple.json ? GoogleDrive:ClientId
    ///      ? Set RedirectUri to: com.googleusercontent.apps.{numeric_id}:/oauth2redirect
    ///
    /// NO client secret is needed – mobile uses PKCE (installed-app flow).
    /// END USERS never see or enter any of this.
    /// </summary>
    public class GoogleDriveOptions
    {
        public const string Section = "GoogleDrive";

        /// <summary>OAuth 2.0 Client ID from Google Cloud Console.</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Reverse-client-ID redirect URI registered in Google Cloud Console.
        /// Format: com.googleusercontent.apps.{numeric_id}:/oauth2redirect
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        public bool IsConfigured =>
          !string.IsNullOrWhiteSpace(ClientId) &&
          !ClientId.StartsWith("YOUR_") &&
            !string.IsNullOrWhiteSpace(RedirectUri);
    }
}
