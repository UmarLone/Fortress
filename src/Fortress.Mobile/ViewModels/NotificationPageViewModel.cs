using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Fortress.Services;

namespace Fortress.ViewModels
{
    public class NotificationPageViewModel : ViewModelBase
    {
        #region Properties

        private ObservableCollection<UserNotification> _notifications = new();
        public ObservableCollection<UserNotification> Notifications
        {
            get => _notifications;
            set => SetProperty(ref _notifications, value);
        }

        private UserNotification _selectedNotification;
        public UserNotification SelectedNotification
        {
            get => _selectedNotification;
            set => SetProperty(ref _selectedNotification, value);
        }

        #endregion

        private readonly IDataStorageService _dataStorageService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IUserDialogs _dialogService;
        private readonly IBottomSheetService _bottomSheetService;

        public NotificationPageViewModel(
    INavigationService navigationService,
    IUserDialogs dialogService,
        IDataStorageService dataStorageService,
        IEventAggregator eventAggregator,
            IBottomSheetService bottomSheetService)
         : base(navigationService)
        {
            _dataStorageService = dataStorageService;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _bottomSheetService = bottomSheetService;
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            await Task.WhenAll(
             LoadNotificationsAsync(),
         _dataStorageService.SetNotificationsSeenAsync());
            _eventAggregator.GetEvent<RefreshNotificationsEvent>().Publish(string.Empty);
        }

        private async Task LoadNotificationsAsync()
        {
            try
            {
                var stored = await _dataStorageService.GetNotificationsAsync();
                Notifications = new ObservableCollection<UserNotification>(
                 stored
                 .Where(n => !n.IsExpired)
                  .OrderByDescending(n => n.CreationDateTime));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationPage] Load error: {ex.Message}");
            }
        }

        #region Commands

        private async Task ExecuteViewCommand(UserNotification notification)
        {
            if (notification == null) return;
            var parameters = new NavigationParameters { { "Notification", notification } };
            await NavigationService.NavigateAsync(nameof(Views.NotificationDetailPage), parameters);
        }

        private async Task ExecuteDeleteCommand(UserNotification notification)
        {
            if (notification == null) return;
            var result = await _bottomSheetService.ConfirmAsync(
      "Delete Notification", $"Remove \"{notification.Title}\"?", "Yes", "No");
            if (!result) return;

            Notifications.Remove(notification);
            try
            {
                await _dataStorageService.DeleteNotificationsAsync(new[] { notification.Id });
                _eventAggregator.GetEvent<RefreshNotificationsEvent>().Publish(string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationPage] Delete error: {ex.Message}");
            }
        }

        private async Task ExecuteClearAllCommand()
        {
            if (Notifications.Count == 0) return;
            var result = await _bottomSheetService.ConfirmAsync(
             "Clear All", "Remove all notifications?", "Yes", "No");
            if (!result) return;

            var ids = Notifications.Select(n => n.Id).ToList();
            Notifications.Clear();
            try
            {
                await _dataStorageService.DeleteNotificationsAsync(ids);
                _eventAggregator.GetEvent<RefreshNotificationsEvent>().Publish(string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationPage] ClearAll error: {ex.Message}");
            }
        }

        private AsyncCommand<UserNotification> _viewCommand;
        public ICommand ViewCommand =>
     _viewCommand ??= new AsyncCommand<UserNotification>(ExecuteViewCommand);

        private AsyncCommand<UserNotification> _deleteCommand;
        public ICommand DeleteCommand =>
     _deleteCommand ??= new AsyncCommand<UserNotification>(ExecuteDeleteCommand);

        private AsyncCommand _clearAllCommand;
        public ICommand ClearAllCommand =>
      _clearAllCommand ??= new AsyncCommand(ExecuteClearAllCommand);

        #endregion
    }
}
