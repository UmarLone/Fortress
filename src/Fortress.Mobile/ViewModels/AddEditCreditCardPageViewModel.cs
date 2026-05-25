using Fortress.Mobile.Core.Intelligence;
using Controls.UserDialogs.Maui;
using FluentValidation;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Validators;
using Fortress.Mobile.Adapters;

namespace Fortress.ViewModels
{
    public class AddEditCreditCardPageViewModel : ViewModelBase
    {
        // ── Raised after OnNavigatedTo finishes populating all fields ─────────
        /// <summary>
        /// Fired once per navigation so the view can seed its unbound entries
        /// (card number, expiry) after all VM properties are set.
        /// </summary>
        public event EventHandler SeedRequested;

        #region Properties

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        private string _formTitle = "Add New Card";
        public string FormTitle
        {
            get => _formTitle;
            set => SetProperty(ref _formTitle, value);
        }

        public string SaveButtonText => IsEditMode ? "Update Card" : "Save Card";

        // ── Form fields ───────────────────────────────────────────────────────

        private string _cardName;
        public string CardName
        {
            get => _cardName;
            set { if (SetProperty(ref _cardName, value)) Validate(); }
        }

        private string _cardHolder;
        public string CardHolder
        {
            get => _cardHolder;
            set { if (SetProperty(ref _cardHolder, value)) Validate(); }
        }

        private string _cardNumber = string.Empty;
        /// <summary>Raw digits only, e.g. "4111111111111111". Entry has no formatting — preview displays formatted.</summary>
        public string CardNumber
        {
            get => _cardNumber;
            set
            {
                var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
                // Detect network FIRST so we know the correct max length
                var network = DetectNetwork(digits);
                var maxLen = network == "Amex" ? 15 : 16;
                if (digits.Length > maxLen) digits = digits[..maxLen];
                if (SetProperty(ref _cardNumber, digits))
                {
                    // Only update CardNetwork if it changed — avoids triggering CVV revalidation unnecessarily
                    if (_cardNetwork != network) CardNetwork = network;
                    RaisePropertyChanged(nameof(CardNumberDisplay));
                    Validate();
                }
            }
        }

        /// <summary>"XXXX XXXX XXXX XXXX" for the card preview — never bound to an Entry.</summary>
        public string CardNumberDisplay
        {
            get
            {
                var d = _cardNumber;
                if (string.IsNullOrEmpty(d)) return string.Empty;
                return string.Join(" ", Enumerable.Range(0, (d.Length + 3) / 4)
                  .Select(i => d.Substring(i * 4, Math.Min(4, d.Length - i * 4))));
            }
        }

        private string _expiryMonth = string.Empty;
        /// <summary>2-digit month, e.g. "12".</summary>
        public string ExpiryMonth
        {
            get => _expiryMonth;
            set
            {
                var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
                if (digits.Length > 2) digits = digits[..2];
                if (SetProperty(ref _expiryMonth, digits))
                {
                    RaisePropertyChanged(nameof(ExpiryDisplay));
                    Validate();
                }
            }
        }

        private string _expiryYear = string.Empty;
        /// <summary>2-digit year, e.g. "26".</summary>
        public string ExpiryYear
        {
            get => _expiryYear;
            set
            {
                var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
                if (digits.Length > 2) digits = digits[..2];
                if (SetProperty(ref _expiryYear, digits))
                {
                    RaisePropertyChanged(nameof(ExpiryDisplay));
                    Validate();
                }
            }
        }

        /// <summary>"MM/YY" for the card preview and validator.</summary>
        public string ExpiryDisplay =>
          string.IsNullOrEmpty(_expiryMonth) && string.IsNullOrEmpty(_expiryYear)
      ? string.Empty
      : $"{_expiryMonth}/{_expiryYear}";

        private string _cvv;
        public string Cvv
        {
            get => _cvv;
            set { if (SetProperty(ref _cvv, value)) Validate(); }
        }

