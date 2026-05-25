using Controls.UserDialogs.Maui;
using FluentValidation;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Helpers;
using Fortress.Services;
using Fortress.Validators;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;

namespace Fortress.ViewModels
{
    public class AddEditCredentialPageViewModel : ViewModelBase
    {
        #region Properties

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        private string _formTitle = "Add Password";
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }

        private string _formCredentialType = "Web";
        public string FormCredentialType
        {
            get => _formCredentialType;
            set
            {
                SetProperty(ref _formCredentialType, value);
                RaisePropertyChanged(nameof(FormTypeIndex));
                RaisePropertyChanged(nameof(IsTypeWeb));
                RaisePropertyChanged(nameof(IsTypeMobile));
                RaisePropertyChanged(nameof(IsTypeDesktop));
            }
        }
        public bool FormTypeIndex => _formCredentialType == "Web";
        public bool IsTypeWeb => _formCredentialType == "Web";
        public bool IsTypeMobile => _formCredentialType == "PhoneApplication";
        public bool IsTypeDesktop => _formCredentialType == "Application";

        private string _formDomain;
        public string FormDomain { get => _formDomain; set => SetProperty(ref _formDomain, value); }

        private string _formUsername;
        public string FormUsername { get => _formUsername; set => SetProperty(ref _formUsername, value); }

        private string _formPassword;
        public string FormPassword { get => _formPassword; set => SetProperty(ref _formPassword, value); }

        private bool _isPasswordHidden = true;
        public bool IsPasswordHidden { get => _isPasswordHidden; set => SetProperty(ref _isPasswordHidden, value); }

        private bool _formHasOtp;
        public bool FormHasOtp { get => _formHasOtp; set => SetProperty(ref _formHasOtp, value); }

        private string _formOtpSecret;
        public string FormOtpSecret { get => _formOtpSecret; set => SetProperty(ref _formOtpSecret, value); }

        /// <summary>
        /// When true this credential will require biometric/PIN authentication
        /// before it can be used for autofill.
        /// </summary>
        private bool _formRequireAuth;
        public bool FormRequireAuth
        {
            get => _formRequireAuth;
            set => SetProperty(ref _formRequireAuth, value);
        }

        /// <summary>
        /// True when neither biometric nor PIN is configured – used to show
        /// a hint label beneath the toggle.
        /// </summary>
        public bool NoAuthMethodConfigured =>
      !PreferenceWrapper.Instance.IsBiometricUnlockEnabled &&
              !PreferenceWrapper.Instance.IsPinUnlockEnabled;

        private bool _isSaving;
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        // ── Validation ────────────────────────────────────────────────────────
        private string _domainError;
        public string DomainError { get => _domainError; set { SetProperty(ref _domainError, value); RaisePropertyChanged(nameof(HasDomainError)); } }
        public bool HasDomainError => !string.IsNullOrEmpty(_domainError);

        private string _usernameError;
        public string UsernameError { get => _usernameError; set { SetProperty(ref _usernameError, value); RaisePropertyChanged(nameof(HasUsernameError)); } }
        public bool HasUsernameError => !string.IsNullOrEmpty(_usernameError);

        private string _passwordError;
        public string PasswordError { get => _passwordError; set { SetProperty(ref _passwordError, value); RaisePropertyChanged(nameof(HasPasswordError)); } }
        public bool HasPasswordError => !string.IsNullOrEmpty(_passwordError);

        private string _otpSecretError;
        public string OtpSecretError { get => _otpSecretError; set { SetProperty(ref _otpSecretError, value); RaisePropertyChanged(nameof(HasOtpSecretError)); } }
        public bool HasOtpSecretError => !string.IsNullOrEmpty(_otpSecretError);

        private bool _canSave;
        public bool CanSave { get => _canSave; set => SetProperty(ref _canSave, value); }

        private Guid? _editingId;

        // ── Classifier hints ──────────────────────────────────────────────────
        private bool _isCriticalHint;
        public bool IsCriticalHint { get => _isCriticalHint; set => SetProperty(ref _isCriticalHint, value); }

        private string _criticalHintReason = string.Empty;
        public string CriticalHintReason { get => _criticalHintReason; set => SetProperty(ref _criticalHintReason, value); }

        private string _suggestedTagsDisplay = string.Empty;
        public string SuggestedTagsDisplay { get => _suggestedTagsDisplay; set => SetProperty(ref _suggestedTagsDisplay, value); }

