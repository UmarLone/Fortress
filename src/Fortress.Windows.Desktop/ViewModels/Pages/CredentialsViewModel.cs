using Fortress.Core.Models;
using Fortress.Windows.Desktop.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class CredentialsViewModel : ObservableObject, INavigationAware
    {
        private readonly IDesktopDataService _dataService;
        private readonly ISnackbarService _snackbar;
        private List<LoginItemViewModel> _allItems = new();

        [ObservableProperty] private ObservableCollection<LoginItemViewModel> _items = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private LoginItemViewModel? _selectedItem;
        [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private bool _isDetailOpen;
        [ObservableProperty] private int _totalCount;

        // ── Multi-select ──────────────────────────────────────────────────────
        [ObservableProperty] private bool _isSelectionMode;
        [ObservableProperty] private int _selectedCount;

        // ── Group filter chips ────────────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<CredentialFilterChip> _filterChips = new();
        [ObservableProperty] private CredentialFilterChip? _activeChip;
        partial void OnActiveChipChanged(CredentialFilterChip? value) => ApplyFilter();

        // ── Drawer (Add / Edit) ───────────────────────────────────────────────
        [ObservableProperty] private bool _isDrawerOpen;
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private bool _isSaving;

        // Form fields
        [ObservableProperty] private string _formLabel = string.Empty;
        [ObservableProperty] private string _formUrl = string.Empty;
        [ObservableProperty] private string _formUsername = string.Empty;
        [ObservableProperty] private string _formPassword = string.Empty;
        [ObservableProperty] private string _formOtpSecret = string.Empty;
        [ObservableProperty] private string _formNotes = string.Empty;
        [ObservableProperty] private LoginType _formLoginType = LoginType.Web;
        [ObservableProperty] private bool _formIsFavorite;
        [ObservableProperty] private string _formError = string.Empty;

        private Guid _editingId;

        public IEnumerable<LoginType> LoginTypes { get; } = Enum.GetValues<LoginType>();

        // Explicit stub so XAML design-time analyser resolves the source-generated command.
        public IAsyncRelayCommand SaveAsyncCommand => _saveAsyncCommand ??= new AsyncRelayCommand(SaveAsync);
        private IAsyncRelayCommand? _saveAsyncCommand;

        public IAsyncRelayCommand<LoginItemViewModel?> DeleteAsyncCommand => _deleteAsyncCommand ??= new AsyncRelayCommand<LoginItemViewModel?>(DeleteAsync);
        private IAsyncRelayCommand<LoginItemViewModel?>? _deleteAsyncCommand;

        public IAsyncRelayCommand DeleteSelectedAsyncCommand => _deleteSelectedAsyncCommand ??= new AsyncRelayCommand(DeleteSelectedAsync);
        private IAsyncRelayCommand? _deleteSelectedAsyncCommand;

        public CredentialsViewModel(IDesktopDataService dataService, ISnackbarService snackbar)
        {
            _dataService = dataService;
            _snackbar = snackbar;
        }

        public async Task OnNavigatedToAsync()
        {
            if (_allItems.Count == 0)
                await LoadAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        // ── Load ──────────────────────────────────────────────────────────────

        private async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var logins = await _dataService.GetLoginItemsAsync();
                _allItems = logins.Select(i => new LoginItemViewModel(i)).ToList();
                TotalCount = _allItems.Count;

                await BuildFilterChipsAsync();
                ApplyFilter();
            }
            finally { IsLoading = false; }
        }

        private async Task BuildFilterChipsAsync()
        {
            var chips = new List<CredentialFilterChip>
       {
new() { Label = "All", GroupId = null, Color = null, IsActive = true }
        };
            var groups = await _dataService.GetGroupsAsync();
            var chipTasks = groups.Select(async g =>
          {
              var ids = await _dataService.GetGroupMemberIdsAsync(g.Id);
              return new CredentialFilterChip { Label = g.Name, GroupId = g.Id, Color = g.Color, MemberIds = ids };
          });
            chips.AddRange(await Task.WhenAll(chipTasks));
            FilterChips = new ObservableCollection<CredentialFilterChip>(chips);
            ActiveChip = chips[0];
        }

        // ── Filter ────────────────────────────────────────────────────────────

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var q = SearchText?.Trim() ?? string.Empty;
            var filtered = _allItems.AsEnumerable();
            if (ActiveChip?.GroupId != null && ActiveChip.MemberIds != null)
                filtered = filtered.Where(i => ActiveChip.MemberIds.Contains(i.Id));
            if (!string.IsNullOrEmpty(q))
                filtered = filtered.Where(i =>
               i.Label.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               i.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                   i.Domain.Contains(q, StringComparison.OrdinalIgnoreCase));
            Items = new ObservableCollection<LoginItemViewModel>(filtered.OrderBy(i => i.Label));
        }

        // ── Selection / detail ────────────────────────────────────────────────

        [RelayCommand]
        private void SelectChip(CredentialFilterChip chip)
        {
            if (chip is null) return;
            foreach (var c in FilterChips) c.IsActive = false;
            chip.IsActive = true;
            ActiveChip = chip;
        }

        [RelayCommand]
        private void SelectItem(LoginItemViewModel? item)
        {
            SelectedItem = item;
            IsDetailOpen = item is not null;
            IsDrawerOpen = false;
        }

        [RelayCommand]
        private void CloseDetail() { IsDetailOpen = false; SelectedItem = null; }

        // ── Drawer ────────────────────────────────────────────────────────────

        [RelayCommand]
        private void OpenAddDrawer()
        {
            IsEditMode = false;
            _editingId = Guid.Empty;
            FormLabel = string.Empty;
            FormUrl = string.Empty;
            FormUsername = string.Empty;
            FormPassword = string.Empty;
            FormOtpSecret = string.Empty;
            FormNotes = string.Empty;
            FormLoginType = LoginType.Web;
            FormIsFavorite = false;
            FormError = string.Empty;
            IsDetailOpen = false;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        private void OpenEditDrawer(LoginItemViewModel? item)
        {
            if (item is null) return;
            IsEditMode = true;
            _editingId = item.Id;
            FormLabel = item.Label;
            FormUrl = item.Url;
            FormUsername = item.Username;
            FormPassword = item.Password;
            FormOtpSecret = item.OtpSecret ?? string.Empty;
            FormNotes = item.Notes;
            FormLoginType = item.LoginType;
            FormIsFavorite = item.IsFavorite;
            FormError = string.Empty;
            IsDetailOpen = false;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        private void CloseDrawer() { IsDrawerOpen = false; FormError = string.Empty; }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormLabel)) { FormError = "Label is required."; return; }
            if (string.IsNullOrWhiteSpace(FormPassword)) { FormError = "Password is required."; return; }
            FormError = string.Empty;
            IsSaving = true;
            try
            {
                var item = new LoginItem
                {
                    // Empty Guid signals "new item" to PipeBackedDataService, which routes
                    // SaveXxx vs UpdateXxx accordingly. The service generates the real ID.
                    Id = IsEditMode ? _editingId : Guid.Empty,
                    Label = FormLabel.Trim(),
                    Url = FormUrl.Trim(),
                    Username = FormUsername.Trim(),
                    Password = FormPassword,
                    OtpSecret = string.IsNullOrWhiteSpace(FormOtpSecret) ? null : FormOtpSecret.Trim(),
                    Notes = FormNotes,
                    LoginType = FormLoginType,
                    IsFavorite = FormIsFavorite,
                };
                await _dataService.SaveLoginItemAsync(item);
                IsDrawerOpen = false;
                _snackbar.Show("Vault",
  IsEditMode ? $"\"{item.Label}\" updated." : $"\"{item.Label}\" added.",
         Wpf.Ui.Controls.ControlAppearance.Success,
    new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24),
        TimeSpan.FromSeconds(3));
  await LoadAsync();
            }
