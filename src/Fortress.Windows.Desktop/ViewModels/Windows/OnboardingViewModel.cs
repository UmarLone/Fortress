using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fortress.Windows.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fortress.Windows.Desktop.ViewModels.Windows
{
    /// <summary>
    /// Drives the OnboardingWindow � a pure marketing carousel.
    /// No user input. Mirrors the MAUI OnboardingPageViewModel exactly.
    ///
    /// Slides (0-based):
    ///   0  Meet Fortress
    ///   1  Password Vault
    ///   2  Built-in 2FA Codes
    ///   3  Vault Health Score
    ///   4  PIN &amp; Biometric Lock
    ///   5  Cloud Backup / You're All Set
    /// </summary>
    public partial class OnboardingViewModel : ObservableObject
    {
        private const int TotalSlides = 6;

        [ObservableProperty] private int    _currentSlide    = 0;
      [ObservableProperty] private bool   _isLastSlide     = false;
        [ObservableProperty] private string _nextButtonText  = "Next";

        // ── Commands ───────────────────────────────────────────────────────
        [RelayCommand]
      private void Next()
        {
       if (CurrentSlide < TotalSlides - 1)
      {
          CurrentSlide++;
  UpdateState();
            }
            else
     {
              // Last slide ? open Setup window
 OnCarouselComplete?.Invoke(this, EventArgs.Empty);
     }
        }

 [RelayCommand]
        private void Skip()
        {
      // Skip to the last slide so user sees the summary before setup
       CurrentSlide = TotalSlides - 1;
       UpdateState();
   }

    private void UpdateState()
  {
            IsLastSlide    = CurrentSlide == TotalSlides - 1;
            NextButtonText = IsLastSlide ? "Get Started" : "Next";
}

// Raised when the user taps "Get Started" on the last slide
    public event EventHandler? OnCarouselComplete;
    }
}
