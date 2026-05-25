using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using MauiIcons.Core;
using MauiIcons.Material;

namespace Fortress.ViewModels
{
    /// <summary>
    /// ViewModel for the unified "Secure Items" list page.
    /// Shows all ID documents, Social Security numbers, Tax IDs, Wi-Fi
    /// credentials and SSH entries in a single filterable list.
    /// </summary>
    public class SecureItemsPageViewModel : ViewModelBase
    {
        #region Properties

        private ObservableCollection<SecureItemViewModel> _items = [];
        public ObservableCollection<SecureItemViewModel> Items
        {
            get => _items;
            set
            {
                if (SetProperty(ref _items, value))
                {
                    if (_items != null) _items.CollectionChanged -= OnCollectionChanged;
                    if (value != null) value.CollectionChanged += OnCollectionChanged;
                    RaisePropertyChanged(nameof(ItemsCountText));
                }
            }
        }

        private void OnCollectionChanged(object? sender,
   System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
             => RaisePropertyChanged(nameof(ItemsCountText));

        public string ItemsCountText => $"{Items?.Count ?? 0} item(s)";

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (SetProperty(ref _isRefreshing, value) && !value)
                    RaisePropertyChanged(nameof(ItemsCountText));
            }
        }

        private bool _noData;
        public bool NoData { get => _noData; set => SetProperty(ref _noData, value); }

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
  public bool IsAllSelected => Items.Count > 0 && _selectedCount == Items.Count;
 public string SelectAllButtonText => IsAllSelected ? "Deselect All" : "Select All";

        private List<SecureItemViewModel> _allItems = [];

        #endregion

        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<SecureItemsPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;

        public SecureItemsPageViewModel(
      INavigationService navigationService,
             IDataStorageService dataStorageService,
        ICryptographyService cryptographyService,
         IUserDialogs dialogService,
        IEventAggregator eventAggregator,
             ILogger<SecureItemsPageViewModel> logger,
       IBottomSheetService bottomSheetService)
          : base(navigationService)
        {
            _dataStorageService = dataStorageService;
            _cryptographyService = cryptographyService;
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

        // ── Load ──────────────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            IsRefreshing = true;
            try
            {
                var all = await _dataStorageService.GetSecureItemsAsync();
                var list = new List<SecureItemViewModel>();

                foreach (var item in all)
                {
                    var number     = await DecryptAsync(item.Number);
       var password   = await DecryptAsync(item.Password);
    var sshPwd     = await DecryptAsync(item.SshPassword);
 var privateKey = await DecryptAsync(item.PrivateKey);
  var noteContent = await DecryptAsync(item.NoteContent);

              list.Add(new SecureItemViewModel
              {
          Id          = item.Id,
         ItemType       = item.ItemType,
         Label        = item.Label,
  // Document fields
         FullName       = item.FullName,
DateOfBirth    = item.DateOfBirth,
              Nationality    = item.Nationality,
         Number      = number,
   IssuingCountry = item.IssuingCountry,
        IssuedDate     = item.IssuedDate,
            ExpiryDate     = item.ExpiryDate,
              // Identity fields
    FirstName  = item.FirstName,
          LastName = item.LastName,
        Email  = item.Email,
   Phone          = item.Phone,
   Company        = item.Company,
 AddressLine1   = item.AddressLine1,
   AddressLine2   = item.AddressLine2,
     City           = item.City,
         State          = item.State,
           PostalCode     = item.PostalCode,
         // Wi-Fi
  Ssid           = item.Ssid,
           WifiSecurity   = item.WifiSecurity,
           Password = password,
         // SSH
          Host     = item.Host,
         Port           = item.Port,
        Username       = item.Username,
         SshPassword  = sshPwd,
         PrivateKey     = privateKey,
      KeyFingerprint = item.KeyFingerprint,
            NoteContent    = noteContent,
    });
      }

                _allItems = list;
                Items = new ObservableCollection<SecureItemViewModel>(list);
                NoData = Items.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load secure items");
            }
            finally { IsRefreshing = false; }
        }

