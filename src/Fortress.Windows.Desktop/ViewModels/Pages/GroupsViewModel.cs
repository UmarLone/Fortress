using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class GroupsViewModel : ObservableObject, INavigationAware
    {
      private readonly IDesktopDataService _dataService;

 [ObservableProperty] private ObservableCollection<VaultGroupViewModel> _items = new();
    [ObservableProperty] private bool _isLoading = true;

       public GroupsViewModel(IDesktopDataService dataService) => _dataService = dataService;

       public async Task OnNavigatedToAsync()
      {
      if (Items.Count == 0) await LoadAsync();
 }

  public Task OnNavigatedFromAsync() => Task.CompletedTask;

   private async Task LoadAsync()
     {
        IsLoading = true;
  try
  {
      var groups = await _dataService.GetGroupsAsync();
        Items = new ObservableCollection<VaultGroupViewModel>(
             groups.Select(g => new VaultGroupViewModel(g)));
  }
  finally { IsLoading = false; }
 }
    }
}
