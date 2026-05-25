namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Constants for Dropbox OAuth 2.0 (PKCE) and API v2 integration.
    /// App key comes from IOptions&lt;DropboxOptions&gt; bound from
    /// appsettings.android.json / appsettings.apple.json.
    /// </summary>
    public static class DropboxConstants
    {
        // ── OAuth 2 endpoints ────────────────────────────────────────────────
        public const string AuthorizationEndpoint = "https://www.dropbox.com/oauth2/authorize";
        public const string TokenEndpoint = "https://api.dropboxapi.com/oauth2/token";

        // ── API v2 endpoints ─────────────────────────────────────────────────
        public const string UploadEndpoint = "https://content.dropboxapi.com/2/files/upload";
        public const string DownloadEndpoint = "https://content.dropboxapi.com/2/files/download";
        public const string DeleteEndpoint = "https://api.dropboxapi.com/2/files/delete_v2";
        public const string GetMetadataEndpoint = "https://api.dropboxapi.com/2/files/get_metadata";
        public const string CurrentAccountEndpoint = "https://api.dropboxapi.com/2/users/get_current_account";

        // ── Backup file path inside the Dropbox app folder ───────────────────
        // The app uses the "App folder" permission — only /Apps/{AppName}/ is
        // accessible, so the path here is relative to that root.
        public const string BackupFilePath = "/fortress_vault_backup.gkb";

        // ── Preference keys ──────────────────────────────────────────────────
        public const string PrefAccessToken = "dropbox_access_token";
        public const string PrefRefreshToken = "dropbox_refresh_token";
        public const string PrefTokenExpiry = "dropbox_token_expiry";
        public const string PrefUserEmail = "dropbox_user_email";
        public const string PrefUserName = "dropbox_user_name";
        public const string PrefLastSyncTime = "dropbox_last_sync_time";
    }
}
