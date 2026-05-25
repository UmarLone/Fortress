using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Extensions;
using Microsoft.Extensions.Logging;
using Prism.Navigation;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fortress.ViewModels
{
    public class VaultHealthPageViewModel : ViewModelBase
    {
        private readonly IDataStorageService _dataStorageService;
        private readonly VaultHealthCalculator _calculator;
        private readonly IHaveIBeenPwnedService _hibp;
        private readonly ILogger<VaultHealthPageViewModel> _logger;

        // ── Loading state ────────────────────────────────────────────────────────
        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                SetProperty(ref _isLoading, value);
                RaisePropertyChanged(nameof(IsNotLoading));
                RaisePropertyChanged(nameof(HasNoIssues));
            }
        }

        public bool IsNotLoading => !_isLoading;

        // ── Score ─────────────────────────────────────────────────────────────────
        private int _score;
        public int Score
        {
            get => _score;
            set => SetProperty(ref _score, value);
        }

        private string _statusLabel = string.Empty;
        public string StatusLabel
        {
            get => _statusLabel;
            set => SetProperty(ref _statusLabel, value);
        }

        private string _statusColor = "#94A3B8";
        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        private string _calculatedAt = string.Empty;
        public string CalculatedAt
        {
            get => _calculatedAt;
            set => SetProperty(ref _calculatedAt, value);
        }

        // ── Headline counters ─────────────────────────────────────────────────────
        private int _totalCredentials;
        public int TotalCredentials
        {
            get => _totalCredentials;
            set => SetProperty(ref _totalCredentials, value);
        }

        private int _weakCount;
        public int WeakCount
        {
            get => _weakCount;
            set => SetProperty(ref _weakCount, value);
        }

        private int _reusedCount;
        public int ReusedCount
        {
            get => _reusedCount;
            set => SetProperty(ref _reusedCount, value);
        }

        private int _breachedCount;
        public int BreachedCount
        {
            get => _breachedCount;
            set => SetProperty(ref _breachedCount, value);
        }

        private int _missing2FACount;
        public int Missing2FACount
        {
            get => _missing2FACount;
            set => SetProperty(ref _missing2FACount, value);
        }

        private int _emptyCount;
        public int EmptyCount
        {
            get => _emptyCount;
            set => SetProperty(ref _emptyCount, value);
        }

        // ── Progress bar percentages (0.0–1.0) ───────────────────────────────────
        private double _weakPercent;
        public double WeakPercent
        {
            get => _weakPercent;
            set => SetProperty(ref _weakPercent, value);
        }

        private double _reusedPercent;
        public double ReusedPercent
        {
            get => _reusedPercent;
            set => SetProperty(ref _reusedPercent, value);
        }

        private double _breachedPercent;
        public double BreachedPercent
        {
            get => _breachedPercent;
            set => SetProperty(ref _breachedPercent, value);
        }

        private double _missing2FAPercent;
        public double Missing2FAPercent
        {
            get => _missing2FAPercent;
            set => SetProperty(ref _missing2FAPercent, value);
        }

        // ── Achievements ──────────────────────────────────────────────────────────
        private bool _allStrong;
        public bool AllStrong
        {
            get => _allStrong;
            set => SetProperty(ref _allStrong, value);
        }

        private bool _full2FA;
        public bool Full2FA
        {
            get => _full2FA;
            set => SetProperty(ref _full2FA, value);
        }

        private bool _noBreaches;
        public bool NoBreaches
        {
            get => _noBreaches;
            set => SetProperty(ref _noBreaches, value);
        }

        // ── Findings & details ────────────────────────────────────────────────────
        private ObservableCollection<VaultFinding> _findings = new();
        public ObservableCollection<VaultFinding> Findings
        {
            get => _findings;
            set => SetProperty(ref _findings, value);
        }

        private ObservableCollection<CredentialHealthDetail> _details = new();
        public ObservableCollection<CredentialHealthDetail> Details
        {
            get => _details;
            set
            {
                SetProperty(ref _details, value);
                RaisePropertyChanged(nameof(HasNoIssues));
            }
        }

        // ── Attack surface ────────────────────────────────────────────────────
        private int _attackSurfaceScore;
        public int AttackSurfaceScore
        {
            get => _attackSurfaceScore;
            set { SetProperty(ref _attackSurfaceScore, value); RaisePropertyChanged(nameof(AttackSurfaceLabel)); RaisePropertyChanged(nameof(AttackSurfaceColor)); }
        }
        public string AttackSurfaceLabel => AttackSurfaceScore switch
        {
            <= 20 => "Contained",
            <= 45 => "Moderate",
            <= 70 => "Exposed",
            _ => "Critical"
        };
        public string AttackSurfaceColor => AttackSurfaceScore switch
        {
            <= 20 => "#22C55E",
            <= 45 => "#F59E0B",
            <= 70 => "#EF4444",
            _ => "#7C3AED"
        };

        // ── Credential clusters (compromise chains) ───────────────────────────────────
        private ObservableCollection<CredentialCluster> _clusters = new();
        public ObservableCollection<CredentialCluster> Clusters
        {
            get => _clusters;
            set { SetProperty(ref _clusters, value); RaisePropertyChanged(nameof(HasClusters)); }
        }
        public bool HasClusters => _clusters.Count > 0;

        /// <summary>True when no credentials have issues – shows the "all clear" state.</summary>
        public bool HasNoIssues => _details.Count == 0 && !_isLoading;

        /// <summary>Total number of credentials with issues (may exceed Details.Count).</summary>
        private int _totalIssuesCount;
        public int TotalIssuesCount
        {
            get => _totalIssuesCount;
            set { SetProperty(ref _totalIssuesCount, value); RaisePropertyChanged(nameof(IssuesCapped)); RaisePropertyChanged(nameof(IssuesCappedLabel)); }
        }

        /// <summary>True when the displayed list was capped and there are more items.</summary>
        public bool IssuesCapped => _totalIssuesCount > _details.Count;

        /// <summary>e.g. "Showing 50 of 3,421 accounts with issues"</summary>
        public string IssuesCappedLabel =>
         $"Showing {_details.Count:N0} of {_totalIssuesCount:N0} accounts with issues";

        // ── Health trending ────────────────────────────────────────────────────
        private ObservableCollection<VaultHealthSnapshot> _healthHistory = new();
        public ObservableCollection<VaultHealthSnapshot> HealthHistory
        {
            get => _healthHistory;
            set { SetProperty(ref _healthHistory, value); RaisePropertyChanged(nameof(HasHealthHistory)); RaisePropertyChanged(nameof(TrendLabel)); }
        }
        public bool HasHealthHistory => _healthHistory.Count >= 2;

        /// <summary>e.g. "? 8 pts this week" or "? 3 pts this week".</summary>
        public string TrendLabel
        {
            get
            {
                if (_healthHistory.Count < 2) return string.Empty;
                var oldest = _healthHistory.First().Score;
                var latest = _healthHistory.Last().Score;
                var delta = latest - oldest;
                if (delta == 0) return "No change this month";
                var arrow = delta > 0 ? "?" : "?";
                return $"{arrow} {Math.Abs(delta)} pts vs {_healthHistory.Count} days ago";
            }
        }

        // ── HIBP email check ───────────────────────────────────────────────────
        private bool _isHibpChecking;
        public bool IsHibpChecking
        {
            get => _isHibpChecking;
            set => SetProperty(ref _isHibpChecking, value);
        }

        private int _hibpBreachedCount;
        public int HibpBreachedCount
        {
            get => _hibpBreachedCount;
            set { SetProperty(ref _hibpBreachedCount, value); RaisePropertyChanged(nameof(HasHibpBreaches)); }
        }
        public bool HasHibpBreaches => _hibpBreachedCount > 0;

        private ObservableCollection<HibpEmailResult> _hibpResults = new();
        public ObservableCollection<HibpEmailResult> HibpResults
        {
            get => _hibpResults;
            set => SetProperty(ref _hibpResults, value);
        }

        private int _hibpProgress;
        public int HibpProgress
        {
            get => _hibpProgress;
            set => SetProperty(ref _hibpProgress, value);
        }

        private int _hibpTotal;
        public int HibpTotal
        {
            get => _hibpTotal;
            set => SetProperty(ref _hibpTotal, value);
        }

        // ── Constructor ───────────────────────────────────────────────────────────
        public VaultHealthPageViewModel(
         INavigationService navigationService,
      IDataStorageService dataStorageService,
              VaultHealthCalculator calculator,
        IHaveIBeenPwnedService hibp,
                ILogger<VaultHealthPageViewModel> logger)
          : base(navigationService)
        {
            _dataStorageService = dataStorageService;
            _calculator = calculator;
            _hibp = hibp;
            _logger = logger;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            await LoadAsync();
        }

        // ── Load ─────────────────────────────────────────────────────────────────
        // Phase 1 (critical path, ~fast): fetch data in parallel + run calculator
        //    on a background thread ? paint score/cards immediately.
        // Phase 2 (non-blocking): persist snapshot + load history in background
        //      ? TrendLabel updates without blocking Phase 1.

        private async Task LoadAsync()
        {
            try
            {
                IsLoading = true;

                var (credentials, authenticators) = await FetchVaultDataAsync();

                var (result, issuesList, totalIssues, findingsList, clustersList) = await Task.Run(() =>
    {
        var r = _calculator.Calculate(credentials, authenticators);

        const int MaxDisplayed = 50;
        var allIssues = r.Details
         .Where(d => d.IsWeak || d.IsReused || d.IsOld || !d.HasTwoFactor)
       .OrderByDescending(d => d.IsWeak || d.IsReused)
          .ThenBy(d => d.Label)
        .ToList();

        var issues = allIssues.Take(MaxDisplayed).ToList();

        return (r, issues, allIssues.Count, r.Findings.ToList(), r.CredentialClusters.ToList());
    });

                ApplyResult(result, issuesList, totalIssues, findingsList, clustersList);
                IsLoading = false;

                _ = SaveSnapshotAndLoadHistoryAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VaultHealthPage: load failed");
                Score = 0;
                StatusLabel = "Unknown";
                StatusColor = "#94A3B8";
                CalculatedAt = "Could not load vault data";
                IsLoading = false;
            }
        }

        /// <summary>Fetches credentials and authenticators in parallel.</summary>
        private async Task<(List<LoginItem> credentials, List<Authenticator> authenticators)> FetchVaultDataAsync()
        {
            var credentialsTask = _dataStorageService.GetLoginItemsAsync();
            var authenticatorsTask = _dataStorageService.GetAuthenticatorsAsync();

            await Task.WhenAll(credentialsTask, authenticatorsTask);

            return (
  (await credentialsTask).ToList(),
     (await authenticatorsTask).ToList()
            );
        }

        /// <summary>
        /// Pushes all result values to bound properties in a single batch.
        /// Runs on the UI thread (already there after Task.Run returns).
        /// </summary>
        private void ApplyResult(
         VaultHealthResult result,
       List<CredentialHealthDetail> issuesList,
        int totalIssues,
        List<VaultFinding> findingsList,
          List<CredentialCluster> clustersList)
        {
            // ?? Batch: write all backing fields before raising any notification ???
            _score = result.Score;
            _statusLabel = result.StatusLabel;
            _statusColor = result.StatusColor;
            _calculatedAt = $"Last checked {result.CalculatedAt:HH:mm – dd MMM}";
            _totalCredentials = result.TotalCredentials;
            _weakCount = result.WeakPasswordsCount;
            _reusedCount = result.ReusedPasswordsCount;
            _breachedCount = result.BreachedCount;
            _missing2FACount = result.Missing2FACount;
            _emptyCount = result.EmptyPasswordCount;
            _weakPercent = result.WeakPercent;
            _reusedPercent = result.ReusedPercent;
            _breachedPercent = result.BreachedPercent;
            _missing2FAPercent = result.Missing2FAPercent;
            _allStrong = result.AllPasswordsStrong;
            _full2FA = result.Full2FACoverage;
            _noBreaches = result.NoBreachesDetected;
            _attackSurfaceScore = result.AttackSurfaceScore;
            _findings = new ObservableCollection<VaultFinding>(findingsList);
            _clusters = new ObservableCollection<CredentialCluster>(clustersList);
            _details = new ObservableCollection<CredentialHealthDetail>(issuesList);
            _totalIssuesCount = totalIssues;

            // ── Single bulk notification – one layout pass ────────────────────────
            RaisePropertyChanged(nameof(Score));
            RaisePropertyChanged(nameof(StatusLabel));
            RaisePropertyChanged(nameof(StatusColor));
            RaisePropertyChanged(nameof(CalculatedAt));
            RaisePropertyChanged(nameof(TotalCredentials));
            RaisePropertyChanged(nameof(WeakCount));
            RaisePropertyChanged(nameof(ReusedCount));
            RaisePropertyChanged(nameof(BreachedCount));
            RaisePropertyChanged(nameof(Missing2FACount));
            RaisePropertyChanged(nameof(EmptyCount));
            RaisePropertyChanged(nameof(WeakPercent));
            RaisePropertyChanged(nameof(ReusedPercent));
            RaisePropertyChanged(nameof(BreachedPercent));
            RaisePropertyChanged(nameof(Missing2FAPercent));
            RaisePropertyChanged(nameof(AllStrong));
            RaisePropertyChanged(nameof(Full2FA));
            RaisePropertyChanged(nameof(NoBreaches));
            RaisePropertyChanged(nameof(AttackSurfaceScore));
            RaisePropertyChanged(nameof(AttackSurfaceLabel));
            RaisePropertyChanged(nameof(AttackSurfaceColor));
            RaisePropertyChanged(nameof(Findings));
            RaisePropertyChanged(nameof(Clusters));
            RaisePropertyChanged(nameof(HasClusters));
            RaisePropertyChanged(nameof(Details));
            RaisePropertyChanged(nameof(HasNoIssues));
            RaisePropertyChanged(nameof(TotalIssuesCount));
            RaisePropertyChanged(nameof(IssuesCapped));
            RaisePropertyChanged(nameof(IssuesCappedLabel));

            _logger.LogInformation(
                "VaultHealthPage loaded: score={Score} surface={Surface} issues={Issues}",
                    result.Score, result.AttackSurfaceScore, issuesList.Count);
        }

        /// <summary>
        /// Saves today's snapshot and reloads trend history.
        /// Runs entirely in the background – never blocks the UI.
        /// TrendLabel updates automatically when HealthHistory is set.
        /// </summary>
        private async Task SaveSnapshotAndLoadHistoryAsync(VaultHealthResult result)
        {
            try
            {
                var snapshot = new VaultHealthSnapshot
                {
                    RecordedDate = DateTime.UtcNow.Date,
                    Score = result.Score,
                    Status = result.Status,
                    WeakCount = result.WeakPasswordsCount,
                    ReusedCount = result.ReusedPasswordsCount,
                    BreachedCount = result.BreachedCount,
                    Missing2FACount = result.Missing2FACount,
                    TotalCredentials = result.TotalCredentials,
                    AttackSurfaceScore = result.AttackSurfaceScore,
                };

                // Save and read history in parallel
                var saveTask = _dataStorageService.SaveHealthSnapshotAsync(snapshot);
                var historyTask = _dataStorageService.GetHealthHistoryAsync(30);

                await Task.WhenAll(saveTask, historyTask);

                var history = await historyTask;

                // Marshal back to UI thread for ObservableCollection assignment
                MainThread.BeginInvokeOnMainThread(() =>
            HealthHistory = new ObservableCollection<VaultHealthSnapshot>(history));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VaultHealthPage: snapshot/history background task failed");
            }
        }

        // ── Commands ───────────────────────────────────────────────────────────
        private AsyncCommand? _refreshCommand;
        public ICommand RefreshCommand =>
            _refreshCommand ??= new AsyncCommand(LoadAsync);

        private DelegateCommand? _goBackCommand;
        public DelegateCommand GoBackCommand =>
            _goBackCommand ??= new DelegateCommand(async () => await NavigationService.GoBackAsync());

        private AsyncCommand? _checkEmailsCommand;
        public ICommand CheckEmailsCommand =>
         _checkEmailsCommand ??= new AsyncCommand(RunHibpEmailCheckAsync, () => !IsHibpChecking);

        private async Task RunHibpEmailCheckAsync()
        {
            IsHibpChecking = true;
            HibpProgress = 0;
            HibpResults.Clear();
            HibpBreachedCount = 0;

            try
            {
                var identities = await _dataStorageService.GetIdentityItemsAsync();
                var loginEmails = (await _dataStorageService.GetLoginItemsAsync())
                .Where(x => !string.IsNullOrWhiteSpace(x.Username) && x.Username.Contains('@'))
                   .Select(x => x.Username.Trim().ToLowerInvariant());

                var allEmails = identities
              .Where(i => !string.IsNullOrWhiteSpace(i.Email))
   .Select(i => i.Email.Trim().ToLowerInvariant())
              .Concat(loginEmails)
    .Distinct()
         .ToList();

                HibpTotal = allEmails.Count;
                if (allEmails.Count == 0)
                    return;

                var progress = new Progress<(int done, int total)>(p =>
          {
              HibpProgress = p.done;
              HibpTotal = p.total;
          });

                var results = await _hibp.CheckAllVaultEmailsAsync(allEmails, progress);

                HibpResults = new ObservableCollection<HibpEmailResult>(results);
                HibpBreachedCount = results.Count(r => r.IsBreached);

                _logger.LogInformation("HIBP email check: {Checked} emails, {Breached} breached",
              results.Count, HibpBreachedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HIBP email check failed");
            }
            finally
            {
                IsHibpChecking = false;
            }
        }

        #region Commands

        #endregion
    }
}
