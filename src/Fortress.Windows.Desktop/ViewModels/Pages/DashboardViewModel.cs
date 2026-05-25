using Fortress.Core.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDesktopDataService _dataService;

        // ── Stats ────────────────────────────────────────────────────────────
        [ObservableProperty] private int _credentialsCount;
        [ObservableProperty] private int _creditCardsCount;
        [ObservableProperty] private int _identitiesCount;
        [ObservableProperty] private int _secureItemsCount;
        [ObservableProperty] private int _authenticatorsCount;
        [ObservableProperty] private int _passkeysCount;
        [ObservableProperty] private int _unreadNotificationsCount;

        // ── Health ───────────────────────────────────────────────────────────
        [ObservableProperty] private int _healthScore = 100;
        [ObservableProperty] private string _healthStatusLabel = "Excellent";
        [ObservableProperty] private string _healthStatusColor = "#22C55E";
        [ObservableProperty] private int _weakPasswordsCount;
        [ObservableProperty] private int _reusedPasswordsCount;
        [ObservableProperty] private int _breachedCount;
        [ObservableProperty] private int _missing2FACount;

        // ── Recent ───────────────────────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<LoginItemViewModel> _recentLogins = new();

        // ── State ────────────────────────────────────────────────────────────
        [ObservableProperty] private bool _isLoading = true;

        // ── Derived ──────────────────────────────────────────────────────────
        public bool HasWarnings => WeakPasswordsCount > 0 || ReusedPasswordsCount > 0 || BreachedCount > 0;

        public string WarningText => BreachedCount > 0
                ? $"{BreachedCount} breach detected — act now"
           : $"{WeakPasswordsCount} weak · {ReusedPasswordsCount} reused passwords";

        public bool HasUnreadNotifications => UnreadNotificationsCount > 0;

        /// <summary>Time-aware greeting shown in the hero banner.</summary>
        public string Greeting
        {
            get
            {
                var hour = DateTime.Now.Hour;
                return hour < 12 ? "Good morning 🌤️"
                     : hour < 17 ? "Good afternoon ☀️"
                     : "Good evening 🌙";
            }
        }

        public DashboardViewModel(IDesktopDataService dataService)
        {
            _dataService = dataService;
        }

        // ── Partial callbacks ────────────────────────────────────────────────
        partial void OnUnreadNotificationsCountChanged(int value) =>
         OnPropertyChanged(nameof(HasUnreadNotifications));

        partial void OnWeakPasswordsCountChanged(int value)    => OnPropertyChanged(nameof(HasWarnings));
        partial void OnReusedPasswordsCountChanged(int value)  => OnPropertyChanged(nameof(HasWarnings));
        partial void OnBreachedCountChanged(int value)
      {
OnPropertyChanged(nameof(HasWarnings));
 OnPropertyChanged(nameof(WarningText));
  }

        // ── Load ─────────────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
{
     var logins     = await _dataService.GetLoginItemsAsync();
            var cards         = await _dataService.GetCreditCardsAsync();
                var identities    = await _dataService.GetIdentitiesAsync();
          var secureItems   = await _dataService.GetSecureItemsAsync();
           var authenticators = await _dataService.GetAuthenticatorsAsync();
    var passkeys      = await _dataService.GetPasskeysAsync();
        var notifications = await _dataService.GetNotificationsAsync();
         var health        = await _dataService.GetVaultHealthAsync();

          // Stats
      CredentialsCount    = logins.Count;
 CreditCardsCount    = cards.Count;
          IdentitiesCount     = identities.Count;
           SecureItemsCount    = secureItems.Count;
 AuthenticatorsCount = authenticators.Count;
    PasskeysCount       = passkeys.Count;
                UnreadNotificationsCount = notifications.Count(n => !n.IsSeen);

                // Health
    HealthScore     = health.Score;
     HealthStatusLabel    = health.StatusLabel;
                HealthStatusColor = health.StatusColor;
             WeakPasswordsCount   = health.WeakPasswordsCount;
 ReusedPasswordsCount = health.ReusedPasswordsCount;
 BreachedCount        = health.BreachedCount;
         Missing2FACount      = health.Missing2FACount;

  // Recent logins — most recently updated, top 6
                RecentLogins = new ObservableCollection<LoginItemViewModel>(
       logins
           .OrderByDescending(l => l.UpdatedAt)
       .Take(6)
  .Select(l => new LoginItemViewModel(l)));
}
            finally
            {
                IsLoading = false;
      }
  }
    }
}
