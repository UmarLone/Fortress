using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Extensions;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using MauiIcons.Core;
using MauiIcons.Material;

namespace Fortress.ViewModels;

public class LocalLogsPageViewModel : ViewModelBase
{
    private readonly ILogger<LocalLogsPageViewModel> _logger;
    private readonly IUserDialogs _dialogService;
    private readonly string _logFilePath;
    private readonly IBottomSheetService _bottomSheetService;

    // Raw lines after level-filter; search narrows further from this cache.
    private string[] _filteredLines = [];

    #region Properties

    private string _logContent = string.Empty;
 public string LogContent
    {
        get => _logContent;
        set => SetProperty(ref _logContent, value);
    }

    private string _logFileInfo = string.Empty;
    public string LogFileInfo
 {
        get => _logFileInfo;
        set => SetProperty(ref _logFileInfo, value);
 }

    private int _lineCount;
    public int LineCount
    {
    get => _lineCount;
        set => SetProperty(ref _lineCount, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
          SetProperty(ref _isLoading, value);
            RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => !IsLoading && string.IsNullOrWhiteSpace(LogContent);

    // ── Search ────────────────────────────────────────────────────────────

    private bool _isSearchVisible;
public bool IsSearchVisible
    {
      get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
    }

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

    public int MatchCount { get; private set; }

    // ── Filter ────────────────────────────────────────────────────────────

    private string _selectedLogLevel = "All";
    public string SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
          if (SetProperty(ref _selectedLogLevel, value))
  {
      RaisePropertyChanged(nameof(FilterLabel));
       _ = LoadLogsAsync();
            }
        }
    }

    public string FilterLabel => _selectedLogLevel == "All" ? "Filter" : _selectedLogLevel;

