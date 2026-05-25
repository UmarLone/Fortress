
using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Saves in-app notifications to the local database and broadcasts a
    /// <see cref="Fortress.Mobile.Core.EventAggregators.RefreshNotificationsEvent"/>
    /// so any subscriber (home badge, notification page) can update immediately.
    /// </summary>
    public interface INotificationService
    {
        Task SaveAsync(string title, string message, NotificationType type, string source = null);
    }

    
}
