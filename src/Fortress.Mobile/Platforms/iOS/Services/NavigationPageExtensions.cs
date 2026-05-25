using Foundation;
using Microsoft.Maui.Controls;
using UIKit;

namespace Fortress.iOS.Services
{
   public static class NavigationPageExtensions
    {
        public static void HookNavigationEvents(object handler, NavigationPage nav)
        {
            Console.WriteLine("iOS NavigationViewHandler Mapping - SearchToolbar1");
            if (nav.Handler?.PlatformView is not UINavigationController navController)
                return;
            Console.WriteLine("iOS NavigationViewHandler Mapping - SearchToolbar3");
            
            // Configure navigation bar appearance for simple back arrow
            ConfigureNavigationBarAppearance(navController);
            
            // Re-attach when MAUI navigation stack changes
            nav.Pushed += (_, _) => 
            {
                ConfigureBackButton(navController);
                AttachSearchController(nav, navController);
            };
            nav.Popped += (_, _) => AttachSearchController(nav, navController);

            // Attach once after initial load
            ConfigureBackButton(navController);
            AttachSearchController(nav, navController);
        }
        
        private static void ConfigureNavigationBarAppearance(UINavigationController navController)
        {
            // Create standard appearance
            var appearance = new UINavigationBarAppearance();
            appearance.ConfigureWithOpaqueBackground();
            
            // Create back button appearance that removes the background
            var backButtonAppearance = new UIBarButtonItemAppearance(UIBarButtonItemStyle.Plain);
            
            // Hide the title text
            backButtonAppearance.Normal.TitleTextAttributes = new NSDictionary<NSString, NSObject>(
                new NSString[] { UIStringAttributeKey.ForegroundColor },
                new NSObject[] { UIColor.Clear }
            );
            
            // Remove any background by setting background image to empty
            backButtonAppearance.Normal.BackgroundImage = new UIImage();
            
            appearance.BackButtonAppearance = backButtonAppearance;
            
            // Apply to navigation bar
            navController.NavigationBar.StandardAppearance = appearance;
            navController.NavigationBar.ScrollEdgeAppearance = appearance;
            navController.NavigationBar.CompactAppearance = appearance;
            
            // Ensure tint color is white for the arrow
            navController.NavigationBar.TintColor = UIColor.White;
        }
        
        private static void ConfigureBackButton(UINavigationController navController)
        {
            // Called after each push to ensure back button is plain
            var topVc = navController.TopViewController;
            if (topVc?.NavigationItem != null)
            {
                // Set back button to display only the arrow with no title
                topVc.NavigationItem.BackButtonDisplayMode = UINavigationItemBackButtonDisplayMode.Minimal;
            }
        }

        private static void AttachSearchController(NavigationPage nav, UINavigationController navController)
        {
            var topVc = navController.ViewControllers?.LastOrDefault();
            if (topVc == null)
                return;

            // Only show search on pages that support it
            if (nav.CurrentPage is not Maui.Core.Contracts.ISearchPage)
            {
                topVc.NavigationItem.SearchController = null;
                return;
            }

            // Avoid duplicating
            if (topVc.NavigationItem.SearchController != null)
                return;

            var searchController = new UISearchController((UIViewController?)null)
            {
                ObscuresBackgroundDuringPresentation = false,
                HidesNavigationBarDuringPresentation = false
            };

            searchController.SearchBar.Placeholder = "Search...";
            searchController.SearchResultsUpdater = new SearchResultsUpdater(nav);

            topVc.NavigationItem.SearchController = searchController;
            topVc.NavigationItem.HidesSearchBarWhenScrolling = false;

            // REQUIRED so iOS actually shows it
            topVc.DefinesPresentationContext = true;
        }
    }

    public class SearchResultsUpdater : UISearchResultsUpdating
    {
        private readonly NavigationPage _nav;

        public SearchResultsUpdater(NavigationPage nav)
        {
            _nav = nav;
        }

        public void UpdateSearchResultsForSearchController(UISearchController searchController)
        {
            var text = searchController.SearchBar.Text ?? string.Empty;

            if (_nav.CurrentPage is not Maui.Core.Contracts.ISearchPage searchPage)
                return;

            searchPage.OnSearchBarTextChanged(text);
        }
    }
}
