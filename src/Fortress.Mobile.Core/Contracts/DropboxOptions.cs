namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Strongly-typed options bound from appsettings.android.json / appsettings.apple.json.
    /// Section name: "Dropbox"
    ///
    /// HOW TO GET YOUR APP KEY (developer setup, one-time):
 /// ======================================================
/// 1. Go to https://www.dropbox.com/developers/apps
    /// 2. Click "Create app"
    ///    - API: Scoped access
    ///    - Access type: App folder  (least privilege – only your app's folder)
    ///    - Name: FORTRESS Vault  (or any name)
    /// 3. Under "Permissions" tab enable:
    ///      files.content.write   files.content.read
    /// 4. Under "Settings" tab:
    /// - Note the "App key"  – paste it into appsettings.android.json / appsettings.apple.json
    ///  - Add redirect URI:   db-{AppKey}://2/token   (Android)
    ///          db-{AppKey}://2/token   (iOS)
    ///    - Enable "Allow PKCE without client secret"  (already default for mobile)
    ///
    /// No client secret is needed – mobile uses PKCE (installed-app flow).
    /// End users never see or enter any of this.
    /// </summary>
    public class DropboxOptions
    {
        public const string Section = "Dropbox";

        /// <summary>App key from the Dropbox Developer Console.</summary>
        public string AppKey { get; set; } = string.Empty;

        /// <summary>
 /// Redirect URI registered in the Dropbox app settings.
        /// Format: db-{AppKey}://2/token
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        public bool IsConfigured =>
          !string.IsNullOrWhiteSpace(AppKey) &&
    !AppKey.StartsWith("YOUR_") &&
            !string.IsNullOrWhiteSpace(RedirectUri);
    }
}