        // ── Search ────────────────────────────────────────────────────────────
        private async Task ExecuteApplySearchAsync(string text)
        {
            var filtered = await Task.Run(() =>
           string.IsNullOrWhiteSpace(text)
           ? _allItems
     : _allItems.Where(i =>
   i.Label.Contains(text, StringComparison.OrdinalIgnoreCase) ||
       i.TypeLabel.Contains(text, StringComparison.OrdinalIgnoreCase) ||
    i.Summary.Contains(text, StringComparison.OrdinalIgnoreCase) ||
       (!string.IsNullOrEmpty(i.FullName) && i.FullName.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrEmpty(i.Ssid) && i.Ssid.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
      (!string.IsNullOrEmpty(i.Host) && i.Host.Contains(text, StringComparison.OrdinalIgnoreCase)))
      .ToList());

            Items = new ObservableCollection<SecureItemViewModel>(filtered);
            NoData = Items.Count == 0;
        }

        // ── Options sheet ─────────────────────────────────────────────────────
        private async Task ShowOptionsAsync(SecureItemViewModel item)
        {
            if (item is null) return;

var copyLabel = item.ItemType switch
   {
       SecureItemType.WiFi     => "Copy Password",
     SecureItemType.Ssh=> "Copy Host",
    SecureItemType.Identity  => "Copy Email",
      SecureItemType.SecureNote => "Copy Note",
           _       => "Copy Number",
  };

 var options = new List<BottomSheetOption>
   {
  new() { Title = "Edit",  Icon = new MauiIcon().Icon(MaterialIcons.Edit),
Action = async () => await OpenEditAsync(item) },
    new() { Title = copyLabel, Icon = new MauiIcon().Icon(MaterialIcons.ContentCopy),
    Action = item.ItemType switch
 {
   SecureItemType.WiFi   => async () => { await Clipboard.Default.SetTextAsync(item.Password); _dialogService.ShowToast("Password copied"); },
    SecureItemType.Ssh      => async () => { await Clipboard.Default.SetTextAsync(item.Host);   _dialogService.ShowToast("Host copied"); },
     SecureItemType.Identity => async () => { await Clipboard.Default.SetTextAsync(item.Email);  _dialogService.ShowToast("Email copied"); },
  SecureItemType.SecureNote => async () => { await Clipboard.Default.SetTextAsync(item.NoteContent); _dialogService.ShowToast("Note copied"); },
       _     => async () => { await Clipboard.Default.SetTextAsync(item.Number);  _dialogService.ShowToast("Number copied"); },
          }},
  new() { Title = "Share", Icon = new MauiIcon().Icon(MaterialIcons.Share),
    Action = async () => await ShareSecureItemAsync(item) },
     new() { Title = "Delete", Icon = new MauiIcon().Icon(MaterialIcons.Delete),
  Action = async () => await DeleteAsync(item) },
         };

 await _bottomSheetService.ShowAsync<Views.PopupPages.BottomSheet,
  BottomSheetViewModel, bool>(options, item.Label);
 }

        private async Task ShareSecureItemAsync(SecureItemViewModel item)
        {
            if (item is null) return;

            // Re-encrypt sensitive fields so they travel encrypted inside the .fortress file
        var secureItem = new SecureItem
            {
    Id = item.Id,
              ItemType = item.ItemType,
        Label = item.Label,
    // Document fields
                FullName = item.FullName,
   DateOfBirth = item.DateOfBirth,
          Nationality = item.Nationality,
           Number = await EncryptAsync(item.Number),
                IssuingCountry = item.IssuingCountry,
       IssuedDate = item.IssuedDate,
     ExpiryDate = item.ExpiryDate,
                // Identity fields
   FirstName = item.FirstName,
            LastName = item.LastName,
      Email = item.Email,
   Phone = item.Phone,
       Company = item.Company,
          AddressLine1 = item.AddressLine1,
 AddressLine2 = item.AddressLine2,
           City = item.City,
      State = item.State,
   PostalCode = item.PostalCode,
      // Wi-Fi
    Ssid = item.Ssid,
     WifiSecurity = item.WifiSecurity,
         Password = await EncryptAsync(item.Password),
          // SSH
                Host = item.Host,
   Port = item.Port,
         Username = item.Username,
     SshPassword = await EncryptAsync(item.SshPassword),
        PrivateKey = await EncryptAsync(item.PrivateKey),
                KeyFingerprint = item.KeyFingerprint,
              NoteContent = await EncryptAsync(item.NoteContent),
            };

            await NavigationService.NavigateAsync(
       nameof(Views.ShareItemPage),
     new NavigationParameters { { "secureItem", secureItem } });
    }

        // ── Delete ────────────────────────────────────────────────────────────
        private async Task DeleteAsync(SecureItemViewModel item)
        {
       var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
         "Delete Item", $"Delete \"{item.Label}\"?", "Delete", "Cancel");
            if (!confirmed) return;
      try
   {
     await _dataStorageService.DeleteSecureItemAsync(item.Id);
    Items.Remove(item);
      _allItems.Remove(item);
       NoData = Items.Count == 0;
     _dialogService.ShowToast("Item deleted");
         _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
       }
            catch (Exception ex)
            {
  _logger.LogError(ex, "Failed to delete secure item");
      _dialogService.ShowToast("Failed to delete item");
            }
}

