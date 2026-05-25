using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;
using MauiIcons.Core;
using MauiIcons.Material;

namespace Fortress.ViewModels
{
    /// <summary>
    /// ViewModel for the unified Add / Edit page that covers all SecureItemType
    /// variants: ID Card, Passport, Driver's License, Social Security, Tax Number,
    /// Wi-Fi and SSH.
    ///
    /// The XAML binds <see cref="SelectedType"/> (via a picker) and uses
    /// IsVisible bindings on each section group to show only the relevant fields.
    /// </summary>
    public class AddEditSecureItemPageViewModel : ViewModelBase
    {
        // ── Type picker ───────────────────────────────────────────────────────
        public List<SecureItemTypeOption> TypeOptions { get; } =
      [
      new(SecureItemType.Identity,   "Identity",    "\ue7fd"),
    new(SecureItemType.IdCard,     "ID Card",     "\ue8f4"),
   new(SecureItemType.Passport,       "Passport",  "\ue8f4"),
  new(SecureItemType.DriversLicense, "Driver's License", "\ue531"),
 new(SecureItemType.SocialSecurity, "Social Security",   "\ue8f4"),
   new(SecureItemType.TaxNumber,  "Tax Number",  "\ue8f4"),
  new(SecureItemType.WiFi,   "Wi-Fi","\ue63e"),
   new(SecureItemType.Ssh,   "SSH",              "\ue322"),
     new(SecureItemType.SecureNote, "Secure Note", "\ue873"),
 ];

