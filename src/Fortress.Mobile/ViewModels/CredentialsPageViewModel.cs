using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Mappers;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Core.Utilities;
using Fortress.Extensions;
using Fortress.Helpers;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;
using MauiIcons.Core;
using MauiIcons.Material;
using System.Timers;
using System.Windows.Input;
using Timer = System.Timers.Timer;

namespace Fortress.ViewModels
{
    public class CredentialsPageViewModel : ViewModelBase
    {
        #region List Properties

        private bool _noData;
        public bool NoData { get => _noData; set => SetProperty(ref _noData, value); }

        private CredentialView _selectedCredential;
        public CredentialView SelectedCredential { get => _selectedCredential; set => SetProperty(ref _selectedCredential, value); }

        // ── Multi-select mode ─────────────────────────────────────────────────

        private bool _isSelectionMode;
        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            set
            {
                SetProperty(ref _isSelectionMode, value);
                RaisePropertyChanged(nameof(IsNotSelectionMode));
            }
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

        /// <summary>True when every visible item is selected.</summary>
        public bool IsAllSelected => CredentialsView.Count > 0
        && _selectedCount == CredentialsView.Count;

        /// <summary>Dynamic label for the Select All / Deselect All button.</summary>
        public string SelectAllButtonText => IsAllSelected ? "Deselect All" : "Select All";

        // ── Dynamic group chips ───────────────────────────────────────────────

        /// <summary>
        /// Filter chip view-models shown in the horizontal scroll on CredentialsPage.
        /// Index 0 is always "All". Remaining items are user-defined groups.
        /// </summary>
        private ObservableCollection<CredentialFilterChip> _filterChips = new();
        public ObservableCollection<CredentialFilterChip> FilterChips
        {
            get => _filterChips;
            set => SetProperty(ref _filterChips, value);
        }

        // ── Active chip / tab ────────────────────────────────────────────────
        private int _selectedTab;
        public int SelectedTab
        {
            get => _selectedTab;
            set
            {
                SetProperty(ref _selectedTab, value);
                // Sync IsActive on each chip
                for (int i = 0; i < FilterChips.Count; i++)
                    FilterChips[i].IsActive = i == value;
            }
        }

        // Legacy tab booleans — kept so any remaining XAML bindings don't break
        public bool IsTab0 => _selectedTab == 0;
        public bool IsTab1 => _selectedTab == 1;
        public bool IsTab2 => _selectedTab == 2;
        public bool IsTab3 => _selectedTab == 3;
        public bool IsTab4 => _selectedTab == 4;

        private ObservableCollection<CredentialView> credentials = new();
        public ObservableCollection<CredentialView> Credentials { get => credentials; set => SetProperty(ref credentials, value); }

        private ObservableCollection<CredentialView> credentialsView = new();
        public ObservableCollection<CredentialView> CredentialsView
        {
            get => credentialsView;
            set
            {
                if (SetProperty(ref credentialsView, value))
                {
                    // Unsubscribe from old collection
                    if (credentialsView != null)
                        credentialsView.CollectionChanged -= OnCredentialsViewCollectionChanged;
                    // Subscribe to new collection
                    if (value != null)
                        value.CollectionChanged += OnCredentialsViewCollectionChanged;
                    RaisePropertyChanged(nameof(CredentialsCountText));
                }
            }
        }

        private void OnCredentialsViewCollectionChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => RaisePropertyChanged(nameof(CredentialsCountText));

        /// <summary>
        /// Formatted count string for the hero header label.
        /// Bound directly instead of using StringFormat on .Count because
        /// ObservableCollection does not raise PropertyChanged for Count on Android.
        /// </summary>
        public string CredentialsCountText => $"{CredentialsView?.Count ?? 0} item(s)";

