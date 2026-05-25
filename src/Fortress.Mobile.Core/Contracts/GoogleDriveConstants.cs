namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Constants for Google Drive OAuth 2.0 and API integration.
    /// Replace ClientId values with your own from Google Cloud Console.
    /// OAuth type: Installed App (no client secret needed for mobile).
    /// </summary>
    public static class GoogleDriveConstants
    {
        // ── OAuth Client IDs ─────────────────────────────────────────────────────
        // Create at: https://console.cloud.google.com → APIs & Services → Credentials
        // Type: OAuth 2.0 → Android / iOS (installed app)
        public const string AndroidClientId = "YOUR_ANDROID_CLIENT_ID.apps.googleusercontent.com";
        public const string IosClientId   = "YOUR_IOS_CLIENT_ID.apps.googleusercontent.com";

        // ── OAuth Scopes ─────────────────────────────────────────────────────────
        // Drive.File = only files created by this app (least privilege)
        public const string DriveFileScope = "https://www.googleapis.com/auth/drive.file";
        public const string DriveProfileScope = "https://www.googleapis.com/auth/userinfo.profile";
        public const string DriveEmailScope = "https://www.googleapis.com/auth/userinfo.email";

        // ── Authorization endpoint ───────────────────────────────────────────────
        public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        public const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

        // ── Redirect URI (must match Google Cloud Console) ───────────────────────
        // Android: reverse of client ID
        // iOS: reverse of client ID
        public const string AndroidRedirectUri = "com.fortress.app:/oauth2redirect";
        public const string IosRedirectUri     = "com.fortress.app:/oauth2redirect";

        // ── Backup file details ──────────────────────────────────────────────────
        public const string BackupFileName = "fortress_vault_backup.gkb";
        public const string BackupMimeType = "application/octet-stream";
        public const string AppFolderName  = "FORTRESS Vault";

        // ── Preference keys ──────────────────────────────────────────────────────
        public const string PrefAccessToken = "gdrive_access_token";
        public const string PrefRefreshToken = "gdrive_refresh_token";
        public const string PrefTokenExpiry = "gdrive_token_expiry";
        public const string PrefUserEmail = "gdrive_user_email";
        public const string PrefUserName = "gdrive_user_name";
        public const string PrefLastSyncTime = "gdrive_last_sync_time";
    }
}
