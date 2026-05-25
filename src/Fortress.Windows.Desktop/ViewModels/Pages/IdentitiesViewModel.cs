using Fortress.Core.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class IdentitiesViewModel : ObservableObject, INavigationAware
    {
        private readonly IDesktopDataService _dataService;
        private readonly ISnackbarService _snackbar;
        private List<IdentityItem> _allItems = new();

        [ObservableProperty] private ObservableCollection<IdentityItem> _items = new();
        [ObservableProperty] private IdentityItem? _selectedItem;
        [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private bool _isDetailOpen;
        [ObservableProperty] private string _searchText = string.Empty;

        // ── Drawer (add / edit) ──────────────────────────────────────────────
        [ObservableProperty] private bool _isDrawerOpen;
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private bool _isSaving;

        [ObservableProperty] private string _formLabel        = string.Empty;
        [ObservableProperty] private string _formFirstName    = string.Empty;
        [ObservableProperty] private string _formLastName     = string.Empty;
        [ObservableProperty] private string _formEmail        = string.Empty;
        [ObservableProperty] private string _formPhone        = string.Empty;
        [ObservableProperty] private string _formCompany      = string.Empty;
        [ObservableProperty] private string _formAddressLine1 = string.Empty;
        [ObservableProperty] private string _formAddressLine2 = string.Empty;
        [ObservableProperty] private string _formCity         = string.Empty;
        [ObservableProperty] private string _formState        = string.Empty;
        [ObservableProperty] private string _formCountry      = string.Empty;
        [ObservableProperty] private string _formPostalCode   = string.Empty;
        [ObservableProperty] private string _formNotes        = string.Empty;
        [ObservableProperty] private string _formError        = string.Empty;

        private Guid _editingId;

        // Explicit command stubs — the [RelayCommand] source generator is unreliable
        // on the .NET 10 preview SDK (same workaround as LockScreenViewModel). Without
        // these, the XAML bindings silently fail and buttons appear inert at runtime.
        public IRelayCommand           OpenAddDrawerCommand   => _openAddDrawerCommand   ??= new RelayCommand(OpenAddDrawer);
        public IRelayCommand<IdentityItem?> OpenEditDrawerCommand => _openEditDrawerCommand ??= new RelayCommand<IdentityItem?>(OpenEditDrawer);
        public IRelayCommand           CloseDrawerCommand     => _closeDrawerCommand     ??= new RelayCommand(CloseDrawer);
        public IRelayCommand<IdentityItem?> SelectItemCommand      => _selectItemCommand      ??= new RelayCommand<IdentityItem?>(SelectItem);
        public IRelayCommand           CloseDetailCommand     => _closeDetailCommand     ??= new RelayCommand(CloseDetail);
        public IAsyncRelayCommand      SaveAsyncCommand       => _saveAsyncCommand       ??= new AsyncRelayCommand(SaveAsync);
        public IAsyncRelayCommand<IdentityItem?> DeleteAsyncCommand => _deleteAsyncCommand ??= new AsyncRelayCommand<IdentityItem?>(DeleteAsync);
        private IRelayCommand?           _openAddDrawerCommand;
        private IRelayCommand<IdentityItem?>? _openEditDrawerCommand;
        private IRelayCommand?           _closeDrawerCommand;
        private IRelayCommand<IdentityItem?>? _selectItemCommand;
        private IRelayCommand?           _closeDetailCommand;
        private IAsyncRelayCommand?      _saveAsyncCommand;
        private IAsyncRelayCommand<IdentityItem?>? _deleteAsyncCommand;

        public IdentitiesViewModel(IDesktopDataService dataService, ISnackbarService snackbar)
        {
            _dataService = dataService;
            _snackbar    = snackbar;
        }

        public async Task OnNavigatedToAsync()
        {
            if (_allItems.Count == 0) await LoadAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        // ── Load ─────────────────────────────────────────────────────────────

        private async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _allItems = await _dataService.GetIdentitiesAsync();
                ApplyFilter();
            }
            finally { IsLoading = false; }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var q        = SearchText?.Trim() ?? string.Empty;
            var filtered = _allItems.AsEnumerable();
            if (!string.IsNullOrEmpty(q))
                filtered = filtered.Where(i =>
                    i.Label.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    $"{i.FirstName} {i.LastName}".Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    i.Email.Contains(q, StringComparison.OrdinalIgnoreCase));
            Items = new ObservableCollection<IdentityItem>(filtered);
        }

        // ── Selection ────────────────────────────────────────────────────────

        private void SelectItem(IdentityItem? item)
        {
            SelectedItem = item;
            IsDetailOpen = item is not null;
            IsDrawerOpen = false;
        }

        private void CloseDetail() { IsDetailOpen = false; SelectedItem = null; }

        // ── Drawer ───────────────────────────────────────────────────────────

        private void OpenAddDrawer()
        {
            IsEditMode       = false;
            _editingId       = Guid.Empty;
            FormLabel        = string.Empty;
            FormFirstName    = string.Empty;
            FormLastName     = string.Empty;
            FormEmail        = string.Empty;
            FormPhone        = string.Empty;
            FormCompany      = string.Empty;
            FormAddressLine1 = string.Empty;
            FormAddressLine2 = string.Empty;
            FormCity         = string.Empty;
            FormState        = string.Empty;
            FormCountry      = string.Empty;
            FormPostalCode   = string.Empty;
            FormNotes        = string.Empty;
            FormError        = string.Empty;
            IsDetailOpen     = false;
            IsDrawerOpen     = true;
        }

        private void OpenEditDrawer(IdentityItem? item)
        {
            if (item is null) return;
            IsEditMode       = true;
            _editingId       = item.Id;
            FormLabel        = item.Label;
            FormFirstName    = item.FirstName;
            FormLastName     = item.LastName;
            FormEmail        = item.Email;
            FormPhone        = item.Phone;
            FormCompany      = item.Company;
            FormAddressLine1 = item.AddressLine1;
            FormAddressLine2 = item.AddressLine2;
            FormCity         = item.City;
            FormState        = item.State;
            FormCountry      = item.Country;
            FormPostalCode   = item.PostalCode;
            FormNotes        = item.Notes ?? string.Empty;
            FormError        = string.Empty;
            IsDetailOpen     = false;
            IsDrawerOpen     = true;
        }

        private void CloseDrawer() { IsDrawerOpen = false; FormError = string.Empty; }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormLabel))     { FormError = "Label is required.";      return; }
            if (string.IsNullOrWhiteSpace(FormFirstName)) { FormError = "First name is required."; return; }
            FormError = string.Empty;
            IsSaving = true;
            try
            {
                var item = new IdentityItem
                {
                    // Empty Guid signals "new item" to PipeBackedDataService, which routes
                    // SaveXxx vs UpdateXxx accordingly. The service generates the real ID.
                    Id           = IsEditMode ? _editingId : Guid.Empty,
                    Label        = FormLabel.Trim(),
                    FirstName    = FormFirstName.Trim(),
                    LastName     = FormLastName.Trim(),
                    Email        = FormEmail.Trim(),
                    Phone        = FormPhone.Trim(),
                    Company      = FormCompany.Trim(),
                    AddressLine1 = FormAddressLine1.Trim(),
                    AddressLine2 = FormAddressLine2.Trim(),
                    City         = FormCity.Trim(),
                    State        = FormState.Trim(),
                    Country      = FormCountry.Trim(),
                    PostalCode   = FormPostalCode.Trim(),
                    Notes        = FormNotes.Trim(),
                };
                await _dataService.SaveIdentityAsync(item);
                IsDrawerOpen = false;
                _snackbar.Show("Vault",
                    IsEditMode ? $"\"{item.Label}\" updated." : $"\"{item.Label}\" added.",
                    Wpf.Ui.Controls.ControlAppearance.Success,
                    new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24),
                    TimeSpan.FromSeconds(3));
                await LoadAsync();
            }
            catch (Exception ex) { FormError = ex.Message; }
            finally { IsSaving = false; }
        }

        private async Task DeleteAsync(IdentityItem? item)
        {
            if (item is null) return;
            try
            {
                await _dataService.DeleteIdentityAsync(item.Id);
                IsDetailOpen = false; SelectedItem = null;
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
    }
}
