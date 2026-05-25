using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    public class IdentitiesPageViewModel : ViewModelBase
    {
        #region Properties

        private ObservableCollection<IdentityItemViewModel> _identities = [];
        public ObservableCollection<IdentityItemViewModel> Identities
        {
            get => _identities;
            set
            {
                if (SetProperty(ref _identities, value))
                {
                    if (_identities != null) _identities.CollectionChanged -= OnCollectionChanged;
                    if (value != null) value.CollectionChanged += OnCollectionChanged;
                    RaisePropertyChanged(nameof(IdentitiesCountText));
                }
            }
        }

        private void OnCollectionChanged(object? sender,
       System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
      => RaisePropertyChanged(nameof(IdentitiesCountText));

        public string IdentitiesCountText => $"{Identities?.Count ?? 0} identity(ies)";

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (SetProperty(ref _isRefreshing, value) && !value)
                    RaisePropertyChanged(nameof(IdentitiesCountText));
            }
        }

        private bool _noData;
        public bool NoData { get => _noData; set => SetProperty(ref _noData, value); }

        // Plain pass-through – debounce handled by VaultPageHero SearchCommand
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        // ── Multi-select mode ─────────────────────────────────────────────────
        private bool _isSelectionMode;
      public bool IsSelectionMode
          {
      get => _isSelectionMode;
     set { SetProperty(ref _isSelectionMode, value); RaisePropertyChanged(nameof(IsNotSelectionMode)); }
      }
       public bool IsNotSelectionMode => !_isSelectionMode;

      private int _selectedCount;
       public int SelectedCount
     {
     get => _selectedCount;
  set
     {
  SetProperty(ref _selectedCount, value);
  RaisePropertyChanged(nameof(SelectedCountText));
   RaisePropertyChanged(nameof(HasSelection));
    RaisePropertyChanged(nameof(IsAllSelected));
   RaisePropertyChanged(nameof(SelectAllButtonText));
     }
    }
  public string SelectedCountText => $"{_selectedCount} selected";
  public bool HasSelection => _selectedCount > 0;
  public bool IsAllSelected => Identities.Count > 0 && _selectedCount == Identities.Count;
  public string SelectAllButtonText => IsAllSelected ? "Deselect All" : "Select All";

