namespace Fortress.Core.Contracts
{
    /// <summary>
    /// Contract for cloud backup providers (Google Drive, Dropbox, OneDrive).
    /// Identical surface to Fortress.Mobile.Core ICloudSyncService so that
    /// backup/restore logic is portable across platforms.
  /// </summary>
    public interface ICloudSyncService
    {
     string ProviderName { get; }
        Task<bool> AuthenticateAsync();
        Task<bool> IsAuthenticatedAsync();
        Task SignOutAsync();
        Task<CloudSyncResult> UploadBackupAsync(byte[] encryptedData);
        Task<CloudSyncResult<byte[]>> DownloadBackupAsync();
        Task<DateTime?> GetLastSyncTimeAsync();
        Task<bool> BackupExistsAsync();
        Task<CloudSyncResult> DeleteBackupAsync();
      Task<(string Email, string Name)?> GetUserInfoAsync();
  }

    public class CloudSyncResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? SyncTime { get; set; }
    }

    public class CloudSyncResult<T> : CloudSyncResult
    {
   public T? Data { get; set; }
    }
}
