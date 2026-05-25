
namespace Fortress.Mobile.Core.Contracts
{
    public interface IUnlockService
    {
        /// <summary>
        /// Shows the unlock screen and returns whether the user successfully unlocked.
        /// </summary>
        Task<bool> RequestUnlockAsync(bool forceRefresh = false);

        /// <summary>
        /// Signals that the next unlock request is a cold start.
        /// Call this from App.OnStart before CheckAuthentication so UnlockService
        /// navigates to HomePage directly after unlock (avoiding the OS-backgrounding race).
        /// </summary>
        void MarkColdStart();
    }

}
