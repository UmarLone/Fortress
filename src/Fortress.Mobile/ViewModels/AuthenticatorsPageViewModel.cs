using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Core.Utilities;
using Fortress.Helpers;
using Fortress.Services;
using Fortress.Extensions;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views;
using Fortress.Views.PopupPages;
using MauiIcons.Core;
using MauiIcons.Material;
using System.Timers;
using System.Windows.Input;
using NavigationMode = Prism.Navigation.NavigationMode;
using Timer = System.Timers.Timer;

namespace Fortress.ViewModels
{
    public class AuthenticatorsPageViewModel : ViewModelBase
    {
        #region Properties

        private Authenticator _selectedAuthenticator;
        public Authenticator SelectedAuthenticator
        {
            get => _selectedAuthenticator;
            set => SetProperty(ref _selectedAuthenticator, value);
        }

        private ObservableCollection<Authenticator> authenticators = new();
        public ObservableCollection<Authenticator> Authenticators
        {
            get => authenticators;
            set => SetProperty(ref authenticators, value);
        }

        private ObservableCollection<Authenticator> authenticatorsView = new();
        public ObservableCollection<Authenticator> AuthenticatorsView
        {
            get => authenticatorsView;
            set
            {
                if (SetProperty(ref authenticatorsView, value))
                {
                    if (authenticatorsView != null)
                        authenticatorsView.CollectionChanged -= OnAuthenticatorsViewCollectionChanged;
                    if (value != null)
                        value.CollectionChanged += OnAuthenticatorsViewCollectionChanged;
                    RaisePropertyChanged(nameof(AuthenticatorsCountText));
                }
            }
        }

