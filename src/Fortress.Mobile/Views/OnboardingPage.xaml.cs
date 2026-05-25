using Fortress.ViewModels;
using MauiIcons.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp.Extended.UI.Controls;

namespace Fortress.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class OnboardingPage : ContentPage
    {
        private int _currentPage = 0;
        private const int TotalPages = 8; // Welcome, PasswordVault, Totp, Autofill, Health, Voice, AuthenticationLock, Backups
        private ScrollView _scrollView;
        private OnboardingPageViewModel? _viewModel => BindingContext as OnboardingPageViewModel;

        /// <summary>
        /// Set to true while ScrollToAsync is running so OnScrolled does not
        /// double-update indicators/buttons during a programmatic scroll.
        /// </summary>
        private bool _isProgrammaticScroll;

        // Debounce: only commit a new page after scrolling has settled for one frame
        private int _pendingPage = -1;

        public OnboardingPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel?.OnBoarding();

            // Find the ScrollView
            _scrollView = this.FindByName<ScrollView>("OnboardingScroll");

            // Create and initialize indicators
            CreateIndicators();
            UpdateIndicators(_currentPage);
            UpdateButtonStates();
        }

        private void CreateIndicators()
        {
            IndicatorsStack.Children.Clear();

            for (int i = 0; i < TotalPages; i++)
            {
                var indicator = new Border
                {
                    HeightRequest = 8,
                    WidthRequest = 8,
                    BackgroundColor = i == 0 ? Color.FromArgb("#407cca") : Colors.Transparent,
                    StrokeThickness = 2,
                    Stroke = i == 0 ? Color.FromArgb("#407cca") : Color.FromArgb("#E5E7EB"),
                    StrokeShape = new RoundRectangle { CornerRadius = 4 }
                };
                IndicatorsStack.Children.Add(indicator);
            }
        }

        private void UpdateIndicators(int page)
        {
            for (int i = 0; i < IndicatorsStack.Children.Count; i++)
            {
                if (IndicatorsStack.Children[i] is Border indicator)
                {
                    if (i == page)
                    {
                        indicator.BackgroundColor = Color.FromArgb("#407cca");
                        indicator.Stroke = Color.FromArgb("#407cca");
                        indicator.WidthRequest = 32;
                    }
                    else if (i < page)
                    {
                        indicator.BackgroundColor = Color.FromArgb("#407cca");
                        indicator.Stroke = Color.FromArgb("#407cca");
                        indicator.WidthRequest = 8;
                    }
                    else
                    {
                        indicator.BackgroundColor = Colors.Transparent;
                        indicator.Stroke = Color.FromArgb("#E5E7EB");
                        indicator.WidthRequest = 8;
                    }
                }
            }
        }

        private async void OnNextClicked(object sender, EventArgs e)
        {
            if (_currentPage < TotalPages - 1)
            {
                _currentPage++;
                UpdateIndicators(_currentPage);
                UpdateButtonStates();
                await ScrollToPage(_currentPage);
            }
            else
            {
                // Last page - finish
                _viewModel?.FinishCommand.Execute();
            }
        }

        private async void OnSkipClicked(object sender, EventArgs e)
        {
            // Skip to last page
            _currentPage = TotalPages - 1;
            UpdateIndicators(_currentPage);
            UpdateButtonStates();
            await ScrollToPage(_currentPage);
        }

        private void OnScrolled(object sender, ScrolledEventArgs e)
        {
            // Ignore events fired by our own ScrollToAsync calls
            if (_isProgrammaticScroll) return;

            var pageWidth = Width;
            if (pageWidth <= 0) return;

            var page = (int)Math.Round(e.ScrollX / pageWidth);
            if (page < 0 || page >= TotalPages || page == _currentPage) return;

            _currentPage = page;
            UpdateIndicators(_currentPage);
            UpdateButtonStates();
        }

        private async Task ScrollToPage(int page)
        {
            if (_scrollView == null) return;
            _isProgrammaticScroll = true;
            try
            {
                await _scrollView.ScrollToAsync(page * Width, 0, animated: true);
            }
            finally
            {
                _isProgrammaticScroll = false;
            }
        }

        private void UpdateButtonStates()
        {
            var primaryColor = Color.FromArgb("#407cca");

            if (_currentPage == TotalPages - 1)
            {
                // Last page - "Begin Setup" (keep blue, not green)
                NextButtonText.Text = "Begin Setup";
                NextButton.BackgroundColor = primaryColor; // Keep primary blue

                // Update shadow color to match button
                if (NextButton.Shadow is Shadow shadow)
                {
                    shadow.Brush = new SolidColorBrush(primaryColor);
                }

                SkipButton.IsVisible = false;
            }
            else
            {
                // Regular pages - "Next"
                NextButtonText.Text = "Next";
                NextButton.BackgroundColor = primaryColor;

                // Update shadow color to match button
                if (NextButton.Shadow is Shadow shadow)
                {
                    shadow.Brush = new SolidColorBrush(primaryColor);
                }

                SkipButton.IsVisible = true;
            }
        }
    }
}