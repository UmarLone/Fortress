using Fortress.Core.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class SecureItemsViewModel : ObservableObject, INavigationAware
    {
        private readonly IDesktopDataService _dataService;
   private readonly ISnackbarService _snackbar;
        private List<SecureItem> _allItems = new();

        [ObservableProperty] private ObservableCollection<SecureItem> _items = new();
     [ObservableProperty] private SecureItem? _selectedItem;
    [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private bool _isDetailOpen;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedFilter = "All";

        public ObservableCollection<string> Filters { get; } = new()
        { "All", "ID Card", "Passport", "Driver's License", "Social Security", "Wi-Fi", "SSH", "Secure Note" };

        // ── Drawer ────────────────────────────────────────────────────────────
        [ObservableProperty] private bool _isDrawerOpen;
      [ObservableProperty] private bool _isEditMode;
 [ObservableProperty] private bool _isSaving;

// Shared form fields
        [ObservableProperty] private string _formLabel     = string.Empty;
  [ObservableProperty] private string _formNotes       = string.Empty;
   [ObservableProperty] private SecureItemType _formItemType = SecureItemType.IdCard;
        [ObservableProperty] private string _formError       = string.Empty;

   // Document fields
        [ObservableProperty] private string _formFullName = string.Empty;
    [ObservableProperty] private string _formNumber      = string.Empty;
 [ObservableProperty] private string _formIssuingCountry = string.Empty;
        [ObservableProperty] private string _formIssuedDate   = string.Empty;
        [ObservableProperty] private string _formExpiryDate     = string.Empty;
  [ObservableProperty] private string _formNationality    = string.Empty;
        [ObservableProperty] private string _formDateOfBirth    = string.Empty;

    // Wi-Fi fields
        [ObservableProperty] private string _formSsid         = string.Empty;
   [ObservableProperty] private string _formWifiPassword = string.Empty;
        [ObservableProperty] private string _formWifiSecurity = string.Empty;

        // SSH fields
        [ObservableProperty] private string _formHost        = string.Empty;
        [ObservableProperty] private string _formPort        = string.Empty;
        [ObservableProperty] private string _formSshUsername = string.Empty;
        [ObservableProperty] private string _formSshPassword = string.Empty;

     // Secure Note
    [ObservableProperty] private string _formNoteContent = string.Empty;

  private Guid _editingId;

        public IEnumerable<SecureItemType> SecureItemTypes { get; } = Enum.GetValues<SecureItemType>();

        // Explicit stub so XAML design-time analyser resolves the source-generated command.
  public IAsyncRelayCommand SaveAsyncCommand => _saveAsyncCommand ??= new AsyncRelayCommand(SaveAsync);
      private IAsyncRelayCommand? _saveAsyncCommand;

        public SecureItemsViewModel(IDesktopDataService dataService, ISnackbarService snackbar)
        {
    _dataService = dataService;
    _snackbar    = snackbar;
        }

        public async Task OnNavigatedToAsync()
        {
            if (_allItems.Count == 0) await LoadAsync();
    }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        // ── Load ──────────────────────────────────────────────────────────────
   private async Task LoadAsync()
      {
IsLoading = true;
    try { _allItems = await _dataService.GetSecureItemsAsync(); ApplyFilter(); }
     finally { IsLoading = false; }
        }

 partial void OnSearchTextChanged(string value)=> ApplyFilter();
        partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
        {
            var q    = SearchText?.Trim() ?? string.Empty;
          var filtered = _allItems.AsEnumerable();

            if (!string.IsNullOrEmpty(q))
       filtered = filtered.Where(i => i.Label.Contains(q, StringComparison.OrdinalIgnoreCase));

      filtered = SelectedFilter switch
    {
     "ID Card"      => filtered.Where(i => i.ItemType == SecureItemType.IdCard),
       "Passport"         => filtered.Where(i => i.ItemType == SecureItemType.Passport),
                "Driver's License" => filtered.Where(i => i.ItemType == SecureItemType.DriversLicense),
      "Social Security"  => filtered.Where(i => i.ItemType == SecureItemType.SocialSecurity),
       "Wi-Fi"            => filtered.Where(i => i.ItemType == SecureItemType.WiFi),
       "SSH"            => filtered.Where(i => i.ItemType == SecureItemType.Ssh),
      "Secure Note"      => filtered.Where(i => i.ItemType == SecureItemType.SecureNote),
           _    => filtered
     };

      Items = new ObservableCollection<SecureItem>(filtered.OrderBy(i => i.Label));
        }

 // ── Selection ─────────────────────────────────────────────────────────
  [RelayCommand]
      private void SelectItem(SecureItem? item)
        {
  SelectedItem = item;
     IsDetailOpen = item is not null;
   IsDrawerOpen = false;
        }

        [RelayCommand]
        private void CloseDetail() { IsDetailOpen = false; SelectedItem = null; }

        [RelayCommand]
        private void SetFilter(string? filter) => SelectedFilter = filter ?? "All";

        // ── Drawer ────────────────────────────────────────────────────────────
     [RelayCommand]
        private void OpenAddDrawer()
        {
      IsEditMode      = false;
            _editingId      = Guid.Empty;
         FormLabel       = string.Empty;
            FormNotes       = string.Empty;
  FormItemType    = SecureItemType.IdCard;
       FormFullName    = FormNumber = FormIssuingCountry = FormIssuedDate =
       FormExpiryDate  = FormNationality = FormDateOfBirth = string.Empty;
            FormSsid = FormWifiPassword = FormWifiSecurity = string.Empty;
            FormHost        = FormPort = FormSshUsername = FormSshPassword = string.Empty;
          FormNoteContent = string.Empty;
            FormError       = string.Empty;
       IsDetailOpen    = false;
       IsDrawerOpen    = true;
        }

        [RelayCommand]
        private void OpenEditDrawer(SecureItem? item)
        {
     if (item is null) return;
       IsEditMode    = true;
            _editingId         = item.Id;
     FormLabel          = item.Label;
       FormNotes     = item.Notes;
            FormItemType       = item.ItemType;
            FormFullName       = item.FullName;
    FormNumber         = item.Number;
            FormIssuingCountry = item.IssuingCountry;
            FormIssuedDate     = item.IssuedDate;
  FormExpiryDate     = item.ExpiryDate;
 FormNationality    = item.Nationality;
          FormDateOfBirth    = item.DateOfBirth;
   FormSsid     = item.Ssid;
            FormWifiPassword   = item.Password;
    FormWifiSecurity = item.WifiSecurity;
            FormHost   = item.Host;
         FormPort        = item.Port;
      FormSshUsername    = item.Username;
        FormSshPassword    = item.SshPassword;
        FormNoteContent  = item.NoteContent;
            FormError          = string.Empty;
 IsDetailOpen       = false;
        IsDrawerOpen       = true;
        }

        [RelayCommand]
     private void CloseDrawer() { IsDrawerOpen = false; FormError = string.Empty; }

        [RelayCommand]
        private async Task SaveAsync()
        {
    if (string.IsNullOrWhiteSpace(FormLabel)) { FormError = "Label is required."; return; }
            FormError = string.Empty;
        IsSaving  = true;
            try
    {
         var item = new SecureItem
                {
                    // Empty Guid signals "new item" to PipeBackedDataService.
                    Id    = IsEditMode ? _editingId : Guid.Empty,
          Label          = FormLabel.Trim(),
         Notes  = FormNotes,
        ItemType     = FormItemType,
          FullName       = FormFullName,
  Number     = FormNumber,
       IssuingCountry = FormIssuingCountry,
        IssuedDate     = FormIssuedDate,
     ExpiryDate     = FormExpiryDate,
   Nationality    = FormNationality,
  DateOfBirth    = FormDateOfBirth,
  Ssid   = FormSsid,
Password       = FormWifiPassword,
    WifiSecurity   = FormWifiSecurity,
          Host  = FormHost,
        Port           = FormPort,
    Username       = FormSshUsername,
SshPassword    = FormSshPassword,
            NoteContent    = FormNoteContent,
            };
       await _dataService.SaveSecureItemAsync(item);
              IsDrawerOpen = false;
      _snackbar.Show("Vault",
              IsEditMode ? $"\"{item.Label}\" updated." : $"\"{item.Label}\" added.",
         Wpf.Ui.Controls.ControlAppearance.Success, null, TimeSpan.FromSeconds(3));
          await LoadAsync();
        }
            catch (Exception ex) { FormError = ex.Message; }
 finally { IsSaving = false; }
}

     [RelayCommand]
      private async Task DeleteAsync(SecureItem? item)
        {
          if (item is null) return;
          try
    {
                await _dataService.DeleteSecureItemAsync(item.Id);
             IsDetailOpen = false;
   SelectedItem = null;
                _snackbar.Show("Vault", $"\"{item.Label}\" deleted.",
     Wpf.Ui.Controls.ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
   await LoadAsync();
    }
            catch (Exception ex)
  {
       _snackbar.Show("Error", ex.Message,
               Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(4));
    }
        }
    }
}