        private bool isRefreshing;
        public bool IsRefreshing
        {
            get => isRefreshing;
            set
            {
                if (SetProperty(ref isRefreshing, value) && !value)
                    RaisePropertyChanged(nameof(CredentialsCountText));
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        #endregion

        #region Form Properties

        // Form properties removed - form lives on AddEditCredentialPage.
        // IsFormVisible kept as a stub so any residual XAML bindings don't break at runtime.
        public bool IsFormVisible => false;

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        private string _formTitle = "Add Password";
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }

        private string _formCredentialType = "Web";
        public string FormCredentialType
        {
            get => _formCredentialType;
            set { SetProperty(ref _formCredentialType, value); RaisePropertyChanged(nameof(FormTypeIndex)); }
        }
        public bool FormTypeIndex => _formCredentialType == "Web";

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

        private bool _isSaving;
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        private Guid? _editingId;

        #endregion

        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataStorageService _dataStorageService;
        private readonly IDeviceServices _deviceInfo;
        private readonly ICryptographyService _cryptographyService;
        private readonly IEventLogProcessor _eventLogProcessor;
        private readonly ILogger<CredentialsPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;
        private readonly ISharedCredentialWriter? _sharedCredentialWriter;
        private readonly SemanticVaultSearch _semanticSearch = new();
        private List<CredentialView> _allCredentials = new();
        // raw LoginItems kept in parallel so SemanticVaultSearch can operate on them
        private List<LoginItem> _rawLoginItems = new();

        public CredentialsPageViewModel(
           INavigationService navigationService,
                   IUserDialogs dialogService,
                   IEventAggregator eventAggregator,
        IDataStorageService dataStorageService,
                   IDeviceServices deviceInfo,
         ICryptographyService cryptographyService,
                   IEventLogProcessor eventLogProcessor,
           ILogger<CredentialsPageViewModel> logger,
         IBottomSheetService bottomSheetService,
          ISharedCredentialWriter? sharedCredentialWriter = null)
              : base(navigationService)
        {
            _sharedCredentialWriter = sharedCredentialWriter;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _deviceInfo = deviceInfo;
            _dataStorageService = dataStorageService;
            _cryptographyService = cryptographyService;
            _eventLogProcessor = eventLogProcessor;
            _logger = logger;
            _bottomSheetService = bottomSheetService;

            _eventAggregator.GetEvent<RefreshProfileEvent>().Subscribe(async msg => await RefreshProfileAction(msg));
            _eventAggregator.GetEvent<ApplicationStateChangeEvent>().Subscribe(OnApplicationStateChanged);
        }

        private void OnApplicationStateChanged(ApplicationState state)
        {
            if (state == ApplicationState.Background) StopAuthenticators();
            else StartAuthenticators();
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            if (parameters.GetNavigationMode() == Prism.Navigation.NavigationMode.Back)
            {
                StopAuthenticators();
                _eventAggregator.GetEvent<ApplicationStateChangeEvent>().Unsubscribe(OnApplicationStateChanged);
            }
            base.OnNavigatedFrom(parameters);
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            await Application.Current.Dispatcher.DispatchAsync(() => IsRefreshing = true);

            if (parameters.ContainsKey("selectedTab"))
            {
                SelectedTab = parameters.GetValue<int>("selectedTab");
                if (SelectedTab > 4 || SelectedTab < 0) SelectedTab = 0;
            }

            // Always reload — covers both first navigation and return from AddEditCredentialPage
            await RefreshProfileAction(null);
            StartAuthenticators();

            await Application.Current.Dispatcher.DispatchAsync(() => IsRefreshing = false);
        }

        #region Data loading

        private async Task RefreshProfileAction(string message)
        {
            try
            {
                _allCredentials.Clear();
                Credentials?.Clear();
                // Do NOT Clear() CredentialsView here — Syncfusion SfCircularProgressBar
                // cells hold native Android views that get disposed if we reset the
                // collection while they are still attached to the layout tree.
                // The safe in-place helpers in ApplyChipFilter / ExecuteApplySearchCommand
                // will reconcile the visible list after the data is loaded.
                await LoadAllCredentials();
                await LoadFilterChipsAsync();

                // Re-apply the active chip (default to 0 = All)
                var activeChip = FilterChips.ElementAtOrDefault(SelectedTab);
                await ApplyChipFilter(activeChip ?? FilterChips.FirstOrDefault());

#if IOS
     if (_sharedCredentialWriter != null)
          {
      try
        {
  await _sharedCredentialWriter.SyncCredentialsToSharedStorageAsync();
      _sharedCredentialWriter.SyncLockStateToSharedPreferences();
    }
         catch (Exception ex) { _logger.LogError(ex, "Failed to sync credentials"); }
    }
#endif
            }
            catch (Exception ex) { _logger.LogError(ex, "Error refreshing credentials"); }
        }

        private async Task LoadAllCredentials()
        {
            try
            {
                var result = await _dataStorageService.GetLoginItemsAsync();
                var rawList = result.ToList();
                var mapped = LoginItemMapper.Map(rawList)
               .OrderBy(x => x.Domain).ThenBy(x => x.Username).ToList();

                // Decrypt all credentials in parallel — key is now cached so
                // each call is cheap (no PBKDF1 re-derivation).
                await Task.WhenAll(mapped.Select(DecryptCredentialDataAsync));

                _allCredentials = mapped;
                _rawLoginItems = rawList;
                // Invalidate semantic index whenever vault data is refreshed
                _semanticSearch.Invalidate();
                Credentials = new ObservableCollection<CredentialView>(_allCredentials);
            }
            catch (Exception ex) { _logger.LogError(ex, "Error loading credentials"); }
        }

        /// <summary>
        /// Builds FilterChips: index 0 = "All", then one per VaultGroup.
        /// Pre-loads MemberIds so ApplyChipFilter is synchronous.
        /// </summary>
        private async Task LoadFilterChipsAsync()
        {
            try
            {
                // Seed default groups on first launch so chips appear without needing to visit GroupsPage
                if (!Preferences.Default.Get(GroupsPageViewModel.SeedDonePrefKeyPublic, false))
                    await GroupsPageViewModel.SeedDefaultGroupsAsync(_dataStorageService);

                var groups = (await _dataStorageService.GetVaultGroupsAsync())
                 .OrderBy(g => g.Name).ToList();

                var chips = new List<CredentialFilterChip>
      {
             new() { Label = "All", GroupId = null, Color = null, IsActive = SelectedTab == 0 }
    };

                var groupChipTasks = groups.Select(async g =>
               {
                   var ids = await _dataStorageService.GetCredentialIdsInGroupAsync(g.Id);
                   return new CredentialFilterChip
                   {
                       Label = g.Name,
                       GroupId = g.Id,
                       Color = g.Color,
                       IsActive = false,
                       MemberIds = new HashSet<Guid>(ids),
                   };
               });
                chips.AddRange(await Task.WhenAll(groupChipTasks));

                int prevIdx = SelectedTab;
                FilterChips = new ObservableCollection<CredentialFilterChip>(chips);
                SelectedTab = Math.Clamp(prevIdx, 0, FilterChips.Count - 1);
            }
            catch (Exception ex) { _logger.LogError(ex, "LoadFilterChipsAsync failed"); }
        }

        /// <summary>Applies filtering for <paramref name="chip"/> and marks it active.</summary>
        private async Task ApplyChipFilter(CredentialFilterChip chip)
        {
            await Task.Run(() =>
            {
                if (chip == null) return;

                List<CredentialView> desired;
                if (chip.GroupId == null)
                {
                    // "All" chip — show every credential
                    desired = _allCredentials.ToList();
                }
                else
                {
                    var ids = chip.MemberIds;
                    desired = ids != null
                    ? _allCredentials.Where(c => ids.Contains(c.Id)).ToList()
                  : _allCredentials.ToList();
                }

                // Safe in-place update — never replace the CredentialsView instance.
                // Swapping to a new ObservableCollection while Syncfusion cells are
                // mid-layout causes ObjectDisposedException on LayoutViewGroupExt.
                var desiredSet = new HashSet<CredentialView>(desired, ReferenceEqualityComparer.Instance);
                for (int i = CredentialsView.Count - 1; i >= 0; i--)
                {
                    if (!desiredSet.Contains(CredentialsView[i]))
                        CredentialsView.RemoveAt(i);
                }
                var existingSet = new HashSet<CredentialView>(CredentialsView, ReferenceEqualityComparer.Instance);
                foreach (var item in desired)
                {
                    if (!existingSet.Contains(item))
                    {
                        CredentialsView.Add(item);
                        existingSet.Add(item);
                    }
                }

                NoData = CredentialsView.Count == 0 && !IsRefreshing;
            });
        }

        private async Task DecryptCredentialDataAsync(CredentialView credential)
        {
            try
            {
                if (string.IsNullOrEmpty(credential.Data)) return;
                var r = await _cryptographyService.Decrypt(credential.Data);
                if (r.Succeeded) credential.Data = r.Data;
            }
            catch (Exception ex) { _logger.LogError(ex.Message); }
        }

        #endregion

        #region Form open / close

        private async Task OpenAddForm()
        {
            await NavigationService.NavigateAsync(nameof(Views.AddEditCredentialPage));
        }

        private async Task OpenEditFormAsync(CredentialView credential)
        {
            var parameters = new NavigationParameters { { "credential", credential } };
            await NavigationService.NavigateAsync(nameof(Views.AddEditCredentialPage), parameters);
        }

        private void CloseForm()
        {
            StopAuthenticators();
            StartAuthenticators();
        }

        #endregion

        #region Save / Delete

        private async Task SaveCredentialAsync()
        {
            if (IsSaving) return;
            if (string.IsNullOrWhiteSpace(FormDomain))
            {
                _dialogService.ShowToast("Website / app name is required");
                return;
            }

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
                };

                if (!string.IsNullOrEmpty(item.Password))
                {
                    var (score, level) = new VaultHealthCalculator().ScorePassword(item.Password);
                    item.PasswordStrengthScore = score;
                    item.PasswordStrengthLevel = (int)level;
                    item.PasswordHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                 System.Text.Encoding.UTF8.GetBytes(item.Password))).ToLowerInvariant();
                    var enc = await _cryptographyService.Encrypt(item.Password);
                    if (enc.Succeeded) item.Password = enc.Data;
                }
                if (!string.IsNullOrEmpty(item.OtpSecret))
                {
                    var enc = await _cryptographyService.Encrypt(item.OtpSecret);
                    if (enc.Succeeded) item.OtpSecret = enc.Data;
                }