        // ── Navigation helpers ────────────────────────────────────────────────
        private Task OpenEditAsync(SecureItemViewModel item)
        => NavigationService.NavigateAsync(nameof(Views.AddEditSecureItemPage),
        new NavigationParameters { { "secureItem", item } });

        // ── Decrypt / Encrypt helpers ─────────────────────────────────────────
        private async Task<string> DecryptAsync(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return cipher;
            var r = await _cryptographyService.Decrypt(cipher);
            return r.Succeeded ? r.Data : cipher;
        }

        private async Task<string> EncryptAsync(string plaintext)
   {
    if (string.IsNullOrEmpty(plaintext)) return plaintext;
 var r = await _cryptographyService.Encrypt(plaintext);
    return r.Succeeded ? r.Data : plaintext;
        }

        // ?? Commands AddCommand

        private AsyncCommand _addCommand;
        public ICommand AddCommand =>
  _addCommand ??= new AsyncCommand(async () =>
        await NavigationService.NavigateAsync(nameof(Views.AddEditSecureItemPage)));

        private AsyncCommand _refreshCommand;
        public ICommand RefreshCommand =>
   _refreshCommand ??= new AsyncCommand(LoadAsync);

        private AsyncCommand<SecureItemViewModel> _showOptionsCommand;
        public ICommand ShowOptionsCommand =>
           _showOptionsCommand ??= new AsyncCommand<SecureItemViewModel>(ShowOptionsAsync);

        private AsyncCommand<SecureItemViewModel> _editCommand;
        public ICommand EditCommand =>
        _editCommand ??= new AsyncCommand<SecureItemViewModel>(OpenEditAsync);

        private AsyncCommand<SecureItemViewModel> _deleteCommand;
        public ICommand DeleteCommand =>
        _deleteCommand ??= new AsyncCommand<SecureItemViewModel>(DeleteAsync);

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
   foreach (var item in Items) item.IsSelected = false;
      IsSelectionMode = false; SelectedCount = 0;
    });

    private DelegateCommand<SecureItemViewModel> _toggleItemSelectionCommand;
   public ICommand ToggleItemSelectionCommand =>
    _toggleItemSelectionCommand ??= new DelegateCommand<SecureItemViewModel>(item =>
 {
      if (item is null) return;
    item.IsSelected = !item.IsSelected;
SelectedCount = Items.Count(i => i.IsSelected);
    });

   private DelegateCommand<SecureItemViewModel> _longPressItemCommand;
     public ICommand LongPressItemCommand =>
     _longPressItemCommand ??= new DelegateCommand<SecureItemViewModel>(item =>
    {
    if (item is null) return;
   if (!IsSelectionMode) { IsSelectionMode = true; SelectedCount = 0; }
     item.IsSelected = true;
 SelectedCount = Items.Count(i => i.IsSelected);
  });

  private DelegateCommand _selectAllCommand;
    public ICommand SelectAllCommand =>
    _selectAllCommand ??= new DelegateCommand(() =>
    {
    var newState = !IsAllSelected;
 foreach (var item in Items) item.IsSelected = newState;
    SelectedCount = newState ? Items.Count : 0;
    });

     private AsyncCommand _deleteSelectedCommand;
    public ICommand DeleteSelectedCommand =>
    _deleteSelectedCommand ??= new AsyncCommand(DeleteSelectedItemsAsync);

  private async Task DeleteSelectedItemsAsync()
    {
       var selected = Items.Where(i => i.IsSelected).ToList();
   if (selected.Count == 0) return;
    var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
    "Delete Selected",
    $"Delete {selected.Count} item{(selected.Count > 1 ? "s" : "")}? This cannot be undone.",
    "Delete", "Cancel");
    if (!confirmed) return;
  try
   {
  foreach (var item in selected)
    await _dataStorageService.DeleteSecureItemAsync(item.Id);
    foreach (var item in selected) { Items.Remove(item); _allItems.Remove(item); }
  _dialogService.ShowToast($"{selected.Count} item{(selected.Count > 1 ? "s" : "")} deleted");
   IsSelectionMode = false; SelectedCount = 0;
       NoData = Items.Count == 0;
   _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
   }
    catch (Exception ex)
        {
    _logger.LogError(ex, "Failed to delete selected secure items");
    _dialogService.ShowToast("Failed to delete some items");
   }
   }
    }
}
