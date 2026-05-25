using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Helpers;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;

namespace Fortress.ViewModels
{
    public class AddEditAuthenticatorPageViewModel : ViewModelBase
    {
        #region Properties

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        private string _formTitle = "Add Authenticator";
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }

        // ── Manual entry fields ───────────────────────────────────────────────
        private string _issuer = string.Empty;
        public string Issuer { get => _issuer; set => SetProperty(ref _issuer, value); }

        private string _account = string.Empty;
        public string Account { get => _account; set => SetProperty(ref _account, value); }

        private string _secret = string.Empty;
        public string Secret { get => _secret; set => SetProperty(ref _secret, value); }

        private string _period = "30";
        public string Period { get => _period; set => SetProperty(ref _period, value); }

        // ── Validation ────────────────────────────────────────────────────────
        private string _issuerError = string.Empty;
        public string IssuerError { get => _issuerError; set { SetProperty(ref _issuerError, value); RaisePropertyChanged(nameof(HasIssuerError)); } }
        public bool HasIssuerError => !string.IsNullOrEmpty(_issuerError);

        private string _secretError = string.Empty;
        public string SecretError { get => _secretError; set { SetProperty(ref _secretError, value); RaisePropertyChanged(nameof(HasSecretError)); } }
        public bool HasSecretError => !string.IsNullOrEmpty(_secretError);

        private string _periodError = string.Empty;
        public string PeriodError { get => _periodError; set { SetProperty(ref _periodError, value); RaisePropertyChanged(nameof(HasPeriodError)); } }
        public bool HasPeriodError => !string.IsNullOrEmpty(_periodError);

        private bool _canSave;
        public bool CanSave { get => _canSave; set => SetProperty(ref _canSave, value); }

        private bool _isSaving;
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        private Guid? _editingId;

        #endregion

        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly ILogger<AddEditAuthenticatorPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;

        public AddEditAuthenticatorPageViewModel(
     INavigationService navigationService,
       IUserDialogs dialogService,
     IEventAggregator eventAggregator,
    IDataStorageService dataStorageService,
               ICryptographyService cryptographyService,
               ILogger<AddEditAuthenticatorPageViewModel> logger,
     IBottomSheetService bottomSheetService)
   : base(navigationService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _dataStorageService = dataStorageService;
            _cryptographyService = cryptographyService;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);

            if (parameters.TryGetValue("authenticator", out Authenticator auth) && auth != null)
            {
                // Edit mode – decrypt secret for display
                _editingId = auth.Id;
                IsEditMode = true;
                FormTitle = "Edit Authenticator";
                Issuer = auth.Issuer ?? string.Empty;
                Account = auth.Username ?? string.Empty;
                var dec = await _cryptographyService.Decrypt(auth.Secret);
                Secret = dec.Succeeded ? dec.Data : auth.Secret;
                Period = auth.Period.ToString();
            }
            else
            {
                _editingId = null;
                IsEditMode = false;
                FormTitle = "Add Authenticator";
                Issuer = string.Empty;
                Account = string.Empty;
                Secret = string.Empty;
                Period = "30";
            }

            ClearErrors();
            Validate();
        }

        // ── QR scan via shared ScanQrCodeSheet ────────────────────────────────
        private async Task ExecuteScanQrAsync()
        {
            try
            {
                // ScanOnlyMode = true: hide the "Enter Manually" tab – the page
                // itself already has full manual entry fields below the scan card.
                var uri = await _bottomSheetService
                         .ShowAsync<ScanQrCodeSheet, ScanQrCodeSheetViewModel, string>(
                  args: new ScanQrArgs(
               ScanOnlyMode: true,
                 SheetTitle: "Scan QR Code",
                       SheetSubtitle: "Point your camera at the 2FA QR code"));

                if (string.IsNullOrWhiteSpace(uri)) return;

                var parsed = AuthenticatorHelper.FromOtpAuthUri(uri);
                Issuer = parsed.Issuer ?? string.Empty;
                Account = parsed.Username ?? string.Empty;
                Secret = parsed.Secret ?? string.Empty;
                Period = parsed.Period.ToString();
                Validate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddEditAuthenticatorVM: QR scan failed");
                _dialogService.ShowToast("Could not read QR code – please enter manually");
            }
        }

        // ── Validation ────────────────────────────────────────────────────────
        private void ClearErrors() => IssuerError = SecretError = PeriodError = string.Empty;

        public void Validate()
        {
            IssuerError = string.IsNullOrWhiteSpace(Issuer)
      ? "Issuer / service name is required"
  : string.Empty;

            if (string.IsNullOrWhiteSpace(Secret))
                SecretError = "Secret key is required";
            else if (!IsValidBase32(Secret.Trim()))
                SecretError = "Must be a valid Base32 secret (A–Z, 2–7)";
            else
                SecretError = string.Empty;

            if (!int.TryParse(Period, out int p) || p < 15 || p > 120)
                PeriodError = "Period must be between 15 and 120 seconds";
            else
                PeriodError = string.Empty;

            CanSave = string.IsNullOrEmpty(IssuerError)
       && string.IsNullOrEmpty(SecretError)
      && string.IsNullOrEmpty(PeriodError);
        }

        private static bool IsValidBase32(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var clean = value.ToUpperInvariant().Replace(" ", "").TrimEnd('=');
            return clean.All(c => (c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7'));
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            if (IsSaving) return;
            Validate();
            if (!CanSave) return;

            IsSaving = true;
            try
            {
                var secretClean = Secret.Trim().ToUpperInvariant();
                var encSecret = (await _cryptographyService.Encrypt(secretClean)).Data ?? secretClean;
                var period = int.Parse(Period);

                if (_editingId.HasValue)
                    await _dataStorageService.DeleteAuthenticatorAsync(_editingId.Value);

                var auth = new Authenticator
                {
                    Id = _editingId ?? Guid.NewGuid(),
                    Issuer = Issuer.Trim(),
                    Username = Account.Trim(),
                    Secret = encSecret,
                    Period = period,
                };
                await _dataStorageService.AddAuthenticatorAsync(auth);

                if (!_editingId.HasValue)
                    _eventAggregator.GetEvent<AuthenticatorAddedEvent>().Publish(auth);

                _dialogService.ShowToast(
           _editingId.HasValue ? "Authenticator updated" : "Authenticator added");
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save authenticator");
                _dialogService.ShowToast("Failed to save authenticator");
            }
            finally { IsSaving = false; }
        }

        // ── Delete ────────────────────────────────────────────────────────────
        private async Task DeleteAsync()
        {
            if (_editingId == null) return;

            var confirmed = await _bottomSheetService.ConfirmAsync(
    "Delete Authenticator", $"Delete {Issuer}?", "Yes", "No");
            if (!confirmed) return;

            try
            {
                await _dataStorageService.DeleteAuthenticatorAsync(_editingId.Value);
                _eventAggregator.GetEvent<AuthenticatorDeletedEvent>().Publish(null);
                _dialogService.ShowToast("Authenticator deleted");
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete authenticator");
                _dialogService.ShowToast("Failed to delete authenticator");
            }
        }

        #region Commands

        private AsyncCommand _scanQrCommand;
        public ICommand ScanQrCommand =>
       _scanQrCommand ??= new AsyncCommand(ExecuteScanQrAsync);

        private AsyncCommand _saveCommand;
        public ICommand SaveCommand =>
     _saveCommand ??= new AsyncCommand(SaveAsync);

        private AsyncCommand _deleteCommand;
  public ICommand DeleteCommand =>
  _deleteCommand ??= new AsyncCommand(DeleteAsync);

 #endregion
    }
}
