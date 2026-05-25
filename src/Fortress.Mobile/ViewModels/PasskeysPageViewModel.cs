using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    public class PasskeysPageViewModel : ViewModelBase
    {
     private readonly IDataStorageService _storage;
  private readonly ILogger<PasskeysPageViewModel> _logger;
   private List<PasskeyItem> _allPasskeys = new();

        private ObservableCollection<PasskeyItem> _passkeys = new();
        public ObservableCollection<PasskeyItem> Passkeys
  {
     get => _passkeys;
            set => SetProperty(ref _passkeys, value);
        }

     private string _searchText = string.Empty;
        public string SearchText
        {
   get => _searchText;
  set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }

    private bool _isEmpty;
      public bool IsEmpty
    {
  get => _isEmpty;
  set => SetProperty(ref _isEmpty, value);
        }

  public string CountText => _allPasskeys.Count == 1
      ? "1 passkey"
   : $"{_allPasskeys.Count} passkeys";

 public PasskeysPageViewModel(
  INavigationService navigationService,
       IDataStorageService storage,
     ILogger<PasskeysPageViewModel> logger)
     : base(navigationService)
   {
 _storage = storage;
   _logger = logger;
   }

        public override async void OnNavigatedTo(INavigationParameters parameters)
   {
  base.OnNavigatedTo(parameters);
     await LoadAsync();
 }

  private async Task LoadAsync()
  {
    try
   {
    _allPasskeys = (await _storage.GetPasskeyItemsAsync())
       .OrderBy(p => p.RpId)
     .ToList();
      ApplyFilter();
 RaisePropertyChanged(nameof(CountText));
      }
  catch (Exception ex)
            {
     _logger.LogError(ex, "PasskeysPage: failed to load");
}
   }

  private void ApplyFilter()
    {
 var q = _searchText;
            var filtered = string.IsNullOrWhiteSpace(q)
  ? _allPasskeys
     : _allPasskeys.Where(p =>
    (p.RpId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
(p.UserDisplayName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
     (p.UserName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

   Passkeys = new ObservableCollection<PasskeyItem>(filtered);
   IsEmpty = Passkeys.Count == 0;
        }

        private AsyncCommand<PasskeyItem> _viewPasskeyCommand;
  public ICommand ViewPasskeyCommand =>
    _viewPasskeyCommand ??= new AsyncCommand<PasskeyItem>(async item =>
     {
 if (item == null) return;
  var p = new NavigationParameters { { "passkeyId", item.Id.ToString() } };
    await NavigationService.NavigateAsync(nameof(Views.PasskeyDetailPage), p);
   });

        private AsyncCommand<PasskeyItem> _deletePasskeyCommand;
        public ICommand DeletePasskeyCommand =>
     _deletePasskeyCommand ??= new AsyncCommand<PasskeyItem>(async item =>
       {
     if (item == null) return;
  await _storage.DeletePasskeyItemAsync(item.Id);
     _allPasskeys.Remove(item);
  ApplyFilter();
        RaisePropertyChanged(nameof(CountText));
  });
    }
}
