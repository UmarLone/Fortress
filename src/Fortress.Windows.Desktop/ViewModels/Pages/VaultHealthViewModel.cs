using Fortress.Core.Models;
using Fortress.Windows.Desktop.Services;
using System.Collections.ObjectModel;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class VaultHealthViewModel : ObservableObject, INavigationAware
    {
        private readonly IDesktopDataService _dataService;

  [ObservableProperty] private int _score;
    [ObservableProperty] private string _statusLabel = string.Empty;
  [ObservableProperty] private string _statusColor = "#94A3B8";
  [ObservableProperty] private int _totalCredentials;
      [ObservableProperty] private int _weakCount;
     [ObservableProperty] private int _reusedCount;
        [ObservableProperty] private int _oldCount;
     [ObservableProperty] private int _breachedCount;
        [ObservableProperty] private int _missing2FACount;
       [ObservableProperty] private int _attackSurfaceScore;
[ObservableProperty] private string _attackSurfaceLabel = string.Empty;
     [ObservableProperty] private ObservableCollection<VaultFinding> _findings = new();
   [ObservableProperty] private ObservableCollection<CredentialHealthDetail> _details = new();
       [ObservableProperty] private bool _isLoading = true;

        // Derived percentages for progress bars
 public double WeakPercent    => TotalCredentials == 0 ? 0 : Math.Min(1.0, (double)WeakCount / TotalCredentials);
   public double ReusedPercent  => TotalCredentials == 0 ? 0 : Math.Min(1.0, (double)ReusedCount / TotalCredentials);
        public double BreachedPercent=> TotalCredentials == 0 ? 0 : Math.Min(1.0, (double)BreachedCount / TotalCredentials);
        public double Missing2FAPercent => TotalCredentials == 0 ? 0 : Math.Min(1.0, (double)Missing2FACount / TotalCredentials);

  public VaultHealthViewModel(IDesktopDataService dataService) => _dataService = dataService;

        public async Task OnNavigatedToAsync()
    {
  await LoadAsync();
  }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

  private async Task LoadAsync()
      {
 IsLoading = true;
       try
 {
       var h = await _dataService.GetVaultHealthAsync();

            // Assign TotalCredentials FIRST — the percent properties divide by it
   TotalCredentials = h.TotalCredentials;

    Score     = h.Score;
  StatusLabel    = h.StatusLabel;
     StatusColor      = h.StatusColor;
    AttackSurfaceScore   = h.AttackSurfaceScore;
    AttackSurfaceLabel   = h.AttackSurfaceLabel;

      // Counts — assigned after TotalCredentials so percent getters never divide by zero
      WeakCount   = h.WeakPasswordsCount;
      ReusedCount     = h.ReusedPasswordsCount;
    OldCount        = h.OldPasswordsCount;
    BreachedCount   = h.BreachedCount;
   Missing2FACount = h.Missing2FACount;

   // Raise percent notifications once all counts are stable
     OnPropertyChanged(nameof(WeakPercent));
            OnPropertyChanged(nameof(ReusedPercent));
       OnPropertyChanged(nameof(BreachedPercent));
            OnPropertyChanged(nameof(Missing2FAPercent));

      Findings = new ObservableCollection<VaultFinding>(h.Findings);
            Details  = new ObservableCollection<CredentialHealthDetail>(h.Details);
       }
      finally { IsLoading = false; }
  }
 }
}