    public static readonly IReadOnlyList<string> LogLevels =
    ["All", "Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    #endregion

    #region Commands

    private AsyncCommand? _refreshCommand;
    public ICommand RefreshCommand =>
        _refreshCommand ??= new AsyncCommand(LoadLogsAsync);

    private AsyncCommand? _shareCommand;
  public ICommand ShareCommand =>
        _shareCommand ??= new AsyncCommand(ShareLogsAsync);

    private AsyncCommand? _clearLogsCommand;
    public ICommand ClearLogsCommand =>
    _clearLogsCommand ??= new AsyncCommand(ClearLogsAsync);

    private AsyncCommand? _showFilterCommand;
    public ICommand ShowFilterCommand =>
      _showFilterCommand ??= new AsyncCommand(ShowFilterSheetAsync);

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

  #endregion

    public LocalLogsPageViewModel(
        INavigationService navigationService,
    ILogger<LocalLogsPageViewModel> logger,
        IUserDialogs dialogService,
        IBottomSheetService bottomSheetService)
        : base(navigationService)
{
        _logger = logger;
        _dialogService = dialogService;
        _logFilePath = Path.Combine(FileSystem.Current.CacheDirectory, "fortress.log");
        _bottomSheetService = bottomSheetService;
    }

    public override async void OnNavigatedTo(INavigationParameters parameters)
    {
        base.OnNavigatedTo(parameters);
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        // Capture on the UI thread before entering Task.Run to avoid a race
        // where SelectedLogLevel still holds the previous value on the bg thread.
   var levelToFilter = SelectedLogLevel;

        try
        {
    IsLoading = true;
          LogContent = string.Empty;

            await Task.Run(async () =>
     {
   if (!File.Exists(_logFilePath))
    {
     await MainThread.InvokeOnMainThreadAsync(() =>
         {
       _filteredLines = [];
              LogContent = string.Empty;
    LogFileInfo = "Log file not found";
   LineCount = 0;
     });
return;
          }

       var fileInfo = new FileInfo(_logFilePath);
          var fileSizeKb = fileInfo.Length / 1024.0;
   var lastModified = fileInfo.LastWriteTime;

                string raw;
      using (var stream = new FileStream(
         _logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    using (var reader = new StreamReader(stream))
       {
  raw = await reader.ReadToEndAsync();
  }

                var allLines = raw.Split('\n');

         // Apply level filter
     string[] levelFiltered;
 if (levelToFilter == "All")
  {
  levelFiltered = allLines;
       }
       else
     {
               var patterns = GetLogLevelPatterns(levelToFilter);
     levelFiltered = allLines
.Where(line => patterns.Any(p =>
 !string.IsNullOrEmpty(p) &&
      line.Contains(p, StringComparison.OrdinalIgnoreCase)))
   .ToArray();
    }

     await MainThread.InvokeOnMainThreadAsync(() =>
    {
      _filteredLines = levelFiltered;
          LogFileInfo = $"Size: {fileSizeKb:F1} KB | Modified: {lastModified:g}";

   // Re-apply any live search on top of the new level-filtered set
         ApplySearch();
   });
       });
    }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load log file");
            _dialogService.ShowToast("Failed to load logs");
    }
        finally
        {
            IsLoading = false;
          RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// Narrows <see cref="_filteredLines"/> by the current <see cref="SearchText"/>
    /// and updates <see cref="LogContent"/> and <see cref="LineCount"/>.
    /// Must be called on the UI thread.
 /// </summary>
  private void ApplySearch()
    {
   var term = _searchText?.Trim() ?? string.Empty;

      string[] visible;
  if (string.IsNullOrEmpty(term))
        {
            visible = _filteredLines;
  }
  else
        {
            visible = _filteredLines
      .Where(l => l.Contains(term, StringComparison.OrdinalIgnoreCase))
    .ToArray();
        }

        MatchCount = visible.Length;
        RaisePropertyChanged(nameof(MatchCount));

     LogContent = string.Join('\n', visible);
        LineCount = visible.Length;
        RaisePropertyChanged(nameof(IsEmpty));
    }

  private async Task ShowFilterSheetAsync()
    {
        var options = LogLevels.Select(level => new BottomSheetOption
        {
            Title = level == SelectedLogLevel ? $"✓  {level}" : level,
        Icon = new MauiIcon().Icon(level switch
       {
 "Trace"       => MaterialIcons.BugReport,
    "Debug"       => MaterialIcons.Code,
         "Information" => MaterialIcons.Info,
    "Warning"     => MaterialIcons.Warning,
         "Error"       => MaterialIcons.Error,
                "Critical"    => MaterialIcons.GppBad,
           _ => MaterialIcons.FilterList,
            }),
            Action = () => SelectedLogLevel = level,
    }).ToList();

        await _bottomSheetService.ShowAsync<
   Views.PopupPages.BottomSheet,
     BottomSheetViewModel,
     object>(options, "Filter by Log Level");
    }

    private async Task ShareLogsAsync()
    {
        try
{
            if (!File.Exists(_logFilePath))
 {
     _dialogService.ShowToast("No log file to share");
 return;
      }

          await Share.Default.RequestAsync(new ShareFileRequest
        {
   Title = "Share Fortress Logs",
       File = new ShareFile(_logFilePath)
         });
     }
     catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to share log file");
            _dialogService.ShowToast("Failed to share logs");
        }
    }

    private async Task ClearLogsAsync()
    {
        var result = await _bottomSheetService.ConfirmAsync(
"Delete Logs",
            "Are you sure you want to delete all local logs?",
     "Yes", "No");
        if (!result) return;

try
        {
   if (File.Exists(_logFilePath))
    File.Delete(_logFilePath);

     var logDir = Path.GetDirectoryName(_logFilePath);
   if (logDir != null)
      {
    foreach (var rolledLog in Directory.GetFiles(logDir, "fortress.log.*"))
              File.Delete(rolledLog);
            }

    _filteredLines = [];
         SearchText = string.Empty;
            LogContent = string.Empty;
       LogFileInfo = "Logs cleared";
  LineCount = 0;
RaisePropertyChanged(nameof(IsEmpty));
            _dialogService.ShowToast("Logs cleared successfully");
        }
        catch (Exception ex)
        {
 _logger.LogError(ex, "Failed to clear logs");
         _dialogService.ShowToast("Failed to clear logs");
        }
    }

    /// <summary>
    /// NReco.Logging.File 1.3.x tab-separated format:
    ///   {datetime}\t{LEVEL}\t{category}\t{message}
    ///
    ///   Trace       → TRCE
    ///   Debug       → DBUG
    ///   Information → INFO
    ///   Warning     → WARN
    ///   Error       → FAIL  (NReco uses FAIL, not ERRO)
    ///   Critical    → CRIT
    ///
    /// Matching \tTOKEN\t avoids false positives inside message bodies.
    /// </summary>
    private static string[] GetLogLevelPatterns(string logLevel) => logLevel switch
    {
        "Trace"       => ["\tTRCE\t"],
   "Debug"       => ["\tDBUG\t"],
    "Information" => ["\tINFO\t"],
        "Warning" => ["\tWARN\t"],
        "Error"       => ["\tFAIL\t"],
        "Critical"    => ["\tCRIT\t"],
 _    => [$"\t{logLevel.ToUpperInvariant()}\t"],
    };
}
