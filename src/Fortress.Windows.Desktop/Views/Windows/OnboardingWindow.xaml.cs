using Fortress.Windows.Desktop.ViewModels.Windows;
using LottieSharp.WPF;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.IO;

namespace Fortress.Windows.Desktop.Views.Windows
{
    public partial class OnboardingWindow
    {
        public OnboardingViewModel ViewModel { get; }

        // Maps slide index → (LottieAnimationView, json filename)
        private readonly Dictionary<int, (LottieAnimationView View, string FileName)> _slideAnimations;

        public OnboardingWindow(OnboardingViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;

            _slideAnimations = new()
            {
                { 1, (LottieVault,    "PasswordVault.json") },
                { 2, (LottieTotp,     "Totp.json") },
                { 3, (LottieHealth,   "Health.json") },
                { 4, (LottieAuth,   "AuthenticationLock.json") },
                { 5, (LottieBackups,  "Backups.json") },
            };

            Loaded += OnWindowLoaded;

            // When the user navigates to a new slide, ensure that slide's animation plays.
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            viewModel.OnCarouselComplete += (_, _) =>
               {
                   // Carousel done → open the setup wizard
                   var setup = App.Services.GetRequiredService<SetupWindow>();
                   setup.Show();
                   Close();
               };
        }

        private void OnWindowLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Pre-load every animation file so the path is validated immediately.
            // Animations on currently-collapsed slides will start when they become visible.
            foreach (var (_, (view, fileName)) in _slideAnimations)
                LoadLottie(view, fileName);

            // Play whichever slide is active on first open (slide 0 has no Lottie).
            PlayCurrentSlideAnimation();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(OnboardingViewModel.CurrentSlide)) return;

            // Must update the UI on the dispatcher thread (property change fires from any thread).
            Dispatcher.InvokeAsync(PlayCurrentSlideAnimation);
        }

        private void PlayCurrentSlideAnimation()
        {
            var slide = ViewModel.CurrentSlide;
            if (!_slideAnimations.TryGetValue(slide, out var entry)) return;

            var (view, fileName) = entry;

            // Re-assign FileName now that the control is Visible + measured.
            // Clearing first forces LottieSharp to re-initialise the SkiaSharp surface.
            view.StopAnimation();
            view.FileName = null!;

            // Yield one layout pass so the Visible panel is measured before we load.
            Dispatcher.InvokeAsync(() =>
            {
                LoadLottie(view, fileName);
                if (!string.IsNullOrEmpty(view.FileName))
                    view.PlayAnimation();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static void LoadLottie(LottieAnimationView view, string fileName)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);
            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[Lottie] File not found: {path}");
                return;
            }
            view.FileName = path;
        }
    }
}
