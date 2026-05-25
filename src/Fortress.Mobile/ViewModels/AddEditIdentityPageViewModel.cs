using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Services;

namespace Fortress.ViewModels
{
    public class AddEditIdentityPageViewModel : ViewModelBase
    {
        #region Properties

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        private string _formTitle = "Add Identity";
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }

        public string SaveButtonText => IsEditMode ? "Update Identity" : "Save Identity";

        // ── Form fields ───────────────────────────────────────────────────────
        private string _label = string.Empty;
        public string Label
        {
            get => _label;
            set { if (SetProperty(ref _label, value)) Validate(); }
        }

        private string _firstName = string.Empty;
        public string FirstName
        {
            get => _firstName;
            set { if (SetProperty(ref _firstName, value)) Validate(); }
        }

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

        // ── Validation ────────────────────────────────────────────────────────
        private string _labelError = string.Empty;
        public string LabelError { get => _labelError; set { SetProperty(ref _labelError, value); RaisePropertyChanged(nameof(HasLabelError)); } }
        public bool HasLabelError => !string.IsNullOrEmpty(_labelError);

        private string _firstNameError = string.Empty;
        public string FirstNameError { get => _firstNameError; set { SetProperty(ref _firstNameError, value); RaisePropertyChanged(nameof(HasFirstNameError)); } }
        public bool HasFirstNameError => !string.IsNullOrEmpty(_firstNameError);

        private bool _canSave;
        public bool CanSave { get => _canSave; set => SetProperty(ref _canSave, value); }

        private bool _isSaving;
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        private Guid? _editingId;

        #endregion

        private readonly IDataStorageService _dataStorageService;
        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<AddEditIdentityPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;

        public AddEditIdentityPageViewModel(
     INavigationService navigationService,
    IDataStorageService dataStorageService,
      IUserDialogs dialogService,
     IEventAggregator eventAggregator,
      ILogger<AddEditIdentityPageViewModel> logger,
      IBottomSheetService bottomSheetService)
     : base(navigationService)
        {
            _dataStorageService = dataStorageService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
        }

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            ClearErrors();

            if (parameters.TryGetValue("identity", out IdentityItemViewModel item) && item != null)
            {
                _editingId = item.Id;
                IsEditMode = true;
                FormTitle = "Edit Identity";
                Label = item.Label;
                FirstName = item.FirstName;
                LastName = item.LastName;
                Email = item.Email;
                Phone = item.Phone;
                Company = item.Company;
                AddressLine1 = item.AddressLine1;
                AddressLine2 = item.AddressLine2;
                City = item.City;
                State = item.State;
                Country = item.Country;
                PostalCode = item.PostalCode;
            }
            else
            {
                _editingId = null;
                IsEditMode = false;
                FormTitle = "Add Identity";
                Label = FirstName = LastName = Email = Phone = Company
         = AddressLine1 = AddressLine2 = City = State = Country = PostalCode
        = string.Empty;
            }

            RaisePropertyChanged(nameof(SaveButtonText));
            Validate();
        }

        // ── Save / Delete ─────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            if (IsSaving) return;
            Validate();
            if (!CanSave) return;

            IsSaving = true;
            try
            {
                var identity = new IdentityItem
                {
                    Id = _editingId ?? Guid.NewGuid(),
                    Label = Label.Trim(),
                    FirstName = FirstName.Trim(),
                    LastName = LastName.Trim(),
                    Email = Email.Trim(),
                    Phone = Phone.Trim(),
                    Company = Company.Trim(),
                    AddressLine1 = AddressLine1.Trim(),
                    AddressLine2 = AddressLine2.Trim(),
                    City = City.Trim(),
                    State = State.Trim(),
                    Country = Country.Trim(),
                    PostalCode = PostalCode.Trim(),
                };

                await _dataStorageService.SaveIdentityItemAsync(identity);
                _dialogService.ShowToast(IsEditMode ? "Identity updated" : "Identity saved");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save identity");
                _dialogService.ShowToast("Failed to save identity");
            }
            finally { IsSaving = false; }
        }

        private async Task DeleteAsync()
        {
            if (_editingId == null) return;

            var result = await _bottomSheetService.ConfirmAsync(
              "Delete Identity",
           $"Delete {Label}?",
              "Yes", "No");
            if (!result) return;



            try
            {
                await _dataStorageService.DeleteIdentityItemAsync(_editingId.Value);
                _dialogService.ShowToast("Identity deleted");
                _eventAggregator.GetEvent<RefreshProfileEvent>().Publish(null);
                await NavigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete identity");
                _dialogService.ShowToast("Failed to delete identity");
            }
        }

        // ── Validation ────────────────────────────────────────────────────────
        private void ClearErrors() => LabelError = FirstNameError = string.Empty;

        public void Validate()
        {
            LabelError = string.IsNullOrWhiteSpace(Label) ? "Identity label is required" : string.Empty;
            FirstNameError = string.IsNullOrWhiteSpace(FirstName) ? "First name is required" : string.Empty;
            CanSave = string.IsNullOrEmpty(LabelError) && string.IsNullOrEmpty(FirstNameError);
        }

        // ── Commands ──────────────────────────────────────────────────────────
        private DelegateCommand _saveCommand;
        public DelegateCommand SaveCommand =>
         _saveCommand ??= new DelegateCommand(async () => await SaveAsync())
       .ObservesCanExecute(() => CanSave);

        private DelegateCommand _deleteCommand;
        public DelegateCommand DeleteCommand =>
    _deleteCommand ??= new DelegateCommand(async () => await DeleteAsync());

        private DelegateCommand _goBackCommand;
        public DelegateCommand GoBackCommand =>
             _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());
    }
}