        #endregion

        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly ILogger<AddEditCredentialPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;
        private readonly VaultHealthCalculator _healthCalculator;
        private readonly IValidator<AddEditCredentialPageViewModel> _validator = new AddEditCredentialValidator();
        private readonly IItemClassifier _itemClassifier;
        private readonly IEventLogProcessor _eventLogProcessor;

        public AddEditCredentialPageViewModel(
           INavigationService navigationService,
        IUserDialogs dialogService,
          IEventAggregator eventAggregator,
       IDataStorageService dataStorageService,
             ICryptographyService cryptographyService,
              ILogger<AddEditCredentialPageViewModel> logger,
              IBottomSheetService bottomSheetService,
              VaultHealthCalculator healthCalculator,
        IItemClassifier itemClassifier,
           IDeviceServices deviceService,
          IEventLogProcessor eventLogProcessor)
        : base(navigationService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _dataStorageService = dataStorageService;
            _cryptographyService = cryptographyService;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
            _healthCalculator = healthCalculator;
            _itemClassifier = itemClassifier;
            _eventLogProcessor = eventLogProcessor;
        }

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            // Re-evaluate every time – covers returning from the Settings page
            // after the user has just configured biometric or PIN unlock.
            RaisePropertyChanged(nameof(NoAuthMethodConfigured));

            if (parameters.TryGetValue("credential", out CredentialView credential) && credential != null)
            {
                // Edit mode
                _editingId = credential.Id;
                IsEditMode = true;
                FormTitle = "Edit Password";
                FormCredentialType = credential.CredentialType switch
                {
                    "PhoneApplication" => "PhoneApplication",
                    "Application" => "Application",
                    _ => "Web"
                };
                FormDomain = credential.Domain;
                FormUsername = credential.Username;
                FormPassword = string.Empty;
                FormHasOtp = credential.HasOtp;
                FormOtpSecret = credential.HasOtp ? credential.Data : string.Empty;
                IsPasswordHidden = true;
                FormRequireAuth = credential.RequireAuthBeforeFill;

                _ = _eventLogProcessor.ProcessEventLogAsync(new EventLog
                {
                    EventType = (int)EventLogType.CredentialViewed,
                    CredentialId = credential.Id,
                    CredentialLabel = credential.Domain,
                });
            }
            else
            {
                // Add mode
                _editingId = null;
                IsEditMode = false;
                FormTitle = "Add Password";
                FormCredentialType = "Web";
                FormUsername = string.Empty;
                FormPassword = string.Empty;
                FormHasOtp = false;
                FormOtpSecret = string.Empty;
                IsPasswordHidden = true;
                // New items inherit the global default
                FormRequireAuth = PreferenceWrapper.Instance.RequireAuthForPasswordFill;

                FormDomain = parameters.TryGetValue("domain", out string domain) && !string.IsNullOrWhiteSpace(domain)
   ? domain
         : string.Empty;

                if (!string.IsNullOrWhiteSpace(FormDomain))
                    RunClassifier();
            }

