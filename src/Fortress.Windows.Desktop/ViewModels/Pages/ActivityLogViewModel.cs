using Fortress.Core.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class ActivityLogViewModel : ObservableObject, INavigationAware
    {
     private readonly IDesktopDataService _dataService;
     private List<EventLogViewModel> _allItems = new();

        [ObservableProperty] private ObservableCollection<EventLogViewModel> _items = new();
        [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _searchText = string.Empty;

   public ActivityLogViewModel(IDesktopDataService dataService) => _dataService = dataService;

        public async Task OnNavigatedToAsync()
 {
  if (_allItems.Count == 0) await LoadAsync();
  }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

   private async Task LoadAsync()
        {
   IsLoading = true;
try
     {
     var logs = await _dataService.GetEventLogsAsync();
  _allItems = logs.OrderByDescending(l => l.DateTime)
     .Select(l => new EventLogViewModel(l)).ToList();
       ApplyFilter();
     }
  finally { IsLoading = false; }
     }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

     private void ApplyFilter()
     {
   var q = SearchText?.Trim() ?? string.Empty;
      var filtered = _allItems.AsEnumerable();
   if (!string.IsNullOrEmpty(q))
     filtered = filtered.Where(i =>
        i.CredentialLabel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
         i.Detail.Contains(q, StringComparison.OrdinalIgnoreCase));
 Items = new ObservableCollection<EventLogViewModel>(filtered);
        }
    }
}
