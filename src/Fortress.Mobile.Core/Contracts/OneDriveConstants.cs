namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Constants for Microsoft OneDrive / Graph OAuth 2.0 (PKCE) integration.
    /// Client ID comes from IOptions&lt;OneDriveOptions&gt; bound from
    /// appsettings.android.json / appsettings.apple.json.
    /// </summary>
    public static class OneDriveConstants
    {
        // OAuth 2.0 / Microsoft identity platform endpoints
//
    // /consumers= personal Microsoft accounts only (outlook.com, hotmail.com, live.com)
 //             Use this when your Azure app registration is:
        //               "Accounts in any organizational directory AND personal Microsoft accounts"
        //          OR "Personal Microsoft accounts only"
        //
        // /common     = personal + work/school accounts – requires the Azure app to be registered
        //      as multi-tenant ("Accounts in any organizational directory and personal
  //  Microsoft accounts"). If you see "userAudience" errors switch back to
        //               /consumers or fix the app registration in the Azure portal.
        //
  // We default to /consumers because FORTRESS users sign in with personal OneDrive accounts.
        public const string AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        public const string TokenEndpoint         = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
      public const string RevokeEndpoint        = "https://login.microsoftonline.com/consumers/oauth2/v2.0/logout";

        // ── Microsoft Graph endpoints ────────────────────────────────────────
  public const string GraphMeEndpoint       = "https://graph.microsoft.com/v1.0/me";
        // App folder – a dedicated /Apps/{AppName}/ folder scoped to this app only.
        // Requires Files.ReadWrite.AppFolder scope (least privilege).
        public const string AppFolderEndpoint = "https://graph.microsoft.com/v1.0/me/drive/special/approot";
  public const string BackupFileEndpoint    = "https://graph.microsoft.com/v1.0/me/drive/special/approot:/{0}:/content";
public const string BackupFileMetaEndpoint = "https://graph.microsoft.com/v1.0/me/drive/special/approot:/{0}";
     public const string DeleteFileEndpoint    = "https://graph.microsoft.com/v1.0/me/drive/special/approot:/{0}";

        // ── OAuth scopes ─────────────────────────────────────────────────────
        // Files.ReadWrite.AppFolder = only the app's own folder (least privilege)
        // User.Read = display name + email for the account card
        // offline_access = refresh token so the user stays connected
        public const string Scopes = "Files.ReadWrite.AppFolder User.Read offline_access";

        // ── Backup file name ─────────────────────────────────────────────────
        public const string BackupFileName = "fortress_vault_backup.gkb";

// ── Preference keys ──────────────────────────────────────────────────
    public const string PrefAccessToken  = "onedrive_access_token";
        public const string PrefRefreshToken = "onedrive_refresh_token";
        public const string PrefTokenExpiry  = "onedrive_token_expiry";
        public const string PrefUserEmail    = "onedrive_user_email";
        public const string PrefUserName     = "onedrive_user_name";
        public const string PrefLastSyncTime = "onedrive_last_sync_time";
}
}
