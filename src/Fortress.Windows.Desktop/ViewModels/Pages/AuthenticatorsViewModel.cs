using Fortress.Core.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class AuthenticatorsViewModel : ObservableObject, INavigationAware
    {
  private readonly IDesktopDataService _dataService;
  private readonly ISnackbarService _snackbar;
  private DispatcherTimer? _timer;
  private List<AuthenticatorViewModel> _allItems = new();

        [ObservableProperty] private ObservableCollection<AuthenticatorViewModel> _items = new();
  [ObservableProperty] private bool _isLoading = true;
      [ObservableProperty] private string _searchText = string.Empty;

    // ── Drawer ────────────────────────────────────────────────────────────
      [ObservableProperty] private bool _isDrawerOpen;
  [ObservableProperty] private bool _isEditMode;
      [ObservableProperty] private bool _isSaving;
  [ObservableProperty] private string _formIssuer   = string.Empty;
  [ObservableProperty] private string _formUsername = string.Empty;
   [ObservableProperty] private string _formSecret   = string.Empty;
  [ObservableProperty] private int    _formPeriod   = 30;
   [ObservableProperty] private int    _formDigits   = 6;
  [ObservableProperty] private bool   _formIsFavorite;
  [ObservableProperty] private AuthenticatorType _formType = AuthenticatorType.Totp;
  [ObservableProperty] private string _formError    = string.Empty;

  private Guid _editingId;

 public IEnumerable<AuthenticatorType> AuthenticatorTypes { get; } = Enum.GetValues<AuthenticatorType>();
        public IRelayCommand<AuthenticatorViewModel?> CopyCodeCommand { get; }

     public AuthenticatorsViewModel(IDesktopDataService dataService, ISnackbarService snackbar)
        {
 _dataService    = dataService;
     _snackbar   = snackbar;
 CopyCodeCommand = new RelayCommand<AuthenticatorViewModel?>(CopyCode);
  }

  public async Task OnNavigatedToAsync()
     {
   if (_allItems.Count == 0) await LoadAsync();
StartTimer();
    }

 public Task OnNavigatedFromAsync() { StopTimer(); return Task.CompletedTask; }

        // ── Load ──────────────────────────────────────────────────────────────
     private async Task LoadAsync()
  {
      IsLoading = true;
     try
  {
 var items = await _dataService.GetAuthenticatorsAsync();
       _allItems = items.Select(a => new AuthenticatorViewModel(a)).ToList();
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
          i.Issuer.Contains(q, StringComparison.OrdinalIgnoreCase) ||
       i.Username.Contains(q, StringComparison.OrdinalIgnoreCase));
     Items = new ObservableCollection<AuthenticatorViewModel>(
       filtered.OrderByDescending(i => i.IsFavorite).ThenBy(i => i.Issuer));
        }

        // ── Timer ─────────────────────────────────────────────────────────────
   private void StartTimer()
        {
     _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    _timer.Tick += OnTick;
_timer.Start();
}

  private void StopTimer()
   {
        if (_timer is null) return;
       _timer.Tick -= OnTick;
   _timer.Stop();
 }

    private void OnTick(object? sender, EventArgs e)
 {
     foreach (var item in _allItems) item.Tick();
   }

        private void CopyCode(AuthenticatorViewModel? item)
   {
  if (item is null) return;
      System.Windows.Clipboard.SetText(item.CurrentCode);
            _snackbar.Show("Copied",
   $"{item.Issuer} code copied.",
    Wpf.Ui.Controls.ControlAppearance.Secondary,
       new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.ClipboardCheckmark24),
       TimeSpan.FromSeconds(2));
   }

  // ── Drawer ────────────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenAddDrawer()
      {
      IsEditMode    = false;
     _editingId    = Guid.Empty;
     FormIssuer    = string.Empty;
   FormUsername  = string.Empty;
 FormSecret    = string.Empty;
     FormPeriod    = 30;
      FormDigits    = 6;
  FormIsFavorite = false;
        FormType      = AuthenticatorType.Totp;
       FormError     = string.Empty;
     IsDrawerOpen  = true;
   }

[RelayCommand]
     private void OpenEditDrawer(AuthenticatorViewModel? item)
        {
    if (item is null) return;
         IsEditMode    = true;
        _editingId    = item.Id;
  FormIssuer    = item.Issuer;
     FormUsername  = item.Username;
   FormSecret    = string.Empty; // never pre-fill secret
  FormPeriod = item.Period;
   FormDigits    = item.Digits;
 FormIsFavorite = item.IsFavorite;
   FormType      = item.Type;
     FormError     = string.Empty;
  IsDrawerOpen  = true;
   }

     [RelayCommand]
  private void CloseDrawer() { IsDrawerOpen = false; FormError = string.Empty; }

  // Explicit stub so XAML design-time analyser resolves the source-generated command.
        public IAsyncRelayCommand SaveAsyncCommand => _saveAsyncCommand ??= new AsyncRelayCommand(SaveAsync);
      private IAsyncRelayCommand? _saveAsyncCommand;

        [RelayCommand]
       private async Task SaveAsync()
    {
       if (string.IsNullOrWhiteSpace(FormIssuer)) { FormError = "Issuer is required."; return; }
   if (!IsEditMode && string.IsNullOrWhiteSpace(FormSecret)) { FormError = "Secret is required."; return; }
            FormError = string.Empty;
IsSaving = true;
try
            {
        var auth = new Authenticator
          {
                    // Empty Guid signals "new item" to PipeBackedDataService.
                    Id = IsEditMode ? _editingId : Guid.Empty,
                    Issuer = FormIssuer.Trim(),
      Username = FormUsername.Trim(),
        Secret = FormSecret.Trim(),
     Type = FormType,
 Period = FormPeriod,
          Digits = FormDigits,
     IsFavorite = FormIsFavorite,
              };
    await _dataService.SaveAuthenticatorAsync(auth);
        IsDrawerOpen = false;
      _snackbar.Show("Vault",
     IsEditMode ? $"\"{auth.Issuer}\" updated." : $"\"{auth.Issuer}\" added.",
    Wpf.Ui.Controls.ControlAppearance.Success,
      new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24),
      TimeSpan.FromSeconds(3));
 await LoadAsync();
            }
            catch (Exception ex) { FormError = ex.Message; }
      finally { IsSaving = false; }
        }

        [RelayCommand]
        private async Task DeleteAsync(AuthenticatorViewModel? item)
        {
            if (item is null) return;
try
         {
      await _dataService.DeleteAuthenticatorAsync(item.Id);
_snackbar.Show("Vault",
    $"\"{item.Issuer}\" deleted.",
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
