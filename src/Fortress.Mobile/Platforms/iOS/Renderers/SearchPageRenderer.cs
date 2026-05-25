using Foundation;
using Fortress.Mobile.Core.Contracts;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using UIKit;

namespace Fortress.iOS.Renderers
{
    public static class SearchPageBehavior_iOS
    {
        static UISearchController? searchController;
        static UIViewController? parentVC;
        static int retryCount = 0;

        public static void Attach(Page page)
        {
            Console.WriteLine($"SearchPageBehavior_iOS.Attach called, retry={retryCount}");
            
            // Get the page's own view controller
            if (page.Handler is not IPlatformViewHandler pageHandler || pageHandler.ViewController == null)
            {
                Console.WriteLine("SearchPageBehavior_iOS: No handler yet");
                if (retryCount < 10)
                {
                    retryCount++;
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(150);
                        Attach(page);
                    });
                }
                return;
            }

            var pageVC = pageHandler.ViewController;
            Console.WriteLine($"SearchPageBehavior_iOS: PageVC = {pageVC.GetType().Name}");
            Console.WriteLine($"SearchPageBehavior_iOS: PageVC.NavigationController = {pageVC.NavigationController?.GetType().Name ?? "null"}");
            Console.WriteLine($"SearchPageBehavior_iOS: PageVC.ParentVC = {pageVC.ParentViewController?.GetType().Name ?? "null"}");
            
            // Walk up to find a VC that has a NavigationController
            UIViewController? vcWithNav = pageVC;
            while (vcWithNav != null && vcWithNav.NavigationController == null)
            {
                Console.WriteLine($"SearchPageBehavior_iOS: Checking {vcWithNav.GetType().Name}, NavController = null, going to parent");
                vcWithNav = vcWithNav.ParentViewController;
            }

            if (vcWithNav?.NavigationController == null)
            {
                Console.WriteLine("SearchPageBehavior_iOS: No NavigationController found in hierarchy");
                if (retryCount < 10)
                {
                    retryCount++;
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(150);
                        Attach(page);
                    });
                }
                return;
            }

            Console.WriteLine($"SearchPageBehavior_iOS: Found VC with NavController: {vcWithNav.GetType().Name}");
            
            // Use this VC - it's the one inside the navigation controller
            parentVC = vcWithNav;
            retryCount = 0;
            AttachToViewController(page, parentVC);
        }

        static void AttachToViewController(Page page, UIViewController vc)
        {
            searchController = new UISearchController(searchResultsController: null)
            {
                ObscuresBackgroundDuringPresentation = false,
                HidesNavigationBarDuringPresentation = false
            };
            searchController.SearchResultsUpdater = new SearchResultsUpdater(page);
            searchController.SearchBar.Placeholder = "Search...";
            searchController.SearchBar.SearchTextField.BackgroundColor = UIColor.White;

            vc.NavigationItem.SearchController = searchController;
            vc.NavigationItem.HidesSearchBarWhenScrolling = false;
            vc.DefinesPresentationContext = true;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                searchController.Active = true;
                searchController.Active = false;
            });
        }

        public static void Detach()
        {
            if (parentVC != null)
                parentVC.NavigationItem.SearchController = null;
            searchController?.Dispose();
            searchController = null;
            parentVC = null;
        }

        class SearchResultsUpdater : NSObject, IUISearchResultsUpdating
        {
            readonly WeakReference<Page> pageRef;
            public SearchResultsUpdater(Page page) { pageRef = new WeakReference<Page>(page); }
            public void UpdateSearchResultsForSearchController(UISearchController sc)
            {
                if (pageRef.TryGetTarget(out var page) && page is ISearchPage searchPage)
                    searchPage.OnSearchBarTextChanged(sc.SearchBar.Text ?? string.Empty);
            }
        }
    }
}