            Validate();
        }

        #region Save / Delete

        private async Task SaveCredentialAsync()
        {
            if (IsSaving) return;
            Validate();
            if (!CanSave) return;

            IsSaving = true;
            try
            {
                var loginType = FormCredentialType switch
                {
                    "PhoneApplication" => LoginType.PhoneApp,
                    "Application" => LoginType.DesktopApp,
                    _ => LoginType.Web
                };

                var item = new LoginItem
                {
                    Id = _editingId ?? Guid.NewGuid(),
                    Label = FormDomain.Trim(),
                    Url = FormDomain.Trim(),
                    Username = FormUsername?.Trim() ?? string.Empty,
                    Password = FormPassword ?? string.Empty,
                    OtpSecret = FormHasOtp ? FormOtpSecret ?? string.Empty : null,
                    LoginType = loginType,
                    RequireAuthBeforeFill = FormRequireAuth,
                };

                if (!string.IsNullOrEmpty(item.Password))
                {
                    var (score, level) = _healthCalculator.ScorePassword(item.Password);
                    item.PasswordStrengthScore = score;
                    item.PasswordStrengthLevel = (int)level;
                    item.PasswordHash = ComputePasswordHash(item.Password);
                    var enc = await _cryptographyService.Encrypt(item.Password);
                    if (enc.Succeeded) item.Password = enc.Data;
                }
                if (!string.IsNullOrEmpty(item.OtpSecret))
                {
                    var enc = await _cryptographyService.Encrypt(item.OtpSecret);
                    if (enc.Succeeded) item.OtpSecret = enc.Data;
                }

                await _dataStorageService.SaveLoginItemAsync(item);
                _ = _eventLogProcessor.ProcessEventLogAsync(new EventLog
                {
                    EventType = (int)(_editingId.HasValue
                 ? EventLogType.CredentialUpdated
            : EventLogType.CredentialAdded),
                    CredentialId = item.Id,
                    CredentialLabel = item.Label,
                });
                _dialogService.ShowToast(_editingId.HasValue ? "Password updated" : "Password added");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save credential");
                _dialogService.ShowToast("Failed to save password");
            }
            finally { IsSaving = false; }
        }

        private async Task DeleteCredentialAsync()
        {
            if (_editingId == null) return;

            var result = await _bottomSheetService.ConfirmAsync(
"Delete Password", $"Delete {FormDomain}?", "Yes", "No");
            if (!result) return;

            try
            {
                await _dataStorageService.DeleteLoginItemAsync(_editingId.Value);
                _ = _eventLogProcessor.ProcessEventLogAsync(new EventLog
                {
                    EventType = (int)EventLogType.CredentialDeleted,
                    CredentialId = _editingId.Value,
                    CredentialLabel = FormDomain,
                });
                _dialogService.ShowToast("Password deleted");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete credential");
                _dialogService.ShowToast("Failed to delete password");
            }
        }

        #endregion

        #region Validation

        public void Validate()
        {
            var result = _validator.Validate(this);
            var errors = result.Errors
           .GroupBy(f => f.PropertyName)
             .ToDictionary(g => g.Key, g => g.First().ErrorMessage);

            DomainError = errors.GetValueOrDefault(nameof(FormDomain), string.Empty);
            UsernameError = errors.GetValueOrDefault(nameof(FormUsername), string.Empty);
            PasswordError = errors.GetValueOrDefault(nameof(FormPassword), string.Empty);
            OtpSecretError = errors.GetValueOrDefault(nameof(FormOtpSecret), string.Empty);

            CanSave = result.IsValid;
        }

        #endregion

        #region OTP scan via shared ScanQrCodeSheet

        private async Task ExecuteScanOtpAsync()
        {
            try
            {
                var uri = await _bottomSheetService
           .ShowAsync<ScanQrCodeSheet, ScanQrCodeSheetViewModel, string>();

                if (string.IsNullOrWhiteSpace(uri)) return;

                var secret = AuthenticatorHelper.FromOtpAuthUri(uri).Secret;
                FormOtpSecret = secret ?? string.Empty;
                FormHasOtp = true;
                Validate();
                _dialogService.ShowToast("OTP secret linked");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddEditCredentialVM: OTP QR scan failed");
                _dialogService.ShowToast("Could not read QR code");
            }
        }

        #endregion

        #region Classifier

        public void RunClassifier()
        {
            try
            {
                var hint = _itemClassifier.Classify(FormDomain ?? string.Empty, FormDomain ?? string.Empty);
                IsCriticalHint = hint.IsCritical;
                CriticalHintReason = hint.CriticalReason ?? string.Empty;
                SuggestedTagsDisplay = hint.SuggestedTags.Count > 0
                       ? string.Join(" – ", hint.SuggestedTags)
                     : string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Classifier failed for domain {D}", FormDomain);
            }
        }

        #endregion

        #region Commands

        private DelegateCommand _scanOtpCommand;
        public DelegateCommand ScanOtpCommand =>
              _scanOtpCommand ??= new DelegateCommand(async () => await ExecuteScanOtpAsync());

        private DelegateCommand _saveCommand;
        public DelegateCommand SaveCommand =>
            _saveCommand ??= new DelegateCommand(async () => await SaveCredentialAsync());

        private DelegateCommand _deleteCommand;
        public DelegateCommand DeleteCommand =>
            _deleteCommand ??= new DelegateCommand(async () => await DeleteCredentialAsync());

        private DelegateCommand _goBackCommand;
        public DelegateCommand GoBackCommand =>
     _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());

        private DelegateCommand _togglePasswordVisibilityCommand;
        public DelegateCommand TogglePasswordVisibilityCommand =>
    _togglePasswordVisibilityCommand ??= new DelegateCommand(() => IsPasswordHidden = !IsPasswordHidden);

        private DelegateCommand<string> _setTypeCommand;
        public DelegateCommand<string> SetTypeCommand =>
            _setTypeCommand ??= new DelegateCommand<string>(t => FormCredentialType = t);

        #endregion

        private static string ComputePasswordHash(string password)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(
System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