private List<IdentityItemViewModel> _allIdentities = [];

        #endregion

        private readonly IDataStorageService _dataStorageService;
        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<IdentitiesPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;

        public IdentitiesPageViewModel(
      INavigationService navigationService,
      IDataStorageService dataStorageService,
       IUserDialogs dialogService,
         IEventAggregator eventAggregator,
      ILogger<IdentitiesPageViewModel> logger,
                IBottomSheetService bottomSheetService)
     : base(navigationService)
        {
            _dataStorageService = dataStorageService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            await LoadAsync();
        }

        // ── Load ──────────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            IsRefreshing = true;
            try
            {
                var all = await _dataStorageService.GetIdentityItemsAsync();
                var items = all.Select(item => new IdentityItemViewModel
                {
                    Id = item.Id,
                    Label = item.Label,
                    FirstName = item.FirstName,
                    LastName = item.LastName,
                    Email = item.Email,
                    Phone = item.Phone,
                    Company = item.Company,
                    AddressLine1 = item.AddressLine1,
                    AddressLine2 = item.AddressLine2,
                    City = item.City,
                    State = item.State,
                    Country = item.Country,
                    PostalCode = item.PostalCode,
                }).ToList();

                _allIdentities = items;
                Identities = new ObservableCollection<IdentityItemViewModel>(items);
                NoData = Identities.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load identities");
            }
            finally { IsRefreshing = false; }
        }

        // ── Search ────────────────────────────────────────────────────────
        private async Task ExecuteApplySearchAsync(string text)
        {
            var filtered = await Task.Run(() =>
            string.IsNullOrWhiteSpace(text)
                 ? _allIdentities
                : _allIdentities.Where(i =>
            (!string.IsNullOrEmpty(i.Label) && i.Label.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
     (!string.IsNullOrEmpty(i.FullName) && i.FullName.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrEmpty(i.Email) && i.Email.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrEmpty(i.Company) && i.Company.Contains(text, StringComparison.OrdinalIgnoreCase)))
             .ToList());

            Identities = new ObservableCollection<IdentityItemViewModel>(filtered);
            NoData = Identities.Count == 0;
        }

        // ── Show Options ──────────────────────────────────────────────────────────
        private async Task ShowOptionsAsync(IdentityItemViewModel item)
        {
   if (item == null) return;

      var options = new List<BottomSheetOption>
 {
  new BottomSheetOption
       {
     Title= "Edit",
  IconGlyph = "\xe3c9",
     Action    = async () =>
      await NavigationService.NavigateAsync(
  nameof(Views.AddEditIdentityPage),
 new NavigationParameters { { "identity", item } })
        },
 new BottomSheetOption
     {
  Title  = "Copy Name",
  IconGlyph = "\xe14d",
 Action    = () =>
  {
    var name = item.FullName?.Trim();
     if (!string.IsNullOrEmpty(name))
     Clipboard.SetTextAsync(name);
  }
  },
    new BottomSheetOption
   {
 Title     = "Copy Email",
    IconGlyph = "\xe0be",
  Action    = () =>
{
     if (!string.IsNullOrEmpty(item.Email))
   Clipboard.SetTextAsync(item.Email);
}
     },
  new BottomSheetOption
 {
   Title       = "Share",
  IconGlyph       = "\xe80d",
     Action     = async () => await ShareIdentityAsync(item)
     },
  new BottomSheetOption
 {
   Title       = "Delete",
  IconGlyph       = "\xe872",
      IconBadgeColor  = Color.FromArgb("#FEE2E2"),
   IconColor    = Color.FromArgb("#EF4444"),
     Action     = async () => await DeleteAsync(item)
     },
 };

     await _bottomSheetService.ShowAsync<Views.PopupPages.BottomSheet,
       ViewModels.PopupPagesViewModels.BottomSheetViewModel, bool>(
   options,
     string.IsNullOrWhiteSpace(item.FullName) ? item.Label : item.FullName);
   }

private async Task ShareIdentityAsync(IdentityItemViewModel item)
        {
if (item is null) return;

        var identityItem = new IdentityItem
 {
          Id = item.Id,
 Label = item.Label,
     FirstName = item.FirstName,
   LastName = item.LastName,
    Email = item.Email,
        Phone = item.Phone,
          Company = item.Company,
     AddressLine1 = item.AddressLine1,
          AddressLine2 = item.AddressLine2,
     City = item.City,
                State = item.State,
     Country = item.Country,
          PostalCode = item.PostalCode,
  };

    await NavigationService.NavigateAsync(
             nameof(Views.ShareItemPage),
           new NavigationParameters { { "identity", identityItem } });
        }

        // ── Delete ────────────────────────────────────────────────────────
        private async Task DeleteAsync(IdentityItemViewModel item)
        {
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
  "Delete Identity",
      $"Delete \"{item.Label}\"?",
   "Delete", "Cancel");
            if (!confirmed) return;
            try
            {
                await _dataStorageService.DeleteIdentityItemAsync(item.Id);
                Identities.Remove(item);
                _allIdentities.Remove(item);
                NoData = Identities.Count == 0;
                _dialogService.ShowToast("Identity deleted");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete identity");
                _dialogService.ShowToast("Failed to delete identity");
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────
        private AsyncCommand _addCommand;
        public ICommand AddCommand =>
     _addCommand ??= new AsyncCommand(async () => await NavigationService.NavigateAsync(nameof(Views.AddEditIdentityPage)));

        private AsyncCommand _refreshCommand;
        public ICommand RefreshCommand =>
       _refreshCommand ??= new AsyncCommand(LoadAsync);

        private AsyncCommand<IdentityItemViewModel> _editCommand;
        public ICommand EditCommand =>
           _editCommand ??= new AsyncCommand<IdentityItemViewModel>(async item =>
    await NavigationService.NavigateAsync(nameof(Views.AddEditIdentityPage),
       new NavigationParameters { { "identity", item } }));

        private AsyncCommand<IdentityItemViewModel> _deleteCommand;
        public ICommand DeleteCommand =>
      _deleteCommand ??= new AsyncCommand<IdentityItemViewModel>(DeleteAsync);

        private AsyncCommand<IdentityItemViewModel> _showOptionsCommand;
        public ICommand ShowOptionsCommand =>
     _showOptionsCommand ??= new AsyncCommand<IdentityItemViewModel>(ShowOptionsAsync);

        private AsyncCommand<string>? _applySearchCommand;
        public ICommand ApplySearchCommand =>
   _applySearchCommand ??= new AsyncCommand<string>(ExecuteApplySearchAsync);

        // ── Multi-select commands ─────────────────────────────────────────────
     private DelegateCommand _enterSelectionModeCommand;
     public ICommand EnterSelectionModeCommand =>
     _enterSelectionModeCommand ??= new DelegateCommand(() => { IsSelectionMode = true; SelectedCount = 0; });

 private DelegateCommand _exitSelectionModeCommand;
  public ICommand ExitSelectionModeCommand =>
  _exitSelectionModeCommand ??= new DelegateCommand(() =>
   {
    foreach (var item in Identities) item.IsSelected = false;
      IsSelectionMode = false; SelectedCount = 0;
  });

 private DelegateCommand<IdentityItemViewModel> _toggleItemSelectionCommand;
  public ICommand ToggleItemSelectionCommand =>
  _toggleItemSelectionCommand ??= new DelegateCommand<IdentityItemViewModel>(item =>
    {
  if (item is null) return;
  item.IsSelected = !item.IsSelected;
  SelectedCount = Identities.Count(i => i.IsSelected);
  });

 private DelegateCommand<IdentityItemViewModel> _longPressItemCommand;
  public ICommand LongPressItemCommand =>
  _longPressItemCommand ??= new DelegateCommand<IdentityItemViewModel>(item =>
  {
  if (item is null) return;
  if (!IsSelectionMode) { IsSelectionMode = true; SelectedCount = 0; }
  item.IsSelected = true;
  SelectedCount = Identities.Count(i => i.IsSelected);
  });

 private DelegateCommand _selectAllCommand;
  public ICommand SelectAllCommand =>
  _selectAllCommand ??= new DelegateCommand(() =>
  {
  var newState = !IsAllSelected;
  foreach (var item in Identities) item.IsSelected = newState;
  SelectedCount = newState ? Identities.Count : 0;
  });

 private AsyncCommand _deleteSelectedCommand;
  public ICommand DeleteSelectedCommand =>
  _deleteSelectedCommand ??= new AsyncCommand(DeleteSelectedIdentitiesAsync);

 private async Task DeleteSelectedIdentitiesAsync()
  {
  var selected = Identities.Where(i => i.IsSelected).ToList();
  if (selected.Count == 0) return;
  var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
  "Delete Selected",
  $"Delete {selected.Count} identity(ies)? This cannot be undone.",
  "Delete", "Cancel");
  if (!confirmed) return;
  try
  {
  foreach (var item in selected)
      await _dataStorageService.DeleteIdentityItemAsync(item.Id);
  foreach (var item in selected) { Identities.Remove(item); _allIdentities.Remove(item); }
  _dialogService.ShowToast($"{selected.Count} identity(ies) deleted");
  IsSelectionMode = false; SelectedCount = 0;
  NoData = Identities.Count == 0;
  _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
  }
  catch (Exception ex)
  {
  _logger.LogError(ex, "Failed to delete selected identities");
  _dialogService.ShowToast("Failed to delete some identities");
  }
  }
    }
}