        private void OnAuthenticatorsViewCollectionChanged(object? sender,
    System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => RaisePropertyChanged(nameof(AuthenticatorsCountText));

        /// <summary>Formatted count for the hero header — binds directly instead of .Count on Android.</summary>
        public string AuthenticatorsCountText => $"{AuthenticatorsView?.Count ?? 0} account(s)";

        private bool isRefreshing;
        public bool IsRefreshing
        {
            get => isRefreshing;
            set
            {
                if (SetProperty(ref isRefreshing, value) && !value)
                    RaisePropertyChanged(nameof(AuthenticatorsCountText));
            }
        }

        private bool _noData;
        public bool NoData
        {
            get => _noData;
            set => SetProperty(ref _noData, value);
        }

        // Plain pass-through — debounce is handled entirely by VaultPageHero via SearchCommand
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
        public bool IsAllSelected => AuthenticatorsView.Count > 0 && _selectedCount == AuthenticatorsView.Count;
        public string SelectAllButtonText => IsAllSelected ? "Deselect All" : "Select All";

        private Timer _authenticatortimer;

        #endregion

        private readonly IUserDialogs _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeviceServices _deviceService;
        private readonly IDataStorageService _dataStorageService;
        private readonly ICryptographyService _cryptographyService;
        private readonly IBottomSheetService _bottomSheetService;

        // Dedicated lock — never changes reference, unlike the collection itself
        private readonly object _tickLock = new();

        public AuthenticatorsPageViewModel(
    INavigationService navigationService,
            IUserDialogs dialogService,
            IEventAggregator eventAggregator,
   IDataStorageService dataStorageService,
IDeviceServices deviceService,
  ICryptographyService cryptographyService,
            IBottomSheetService bottomSheetService) : base(navigationService)
        {
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _dataStorageService = dataStorageService;
            _deviceService = deviceService;
            _cryptographyService = cryptographyService;
            _bottomSheetService = bottomSheetService;
            _eventAggregator.GetEvent<AuthenticatorDeletedEvent>().Subscribe(async _ => await RefreshAuthenticators());
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            if (parameters.GetNavigationMode() == NavigationMode.Back)
            {
                StopAuthenticators();
                _eventAggregator.GetEvent<AuthenticatorDeletedEvent>().Unsubscribe(async _ => await RefreshAuthenticators());
            }
            base.OnNavigatedFrom(parameters);
        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            await RefreshAuthenticators();
        }

        #region TOTP timer

        private void StartAuthenticators()
        {
            _authenticatortimer = new Timer { Interval = 1000, Enabled = true };
            _authenticatortimer.Elapsed += RunAuthenticators;
            _authenticatortimer.Start();
        }

        private void StopAuthenticators()
        {
            if (_authenticatortimer == null) return;
            _authenticatortimer.Stop();
            _authenticatortimer.Elapsed -= RunAuthenticators;
            _authenticatortimer.Dispose();
            _authenticatortimer = null;
        }

        private void RunAuthenticators(object sender, ElapsedEventArgs e) => Tick();

        public void Tick()
        {
            lock (_tickLock)
            {
                var snapshot = AuthenticatorsView?.ToList();
                if (snapshot == null) return;

                var updates = snapshot.Select(a =>
                      {
                          try
                          {
                              var totp = OtpHelper.GenerateOtp(a.Secret);
                              return (a, totp.RemainingSeconds, a.Period, totp.Code);
                          }
                          catch { return (a, 0, a.Period, string.Empty); }
                      }).ToList();

                MainThread.BeginInvokeOnMainThread(() =>
             {
                 foreach (var (authenticator, progress, duration, code) in updates)
                 {
                     authenticator.Progress = progress;
                     authenticator.Duration = duration;
                     authenticator.Code = code;
                 }
             });
            }
        }

        #endregion

        #region Data loading

        private async Task RefreshAuthenticators()
        {
            await Application.Current.Dispatcher.DispatchAsync(() => IsRefreshing = true);

            var result = await _dataStorageService.GetAuthenticatorsAsync();

            var tasks = result.Select(async x =>
  {
      var secretResult = await _cryptographyService.Decrypt(x.Secret);
      if (secretResult.Succeeded) x.Secret = secretResult.Data;
      return x;
  });
            result = (await Task.WhenAll(tasks)).ToList();

            // Pause the timer while we mutate both collections
            StopAuthenticators();

            lock (_tickLock)
            {
                // Rebuild Authenticators (master list) in-place
                var toRemove = Authenticators.Except(result, AuthenticatorIdComparer.Instance).ToList();
                var toAdd = result.Except(Authenticators, AuthenticatorIdComparer.Instance).ToList();
                foreach (var item in toRemove) Authenticators.Remove(item);
                foreach (var item in toAdd) Authenticators.Add(item);

                // Update mutable fields on items that stayed
                foreach (var updated in result)
                {
                    var existing = Authenticators.FirstOrDefault(a => a.Id == updated.Id);
                    if (existing != null)
                    {
                        existing.Secret = updated.Secret;
                        existing.Code = updated.Code;
                        existing.Progress = updated.Progress;
                    }
                }

                // Safe in-place sync of AuthenticatorsView — never replace the instance.
                // Replacing the collection while Syncfusion SfCircularProgressBar cells
                // are still attached crashes with ObjectDisposedException (LayoutViewGroupExt).
                UpdateViewInPlace(Authenticators);
            }

            if (Authenticators.Any())
                StartAuthenticators();

            await Application.Current.Dispatcher.DispatchAsync(() => IsRefreshing = false);
            NoData = AuthenticatorsView.Count == 0 && !IsRefreshing;
        }

        // Simple Id-based equality comparer so Except/Intersect work on Authenticator
        private sealed class AuthenticatorIdComparer : IEqualityComparer<Authenticator>
        {
            public static readonly AuthenticatorIdComparer Instance = new();
            private AuthenticatorIdComparer() { }
            public bool Equals(Authenticator x, Authenticator y) => x?.Id == y?.Id;
            public int GetHashCode(Authenticator obj) => obj.Id.GetHashCode();
        }

        #endregion

        #region Search / filter

        private async Task ExecuteApplySearchCommand(string text)
        {
            // Build filtered list off the UI thread
            var filtered = await Task.Run(() =>
              {
                  lock (_tickLock)
                  {
                      return Authenticators
             .Where(x =>
            string.IsNullOrWhiteSpace(text) ||
             (!string.IsNullOrWhiteSpace(x.Username) && x.Username.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrWhiteSpace(x.Issuer) && x.Issuer.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
               .ToList();
                  }
              });

            // Apply to view on the UI thread using safe in-place update
            UpdateViewInPlace(filtered);
            NoData = AuthenticatorsView.Count == 0 && !IsRefreshing;
        }

        /// <summary>
        /// Reconciles <see cref="AuthenticatorsView"/> with <paramref name="desired"/> in-place.
        /// Never replaces the collection instance — swapping to a new ObservableCollection
        /// while Syncfusion SfCircularProgressBar cells are mid-layout causes
        /// ObjectDisposedException on the native LayoutViewGroupExt.
        /// </summary>
        private void UpdateViewInPlace(IList<Authenticator> desired)
        {
            var desiredSet = new HashSet<Authenticator>(desired, AuthenticatorIdComparer.Instance);

            for (int i = AuthenticatorsView.Count - 1; i >= 0; i--)
            {
                if (!desiredSet.Contains(AuthenticatorsView[i]))
                    AuthenticatorsView.RemoveAt(i);
            }

            var existingSet = new HashSet<Authenticator>(AuthenticatorsView, AuthenticatorIdComparer.Instance);
            foreach (var item in desired)
            {
                if (!existingSet.Contains(item))
                {
                    AuthenticatorsView.Add(item);
                    existingSet.Add(item);
                }
            }
        }

        #endregion

        #region Actions

        private async Task ExecuteRefreshCommand()
        {
            await RefreshAuthenticators();
        }

        private async Task ExecuteShowOptionsCommand(object authenticator)
        {
            SelectedAuthenticator = authenticator as Authenticator;
            if (SelectedAuthenticator == null) return;

            var options = new List<BottomSheetOption>
            {
 new() { Title = "Copy OTP Code", Icon = new MauiIcon().Icon(MaterialIcons.CopyAll),   Action = async () => await CopyOtpCode() },
      new() { Title = "View",        Icon = new MauiIcon().Icon(MaterialIcons.Visibility), Action = async () => await ViewAuthenticator() },
  new() { Title = "Edit",          Icon = new MauiIcon().Icon(MaterialIcons.Edit),     Action = async () => await EditAuthenticator() },
              new() { Title = "View QR Code",  Icon = new MauiIcon().Icon(MaterialIcons.QrCode),     Action = async () => await ShowQrCode() },
     new() { Title = "Share",        Icon = new MauiIcon().Icon(MaterialIcons.Share),     Action = async () => await ShareAuthenticator() },
        new() { Title = "Delete",        Icon = new MauiIcon().Icon(MaterialIcons.Delete),     Action = async () => await DeleteAuthenticator() },
     };

            await _bottomSheetService.ShowAsync<BottomSheet, BottomSheetViewModel, bool>(
     options, SelectedAuthenticator.Issuer ?? "Actions");
        }

        private async Task ViewAuthenticator()
        {
            if (SelectedAuthenticator == null) return;
            await NavigationService.NavigateAsync(
        nameof(AuthenticatorDetailPage),
         new NavigationParameters { { "authenticator", SelectedAuthenticator } });
        }

        private async Task EditAuthenticator()
        {
            if (SelectedAuthenticator == null) return;
            await NavigationService.NavigateAsync(nameof(AddEditAuthenticatorPage),
           new NavigationParameters { { "authenticator", SelectedAuthenticator } });
        }

        private async Task ShowQrCode()
        {
            await NavigationService.NavigateAsync(nameof(QrCodePage),
                         new NavigationParameters { { "Id", SelectedAuthenticator.Id }, { "Title", SelectedAuthenticator.Issuer } });
        }

        private async Task CopyOtpCode()
        {
            if (SelectedAuthenticator != null && !string.IsNullOrEmpty(SelectedAuthenticator.Secret))
                await _deviceService.CopyToClipboard(
                        OtpHelper.GenerateOtp(SelectedAuthenticator.Secret).Code,
                 "OTP copied to clipboard",
                  PreferenceWrapper.Instance.ClearClipboardTimeout);
        }

        private async Task DeleteAuthenticator()
        {
            var result = await _bottomSheetService.ConfirmAsync(
                      "Delete Authenticator",
               $"Delete {SelectedAuthenticator.Issuer}?",
                      "Yes", "No");
            if (!result) return;

            using var dlg = _dialogService.Loading("Deleting Authenticator...");
            await _dataStorageService.DeleteAuthenticatorAsync(SelectedAuthenticator.Id);
            lock (_tickLock)
            {
                AuthenticatorsView.Remove(SelectedAuthenticator);
                Authenticators.Remove(SelectedAuthenticator);
            }
            _eventAggregator.GetEvent<AuthenticatorDeletedEvent>().Publish(SelectedAuthenticator);
            _dialogService.ShowToast("Authenticator deleted successfully");
        }

        private async Task ShareAuthenticator()
        {
            if (SelectedAuthenticator == null) return;
            await NavigationService.NavigateAsync(
                        nameof(ShareItemPage),
            new NavigationParameters { { "authenticator", SelectedAuthenticator } });
        }

        private async Task ExecuteScanCommand()
        {
            await NavigationService.NavigateAsync(nameof(AddEditAuthenticatorPage));
        }

        #endregion

        #region Commands

        private AsyncCommand<string>? _applySearchCommand;
        public ICommand ApplySearchCommand => _applySearchCommand ??= new AsyncCommand<string>(ExecuteApplySearchCommand);

        private AsyncCommand<object> _showOptionsCommand;
        public ICommand ShowOptionsCommand => _showOptionsCommand ??= new AsyncCommand<object>(ExecuteShowOptionsCommand);

        private AsyncCommand _refreshCommand;
        public ICommand RefreshCommand => _refreshCommand ??= new AsyncCommand(ExecuteRefreshCommand);

        private AsyncCommand _scanCommand;
        public ICommand ScanCommand => _scanCommand ??= new AsyncCommand(ExecuteScanCommand);

        private AsyncCommand _goBackCommand;
        public ICommand GoBackCommand => _goBackCommand ??= new AsyncCommand(async () => await NavigationService.GoBackAsync());

        // ── Multi-select commands ─────────────────────────────────────────────

        private DelegateCommand _enterSelectionModeCommand;
        public ICommand EnterSelectionModeCommand =>
   _enterSelectionModeCommand ??= new DelegateCommand(() => { IsSelectionMode = true; SelectedCount = 0; });

        private DelegateCommand _exitSelectionModeCommand;
        public ICommand ExitSelectionModeCommand =>
            _exitSelectionModeCommand ??= new DelegateCommand(() =>
     {
         foreach (var item in AuthenticatorsView) item.IsSelected = false;
         IsSelectionMode = false;
         SelectedCount = 0;
     });

        private DelegateCommand<Authenticator> _toggleItemSelectionCommand;
        public ICommand ToggleItemSelectionCommand =>
                  _toggleItemSelectionCommand ??= new DelegateCommand<Authenticator>(item =>
             {
                 if (item is null) return;
                 item.IsSelected = !item.IsSelected;
                 SelectedCount = AuthenticatorsView.Count(i => i.IsSelected);
             });

        private DelegateCommand<Authenticator> _longPressItemCommand;
        public ICommand LongPressItemCommand =>
            _longPressItemCommand ??= new DelegateCommand<Authenticator>(item =>
            {
                if (item is null) return;
                if (!IsSelectionMode) { IsSelectionMode = true; SelectedCount = 0; }
                item.IsSelected = true;
                SelectedCount = AuthenticatorsView.Count(i => i.IsSelected);
            });

        private DelegateCommand _selectAllCommand;
        public ICommand SelectAllCommand =>
  _selectAllCommand ??= new DelegateCommand(() =>
 {
     var newState = !IsAllSelected;
     foreach (var item in AuthenticatorsView) item.IsSelected = newState;
     SelectedCount = newState ? AuthenticatorsView.Count : 0;
 });

        private AsyncCommand _deleteSelectedCommand;
        public ICommand DeleteSelectedCommand =>
              _deleteSelectedCommand ??= new AsyncCommand(DeleteSelectedAsync);

        private async Task DeleteSelectedAsync()
        {
            var selected = AuthenticatorsView.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0) return;
            var confirmed = await _bottomSheetService.DestructiveConfirmAsync(
       "Delete Selected",
  $"Delete {selected.Count} authenticator{(selected.Count > 1 ? "s" : "")}? This cannot be undone.",
       "Delete", "Cancel");
            if (!confirmed) return;
            try
            {
                foreach (var item in selected)
                    await _dataStorageService.DeleteAuthenticatorAsync(item.Id);
                foreach (var item in selected)
                {
                    AuthenticatorsView.Remove(item);
                    Authenticators.Remove(item);
                }
                _dialogService.ShowToast($"{selected.Count} authenticator{(selected.Count > 1 ? "s" : "")} deleted");
                IsSelectionMode = false;
                SelectedCount = 0;
                NoData = AuthenticatorsView.Count == 0;
            }
            catch (Exception ex)
            {
                _dialogService.ShowToast("Failed to delete some authenticators");
            }
        }

        #endregion
    }
}
