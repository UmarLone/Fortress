using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fortress.ViewModels
{
  /// <summary>
    /// Powers the AI Security Insights page.
    ///
    /// All data comes from the local <see cref="EventLog"/> store – nothing
  /// is fabricated. If the user has never triggered a risk warning, every
    /// counter is zero and the page says so honestly.
    /// </summary>
    public class SecurityInsightsViewModel : ViewModelBase
    {
      // ── Summary counters ─────────────────────────────────────────────
        private int _blockedCount;
  public int BlockedCount
 {
            get => _blockedCount;
          set
          {
 SetProperty(ref _blockedCount, value);
         RaisePropertyChanged(nameof(ThreatSummaryLabel));
     RaisePropertyChanged(nameof(HasThreats));
     RaisePropertyChanged(nameof(ThreatBadgeText));
         }
        }

        private int _warnedCount;
        public int WarnedCount
   {
          get => _warnedCount;
            set => SetProperty(ref _warnedCount, value);
        }

     private int _filledDespiteWarningCount;
        public int FilledDespiteWarningCount
        {
          get => _filledDespiteWarningCount;
            set
       {
        SetProperty(ref _filledDespiteWarningCount, value);
 RaisePropertyChanged(nameof(FilledDespiteWarningVisible));
            }
        }

        private int _trustedDomainsCount;
        public int TrustedDomainsCount
        {
   get => _trustedDomainsCount;
            set => SetProperty(ref _trustedDomainsCount, value);
     }

        private int _totalFillsCount;
    public int TotalFillsCount
 {
          get => _totalFillsCount;
    set
            {
        SetProperty(ref _totalFillsCount, value);
   RaisePropertyChanged(nameof(SafeFillsCount));
       RaisePropertyChanged(nameof(SafeFillsPercent));
            }
}

        // ── Derived ──────────────────────────────────────────────────────
        public int SafeFillsCount => Math.Max(0, TotalFillsCount - BlockedCount - FilledDespiteWarningCount);
        public double SafeFillsPercent => TotalFillsCount == 0
          ? 100d
            : Math.Round((SafeFillsCount / (double)TotalFillsCount) * 100d, 0);

      public bool HasThreats => BlockedCount > 0 || WarnedCount > 0;
        public bool FilledDespiteWarningVisible => FilledDespiteWarningCount > 0;

        public string ThreatSummaryLabel => BlockedCount switch
        {
            0 => "No threats detected",
      1 => "1 threat blocked",
            _ => $"{BlockedCount} threats blocked"
   };

        public string ThreatBadgeText => BlockedCount > 0 ? BlockedCount.ToString() : string.Empty;

        // ── AI narrative ─────────────────────────────────────────────────
        private string _aiNarrative = "Loading insights…";
        public string AiNarrative
        {
            get => _aiNarrative;
     set => SetProperty(ref _aiNarrative, value);
      }

    private string _protectionStatusLabel = "Analysing…";
   public string ProtectionStatusLabel
        {
    get => _protectionStatusLabel;
            set => SetProperty(ref _protectionStatusLabel, value);
        }

        private string _protectionStatusColor = "#3B82F6";
        public string ProtectionStatusColor
        {
       get => _protectionStatusColor;
   set => SetProperty(ref _protectionStatusColor, value);
        }

   // ── 7-day bar chart data ─────────────────────────────────────────
        private ObservableCollection<DayThreatBar> _weekBars = new(CreateEmptyWeekBars());
        public ObservableCollection<DayThreatBar> WeekBars
        {
          get => _weekBars;
         set => SetProperty(ref _weekBars, value);
}

        // ── Recent threat log ────────────────────────────────────────────
        private ObservableCollection<ThreatLogItem> _recentThreats = new();
      public ObservableCollection<ThreatLogItem> RecentThreats
    {
            get => _recentThreats;
         set => SetProperty(ref _recentThreats, value);
        }

        private bool _hasRecentThreats;
        public bool HasRecentThreats
        {
   get => _hasRecentThreats;
   set => SetProperty(ref _hasRecentThreats, value);
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
       get => _isLoading;
  set => SetProperty(ref _isLoading, value);
        }

      // ── Selected timeframe ───────────────────────────────────────────
        private int _selectedDays = 30;
     public int SelectedDays
        {
        get => _selectedDays;
            set
            {
   if (SetProperty(ref _selectedDays, value))
        {
      RaisePropertyChanged(nameof(Is7DaysSelected));
          RaisePropertyChanged(nameof(Is30DaysSelected));
    RaisePropertyChanged(nameof(Is90DaysSelected));
          RaisePropertyChanged(nameof(Pill7BgColor));
            RaisePropertyChanged(nameof(Pill30BgColor));
          RaisePropertyChanged(nameof(Pill90BgColor));
                RaisePropertyChanged(nameof(Pill7TextColor));
         RaisePropertyChanged(nameof(Pill30TextColor));
           RaisePropertyChanged(nameof(Pill90TextColor));
       _ = LoadAsync();
         }
          }
        }

        public bool Is7DaysSelected  => _selectedDays == 7;
        public bool Is30DaysSelected => _selectedDays == 30;
        public bool Is90DaysSelected => _selectedDays == 90;

        // Active pill = primary blue filled; inactive = card background with border
      public string Pill7BgColor   => _selectedDays == 7  ? "#407CCA" : "Transparent";
        public string Pill30BgColor  => _selectedDays == 30 ? "#407CCA" : "Transparent";
    public string Pill90BgColor  => _selectedDays == 90 ? "#407CCA" : "Transparent";
   public string Pill7TextColor  => _selectedDays == 7  ? "#FFFFFF" : "#407CCA";
      public string Pill30TextColor => _selectedDays == 30 ? "#FFFFFF" : "#407CCA";
        public string Pill90TextColor => _selectedDays == 90 ? "#FFFFFF" : "#407CCA";

        // ── Fields ───────────────────────────────────────────────────────
     private readonly IEventLogProcessor _eventLogProcessor;
        private readonly ILogger<SecurityInsightsViewModel> _logger;

        public SecurityInsightsViewModel(
    INavigationService navigationService,
   IEventLogProcessor eventLogProcessor,
  ILogger<SecurityInsightsViewModel> logger)
     : base(navigationService)
        {
  _eventLogProcessor = eventLogProcessor;
_logger = logger;
}

        // ── Lifecycle ────────────────────────────────────────────────────
        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
base.OnNavigatedTo(parameters);
     await LoadAsync();
        }

        // ── Data loading ─────────────────────────────────────────────────
        private async Task LoadAsync()
        {
 IsLoading = true;
     try
    {
     var since = DateTime.UtcNow.AddDays(-_selectedDays);
          var until = DateTime.UtcNow;

          // Pull all autofill-related events for the window
          var riskEventTypes = new List<int>
            {
     (int)EventLogType.AutofillBlockedRisk,
   (int)EventLogType.AutofillWarnedRisk,
      (int)EventLogType.WebCredentialUsed,
          (int)EventLogType.PhonePasswordUsed,
                };

        var logs = (await _eventLogProcessor.GetLocalLogsAsync(
             riskEventTypes, since, until, recordCount: 500)).ToList();

     // ── Counters ─────────────────────────────────────────────
   var blocked = logs.Where(l => l.EventTypeId == (int)EventLogType.AutofillBlockedRisk).ToList();
                var warned  = logs.Where(l => l.EventTypeId == (int)EventLogType.AutofillWarnedRisk).ToList();
      var fills   = logs.Where(l => l.EventTypeId == (int)EventLogType.WebCredentialUsed
       || l.EventTypeId == (int)EventLogType.PhonePasswordUsed).ToList();

 BlockedCount           = blocked.Count;
   WarnedCount   = warned.Count;
       FilledDespiteWarningCount = warned.Count; // warned = user chose "Fill Anyway"
        TotalFillsCount    = fills.Count + warned.Count + blocked.Count;
                TrustedDomainsCount = blocked
                    .Select(l => l.CredentialLabel)
   .Where(d => !string.IsNullOrEmpty(d))
   .Distinct(StringComparer.OrdinalIgnoreCase)
         .Count();

          // ── 7-day bar chart ──────────────────────────────────────
              BuildWeekBars(blocked, warned);

      // ?? Recent threat log (blocked + warned, newest first) ???
              BuildRecentThreats(blocked, warned);

   // ── AI narrative ─────────────────────────────────────────
         BuildNarrative();
    }
          catch (Exception ex)
    {
                _logger.LogError(ex, "SecurityInsightsViewModel: LoadAsync failed");
     }
        finally
       {
                IsLoading = false;
}
        }

        private void BuildWeekBars(List<AuditLog> blocked, List<AuditLog> warned)
        {
     var bars = new List<DayThreatBar>();
    var maxCount = 1; // avoid divide-by-zero

        for (int i = 6; i >= 0; i--)
      {
      var day = DateTime.UtcNow.Date.AddDays(-i);
      var dayBlocked = blocked.Count(l => l.DateTimeRaw.Date == day);
  var dayWarned  = warned.Count(l => l.DateTimeRaw.Date == day);
      var total = dayBlocked + dayWarned;
                if (total > maxCount) maxCount = total;
           bars.Add(new DayThreatBar
              {
     DayLabel   = i == 0 ? "Today" : day.ToString("ddd"),
   BlockedCount = dayBlocked,
           WarnedCount  = dayWarned,
            Total        = total,
    });
            }

          // Normalise bar heights to a max of 60dp
            foreach (var b in bars)
           b.BarHeight = Math.Max(4, (int)Math.Round(b.Total / (double)maxCount * 60));

      WeekBars = new ObservableCollection<DayThreatBar>(bars);
        }

        private void BuildRecentThreats(List<AuditLog> blocked, List<AuditLog> warned)
        {
          var items = blocked
                .Select(l => new ThreatLogItem
    {
       Domain      = string.IsNullOrWhiteSpace(l.CredentialLabel) ? "Unknown domain" : l.CredentialLabel,
        Detail      = l.Detail ?? string.Empty,
TimeLabel   = FormatRelativeTime(l.DateTimeRaw),
                DateTimeRaw = l.DateTimeRaw,
             IsBlocked   = true,
     BadgeLabel  = "BLOCKED",
        BadgeColor  = "#DC2626",
 BadgeBg     = "#FEE2E2",
         IconGlyph   = "\ue897",
 IconColor   = "#DC2626",
        IconBg    = "#FEE2E2",
    })
                .Concat(warned.Select(l => new ThreatLogItem
        {
   Domain      = string.IsNullOrWhiteSpace(l.CredentialLabel) ? "Unknown domain" : l.CredentialLabel,
      Detail      = l.Detail ?? string.Empty,
            TimeLabel   = FormatRelativeTime(l.DateTimeRaw),
            DateTimeRaw = l.DateTimeRaw,
         IsBlocked   = false,
             BadgeLabel  = "WARNED",
       BadgeColor  = "#D97706",
           BadgeBg = "#FEF3C7",
               IconGlyph   = "\ue002",
       IconColor   = "#D97706",
            IconBg      = "#FEF3C7",
       }))
        .OrderByDescending(t => t.DateTimeRaw)
           .Take(20)
    .ToList();

            RecentThreats    = new ObservableCollection<ThreatLogItem>(items);
  HasRecentThreats = items.Count > 0;
        }

      private void BuildNarrative()
     {
  if (BlockedCount == 0 && WarnedCount == 0)
{
       AiNarrative = "FORTRESS has not detected any suspicious fill requests in this period. " +
   "Your browsing and autofill habits look clean. " +
"The on-device AI continues monitoring every autofill request in real-time.";
      ProtectionStatusLabel = "All Clear";
         ProtectionStatusColor = "#16A34A";
     return;
     }

      var lines = new List<string>();

       if (BlockedCount > 0)
        lines.Add($"FORTRESS blocked {BlockedCount} suspicious autofill attempt{(BlockedCount == 1 ? "" : "s")} " +
      $"across {TrustedDomainsCount} domain{(TrustedDomainsCount == 1 ? "" : "s")}.");

         if (FilledDespiteWarningCount > 0)
     lines.Add($"You chose to fill on {FilledDespiteWarningCount} flagged site{(FilledDespiteWarningCount == 1 ? "" : "s")} despite warnings – consider reviewing those domains.");

            if (TrustedDomainsCount > 1)
 lines.Add($"Threats came from {TrustedDomainsCount} distinct domains – this may indicate a coordinated phishing campaign targeting your accounts.");

        // Safe fills ratio
         if (TotalFillsCount > 0 && SafeFillsPercent < 100)
         lines.Add($"Your safe fill rate is {SafeFillsPercent}% – {SafeFillsCount} of {TotalFillsCount} total autofill requests were clean.");

  // Trend hint from the 7-day chart
    var last3 = WeekBars.TakeLast(3).Sum(b => b.Total);
          var first3 = WeekBars.Take(3).Sum(b => b.Total);
            if (last3 > first3 && last3 > 0)
      lines.Add("? Threat activity has increased over the last 3 days. Stay alert and avoid unfamiliar links.");
       else if (last3 < first3 && first3 > 0)
       lines.Add("Threat activity appears to be decreasing – good trend. Keep up your browsing habits.");
    else if (last3 == 0 && first3 > 0)
           lines.Add("No threat activity in the last 3 days – the situation has stabilised.");

 AiNarrative = string.Join(" ", lines);

        ProtectionStatusLabel = BlockedCount > 5 ? "High Activity" : "Monitoring";
ProtectionStatusColor = BlockedCount > 5 ? "#DC2626" : "#D97706";
 }

        private static string FormatRelativeTime(DateTime utc)
        {
            var ago = DateTime.UtcNow - utc;
      if (ago.TotalMinutes < 1)  return "Just now";
 if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes}m ago";
      if (ago.TotalHours   < 24) return $"{(int)ago.TotalHours}h ago";
   if (ago.TotalDays    < 7)  return $"{(int)ago.TotalDays}d ago";
       return utc.ToLocalTime().ToString("MMM d");
      }

        /// <summary>
        /// Creates 7 empty <see cref="DayThreatBar"/> entries (today – 6 days ago)
        /// so that XAML compiled bindings to <c>WeekBars[0]</c>–<c>WeekBars[6]</c>
        /// never hit an empty collection during page construction.
        /// </summary>
     private static List<DayThreatBar> CreateEmptyWeekBars()
        {
            var bars = new List<DayThreatBar>(7);
            for (int i = 6; i >= 0; i--)
   {
      var day = DateTime.UtcNow.Date.AddDays(-i);
      bars.Add(new DayThreatBar
                {
    DayLabel = i == 0 ? "Today" : day.ToString("ddd"),
              BarHeight = 4,
             });
          }
      return bars;
        }

