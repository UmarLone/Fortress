using Fortress.Mobile.Core.Models;
using System.Threading.Tasks;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Interface for syncing credential data and preferences to shared storage
    /// for use by app extensions (iOS Autofill Extension).
    /// This interface is only implemented on iOS.
    /// </summary>
    public interface ISharedCredentialWriter
    {
        /// <summary>
        /// Syncs all credentials to shared storage for the extension to access
        /// </summary>
        Task SyncCredentialsToSharedStorageAsync();
        
        /// <summary>
        /// Syncs the lock state and related preferences to shared storage
        /// </summary>
        void SyncLockStateToSharedPreferences();
        
        /// <summary>
        /// Clears all shared data (call on logout)
        /// </summary>
        void ClearSharedData();
        
        /// <summary>
        /// Processes pending credential usage events written by the extension
        /// </summary>
        Task ProcessPendingUsageEventsAsync();
        
        /// <summary>
        /// Checks if the autofill extension is enabled in iOS Settings
        /// </summary>
        bool IsAutofillEnabled();

        /// <summary>
        /// Returns credentials queued for saving by the iOS autofill extension.
        /// Returns an empty list on non-iOS or when the queue is empty.
        /// </summary>
        List<PendingSaveItem> GetPendingSaveCredentials();

        /// <summary>
        /// Removes the first (oldest) item from the pending-save queue after
        /// it has been handed to the main app UI for confirmation.
        /// </summary>
        void ClearFirstPendingSaveCredential();
    }

    /// <summary>Platform-agnostic pending-save item (mirrors the iOS extension's PendingSaveCredential).</summary>
    public sealed class PendingSaveItem
    {
        public string Domain   { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}
