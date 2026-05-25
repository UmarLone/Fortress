using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class NotificationsViewModel : ObservableObject, INavigationAware
    {
    private readonly IDesktopDataService _dataService;

        [ObservableProperty] private ObservableCollection<NotificationViewModel> _items = new();
      [ObservableProperty] private int _unreadCount;
 [ObservableProperty] private bool _isLoading = true;

      public NotificationsViewModel(IDesktopDataService dataService) => _dataService = dataService;

   public async Task OnNavigatedToAsync() => await LoadAsync();
   public Task OnNavigatedFromAsync() => Task.CompletedTask;

  private async Task LoadAsync()
   {
   IsLoading = true;
    try
      {
    var notes = await _dataService.GetNotificationsAsync();
           var vms = notes.OrderByDescending(n => n.CreationDateTime)
         .Select(n => new NotificationViewModel(n)).ToList();
     Items = new ObservableCollection<NotificationViewModel>(vms);
     UnreadCount = vms.Count(v => !v.IsSeen);
  }
    finally { IsLoading = false; }
   }
    }
}