catch (Exception ex)
            {
     FormError = ex.Message;
            }
            finally { IsSaving = false; }
      }

        [RelayCommand]
  private async Task DeleteAsync(LoginItemViewModel? item)
        {
            if (item is null) return;
 try
     {
            await _dataService.DeleteLoginItemAsync(item.Id);
                IsDetailOpen = false;
      SelectedItem = null;
    _snackbar.Show("Vault",
                    $"\"{item.Label}\" deleted.",
        Wpf.Ui.Controls.ControlAppearance.Caution,
         new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Delete24),
                TimeSpan.FromSeconds(3));
   await LoadAsync();
  }
          catch (Exception ex)
            {
    _snackbar.Show("Error", ex.Message,
         Wpf.Ui.Controls.ControlAppearance.Danger,
          new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.ErrorCircle24),
           TimeSpan.FromSeconds(4));
 }
        }

        // ── Multi-select ──────────────────────────────────────────────────────

        [RelayCommand]
 private void EnterSelectionMode()
        {
            IsSelectionMode = true;
         IsDetailOpen = false;
          SelectedItem = null;
   SelectedCount = 0;
 }

        [RelayCommand]
     private void ExitSelectionMode()
        {
     IsSelectionMode = false;
        foreach (var item in Items) item.IsSelected = false;
   SelectedCount = 0;
     }

  [RelayCommand]
        private void ToggleItemSelection(LoginItemViewModel? item)
   {
      if (item is null) return;
 item.IsSelected = !item.IsSelected;
        SelectedCount = Items.Count(i => i.IsSelected);
   OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(SelectAllButtonText));
     }

        /// <summary>True when every visible item is selected.</summary>
        public bool IsAllSelected => Items.Count > 0 && SelectedCount == Items.Count;

        /// <summary>Dynamic label for the Select All / Deselect All button.</summary>
        public string SelectAllButtonText => IsAllSelected ? "Deselect All" : "Select All";

 [RelayCommand]
        private void SelectAll()
        {
          var newState = !IsAllSelected;
            foreach (var item in Items) item.IsSelected = newState;
            SelectedCount = newState ? Items.Count : 0;
     OnPropertyChanged(nameof(IsAllSelected));
          OnPropertyChanged(nameof(SelectAllButtonText));
  }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
    {
            var selected = Items.Where(i => i.IsSelected).ToList();
  if (selected.Count == 0) return;

    try
  {
         IsSaving = true;
       foreach (var item in selected)
         await _dataService.DeleteLoginItemAsync(item.Id);

_snackbar.Show("Vault",
           $"{selected.Count} login{(selected.Count > 1 ? "s" : "")} deleted.",
   Wpf.Ui.Controls.ControlAppearance.Caution,
  new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Delete24),
        TimeSpan.FromSeconds(3));

     IsSelectionMode = false;
          SelectedCount = 0;
await LoadAsync();
   }
          catch (Exception ex)
      {
       _snackbar.Show("Error", ex.Message,
        Wpf.Ui.Controls.ControlAppearance.Danger,
         new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.ErrorCircle24),
           TimeSpan.FromSeconds(4));
            }
            finally { IsSaving = false; }
        }

        // ── Clipboard / URL ───────────────────────────────────────────────────

        [RelayCommand]
    private void CopyUsername()
        {
  if (SelectedItem is null) return;
        System.Windows.Clipboard.SetText(SelectedItem.Username);
            _snackbar.Show("Copied",
"Username copied to clipboard.",
    Wpf.Ui.Controls.ControlAppearance.Secondary,
         new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.ClipboardCheckmark24),
           TimeSpan.FromSeconds(2));
        }

        [RelayCommand]
        private void CopyPassword()
        {
        if (SelectedItem is null) return;
          System.Windows.Clipboard.SetText(SelectedItem.Password);
            _snackbar.Show("Copied",
      "Password copied to clipboard.",
         Wpf.Ui.Controls.ControlAppearance.Secondary,
  new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.ClipboardCheckmark24),
    TimeSpan.FromSeconds(2));
  }

        [RelayCommand]
        private void OpenUrl()
      {
       if (SelectedItem is null || string.IsNullOrWhiteSpace(SelectedItem.Url)) return;
   try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SelectedItem.Url) { UseShellExecute = true }); }
          catch { }
  }
    }
}