#region Commands

        private AsyncCommand _refreshCommand;
      public ICommand RefreshCommand =>
    _refreshCommand ??= new AsyncCommand(LoadAsync);

  private DelegateCommand _select7DaysCommand;
   public DelegateCommand Select7DaysCommand =>
  _select7DaysCommand ??= new DelegateCommand(() => SelectedDays = 7);

        private DelegateCommand _select30DaysCommand;
        public DelegateCommand Select30DaysCommand =>
            _select30DaysCommand ??= new DelegateCommand(() => SelectedDays = 30);

private DelegateCommand _select90DaysCommand;
        public DelegateCommand Select90DaysCommand =>
            _select90DaysCommand ??= new DelegateCommand(() => SelectedDays = 90);

   #endregion
    }

    // ── Chart bar model ───────────────────────────────────────────────────────
    public class DayThreatBar
    {
        public string DayLabel    { get; set; } = string.Empty;
        public int    BlockedCount { get; set; }
      public int    WarnedCount  { get; set; }
        public int    Total        { get; set; }
        /// <summary>Pixel height for the rendered bar – normalised 4–60dp.</summary>
        public int    BarHeight    { get; set; }
        public bool   HasActivity  => Total > 0;
 public string BarColor     => Total == 0 ? "#E5E7EB" : (BlockedCount > WarnedCount ? "#DC2626" : "#D97706");
    }

    // ── Threat log row model ──────────────────────────────────────────────────
  public class ThreatLogItem
    {
        public string   Domain      { get; set; } = string.Empty;
        public string   Detail      { get; set; } = string.Empty;
      public string   TimeLabel   { get; set; } = string.Empty;
   public bool     IsBlocked   { get; set; }
        public string BadgeLabel  { get; set; } = string.Empty;
        public string   BadgeColor  { get; set; } = "#DC2626";
   public string   BadgeBg     { get; set; } = "#FEE2E2";
        public string   IconGlyph   { get; set; } = "\ue897";
        public string   IconColor   { get; set; } = "#DC2626";
        public string   IconBg      { get; set; } = "#FEE2E2";
        public DateTime DateTimeRaw { get; set; }
    }
}