        private string _cardNetwork = "Visa";
        public string CardNetwork
        {
            get => _cardNetwork;
            set
            {
                if (SetProperty(ref _cardNetwork, value))
                    Validate();
            }
        }

        // ── Validation state ─────────────────────────────────────────────────

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

        private Guid? _editingId;

        // ── Classifier hint ───────────────────────────────────────────────────────
        private bool _isCriticalHint = true;
        public bool IsCriticalHint
        {
            get => _isCriticalHint;
            set => SetProperty(ref _isCriticalHint, value);
        }

        /// <summary>
        /// When true this card requires biometric/PIN before autofill.
        /// </summary>
        private bool _formRequireAuth;
        public bool FormRequireAuth
        {
            get => _formRequireAuth;
            set => SetProperty(ref _formRequireAuth, value);
        }

        /// <summary>True when neither biometric nor PIN is configured.</summary>
        public bool NoAuthMethodConfigured =>
          !PreferenceWrapper.Instance.IsBiometricUnlockEnabled &&
                !PreferenceWrapper.Instance.IsPinUnlockEnabled;

        #endregion

        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<AddEditCreditCardPageViewModel> _logger;
        private readonly IValidator<AddEditCreditCardPageViewModel> _validator = new CreditCardValidator();
        private readonly IItemClassifier _itemClassifier;
        private readonly IBottomSheetService _bottomSheetService;

        public AddEditCreditCardPageViewModel(
            INavigationService navigationService,
       IDataStorageService dataStorageService,
            ICryptographyService cryptographyService,
       IUserDialogs dialogService,
            IEventAggregator eventAggregator,
    ILogger<AddEditCreditCardPageViewModel> logger,
            IItemClassifier itemClassifier,
            IBottomSheetService bottomSheetService)
     : base(navigationService)
        {
            _dataStorageService = dataStorageService;
            _cryptographyService = cryptographyService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _itemClassifier = itemClassifier;

            // Cards are always classified as critical — set once at construction
            var hint = _itemClassifier.ClassifyCard(string.Empty);
            IsCriticalHint = hint.IsCritical;
            _bottomSheetService = bottomSheetService;
        }

        // Defined here so CardNumber setter can call it before the Validation region
        public static string DetectNetwork(string number)
        {
            if (string.IsNullOrEmpty(number)) return "Visa";
            var n = new string(number.Where(char.IsDigit).ToArray());
            if (n.StartsWith("34") || n.StartsWith("37")) return "Amex";
            if (n.StartsWith("4")) return "Visa";
            if (n.StartsWith("5") || n.StartsWith("2")) return "Mastercard";
            if (n.StartsWith("6011") || n.StartsWith("65")) return "Discover";
            return "Unknown";
        }

        #region Navigation

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);

            RaisePropertyChanged(nameof(NoAuthMethodConfigured));

            // Always reset to defaults first so no stale state leaks between navigations
            _cardNumber = string.Empty;
            _expiryMonth = string.Empty;
            _expiryYear = string.Empty;
            _cardNetwork = "Visa";
            ClearFormErrors();

