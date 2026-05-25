using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;

namespace Fortress.ViewModels;

public class ActivityLogPageViewModel : ViewModelBase
{
    private readonly IEventLogProcessor _eventLogProcessor;
    private readonly IUserDialogs _dialogService;
    private readonly IBottomSheetService _bottomSheetService;

    // ── Filter state ──────────────────────────────────────────────────────────
    private DateTime _startDate = DateTime.Now.AddMonths(-1).ToUniversalTime();
    private DateTime _endDate = DateTime.Now.ToUniversalTime().Date.Add(new TimeSpan(23, 59, 59));
    private List<int>? _filteredTypes;

    // ── Full unfiltered list for client-side search ───────────────────────────
    private List<AuditLog> _allLogs = [];

    #region Properties

    private ObservableCollection<AuditLog> _logs = [];
    public ObservableCollection<AuditLog> Logs
    {
        get => _logs;
        set => SetProperty(ref _logs, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            SetProperty(ref _isLoading, value);
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(IsNotEmpty));
        }
    }

    public bool IsEmpty => !IsLoading && Logs.Count == 0;
    public bool IsNotEmpty => !IsLoading && Logs.Count > 0;

    // ── Search ────────────────────────────────────────────────────────────────

    private bool _isSearchVisible;
    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
    }

    // Plain pass-through — search triggered via SearchCommand from VaultPageHero
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplySearch();
        }
    }

    private int _matchCount;
    public int MatchCount
    {
        get => _matchCount;
        set => SetProperty(ref _matchCount, value);
    }

    // ── Display helpers ───────────────────────────────────────────────────────

    public string SubtitleText => "Vault activity history";
    public string LogCountText => $"{_allLogs.Count} event{(_allLogs.Count == 1 ? "" : "s")}";
    public string DateRangeText => $"{_startDate.ToLocalTime():d} – {_endDate.ToLocalTime():d}";
    public string FilterLabel => _filteredTypes == null ? "Filter" : "Filtered";

    #endregion

    #region Commands

    private AsyncCommand? _refreshCommand;
    public ICommand RefreshCommand =>
        _refreshCommand ??= new AsyncCommand(LoadLogsAsync);

    private AsyncCommand? _clearLogsCommand;
    public ICommand ClearLogsCommand =>
        _clearLogsCommand ??= new AsyncCommand(ClearLogsAsync);

    private AsyncCommand? _filterCommand;
    public ICommand FilterCommand =>
     _filterCommand ??= new AsyncCommand(ShowFilterAsync);

    private DelegateCommand? _toggleSearchCommand;
    public DelegateCommand ToggleSearchCommand =>
        _toggleSearchCommand ??= new DelegateCommand(() =>
     {
     IsSearchVisible = !IsSearchVisible;
   if (!IsSearchVisible) SearchText = string.Empty;
      });

    private DelegateCommand? _clearSearchCommand;
    public DelegateCommand ClearSearchCommand =>
        _clearSearchCommand ??= new DelegateCommand(() => SearchText = string.Empty);

    private DelegateCommand? _goBackCommand;
    public DelegateCommand GoBackCommand =>
        _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());

    #endregion

    public ActivityLogPageViewModel(
        INavigationService navigationService,
        IEventLogProcessor eventLogProcessor,
        IUserDialogs dialogService,
        IBottomSheetService bottomSheetService)
        : base(navigationService)
    {
        _eventLogProcessor = eventLogProcessor;
        _dialogService = dialogService;
        _bottomSheetService = bottomSheetService;
    }

    public override async void OnNavigatedTo(INavigationParameters parameters)
    {
        base.OnNavigatedTo(parameters);
        await LoadLogsAsync();
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    private async Task LoadLogsAsync()
    {
        IsLoading = true;
        try
        {
            var results = await _eventLogProcessor.GetLocalLogsAsync(
                _filteredTypes, _startDate, _endDate, recordCount: 200);

            _allLogs = results.ToList();
      ApplySearch();
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast($"Failed to load activity log: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RaisePropertyChanged(nameof(LogCountText));
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(IsNotEmpty));
        }
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    private async Task ClearLogsAsync()
    {
        var confirmed = await _bottomSheetService.ConfirmAsync(
            "Clear Activity Log",
            "All recorded vault activity will be permanently deleted. This cannot be undone.",
            "Clear", "Cancel");

        if (!confirmed) return;

        await _eventLogProcessor.ClearLocalLogsAsync();
        Logs.Clear();
        RaisePropertyChanged(nameof(IsEmpty));
        RaisePropertyChanged(nameof(IsNotEmpty));
        RaisePropertyChanged(nameof(LogCountText));
        _dialogService.ShowToast("Activity log cleared");
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private async Task ShowFilterAsync()
    {
        // Build a simple pick-list of all EventLogType values
        var allTypes = Enum.GetValues<EventLogType>().ToList();
        var options = new List<BottomSheetOption>();

        // "All events" reset option
        options.Add(new BottomSheetOption
        {
            Title = "All Events",
            IconGlyph = "\ue868",
            IconBadgeColor = Color.FromArgb("#E0E7FF"),
            IconColor = Color.FromArgb("#4F46E5"),
            IsSelected = _filteredTypes == null,
            Action = async () =>
            {
                _filteredTypes = null;
                RaisePropertyChanged(nameof(FilterLabel));
                await LoadLogsAsync();
            }
        });

        // Group categories
        void AddGroup(string title, int from, int to, string badge, string icon)
        {
            var keys = allTypes
               .Where(t => (int)t >= from && (int)t <= to)
            .Select(t => (int)t)
                .ToList();

            options.Add(new BottomSheetOption
            {
                Title = title,
                IconGlyph = "\ue897",
                IconBadgeColor = Color.FromArgb(badge),
                IconColor = Color.FromArgb(icon),
                IsSelected = _filteredTypes?.Any(k => keys.Contains(k)) ?? false,
                Action = async () =>
                 {
                     _filteredTypes = keys;
                     RaisePropertyChanged(nameof(FilterLabel));
                     await LoadLogsAsync();
                 }
            });
        }

        AddGroup("Vault Lock / Unlock", 1, 3, "#DCFCE7", "#16A34A");
        AddGroup("Credential Changes", 10, 13, "#E0E7FF", "#4F46E5");
        AddGroup("Autofill Activity", 20, 24, "#FEF3C7", "#D97706");
        AddGroup("Passkeys", 30, 32, "#EDE9FE", "#7C3AED");
        AddGroup("Cloud Sync", 40, 41, "#CCFBF1", "#0D9488");
        AddGroup("Security Settings", 50, 54, "#FEE2E2", "#EF4444");
        AddGroup("Vault Data", 60, 62, "#DBEAFE", "#3B82F6");

        await _bottomSheetService.ShowAsync<
  Fortress.Views.PopupPages.BottomSheet,
            Fortress.ViewModels.PopupPagesViewModels.BottomSheetViewModel,
         bool>(options, "Filter by Category");
    }

    private void ApplySearch()
    {
        var term = _searchText?.Trim() ?? string.Empty;

        IEnumerable<AuditLog> visible = _allLogs;
        if (!string.IsNullOrEmpty(term))
            visible = _allLogs.Where(l =>
      (l.EventType?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
           (l.CredentialLabel?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
   (l.Detail?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
       (l.DateTime?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));

        var list = visible.ToList();
        Logs = new ObservableCollection<AuditLog>(list);
      MatchCount = list.Count;
 RaisePropertyChanged(nameof(IsEmpty));
        RaisePropertyChanged(nameof(IsNotEmpty));
        RaisePropertyChanged(nameof(LogCountText));
    }
}
