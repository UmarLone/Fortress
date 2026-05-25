using Fortress.Core.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class CreditCardsViewModel : ObservableObject, INavigationAware
    {
  private readonly IDesktopDataService _dataService;
     private readonly ISnackbarService _snackbar;
    private List<CreditCardViewModel> _allItems = new();

        [ObservableProperty] private ObservableCollection<CreditCardViewModel> _items = new();
        [ObservableProperty] private CreditCardViewModel? _selectedItem;
        [ObservableProperty] private bool _isLoading = true;
      [ObservableProperty] private bool _isDetailOpen;
        [ObservableProperty] private string _searchText = string.Empty;

        // ── Drawer ────────────────────────────────────────────────────────────
   [ObservableProperty] private bool _isDrawerOpen;
 [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private bool _isSaving;

        [ObservableProperty] private string _formLabel         = string.Empty;
     [ObservableProperty] private string _formCardholderName = string.Empty;
     [ObservableProperty] private string _formNumber        = string.Empty;
 [ObservableProperty] private string _formExpiryMonth    = string.Empty;
 [ObservableProperty] private string _formExpiryYear     = string.Empty;
        [ObservableProperty] private string _formCvv          = string.Empty;
        [ObservableProperty] private string _formCardNetwork   = "Visa";
     [ObservableProperty] private string _formBillingAddress = string.Empty;
        [ObservableProperty] private string _formError         = string.Empty;

        private Guid _editingId;

// Explicit stub so XAML design-time analyser resolves the source-generated command.
        public IAsyncRelayCommand SaveAsyncCommand => _saveAsyncCommand ??= new AsyncRelayCommand(SaveAsync);
      private IAsyncRelayCommand? _saveAsyncCommand;

        public CreditCardsViewModel(IDesktopDataService dataService, ISnackbarService snackbar)
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
  try
 {
        var cards = await _dataService.GetCreditCardsAsync();
                _allItems = cards.Select(c => new CreditCardViewModel(c)).ToList();
            ApplyFilter();
         }
  finally { IsLoading = false; }
   }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
  {
            var q      = SearchText?.Trim() ?? string.Empty;
 var filtered = _allItems.AsEnumerable();
     if (!string.IsNullOrEmpty(q))
  filtered = filtered.Where(i =>
      i.Label.Contains(q, StringComparison.OrdinalIgnoreCase) ||
      i.CardholderName.Contains(q, StringComparison.OrdinalIgnoreCase));
            Items = new ObservableCollection<CreditCardViewModel>(filtered);
     }

    // ── Selection ─────────────────────────────────────────────────────────
        [RelayCommand]
  private void SelectItem(CreditCardViewModel? item)
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
     IsEditMode        = false;
         _editingId        = Guid.Empty;
        FormLabel           = string.Empty;
    FormCardholderName  = string.Empty;
     FormNumber   = string.Empty;
       FormExpiryMonth     = string.Empty;
    FormExpiryYear  = string.Empty;
       FormCvv= string.Empty;
    FormCardNetwork     = "Visa";
  FormBillingAddress  = string.Empty;
      FormError           = string.Empty;
  IsDetailOpen = false;
 IsDrawerOpen = true;
    }

   [RelayCommand]
        private void OpenEditDrawer(CreditCardViewModel? item)
{
   if (item is null) return;
    IsEditMode      = true;
     _editingId           = item.Id;
      FormLabel           = item.Label;
         FormCardholderName  = item.CardholderName;
      FormNumber          = item.Number;
   FormExpiryMonth     = item.ExpiryMonth;
     FormExpiryYear      = item.ExpiryYear;
  FormCvv      = item.Cvv;
   FormCardNetwork     = item.CardNetwork;
          FormBillingAddress  = item.BillingAddress;
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
   if (string.IsNullOrWhiteSpace(FormNumber)) { FormError = "Card number is required."; return; }
      FormError = string.Empty;
            IsSaving = true;
            try
 {
      var item = new CreditCardItem
       {
            // Empty Guid signals "new item" to PipeBackedDataService, which routes
            // SaveXxx vs UpdateXxx accordingly. The service generates the real ID.
            Id = IsEditMode ? _editingId : Guid.Empty,
              Label = FormLabel.Trim(),
    CardholderName = FormCardholderName.Trim(),
   Number = FormNumber.Trim(),
           ExpiryMonth = FormExpiryMonth.Trim(),
           ExpiryYear = FormExpiryYear.Trim(),
     Cvv = FormCvv.Trim(),
      CardNetwork = FormCardNetwork,
          BillingAddress = FormBillingAddress.Trim(),
    };
                await _dataService.SaveCreditCardAsync(item);
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

        [RelayCommand]
        private async Task DeleteAsync(CreditCardViewModel? item)
        {
            if (item is null) return;
            try
   {
      await _dataService.DeleteCreditCardAsync(item.Id);
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

    // ── Clipboard ─────────────────────────────────────────────────────────
        [RelayCommand]
        private void CopyNumber()
     {
  if (SelectedItem is null) return;
            System.Windows.Clipboard.SetText(SelectedItem.Number);
            _snackbar.Show("Copied", "Card number copied.",
       Wpf.Ui.Controls.ControlAppearance.Secondary,
   new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.ClipboardCheckmark24),
       TimeSpan.FromSeconds(2));
        }
    }
}