            if (parameters.TryGetValue("card", out CreditCardItemViewModel item) && item != null)
            {
                _editingId = item.CredentialId;
                IsEditMode = true;
                FormTitle = "Edit Card";
                CardName = item.CardName;
                CardHolder = item.CardHolder;
                Cvv = item.Cvv;
                var parts = (item.Expiry ?? string.Empty).Split('/');

                // Set backing fields directly so SetProperty always sees a change from the reset empty value
                ExpiryMonth = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                ExpiryYear = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                // Set CardNumber last — it sets CardNetwork via DetectNetwork
                CardNumber = item.Number ?? string.Empty;
                FormRequireAuth = item.RequireAuthBeforeFill;
            }
            else
            {
                // New cards start with blank state
                _editingId = null;
                IsEditMode = false;
                FormTitle = "Add New Card";
                CardName = string.Empty;
                CardHolder = string.Empty;
                Cvv = string.Empty;
                ExpiryMonth = string.Empty;
                ExpiryYear = string.Empty;
                CardNumber = string.Empty;
                CardNetwork = "Visa";
                FormRequireAuth = PreferenceWrapper.Instance.RequireAuthForCardFill;

                // ── Pre-populate from autofill save prompt ────────────────────
                if (parameters.TryGetValue("cardName", out string savedCardName) && !string.IsNullOrEmpty(savedCardName))
                    CardHolder = savedCardName;   // cardholder name captured by autofill

                if (parameters.TryGetValue("cardNumber", out string savedCardNumber) && !string.IsNullOrEmpty(savedCardNumber))
                    CardNumber = savedCardNumber;

                if (parameters.TryGetValue("cardExpMonth", out string savedExpMonth) && !string.IsNullOrEmpty(savedExpMonth))
                    ExpiryMonth = savedExpMonth;

                if (parameters.TryGetValue("cardExpYear", out string savedExpYear) && !string.IsNullOrEmpty(savedExpYear))
                    ExpiryYear = savedExpYear;

                if (parameters.TryGetValue("cardCode", out string savedCvv) && !string.IsNullOrEmpty(savedCvv))
                    Cvv = savedCvv;
            }

            // All fields are set — tell the view to seed its unbound entries.
            SeedRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ClearFormErrors()
        {
            CardNameError = string.Empty;
            CardNumberError = string.Empty;
            CardHolderError = string.Empty;
            ExpiryError = string.Empty;
            CvvError = string.Empty;
        }

        #endregion

        public void Validate()
        {
            var res = _validator.Validate(this);
            var errors = res.Errors.GroupBy(e => e.PropertyName)
          .ToDictionary(g => g.Key, g => g.First().ErrorMessage);

            CardNameError = errors.GetValueOrDefault(nameof(CardName), string.Empty);
            CardNumberError = errors.GetValueOrDefault(nameof(CardNumber), string.Empty);
            CardHolderError = errors.GetValueOrDefault(nameof(CardHolder), string.Empty);
            ExpiryError = errors.GetValueOrDefault(nameof(ExpiryMonth), string.Empty);
            CvvError = errors.GetValueOrDefault(nameof(Cvv), string.Empty);

            CanSave = res.IsValid;
        }

        private async Task SaveCreditCard()
        {
            if (IsSaving) return;
            Validate();
            if (!CanSave) return;

            IsSaving = true;
            try
            {
                var encNumberResult = await _cryptographyService.Encrypt(CardNumber);
                var encCvvResult = await _cryptographyService.Encrypt(Cvv);

                var encNumber = encNumberResult.Succeeded ? encNumberResult.Data : CardNumber;
                var encCvv = encCvvResult.Succeeded ? encCvvResult.Data : Cvv;

                var item = new CreditCardItem
                {
                    Id = _editingId ?? Guid.NewGuid(),
                    Label = CardName,
                    CardholderName = CardHolder,
                    Number = encNumber,
                    ExpiryMonth = ExpiryMonth,
                    ExpiryYear = ExpiryYear,
                    Cvv = encCvv,
                    CardNetwork = DetectNetwork(CardNumber),
                    Notes = string.Empty,
                    RequireAuthBeforeFill = FormRequireAuth,
                };

                await _dataStorageService.SaveCreditCardItemAsync(item);
                _dialogService.ShowToast(IsEditMode ? "Card updated" : "Card added");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Credit card save failed");
                _dialogService.ShowToast("Failed to save card");
            }
            finally
            {
                IsSaving = false;
            }
        }

        #region Commands

        private DelegateCommand _saveCommand;
        public DelegateCommand SaveCommand =>
               _saveCommand ??= new DelegateCommand(async () => await SaveCreditCard());

        private DelegateCommand _goBackCommand2;
        public DelegateCommand GoBackCommand =>
            _goBackCommand2 ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());

        #endregion
    }
}
