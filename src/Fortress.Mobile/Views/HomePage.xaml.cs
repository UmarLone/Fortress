using Fortress.Services;
using Fortress.ViewModels;

namespace Fortress.Views
{
    public partial class HomePage : ContentPage, ISecurePage
    {
        public HomePage()
        {
            InitializeComponent();
            NavigationPage.SetBackButtonTitle(this, string.Empty);
            NavigationPage.SetHasNavigationBar(this, true);
            Loaded += (_, _) =>
            {
                if (BindingContext is not HomePageViewModel)
                    System.Diagnostics.Debug.WriteLine(
                     $"⚠️ HomePage.BindingContext is {BindingContext?.GetType().Name ?? "null"}");
                else
                    System.Diagnostics.Debug.WriteLine("✅ HomePage.BindingContext = HomePageViewModel");
            };
        }

        // ── Page lifecycle ────────────────────────────────────────────────────────

        protected override void OnAppearing()
        {
            base.OnAppearing();

          // Match the NavigationPage bar to HeroGradientStart so the solid bar
            // colour behind the TitleView aligns with the top of the hero gradient.
      if (Parent is NavigationPage navPage)
     {
    navPage.BarBackgroundColor =
     Application.Current?.Resources.TryGetValue("HeroGradientStart", out var c) == true
       ? (Color)c
     : Color.FromArgb("#407cca");
    navPage.BarTextColor = Colors.White;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }
    }
}