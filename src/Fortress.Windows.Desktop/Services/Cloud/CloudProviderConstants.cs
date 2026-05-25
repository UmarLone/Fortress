namespace Fortress.Windows.Desktop.Services.Cloud
{
    /// <summary>
    /// Google Drive OAuth 2.0 and API constants.
    /// Mirrors Fortress.Mobile.Core GoogleDriveConstants.
/// </summary>
    internal static class GoogleDriveConstants
    {
   public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        public const string TokenEndpoint      = "https://oauth2.googleapis.com/token";
        public const string UserInfoEndpoint      = "https://www.googleapis.com/oauth2/v3/userinfo";
        public const string RevokeEndpoint   = "https://oauth2.googleapis.com/revoke";

      public const string DriveFileScope    = "https://www.googleapis.com/auth/drive.file";
        public const string DriveProfileScope = "https://www.googleapis.com/auth/userinfo.profile";
    public const string DriveEmailScope   = "https://www.googleapis.com/auth/userinfo.email";

        public const string BackupFileName = "fortress_vault_backup.gkb";
        public const string BackupMimeType = "application/octet-stream";
        public const string AppFolderName  = "FORTRESS Vault";

        public const string PrefAccessToken  = "gdrive_access_token";
  public const string PrefRefreshToken = "gdrive_refresh_token";
        public const string PrefTokenExpiry  = "gdrive_token_expiry";
      public const string PrefUserEmail    = "gdrive_user_email";
        public const string PrefUserName     = "gdrive_user_name";
      public const string PrefLastSyncTime = "gdrive_last_sync_time";
    }

    /// <summary>
    /// Dropbox OAuth 2.0 and API v2 constants.
    /// Mirrors Fortress.Mobile.Core DropboxConstants.
    /// </summary>
    internal static class DropboxConstants
    {
  public const string AuthorizationEndpoint  = "https://www.dropbox.com/oauth2/authorize";
 public const string TokenEndpoint     = "https://api.dropboxapi.com/oauth2/token";
        public const string RevokeEndpoint    = "https://api.dropboxapi.com/2/auth/token/revoke";
 public const string UploadEndpoint        = "https://content.dropboxapi.com/2/files/upload";
        public const string DownloadEndpoint  = "https://content.dropboxapi.com/2/files/download";
        public const string DeleteEndpoint           = "https://api.dropboxapi.com/2/files/delete_v2";
        public const string GetMetadataEndpoint      = "https://api.dropboxapi.com/2/files/get_metadata";
        public const string CurrentAccountEndpoint   = "https://api.dropboxapi.com/2/users/get_current_account";

      public const string BackupFilePath = "/fortress_vault_backup.gkb";

        public const string PrefAccessToken  = "dropbox_access_token";
        public const string PrefRefreshToken = "dropbox_refresh_token";
   public const string PrefTokenExpiry  = "dropbox_token_expiry";
  public const string PrefUserEmail    = "dropbox_user_email";
    public const string PrefUserName     = "dropbox_user_name";
        public const string PrefLastSyncTime = "dropbox_last_sync_time";
    }

    /// <summary>
    /// OneDrive / Microsoft Graph OAuth 2.0 and API constants.
  /// Mirrors Fortress.Mobile.Core OneDriveConstants.
    /// </summary>
    internal static class OneDriveConstants
    {
        // /consumers = personal Microsoft accounts only (outlook.com, hotmail.com, live.com)
        public const string AuthorizationEndpoint  = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        public const string TokenEndpoint       = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

        public const string GraphMeEndpoint        = "https://graph.microsoft.com/v1.0/me";
        public const string BackupFileEndpoint     = "https://graph.microsoft.com/v1.0/me/drive/special/approot:/{0}:/content";
        public const string BackupFileMetaEndpoint = "https://graph.microsoft.com/v1.0/me/drive/special/approot:/{0}";
        public const string DeleteFileEndpoint     = "https://graph.microsoft.com/v1.0/me/drive/special/approot:/{0}";

    public const string Scopes = "Files.ReadWrite.AppFolder User.Read offline_access";

        public const string BackupFileName = "fortress_vault_backup.gkb";

      public const string PrefAccessToken  = "onedrive_access_token";
        public const string PrefRefreshToken = "onedrive_refresh_token";
     public const string PrefTokenExpiry  = "onedrive_token_expiry";
     public const string PrefUserEmail    = "onedrive_user_email";
        public const string PrefUserName     = "onedrive_user_name";
  public const string PrefLastSyncTime = "onedrive_last_sync_time";
    }
}