                await _dataStorageService.SaveLoginItemAsync(item);
                _dialogService.ShowToast(_editingId.HasValue ? "Password updated" : "Password added");
                CloseForm();
                await RefreshProfileAction(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save credential");
                _dialogService.ShowToast("Failed to save password");
            }
            finally { IsSaving = false; }
        }

        private async Task DeleteCurrentCredentialAsync()
        {
            if (_editingId == null) return;
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
              "Delete Password", $"Delete \"{FormDomain}\"?", "Delete", "Cancel");
            if (!confirmed) return;
            try
            {
                await _dataStorageService.DeleteLoginItemAsync(_editingId.Value);
                _dialogService.ShowToast("Password deleted");
                CloseForm();
                await RefreshProfileAction(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete credential");
                _dialogService.ShowToast("Failed to delete password");
            }
        }

        #endregion

        #region OTP scan

        private async Task ExecuteScanOtpAsync()
        {
            var uri = await _bottomSheetService.ShowAsync<ScanQrCodeSheet, ScanQrCodeSheetViewModel, string>();
            if (!string.IsNullOrEmpty(uri))
            {
                try
                {
                    var secret = AuthenticatorHelper.FromOtpAuthUri(uri).Secret;
                    FormOtpSecret = secret;
                    FormHasOtp = true;
                    _dialogService.ShowToast("OTP secret linked");
                }
                catch (Exception ex) { _dialogService.ShowToast($"Could not read QR code. {ex.Message}"); }
            }
        }

        #endregion

        #region Options sheet (long press / tap on item)

        private async Task ExecuteShowOptionsCommand(object obj)
      {
      SelectedCredential = obj as CredentialView;
  if (SelectedCredential == null) return;

     var options = CreateOptions(SelectedCredential.CredentialType);

            if (SelectedCredential.HasOtp)
     options.Add(new BottomSheetOption
          {
    Title = "Copy OTP Code",
   Icon = new MauiIcon().Icon(MaterialIcons.CopyAll),
     Action = () => CopyCredentialInfo("3")
         });

// ── View detail (read-only) ───────────────────────────────────────
       options.Add(new BottomSheetOption
      {
        Title = "View",
  Icon = new MauiIcon().Icon(MaterialIcons.Visibility),
  Action = async () =>
    {
        var p = new NavigationParameters { { "credential", SelectedCredential } };
         await NavigationService.NavigateAsync(nameof(Views.CredentialDetailPage), p);
         }
       });

     options.Add(new BottomSheetOption
            {
       Title = "Edit",
                Icon = new MauiIcon().Icon(MaterialIcons.Edit),
                Action = async () => await OpenEditFormAsync(SelectedCredential)
            });

        // ── Share (encrypted .fortress file) ──────────────────────────────
            options.Add(new BottomSheetOption
            {
    Title = "Share",
      Icon = new MauiIcon().Icon(MaterialIcons.Share),
             Action = async () =>
  {
                    var loginItem = LoginItemMapper.Map(SelectedCredential);
        await NavigationService.NavigateAsync(
    nameof(Views.ShareItemPage),
     new NavigationParameters { { "loginItem", loginItem } });
          }
            });

   options.Add(new BottomSheetOption
        {
      Title = "Delete",
         Icon = new MauiIcon().Icon(MaterialIcons.Delete),
     Action = async () => await DeleteSelectedCredentialAsync()
            });

            try
            {
                await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(
             options, $"{SelectedCredential.Domain}");
            }
            catch (Exception ex) { _logger.LogError(ex.Message); }
        }

        private async Task DeleteSelectedCredentialAsync()
        {
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
              "Delete Password",
$"Delete \"{SelectedCredential?.Domain}\"?",
  "Delete", "Cancel");
            if (!confirmed) return;
            try
            {
                await _dataStorageService.DeleteLoginItemAsync(SelectedCredential.Id);
                Credentials.Remove(SelectedCredential);
                CredentialsView.Remove(SelectedCredential);
                _dialogService.ShowToast("Password deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete failed");
                _dialogService.ShowToast("Failed to delete");
            }
        }

        private List<BottomSheetOption> CreateOptions(string type) => type switch
        {
            "Web" or "Otp" => new List<BottomSheetOption>
            {
             new() { Title = "Open in Browser", Icon = new MauiIcon().Icon(MaterialIcons.Link), Action = OpenBrowserOrApp },
  new() { Title = "Copy Username",   Icon = new MauiIcon().Icon(MaterialIcons.CopyAll), Action = () => CopyCredentialInfo("1") },
       new() { Title = "Copy Password",   Icon = new MauiIcon().Icon(MaterialIcons.CopyAll), Action = () => CopyCredentialInfo("2") },
       },
            "PhoneApplication" => new List<BottomSheetOption>
          {
                new() { Title = "Open App",      Icon = new MauiIcon().Icon(MaterialIcons.Link), Action = OpenBrowserOrApp },
   new() { Title = "Copy Username", Icon = new MauiIcon().Icon(MaterialIcons.CopyAll), Action = () => CopyCredentialInfo("1") },
      new() { Title = "Copy Password", Icon = new MauiIcon().Icon(MaterialIcons.CopyAll), Action = () => CopyCredentialInfo("2") },
     },
            _ => new List<BottomSheetOption>
            {
             new() { Title = "Copy Username", Icon = new MauiIcon().Icon(MaterialIcons.CopyAll), Action = () => CopyCredentialInfo("1") },
       new() { Title = "Copy Password", Icon = new MauiIcon().Icon(MaterialIcons.CopyAll), Action = () => CopyCredentialInfo("2") },
      }
        };

        private async void CopyCredentialInfo(string type)
        {
            if (type == "1" && !string.IsNullOrEmpty(SelectedCredential?.Username))
                await _deviceInfo.CopyToClipboard(SelectedCredential.Username, "Username copied", PreferenceWrapper.Instance.ClearClipboardTimeout);
            else if (type == "2")
            {
                var pwd = !string.IsNullOrEmpty(SelectedCredential?.Password)
                ? (await _cryptographyService.Decrypt(SelectedCredential.Password)).Data
                : SelectedCredential?.Password;
                if (!string.IsNullOrEmpty(pwd))
                    await _deviceInfo.CopyToClipboard(pwd, "Password copied", PreferenceWrapper.Instance.ClearClipboardTimeout);
            }
            else if (type == "3" && SelectedCredential?.HasOtp == true && !string.IsNullOrEmpty(SelectedCredential.Data))
            {
                var otp = OtpHelper.GenerateOtp(SelectedCredential.Data).Code;
                if (!string.IsNullOrEmpty(otp))
                    await _deviceInfo.CopyToClipboard(otp, "OTP copied", PreferenceWrapper.Instance.ClearClipboardTimeout);
            }
        }

        private async void OpenBrowserOrApp()
        {
            try
            {
                if (SelectedCredential?.CredentialType is "Web" or "Otp")
                    await Launcher.OpenAsync("https://" + SelectedCredential.Domain.TrimStart('/').Replace("https://", "").Replace("http://", ""));
                else if (SelectedCredential?.CredentialType == "PhoneApplication")
                    await _deviceInfo.LaunchApp(SelectedCredential.Domain);
            }
            catch (Exception ex) { _logger.LogError(ex.Message); }
        }

        #endregion

        #region Filtering / search

        private async Task ExecuteSelectTabCommand(string tabIndex)
        {
            if (int.TryParse(tabIndex, out int index))
            {
                SelectedTab = index;
                var chip = FilterChips.ElementAtOrDefault(index);
                await ApplyChipFilter(chip);
            }
        }

        private async Task ExecuteSelectChip(CredentialFilterChip chip)
        {
            if (chip == null) return;
            int idx = FilterChips.IndexOf(chip);
            SelectedTab = idx >= 0 ? idx : 0;
            await ApplyChipFilter(chip);
        }

        public async Task ExecuteApplyTypeFilterCommand(string type)
        {
            // Legacy path — maps old static type strings to All or exact match
            if (type == "AllPasswords" || string.IsNullOrEmpty(type))
            {
                await ApplyChipFilter(FilterChips.FirstOrDefault());
                return;
            }
            var chip = FilterChips.FirstOrDefault(c =>
         c.Label.Equals(type, StringComparison.OrdinalIgnoreCase));
            await ApplyChipFilter(chip ?? FilterChips.FirstOrDefault());
        }

        private async Task ExecuteApplySearchCommand(string text)
        {
            await Task.Run(() =>
            {

                if (Credentials == null) return;

                List<CredentialView> filtered;

                if (string.IsNullOrWhiteSpace(text))
                {
                    // Restore active chip filter
                    var chip = FilterChips.ElementAtOrDefault(SelectedTab);
                    filtered = chip?.GroupId == null
                            ? _allCredentials.ToList()
                      : _allCredentials.Where(c => chip.MemberIds?.Contains(c.Id) == true).ToList();
                }
                else
                {
                    // ── 1. Exact keyword search (fast, always runs first) ────────
                    var exact = _allCredentials.Where(x => IsTabMatch(x) &&
           ((!string.IsNullOrWhiteSpace(x.Domain) && x.Domain.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
           (!string.IsNullOrWhiteSpace(x.Username) && x.Username.Contains(text, StringComparison.OrdinalIgnoreCase))))
          .ToList();

                    if (exact.Count > 0)
                    {
                        filtered = exact;
                    }
                    else
                    {
                        // ── 2. Semantic fallback ──────────────────────────────────────
                        var semanticIds = _semanticSearch
                     .Search(_rawLoginItems, text, maxResults: 10, minScore: 0.05f)
                            .Select(r => r.LoginItem.Id)
                      .ToHashSet();

                        filtered = _allCredentials
                            .Where(x => IsTabMatch(x) && semanticIds.Contains(x.Id))
                                  .ToList();
                    }
                }

                // ── Safe in-place update — never replaces the collection instance ──
                // Replacing CredentialsView with a new ObservableCollection while
                // Syncfusion SfCircularProgressBar cells are mid-layout causes
                // ObjectDisposedException on the underlying LayoutViewGroupExt.
                // Instead: remove items no longer in the result (back-to-front to keep
                // indices valid), then append any new items at the end.
                var filteredSet = new HashSet<CredentialView>(filtered, ReferenceEqualityComparer.Instance);
                for (int i = CredentialsView.Count - 1; i >= 0; i--)
                {
                    if (!filteredSet.Contains(CredentialsView[i]))
                        CredentialsView.RemoveAt(i);
                }

                var existingSet = new HashSet<CredentialView>(CredentialsView, ReferenceEqualityComparer.Instance);
                foreach (var item in filtered)
                {
                    if (!existingSet.Contains(item))
                    {
                        CredentialsView.Add(item);
                        existingSet.Add(item);
                    }
                }

                NoData = CredentialsView.Count == 0 && !IsRefreshing;

            });
        }

        private bool IsTabMatch(CredentialView c) => SelectedTab switch
        {
            1 => c.CredentialType is "Web" or "Otp",
            2 => c.CredentialType == "PhoneApplication",
            3 => c.CredentialType == "Application",
            4 => c.CredentialType == "SecureNotes",
            _ => true
        };

        private static readonly Dictionary<string, Func<CredentialView, bool>> TypeFilters = new()
        {
     { "AllPasswords",      _ => true },
       { "Websites",          x => x.CredentialType is "Web" or "Otp" },
            { "PhoneApps",         x => x.CredentialType == "PhoneApplication" },
{ "DesktopApps",  x => x.CredentialType == "Application" },
            { "SecureNotes",       x => x.CredentialType == "SecureNotes" },
      { "CreditCards",       x => x.CredentialType == "CreditCard" },
            { "Addresses",   x => x.CredentialType == "Address" },
        };

        private void ExecuteRefreshCommand()
        {
            IsRefreshing = true;
            _ = RefreshProfileAction(null).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => IsRefreshing = false));
        }

        #endregion

        #region TOTP timer

        private Timer _authenticatortimer;

        private void StartAuthenticators()
        {

            _authenticatortimer = new Timer { Interval = 1000, Enabled = true };
            _authenticatortimer.Elapsed += RunAuthenticators;
            _authenticatortimer.Start();
        }

        private void RunAuthenticators(object sender, ElapsedEventArgs e) => Tick();

        private void Tick()
        {
            List<CredentialView> otpCredentials;
            lock (CredentialsView)
            {
                otpCredentials = CredentialsView.Where(x => x.HasOtp).ToList();
            }
            if (otpCredentials.Count == 0) return;

            var updates = otpCredentials.Select(c =>
                        {
                            try
                            {
                                var totp = OtpHelper.GenerateOtp(c.Data);
                                return (c, totp.RemainingSeconds, totp.Code);
                            }
                            catch { return (c, 0, string.Empty); }
                        }).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
               {
                   foreach (var (credential, progress, code) in updates)
                   {
                       credential.Progress = progress;
                       credential.Code = code;
                   }
               });
        }

        private void StopAuthenticators()
        {
            if (_authenticatortimer == null) return;
            _authenticatortimer.Stop();
            _authenticatortimer.Elapsed -= RunAuthenticators;
            _authenticatortimer.Dispose();
            _authenticatortimer = null;
        }

        #endregion

        #region Commands

        private AsyncCommand<string> _selectTabCommand;
        public ICommand SelectTabCommand => _selectTabCommand ??= new AsyncCommand<string>(ExecuteSelectTabCommand);

        private AsyncCommand<string>? _applySearchCommand;
        public ICommand ApplySearchCommand => _applySearchCommand ??= new AsyncCommand<string>(ExecuteApplySearchCommand);

        private AsyncCommand<string> _applyTypeFilterCommand;
        public ICommand ApplyTypeFilterCommand => _applyTypeFilterCommand ??= new AsyncCommand<string>(ExecuteApplyTypeFilterCommand);

        private AsyncCommand<object> _showOptionsCommand;
        public ICommand ShowOptionsCommand => _showOptionsCommand ??= new AsyncCommand<object>(ExecuteShowOptionsCommand);

        private AsyncCommand _addCredentialCommand;
        public ICommand AddCredentialCommand => _addCredentialCommand ??= new AsyncCommand(OpenAddForm);

        private DelegateCommand _closeFormCommand;
        public DelegateCommand CloseFormCommand => _closeFormCommand ??= new DelegateCommand(CloseForm);

        private AsyncCommand _saveCredentialCommand;
        public ICommand SaveCredentialCommand => _saveCredentialCommand ??= new AsyncCommand(SaveCredentialAsync);

        private AsyncCommand _deleteCredentialCommand;
        public ICommand DeleteCredentialCommand => _deleteCredentialCommand ??= new AsyncCommand(DeleteCurrentCredentialAsync);

        private AsyncCommand _scanOtpCommand;
        public ICommand ScanOtpCommand => _scanOtpCommand ??= new AsyncCommand(ExecuteScanOtpAsync);

        private DelegateCommand _togglePasswordVisibilityCommand;
        public DelegateCommand TogglePasswordVisibilityCommand =>
      _togglePasswordVisibilityCommand ??= new DelegateCommand(() => IsPasswordHidden = !IsPasswordHidden);

        private DelegateCommand<string> _setTypeCommand;
        public DelegateCommand<string> SetTypeCommand => _setTypeCommand ??= new DelegateCommand<string>(t => FormCredentialType = t);

        private DelegateCommand _refreshCommand;
        public DelegateCommand RefreshCommand => _refreshCommand ??= new DelegateCommand(ExecuteRefreshCommand);

        private AsyncCommand<CredentialFilterChip> _selectChipCommand;
        public ICommand SelectChipCommand => _selectChipCommand ??= new AsyncCommand<CredentialFilterChip>(ExecuteSelectChip);

        private AsyncCommand _goToGroupsCommand;
        public ICommand GoToGroupsCommand =>
      _goToGroupsCommand ??= new AsyncCommand(async () =>
    await NavigationService.NavigateAsync(nameof(Views.GroupsPage)));

        // ── Multi-select commands ─────────────────────────────────────────────

        private DelegateCommand _enterSelectionModeCommand;
        public ICommand EnterSelectionModeCommand =>
     _enterSelectionModeCommand ??= new DelegateCommand(() =>
        {
            IsSelectionMode = true;
            SelectedCount = 0;
        });

        private DelegateCommand _exitSelectionModeCommand;
        public ICommand ExitSelectionModeCommand =>
       _exitSelectionModeCommand ??= new DelegateCommand(() =>
           {
               foreach (var item in CredentialsView) item.IsSelected = false;
               IsSelectionMode = false;
               SelectedCount = 0;
           });

        private DelegateCommand<CredentialView> _toggleItemSelectionCommand;
        public ICommand ToggleItemSelectionCommand =>
             _toggleItemSelectionCommand ??= new DelegateCommand<CredentialView>(item =>
          {
              if (item is null) return;
              item.IsSelected = !item.IsSelected;
              SelectedCount = CredentialsView.Count(i => i.IsSelected);
          });

        private DelegateCommand<CredentialView> _longPressItemCommand;
        public ICommand LongPressItemCommand =>
      _longPressItemCommand ??= new DelegateCommand<CredentialView>(item =>
            {
                if (item is null) return;
                if (!IsSelectionMode)
                {
                    IsSelectionMode = true;
                    SelectedCount = 0;
                }
                item.IsSelected = true;
                SelectedCount = CredentialsView.Count(i => i.IsSelected);
            });


        private AsyncCommand _deleteSelectedCommand;
        public ICommand DeleteSelectedCommand =>
                _deleteSelectedCommand ??= new AsyncCommand(DeleteSelectedAsync);

        private async Task DeleteSelectedAsync()
        {
            var selected = CredentialsView.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;

            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
        "Delete Selected",
            $"Delete {selected.Count} password{(selected.Count > 1 ? "s" : "")}? This cannot be undone.",
          "Delete", "Cancel");
            if (!confirmed) return;

            try
            {
                var selectedIds = selected.Select(x => x.Id);
                await _dataStorageService.DeleteLoginItemsAsync(selectedIds);
                _allCredentials.RemoveAll(x => selectedIds.Contains(x.Id));
                Credentials.RemoveWhere(x => selectedIds.Contains(x.Id));
                _dialogService.ShowToast($"{selected.Count} password{(selected.Count > 1 ? "s" : "")} deleted");

                IsSelectionMode = false;
                SelectedCount = 0;
               // await RefreshProfileAction(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete selected credentials");
                _dialogService.ShowToast("Failed to delete some passwords");
            }
        }

        private DelegateCommand _selectAllCommand;
        public ICommand SelectAllCommand =>
             _selectAllCommand ??= new DelegateCommand(() =>
          {
              var newState = !IsAllSelected;
              foreach (var item in CredentialsView) item.IsSelected = newState;
              SelectedCount = newState ? CredentialsView.Count : 0;
          });

        #endregion
    }

    // ── Filter chip model ──────────────────────────────────────────────────────

    /// <summary>
    /// Represents one chip in the horizontal filter bar on CredentialsPage.
    /// index 0 = "All", the rest are user groups.
    /// </summary>
    public class CredentialFilterChip : Prism.Mvvm.BindableBase
    {
        public string Label { get; set; } = "All";
        public Guid? GroupId { get; set; }
        public string? Color { get; set; }

        /// <summary>Pre-loaded member credential IDs — null means show all.</summary>
        public HashSet<Guid>? MemberIds { get; set; }

        // ── Resource-value cache — resolved once per app session ──────────────
        private static string? _cachedPrimaryColor;
        private static string? _cachedCardBgAlt;
        private static string? _cachedTextSecondary;
        private static string? _cachedBorderColor;

        private static string PrimaryColor => _cachedPrimaryColor ??= GetResource("PrimaryColor", "#407cca");
        private static string CardBackgroundAlt => _cachedCardBgAlt ??= GetResource("CardBackgroundColorAlt", "#F1F5F9");
        private static string TextSecondary => _cachedTextSecondary ??= GetResource("TextSecondaryColor", "#475569");
        private static string BorderColorValue => _cachedBorderColor ??= GetResource("BorderColor", "#E2E8F0");

        /// <summary>Call when the app theme changes so chips pick up the new palette.</summary>
        public static void InvalidateResourceCache()
        {
            _cachedPrimaryColor = null;
            _cachedCardBgAlt = null;
            _cachedTextSecondary = null;
            _cachedBorderColor = null;
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                SetProperty(ref _isActive, value);
                RaisePropertyChanged(nameof(BackgroundColor));
                RaisePropertyChanged(nameof(TextColor));
                RaisePropertyChanged(nameof(BorderColor));
            }
        }

        public string BackgroundColor => IsActive ? (Color ?? PrimaryColor) : CardBackgroundAlt;
        public string TextColor => IsActive ? "#FFFFFF" : TextSecondary;
        public string BorderColor => IsActive ? (Color ?? PrimaryColor) : BorderColorValue;

        /// <summary>True when this chip represents a group with an assigned colour.</summary>
        public bool HasColor => GroupId != null && !string.IsNullOrEmpty(Color);

        /// <summary>
        /// The raw group colour used for the small dot indicator.
        /// Falls back to PrimaryColor when no custom colour is set.
        /// </summary>
        public string DotColor => Color ?? PrimaryColor;

        private static string GetResource(string key, string fallback)
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var val) == true && val != null)
            {
                if (val is Color c)
                {
                    return c.Alpha >= 0.999f
                   ? $"#{(int)(c.Red * 255):X2}{(int)(c.Green * 255):X2}{(int)(c.Blue * 255):X2}"
                      : $"#{(int)(c.Alpha * 255):X2}{(int)(c.Red * 255):X2}{(int)(c.Green * 255):X2}{(int)(c.Blue * 255):X2}";
                }
                return val.ToString()!;
            }
            return fallback;
        }
    }
}
