using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Interface for cloud synchronization services (Google Drive, Dropbox, etc.)
    /// </summary>
    public interface ICloudSyncService
    {
  /// <summary>
        /// Cloud provider name (e.g., "Google Drive", "Dropbox")
   /// </summary>
        string ProviderName { get; }

        /// <summary>
  /// Authenticate with the cloud provider
        /// </summary>
        Task<bool> AuthenticateAsync();

   /// <summary>
      /// Check if currently authenticated
   /// </summary>
        Task<bool> IsAuthenticatedAsync();

   /// <summary>
        /// Sign out from cloud provider
   /// </summary>
        Task SignOutAsync();

      /// <summary>
      /// Upload encrypted backup to cloud
        /// </summary>
  Task<CloudSyncResult> UploadBackupAsync(byte[] encryptedData);

        /// <summary>
        /// Download encrypted backup from cloud
        /// </summary>
        Task<CloudSyncResult<byte[]>> DownloadBackupAsync();

        /// <summary>
  /// Get last sync timestamp
        /// </summary>
        Task<DateTime?> GetLastSyncTimeAsync();

        /// <summary>
        /// Check if backup exists in cloud
        /// </summary>
        Task<bool> BackupExistsAsync();

        /// <summary>
     /// Delete backup from cloud
        /// </summary>
    Task<CloudSyncResult> DeleteBackupAsync();
    }

    public class CloudSyncResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? SyncTime { get; set; }
    }

    public class CloudSyncResult<T> : CloudSyncResult
    {
        public T Data { get; set; }
    }
}
