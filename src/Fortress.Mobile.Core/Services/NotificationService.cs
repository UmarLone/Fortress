using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Prism.Events;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Persists a <see cref="UserNotification"/> to the local LiteDB store
    /// and fires <see cref="RefreshNotificationsEvent"/> so the home-page badge
    /// and notification list update without requiring a navigation round-trip.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly IDataStorageService _storage;
        private readonly IEventAggregator _eventAggregator;

        public NotificationService(IDataStorageService storage, IEventAggregator eventAggregator)
        {
            _storage = storage;
            _eventAggregator = eventAggregator;
        }

        public async Task SaveAsync(string title, string message, NotificationType type, string source = null)
        {
            var notification = new UserNotification
            {
                Id = Guid.NewGuid(),
                Title = title,
                Message = message,
                Type = type switch
                {
                    NotificationType.Success     => NotificationType.Success,
                    NotificationType.Warning     => NotificationType.Warning,
                    NotificationType.Error       => NotificationType.Error,
                     _      => NotificationType.Information,
                },
                CreationDateTime = DateTime.UtcNow,
                IsSeen = false,
                IsExpired = false,
            };

            await _storage.AddNotificationAsync(notification);

            // Broadcast so any live subscriber (home badge, notification page) refreshes.
            _eventAggregator.GetEvent<RefreshNotificationsEvent>().Publish(string.Empty);
        }
    }
}