        private SecureItemTypeOption _selectedType;
        public SecureItemTypeOption SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value))
                {
                    RaisePropertyChanged(nameof(IsDocumentSection));
                    RaisePropertyChanged(nameof(IsIdentitySection));
                    RaisePropertyChanged(nameof(IsWifiSection));
                    RaisePropertyChanged(nameof(IsSshSection));
                    RaisePropertyChanged(nameof(IsSecureNoteSection));
                    RaisePropertyChanged(nameof(FormTitle));
                    RaisePropertyChanged(nameof(SaveButtonText));
                    Validate();
                }
            }
        }

        // ── Section visibility ────────────────────────────────────────────────
        public bool IsDocumentSection => SelectedType?.Type is
        SecureItemType.IdCard or SecureItemType.Passport or
          SecureItemType.DriversLicense or
         SecureItemType.SocialSecurity or SecureItemType.TaxNumber;

        public bool IsIdentitySection => SelectedType?.Type == SecureItemType.Identity;
        public bool IsWifiSection => SelectedType?.Type == SecureItemType.WiFi;
        public bool IsSshSection => SelectedType?.Type == SecureItemType.Ssh;
        public bool IsSecureNoteSection => SelectedType?.Type == SecureItemType.SecureNote;

        /// <summary>
        /// The type picker is disabled in edit mode — changing the type of an
        /// existing encrypted record would silently orphan encrypted fields.
        /// </summary>
        public bool IsTypePickerEnabled => !_isEditMode;

        // ── Shared fields ─────────────────────────────────────────────────────
        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set { if (SetProperty(ref _isEditMode, value)) RaisePropertyChanged(nameof(IsTypePickerEnabled)); }
        }

        public string FormTitle => IsEditMode ? $"Edit {SelectedType?.Label ?? "Item"}" : $"Add {SelectedType?.Label ?? "Item"}";
        public string SaveButtonText => IsEditMode ? "Update" : "Save";

        private string _label = string.Empty;
        public string Label
        {
            get => _label;
            set { if (SetProperty(ref _label, value)) Validate(); }
        }

        private string _labelError = string.Empty;
        public string LabelError
        {
            get => _labelError;
            set { SetProperty(ref _labelError, value); RaisePropertyChanged(nameof(HasLabelError)); }
        }
        public bool HasLabelError => !string.IsNullOrEmpty(_labelError);

        // ── Document / ID fields ──────────────────────────────────────────────
        private string _fullName = string.Empty;
        public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }

        private string _dateOfBirth = string.Empty;
        public string DateOfBirth { get => _dateOfBirth; set => SetProperty(ref _dateOfBirth, value); }

        private string _nationality = string.Empty;
        public string Nationality { get => _nationality; set => SetProperty(ref _nationality, value); }

        private string _number = string.Empty;
        public string Number { get => _number; set => SetProperty(ref _number, value); }

        private string _issuingCountry = string.Empty;
        public string IssuingCountry { get => _issuingCountry; set => SetProperty(ref _issuingCountry, value); }

        private string _issuedDate = string.Empty;
        public string IssuedDate { get => _issuedDate; set => SetProperty(ref _issuedDate, value); }

        private string _expiryDate = string.Empty;
        public string ExpiryDate { get => _expiryDate; set => SetProperty(ref _expiryDate, value); }

        // ── Identity / personal profile fields ────────────────────────────────
        private string _firstName = string.Empty;
        public string FirstName { get => _firstName; set { if (SetProperty(ref _firstName, value)) Validate(); } }

        private string _lastName = string.Empty;
        public string LastName { get => _lastName; set => SetProperty(ref _lastName, value); }

        private string _email = string.Empty;
        public string Email { get => _email; set => SetProperty(ref _email, value); }

        private string _phone = string.Empty;
        public string Phone { get => _phone; set => SetProperty(ref _phone, value); }

        private string _company = string.Empty;
        public string Company { get => _company; set => SetProperty(ref _company, value); }

        private string _addressLine1 = string.Empty;
        public string AddressLine1 { get => _addressLine1; set => SetProperty(ref _addressLine1, value); }

        private string _addressLine2 = string.Empty;
        public string AddressLine2 { get => _addressLine2; set => SetProperty(ref _addressLine2, value); }

        private string _city = string.Empty;
        public string City { get => _city; set => SetProperty(ref _city, value); }

        private string _state = string.Empty;
        public string State { get => _state; set => SetProperty(ref _state, value); }

        private string _country = string.Empty;
        public string Country { get => _country; set => SetProperty(ref _country, value); }

        private string _postalCode = string.Empty;
        public string PostalCode { get => _postalCode; set => SetProperty(ref _postalCode, value); }

        // ── Wi-Fi fields ──────────────────────────────────────────────────────
        private string _ssid = string.Empty;
        public string Ssid { get => _ssid; set { if (SetProperty(ref _ssid, value)) Validate(); } }

        private string _wifiSecurity = "WPA2";
        public string WifiSecurity { get => _wifiSecurity; set => SetProperty(ref _wifiSecurity, value); }

        public List<string> WifiSecurityOptions { get; } = ["WPA3", "WPA2", "WPA", "WEP", "Open"];

        private string _password = string.Empty;
        public string Password { get => _password; set => SetProperty(ref _password, value); }

        // ── SSH fields ────────────────────────────────────────────────────────
        private string _host = string.Empty;
        public string Host { get => _host; set { if (SetProperty(ref _host, value)) Validate(); } }

        private string _port = "22";
        public string Port { get => _port; set => SetProperty(ref _port, value); }

        private string _username = string.Empty;
        public string Username { get => _username; set => SetProperty(ref _username, value); }

        private string _sshPassword = string.Empty;
        public string SshPassword { get => _sshPassword; set => SetProperty(ref _sshPassword, value); }

        private string _privateKey = string.Empty;
        public string PrivateKey { get => _privateKey; set => SetProperty(ref _privateKey, value); }

        private string _keyFingerprint = string.Empty;
        public string KeyFingerprint { get => _keyFingerprint; set => SetProperty(ref _keyFingerprint, value); }

        // ── Secure Note field ─────────────────────────────────────────────────
        private string _noteContent = string.Empty;
        public string NoteContent { get => _noteContent; set { if (SetProperty(ref _noteContent, value)) Validate(); } }

        // ── Save / busy state ─────────────────────────────────────────────────
        private bool _canSave;
        public bool CanSave { get => _canSave; set => SetProperty(ref _canSave, value); }

        private bool _isSaving;
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        private Guid? _editingId;

        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<AddEditSecureItemPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;

        public AddEditSecureItemPageViewModel(
           INavigationService navigationService,
            IDataStorageService dataStorageService,
             ICryptographyService cryptographyService,
        IUserDialogs dialogService,
           IEventAggregator eventAggregator,
       ILogger<AddEditSecureItemPageViewModel> logger,
            IBottomSheetService bottomSheetService)
              : base(navigationService)
        {
            _dataStorageService = dataStorageService;
            _cryptographyService = cryptographyService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
            _selectedType = TypeOptions[0];
        }

        // ── Navigation ────────────────────────────────────────────────────────
        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            LabelError = string.Empty;

            if (parameters.TryGetValue("secureItem", out SecureItemViewModel vm) && vm != null)
            {
                // Edit mode — type is locked to prevent orphaning encrypted fields
                _editingId = vm.Id;
                IsEditMode = true;
                SelectedType = TypeOptions.FirstOrDefault(o => o.Type == vm.ItemType) ?? TypeOptions[0];

                Label = vm.Label;
                // Document fields
                FullName = vm.FullName;
                DateOfBirth = vm.DateOfBirth;
                Nationality = vm.Nationality;
                Number = vm.Number;
                IssuingCountry = vm.IssuingCountry;
                IssuedDate = vm.IssuedDate;
                ExpiryDate = vm.ExpiryDate;
                // Identity fields
                FirstName = vm.FirstName;
                LastName = vm.LastName;
                Email = vm.Email;
                Phone = vm.Phone;
                Company = vm.Company;
                AddressLine1 = vm.AddressLine1;
                AddressLine2 = vm.AddressLine2;
                City = vm.City;
                State = vm.State;
                Country = vm.Nationality;   // Identity reuses Nationality as Country
                PostalCode = vm.PostalCode;
                // Wi-Fi
                Ssid = vm.Ssid;
                WifiSecurity = string.IsNullOrEmpty(vm.WifiSecurity) ? "WPA2" : vm.WifiSecurity;
                Password = vm.Password;
                // SSH
                Host = vm.Host;
                Port = string.IsNullOrEmpty(vm.Port) ? "22" : vm.Port;
                Username = vm.Username;
                SshPassword = vm.SshPassword;
                PrivateKey = vm.PrivateKey;
                KeyFingerprint = vm.KeyFingerprint;
                // Secure Note
                NoteContent = vm.NoteContent;

            }
            else
            {
                _editingId = null;
                IsEditMode = false;

                if (parameters.TryGetValue("itemType", out SecureItemType preselected))
                    SelectedType = TypeOptions.FirstOrDefault(o => o.Type == preselected) ?? TypeOptions[0];
                else
                    SelectedType = TypeOptions[0];

                ResetFields();
            }

            RaisePropertyChanged(nameof(FormTitle));
            RaisePropertyChanged(nameof(SaveButtonText));
            Validate();
        }

        private void ResetFields()
        {
            Label = FullName = DateOfBirth = Nationality = Number
                 = IssuingCountry = IssuedDate = ExpiryDate
         = FirstName = LastName = Email = Phone = Company
           = AddressLine1 = AddressLine2 = City = State = Country = PostalCode
                = Ssid = Password = Host = Username
           = SshPassword = PrivateKey = KeyFingerprint
                    = NoteContent = string.Empty;
            WifiSecurity = "WPA2";
            Port = "22";
        }

        // ── Validation ────────────────────────────────────────────────────────
        public void Validate()
        {
            LabelError = string.IsNullOrWhiteSpace(Label) ? "Label is required" : string.Empty;

            bool fieldValid = SelectedType?.Type switch
            {
                SecureItemType.Identity => !string.IsNullOrWhiteSpace(FirstName),
                SecureItemType.WiFi => !string.IsNullOrWhiteSpace(Ssid),
                SecureItemType.Ssh => !string.IsNullOrWhiteSpace(Host),
                SecureItemType.SecureNote => !string.IsNullOrWhiteSpace(NoteContent),
                _ => true
            };

            CanSave = string.IsNullOrEmpty(LabelError) && fieldValid;
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
                var encNumber = await EncryptAsync(Number);
                var encPassword = await EncryptAsync(Password);
                var encSshPwd = await EncryptAsync(SshPassword);
                var encPrivateKey = await EncryptAsync(PrivateKey);

                var item = new SecureItem
                {
                    Id = _editingId ?? Guid.NewGuid(),
                    ItemType = SelectedType!.Type,
                    Label = Label.Trim(),
                    // Document
                    FullName = FullName.Trim(),
                    DateOfBirth = DateOfBirth.Trim(),
                    Nationality = IsIdentitySection ? Country.Trim() : Nationality.Trim(),
                    Number = encNumber,
                    IssuingCountry = IssuingCountry.Trim(),
                    IssuedDate = IssuedDate.Trim(),
                    ExpiryDate = ExpiryDate.Trim(),
                    // Identity
                    FirstName = FirstName.Trim(),
                    LastName = LastName.Trim(),
                    Email = Email.Trim(),
                    Phone = Phone.Trim(),
                    Company = Company.Trim(),
                    AddressLine1 = AddressLine1.Trim(),
                    AddressLine2 = AddressLine2.Trim(),
                    City = City.Trim(),
                    State = State.Trim(),
                    PostalCode = PostalCode.Trim(),
                    // Wi-Fi
                    Ssid = Ssid.Trim(),
                    WifiSecurity = WifiSecurity,
                    Password = encPassword,
                    // SSH
                    Host = Host.Trim(),
                    Port = Port.Trim(),
                    Username = Username.Trim(),
                    SshPassword = encSshPwd,
                    PrivateKey = encPrivateKey,
                    KeyFingerprint = KeyFingerprint.Trim(),
                    // Secure Note
                    NoteContent = await EncryptAsync(NoteContent),
                };

                await _dataStorageService.SaveSecureItemAsync(item);
                _dialogService.ShowToast(IsEditMode ? "Item updated" : "Item saved");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save secure item");
                _dialogService.ShowToast("Failed to save item");
            }
            finally { IsSaving = false; }
        }

        private async Task DeleteAsync()
        {
            if (_editingId == null) return;
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
                  "Delete Item", $"Delete \"{Label}\"?", "Delete", "Cancel");
            if (!confirmed) return;
            try
            {
                await _dataStorageService.DeleteSecureItemAsync(_editingId.Value);
                _dialogService.ShowToast("Item deleted");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete secure item");
                _dialogService.ShowToast("Failed to delete item");
            }
        }

        private async Task<string> EncryptAsync(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            var result = await _cryptographyService.Encrypt(plain);
            return result.Succeeded ? result.Data : plain;
        }

        // ── Commands ──────────────────────────────────────────────────────────
        private DelegateCommand _saveCommand;
        public DelegateCommand SaveCommand =>
        _saveCommand ??= new DelegateCommand(async () => await SaveAsync())
 .ObservesCanExecute(() => CanSave);

        private DelegateCommand _deleteCommand;
        public DelegateCommand DeleteCommand =>
      _deleteCommand ??= new DelegateCommand(async () => await DeleteAsync());

        private AsyncCommand _selectTypeCommand;
        public ICommand SelectTypeCommand =>
            _selectTypeCommand ??= new AsyncCommand(SelectTypeAsync);

        private AsyncCommand _selectWifiSecurityCommand;
        public ICommand SelectWifiSecurityCommand =>
         _selectWifiSecurityCommand ??= new AsyncCommand(SelectWifiSecurityAsync);

        // ── Bottom-sheet selection helpers ────────────────────────────────────

        private async Task SelectTypeAsync()
        {
            if (!IsTypePickerEnabled) return;

            var options = TypeOptions.Select(opt => new BottomSheetOption
            {
                Title = opt.Label,
                IsSelected = SelectedType?.Type == opt.Type,
                Action = () => SelectedType = opt,
            }).ToList();

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(
         options, "Select Item Type");
        }

        private async Task SelectWifiSecurityAsync()
        {
            var options = WifiSecurityOptions.Select(opt => new BottomSheetOption
            {
                Title = opt,
                IsSelected = WifiSecurity == opt,
                Icon = new MauiIcon().Icon(MaterialIcons.Wifi),
                Action = () => WifiSecurity = opt,
            }).ToList();

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(
               options, "Security Type");
        }
    }

    // ── Type option (used by the selection bottom sheet) ──────────────────────
    public sealed record SecureItemTypeOption(
  SecureItemType Type,
        string Label,
        string IconGlyph);
}
