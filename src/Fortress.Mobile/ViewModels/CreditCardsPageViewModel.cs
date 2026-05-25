using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using MauiIcons.Core;
using MauiIcons.Material;

namespace Fortress.ViewModels
{
    public class CreditCardsPageViewModel : ViewModelBase
    {
        #region Properties

        private ObservableCollection<CreditCardItemViewModel> _cards = new();
        public ObservableCollection<CreditCardItemViewModel> Cards
        {
            get => _cards;
            set
            {
                if (SetProperty(ref _cards, value))
                {
                    if (_cards != null) _cards.CollectionChanged -= OnCardsCollectionChanged;
                    if (value != null) value.CollectionChanged += OnCardsCollectionChanged;
                    RaisePropertyChanged(nameof(CardsCountText));
                }
            }
        }

        private void OnCardsCollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
       => RaisePropertyChanged(nameof(CardsCountText));

        public string CardsCountText => $"{Cards?.Count ?? 0} card(s) stored";

        private List<CreditCardItemViewModel> _allCards = new();

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (SetProperty(ref _isRefreshing, value) && !value)
                    RaisePropertyChanged(nameof(CardsCountText));
            }
        }

        private bool _noData;
        public bool NoData { get => _noData; set => SetProperty(ref _noData, value); }

        public bool IsFormVisible => false;

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        private string _formTitle = "Add Card";
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }

        public string SaveButtonText => IsEditMode ? "Update Card" : "Save Card";

        private string _cardName;
        public string CardName
        {
            get => _cardName;
            set { SetProperty(ref _cardName, value); if (_submitted) ShowErrors(); UpdateCanSave(); }
        }

        private bool _updatingCardNumber;
        private string _cardNumber;
        public string CardNumber
        {
            get => _cardNumber;
            set => SetFormattedCardNumber(value);
        }

        private string _cardHolder;
        public string CardHolder
        {
            get => _cardHolder;
            set { SetProperty(ref _cardHolder, value); if (_submitted) ShowErrors(); UpdateCanSave(); }
        }

        private bool _updatingExpiry;
        private string _expiry;
        public string Expiry
        {
            get => _expiry;
            set => SetFormattedExpiry(value);
        }

        private string _cvv;
        public string Cvv
        {
            get => _cvv;
            set
            {
                var digits = new string(value?.Where(char.IsDigit).ToArray() ?? []);
                var isAmex = CardNetwork == "Amex";
                var maxLen = isAmex ? 4 : 3;
                if (digits.Length > maxLen) digits = digits[..maxLen];
                if (digits == _cvv) return;
                SetProperty(ref _cvv, digits);
                if (_submitted) ShowErrors();
            }
        }

        private bool _submitted;

        private string _cardNameError;
        public string CardNameError { get => _cardNameError; set { SetProperty(ref _cardNameError, value); RaisePropertyChanged(nameof(HasCardNameError)); } }
        public bool HasCardNameError => !string.IsNullOrEmpty(_cardNameError);

        private string _cardNumberError;
        public string CardNumberError { get => _cardNumberError; set { SetProperty(ref _cardNumberError, value); RaisePropertyChanged(nameof(HasCardNumberError)); } }
        public bool HasCardNumberError => !string.IsNullOrEmpty(_cardNumberError);

        private string _cardHolderError;
        public string CardHolderError { get => _cardHolderError; set { SetProperty(ref _cardHolderError, value); RaisePropertyChanged(nameof(HasCardHolderError)); } }
        public bool HasCardHolderError => !string.IsNullOrEmpty(_cardHolderError);

        private string _expiryError;
        public string ExpiryError { get => _expiryError; set { SetProperty(ref _expiryError, value); RaisePropertyChanged(nameof(HasExpiryError)); } }
        public bool HasExpiryError => !string.IsNullOrEmpty(_expiryError);

        private string _cvvError;
        public string CvvError { get => _cvvError; set { SetProperty(ref _cvvError, value); RaisePropertyChanged(nameof(HasCvvError)); } }
        public bool HasCvvError => !string.IsNullOrEmpty(_cvvError);

        private bool _canSave;
        public bool CanSave { get => _canSave; set => SetProperty(ref _canSave, value); }

        private bool _isSaving;
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        private string _cardNetwork = "Visa";
        public string CardNetwork { get => _cardNetwork; set => SetProperty(ref _cardNetwork, value); }

        private Guid? _editingId;
        public Guid? EditingId => _editingId;

        // Plain pass-through — debounce handled by VaultPageHero SearchCommand
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
        public bool IsAllSelected => Cards.Count > 0 && _selectedCount == Cards.Count;
        public string SelectAllButtonText => IsAllSelected ? "Deselect All" : "Select All";

        #endregion

        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<CreditCardsPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;

        public CreditCardsPageViewModel(
   INavigationService navigationService,
            IDataStorageService dataStorageService,
            ICryptographyService cryptographyService,
         IUserDialogs dialogService,
     IEventAggregator eventAggregator,
     ILogger<CreditCardsPageViewModel> logger,
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
            await LoadCardsAsync();
        }

        // ── Load ──────────────────────────────────────────────────────────

        private async Task LoadCardsAsync()
        {
            IsRefreshing = true;
            try
            {
                var all = await _dataStorageService.GetCreditCardItemsAsync();

                // Decrypt all numbers and CVVs in parallel on the thread pool
                var items = await Task.WhenAll(all.Select(async item =>
       {
           var number = await DecryptAsync(item.Number);
           var cvv = await DecryptAsync(item.Cvv);
           return new CreditCardItemViewModel
           {
               CredentialId = item.Id,
               CardName = item.Label,
               CardHolder = item.CardholderName,
               Number = number,
               Expiry = string.IsNullOrEmpty(item.ExpiryYear)
                   ? item.ExpiryMonth
                    : $"{item.ExpiryMonth}/{item.ExpiryYear}",
               Cvv = cvv,
               CardNetwork = item.CardNetwork,
               RequireAuthBeforeFill = item.RequireAuthBeforeFill,
           };
       }));

                var list = items.ToList();
                Cards = new ObservableCollection<CreditCardItemViewModel>(list);
                _allCards = list;
                NoData = Cards.Count == 0;
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to load credit cards"); }
            finally { IsRefreshing = false; }
        }

        // ── Search ────────────────────────────────────────────────────────

        private async Task ExecuteApplySearchAsync(string text)
        {
            var filtered = await Task.Run(() =>
     string.IsNullOrWhiteSpace(text)
             ? _allCards
  : _allCards.Where(c =>
   (!string.IsNullOrEmpty(c.CardName) && c.CardName.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrEmpty(c.CardHolder) && c.CardHolder.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
         (!string.IsNullOrEmpty(c.CardNetwork) && c.CardNetwork.Contains(text, StringComparison.OrdinalIgnoreCase)))
         .ToList());

            Cards = new ObservableCollection<CreditCardItemViewModel>(filtered);
            NoData = Cards.Count == 0;
        }

        // ── Save ──────────────────────────────────────────────────────────

        private async Task SaveCardAsync()
        {
            if (IsSaving) return;
            _submitted = true;
            ShowErrors();
            UpdateCanSave();
            if (!CanSave) return;

            IsSaving = true;
            try
            {
                var parts = (Expiry ?? string.Empty).Split('/');
                var month = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                var year = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                var encNum = await EncryptAsync(CardNumber);
                var encCvv = await EncryptAsync(Cvv);

                var item = new CreditCardItem
                {
                    Id = _editingId ?? Guid.NewGuid(),
                    Label = CardName,
                    CardholderName = CardHolder,
                    Number = encNum,
                    ExpiryMonth = month,
                    ExpiryYear = year,
                    Cvv = encCvv,
                    CardNetwork = DetectNetwork(CardNumber),
                    Notes = string.Empty,
                };

                await _dataStorageService.SaveCreditCardItemAsync(item);
                _dialogService.ShowToast(_editingId.HasValue ? "Card updated" : "Card added");
                CloseForm();
                await LoadCardsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save credit card");
                _dialogService.ShowToast("Failed to save card");
            }
            finally { IsSaving = false; }
        }

        // ── Delete ────────────────────────────────────────────────────────

        private async Task DeleteCardAsync(CreditCardItemViewModel item)
        {
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
          "Delete Card", $"Delete \"{item.CardName}\"?", "Delete", "Cancel");
            if (!confirmed) return;
            try
            {
                await _dataStorageService.DeleteCreditCardItemAsync(item.CredentialId);
                Cards.Remove(item);
                NoData = Cards.Count == 0;
                _dialogService.ShowToast("Card deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete credit card");
                _dialogService.ShowToast("Failed to delete card");
            }
        }

        // ── Navigation helpers ────────────────────────────────────────────

        private async Task OpenAddFormAsync()
        => await NavigationService.NavigateAsync(nameof(Views.AddEditCreditCardPage));

        private async Task OpenEditFormAsync(CreditCardItemViewModel item)
        => await NavigationService.NavigateAsync(nameof(Views.AddEditCreditCardPage),
   new NavigationParameters { { "card", item } });

        private void CloseForm() { }

        // ── Options sheet ─────────────────────────────────────────────────

        private async Task ShowCardOptionsAsync(CreditCardItemViewModel item)
        {
            if (item is null) return;
        var options = new List<BottomSheetOption>
          {
new() { Title = "Copy Card Number", Icon = new MauiIcon().Icon(MaterialIcons.ContentCopy),
   Action = async () => { await Clipboard.Default.SetTextAsync(item.Number);     _dialogService.ShowToast("Card number copied"); } },
new() { Title = "Copy Card Holder", Icon = new MauiIcon().Icon(MaterialIcons.Person),
   Action = async () => { await Clipboard.Default.SetTextAsync(item.CardHolder); _dialogService.ShowToast("Card holder copied"); } },
       new() { Title = "Copy Expiry",      Icon = new MauiIcon().Icon(MaterialIcons.DateRange),
   Action = async () => { await Clipboard.Default.SetTextAsync(item.Expiry);     _dialogService.ShowToast("Expiry copied"); } },
                new() { Title = "Copy CVV",      Icon = new MauiIcon().Icon(MaterialIcons.Lock),
     Action = async () => { await Clipboard.Default.SetTextAsync(item.Cvv);        _dialogService.ShowToast("CVV copied"); } },
         new() { Title = "Edit",   Icon = new MauiIcon().Icon(MaterialIcons.Edit),   Action = async () => await OpenEditFormAsync(item) },
new() { Title = "Share",  Icon = new MauiIcon().Icon(MaterialIcons.Share),  Action = async () => await ShareCardAsync(item) },
new() { Title = "Delete", Icon = new MauiIcon().Icon(MaterialIcons.Delete), Action = async () => await DeleteCardAsync(item)   },
          };
 await _bottomSheetService.ShowAsync<Views.PopupPages.BottomSheet, BottomSheetViewModel, bool>(
     options, item.CardName ?? "Card Options");
        }

 private async Task ShareCardAsync(CreditCardItemViewModel item)
        {
            if (item is null) return;

            // Re-encrypt sensitive fields so they travel encrypted inside the .fortress file
            var parts = (item.Expiry ?? string.Empty).Split('/');
  var month = parts.Length > 0 ? parts[0].Trim() : string.Empty;
       var year = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            var cardItem = new CreditCardItem
            {
        Id = item.CredentialId,
         Label = item.CardName,
       CardholderName = item.CardHolder,
          Number = await EncryptAsync(item.Number),
     ExpiryMonth = month,
    ExpiryYear = year,
       Cvv = await EncryptAsync(item.Cvv),
      CardNetwork = item.CardNetwork,
   };

            await NavigationService.NavigateAsync(
        nameof(Views.ShareItemPage),
     new NavigationParameters { { "creditCard", cardItem } });
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private async Task<string> EncryptAsync(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;
            var r = await _cryptographyService.Encrypt(plaintext);
            return r.Succeeded ? r.Data : plaintext;
        }

        private async Task<string> DecryptAsync(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
            var r = await _cryptographyService.Decrypt(ciphertext);
            return r.Succeeded ? r.Data : ciphertext;
        }

        // ── Validation ────────────────────────────────────────────────────

        private void ShowErrors()
        {
            CardNameError = string.IsNullOrWhiteSpace(CardName) ? "Card label is required" : string.Empty;
            CardHolderError = string.IsNullOrWhiteSpace(CardHolder) ? "Cardholder name is required" : string.Empty;

            var digits = ExtractDigits(CardNumber);
            var isAmex = CardNetwork == "Amex";
            var expected = isAmex ? 15 : 16;
            CardNumberError = string.IsNullOrWhiteSpace(digits) ? "Card number is required"
       : digits.Length != expected ? $"{CardNetwork} cards require {expected} digits"
                  : !PassesLuhn(digits) ? "Card number is not valid"
             : string.Empty;

            if (string.IsNullOrWhiteSpace(Expiry) || Expiry.Length < 5)
            {
                ExpiryError = "Expiry is required (MM/YY)";
            }
            else
            {
                var p = Expiry.Split('/');
                if (p.Length == 2 && int.TryParse(p[0], out var m) && int.TryParse(p[1], out var y) && m >= 1 && m <= 12)
                {
                    var fy = 2000 + y;
                    var exp = new DateTime(fy, m, DateTime.DaysInMonth(fy, m));
                    ExpiryError = exp < DateTime.Today ? "Card has expired" : string.Empty;
                }
                else ExpiryError = "Enter a valid expiry (MM/YY)";
            }

            var cvvLen = Cvv?.Length ?? 0;
            var expectedCvv = isAmex ? 4 : 3;
            CvvError = cvvLen == 0 ? "CVV is required"
              : cvvLen != expectedCvv ? $"{CardNetwork} CVV must be {expectedCvv} digits"
          : string.Empty;
        }

        private void UpdateCanSave()
        {
            try
            {
                var digits = ExtractDigits(CardNumber);
                var isAmex = CardNetwork == "Amex";
                CanSave = !string.IsNullOrWhiteSpace(CardName)
                 && !string.IsNullOrWhiteSpace(CardHolder)
                   && digits.Length == (isAmex ? 15 : 16)
             && PassesLuhn(digits)
                 && Expiry?.Length == 5
         && IsExpiryValid(Expiry)
                    && Cvv?.Length == (isAmex ? 4 : 3);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error updating save eligibility"); }
        }

        private void ValidateForm() => UpdateCanSave();

        private static bool PassesLuhn(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return false;
            int sum = 0; bool alt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(digits[i])) return false;
                int n = digits[i] - '0';
                if (alt) { n *= 2; if (n > 9) n -= 9; }
                sum += n; alt = !alt;
            }
            return sum % 10 == 0;
        }

        private static bool IsExpiryValid(string expiry)
        {
            if (expiry?.Length != 5) return false;
            var parts = expiry.Split('/');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out var m) || !int.TryParse(parts[1], out var y)) return false;
            if (m < 1 || m > 12) return false;
            var fy = 2000 + y;
            return new DateTime(fy, m, DateTime.DaysInMonth(fy, m)) >= DateTime.Today;
        }

        // ── Formatting ────────────────────────────────────────────────────

        private void SetFormattedCardNumber(string value)
        {
            if (_updatingCardNumber) return;
            var digits = ExtractDigits(value);
            var isAmex = digits.StartsWith("34") || digits.StartsWith("37");
            digits = digits[..Math.Min(digits.Length, isAmex ? 15 : 16)];
            var formatted = FormatCardNumber(digits);
            if (formatted == _cardNumber) return;
            _updatingCardNumber = true;
            try
            {
                _cardNumber = formatted;
                RaisePropertyChanged(nameof(CardNumber));
                CardNetwork = DetectNetwork(digits);
                if (_submitted) ShowErrors();
                UpdateCanSave();
            }
            finally { _updatingCardNumber = false; }
        }

        private void SetFormattedExpiry(string value)
        {
            if (_updatingExpiry) return;
            var formatted = FormatExpiry(value);
            if (formatted == _expiry) return;
            _updatingExpiry = true;
            try
            {
                _expiry = formatted;
                RaisePropertyChanged(nameof(Expiry));
                if (_submitted) ShowErrors();
                UpdateCanSave();
            }
            finally { _updatingExpiry = false; }
        }

        private static string ExtractDigits(string value) =>
   new((value ?? string.Empty).Where(char.IsDigit).ToArray());

        private static string FormatCardNumber(string digits)
        {
            var sb = new System.Text.StringBuilder(19);
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append(' ');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        private static string FormatExpiry(string value)
        {
            var digits = ExtractDigits(value);
            if (digits.Length > 4) digits = digits[..4];
            if (digits.Length >= 2 && int.TryParse(digits[..2], out var month))
            {
                if (month == 0) digits = "01" + digits[2..];
                else if (month > 12) digits = "12" + digits[2..];
            }
            return digits.Length > 2 ? digits[..2] + "/" + digits[2..] : digits;
        }

        public static string DetectNetwork(string number)
        {
            if (string.IsNullOrEmpty(number)) return "Unknown";
            var n = number.Replace(" ", "").Replace("-", "");
            if (n.StartsWith("4")) return "Visa";
            if (n.StartsWith("5") || n.StartsWith("2")) return "Mastercard";
            if (n.StartsWith("34") || n.StartsWith("37")) return "Amex";
            if (n.StartsWith("6011") || n.StartsWith("65")) return "Discover";
            return "Unknown";
        }

        // ── Commands ──────────────────────────────────────────────────────

        private AsyncCommand _addCardCommand;
        public ICommand AddCardCommand =>
  _addCardCommand ??= new AsyncCommand(OpenAddFormAsync);

        private DelegateCommand _closeFormCommand;
        public DelegateCommand CloseFormCommand =>
        _closeFormCommand ??= new DelegateCommand(CloseForm);

        private AsyncCommand _saveCardCommand;
        public ICommand SaveCardCommand =>
            _saveCardCommand ??= new AsyncCommand(SaveCardAsync);

        private AsyncCommand _refreshCommand;
        public ICommand RefreshCommand =>
               _refreshCommand ??= new AsyncCommand(LoadCardsAsync);

        private AsyncCommand<CreditCardItemViewModel> _editCardCommand;
        public ICommand EditCardCommand =>
           _editCardCommand ??= new AsyncCommand<CreditCardItemViewModel>(OpenEditFormAsync);

        private AsyncCommand<CreditCardItemViewModel> _deleteCardCommand;
        public ICommand DeleteCardCommand =>
             _deleteCardCommand ??= new AsyncCommand<CreditCardItemViewModel>(DeleteCardAsync);

        private AsyncCommand<CreditCardItemViewModel> _copyNumberCommand;
        public ICommand CopyNumberCommand =>
           _copyNumberCommand ??= new AsyncCommand<CreditCardItemViewModel>(async item =>
                   {
                       await Clipboard.Default.SetTextAsync(item.Number);
                       _dialogService.ShowToast("Card number copied");
                   });

        private AsyncCommand<CreditCardItemViewModel> _copyCvvCommand;
        public ICommand CopyCvvCommand =>
       _copyCvvCommand ??= new AsyncCommand<CreditCardItemViewModel>(async item =>
    {
        await Clipboard.Default.SetTextAsync(item.Cvv);
        _dialogService.ShowToast("CVV copied");
    });

        private AsyncCommand<CreditCardItemViewModel> _cardOptionsCommand;
        public ICommand CardOptionsCommand =>
      _cardOptionsCommand ??= new AsyncCommand<CreditCardItemViewModel>(ShowCardOptionsAsync);

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
           foreach (var item in Cards) item.IsSelected = false;
           IsSelectionMode = false; SelectedCount = 0;
       });

        private DelegateCommand<CreditCardItemViewModel> _toggleItemSelectionCommand;
        public ICommand ToggleItemSelectionCommand =>
     _toggleItemSelectionCommand ??= new DelegateCommand<CreditCardItemViewModel>(item =>
 {
     if (item is null) return;
     item.IsSelected = !item.IsSelected;
     SelectedCount = Cards.Count(i => i.IsSelected);
 });

        private DelegateCommand<CreditCardItemViewModel> _longPressItemCommand;
        public ICommand LongPressItemCommand =>
        _longPressItemCommand ??= new DelegateCommand<CreditCardItemViewModel>(item =>
 {
     if (item is null) return;
     if (!IsSelectionMode) { IsSelectionMode = true; SelectedCount = 0; }
     item.IsSelected = true;
     SelectedCount = Cards.Count(i => i.IsSelected);
 });

        private DelegateCommand _selectAllCommand;
        public ICommand SelectAllCommand =>
           _selectAllCommand ??= new DelegateCommand(() =>
          {
              var newState = !IsAllSelected;
              foreach (var item in Cards) item.IsSelected = newState;
              SelectedCount = newState ? Cards.Count : 0;
          });

        private AsyncCommand _deleteSelectedCommand;
        public ICommand DeleteSelectedCommand =>
         _deleteSelectedCommand ??= new AsyncCommand(DeleteSelectedCardsAsync);

        private async Task DeleteSelectedCardsAsync()
        {
            var selected = Cards.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
              "Delete Selected",
            $"Delete {selected.Count} card{(selected.Count > 1 ? "s" : "")}? This cannot be undone.",
                 "Delete", "Cancel");
            if (!confirmed) return;
            try
            {
                foreach (var item in selected)
                    await _dataStorageService.DeleteCreditCardItemAsync(item.CredentialId);
                foreach (var item in selected) Cards.Remove(item);
                _dialogService.ShowToast($"{selected.Count} card{(selected.Count > 1 ? "s" : "")} deleted");
                IsSelectionMode = false; SelectedCount = 0;
                NoData = Cards.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete selected cards");
                _dialogService.ShowToast("Failed to delete some cards");
            }
        }
    }

    // ── Per-card display model ─────────────────────────────────────────────

    public class CreditCardItemViewModel : Prism.Mvvm.BindableBase
    {
        public Guid CredentialId { get; set; }

        // ── Multi-select support ─────────────────────────────────────────────
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string _cardName;
        public string CardName { get => _cardName; set => SetProperty(ref _cardName, value); }

        private string _cardHolder;
        public string CardHolder { get => _cardHolder; set => SetProperty(ref _cardHolder, value); }

        private string _number;
        public string Number
        {
            get => _number;
            set { SetProperty(ref _number, value); RaisePropertyChanged(nameof(MaskedNumber)); RaisePropertyChanged(nameof(LastFour)); }
        }

        public string MaskedNumber
        {
            get
            {
                if (string.IsNullOrEmpty(_number)) return "**** **** **** ****";
                var clean = _number.Replace(" ", "").Replace("-", "");
                return clean.Length < 4 ? _number : $"**** **** **** {clean[^4..]}";
            }
        }

        public string LastFour
        {
            get
            {
                if (string.IsNullOrEmpty(_number)) return "••••";
                var clean = _number.Replace(" ", "").Replace("-", "");
                return clean.Length >= 4 ? clean[^4..] : clean;
            }
        }

        private string _expiry;
        public string Expiry { get => _expiry; set => SetProperty(ref _expiry, value); }

        private string _cvv;
        public string Cvv { get => _cvv; set => SetProperty(ref _cvv, value); }

        private string _cardNetwork = "Visa";
        public string CardNetwork
        {
            get => _cardNetwork;
            set { SetProperty(ref _cardNetwork, value); RaisePropertyChanged(nameof(NetworkColor)); RaisePropertyChanged(nameof(NetworkGradientStart)); RaisePropertyChanged(nameof(NetworkGradientEnd)); }
        }

        public string NetworkGradientStart => CardNetwork switch
        {
            "Visa" => "#1A1F71",
            "Mastercard" => "#EB001B",
            "Amex" => "#007B5E",
            "Discover" => "#FF6600",
            _ => "#2B64A3",
        };

        public string NetworkGradientEnd => CardNetwork switch
        {
            "Visa" => "#2F3AAD",
            "Mastercard" => "#F79E1B",
            "Amex" => "#00B59C",
            "Discover" => "#FF9500",
            _ => "#1a3f6f",
        };

        public string NetworkColor => NetworkGradientStart;

        private bool _requireAuthBeforeFill;
        public bool RequireAuthBeforeFill
        {
            get => _requireAuthBeforeFill;
            set => SetProperty(ref _requireAuthBeforeFill, value);
        }
    }
}
