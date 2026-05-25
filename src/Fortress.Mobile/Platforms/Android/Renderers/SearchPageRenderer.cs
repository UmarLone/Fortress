using Android.Content;
using Fortress.Droid;
using Fortress.Views;
using System.Collections.Generic;
using System.Linq;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.AppCompat.Widget;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Microsoft.Maui.Controls.Platform;
using Fortress.Mobile.Core.Contracts;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using Resource = Microsoft.Maui.Controls.Resource;

//https://codetraveler.io/2020/10/31/adding-a-search-bar-to-xamarin-forms-navigationpage/
[assembly: ExportRenderer(typeof(AuthenticatorsPage), typeof(SearchPageRenderer))]
[assembly: ExportRenderer(typeof(CredentialsPage), typeof(SearchPageRenderer))]

namespace Fortress.Droid
{
    public class SearchPageRenderer : PageRenderer
    {
        public SearchPageRenderer(Context context) : base(context)
        {

        }

        //Add the Searchbar once Xamarin.Forms creates the Page
        protected override void OnElementChanged(ElementChangedEventArgs<Page> e)
        {
            base.OnElementChanged(e);

            if (e.NewElement is ISearchPage && e.NewElement is Page page && page.Parent is NavigationPage navigationPage && navigationPage.CurrentPage is ISearchPage)
                AddSearchToToolbar(page.Title);
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();

            if (Element is ISearchPage && Element is Page page && page.Parent is NavigationPage navigationPage)
            {
                //Workaround to re-add the SearchView when navigating back to an ISearchPage, because Xamarin.Forms automatically removes it
                navigationPage.Popped += HandleNavigationPagePopped;
                navigationPage.PoppedToRoot += HandleNavigationPagePopped;
            }
        }

        //Adding the SearchBar in OnSizeChanged ensures the SearchBar is re-added after the device is rotated, because Xamarin.Forms automatically removes it
        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);

            if (Element is ISearchPage && Element is Page page && page.Parent is NavigationPage navigationPage && navigationPage.CurrentPage is ISearchPage)
            {
                AddSearchToToolbar(page.Title);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (GetToolbar() is Toolbar toolBar)
                toolBar.Menu?.RemoveItem(Resource.Menu.MainMenu);

            base.Dispose(disposing);
        }

        static IEnumerable<Toolbar> GetToolbars(ViewGroup viewGroup)
        {
            for (int i = 0; i < viewGroup.ChildCount; i++)
            {
                if (viewGroup.GetChildAt(i) is Toolbar toolbar)
                {
                    yield return toolbar;
                }
                else if (viewGroup.GetChildAt(i) is ViewGroup childViewGroup)
                {
                    foreach (var childToolbar in GetToolbars(childViewGroup))
                        yield return childToolbar;
                }
            }
        }

        Toolbar? GetToolbar()
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;

            if (activity?.Window?.DecorView.RootView is ViewGroup viewGroup)
            {
                var toolbars = GetToolbars(viewGroup);

                //Return top-most Toolbar
                return toolbars.LastOrDefault();
            }

            return null;
        }

        //Workaround to re-add the SearchView when navigating back to an ISearchPage, because Xamarin.Forms automatically removes it
        void HandleNavigationPagePopped(object sender, NavigationEventArgs e)
        {
            if (sender is NavigationPage navigationPage
                && navigationPage.CurrentPage is ISearchPage)
            {
                AddSearchToToolbar(navigationPage.CurrentPage.Title);
            }
        }

        void AddSearchToToolbar(string pageTitle)
        {
            if (GetToolbar() is Toolbar toolBar
                && toolBar.Menu?.FindItem(Resource.Id.ActionSearch)?.ActionView?.JavaCast<SearchView>()?.GetType() != typeof(SearchView))
            {
                toolBar.Title = pageTitle;
                toolBar.InflateMenu(Resource.Menu.MainMenu);

                if (toolBar.Menu?.FindItem(Resource.Id.ActionSearch)?.ActionView?.JavaCast<SearchView>() is SearchView searchView)
                {
                    searchView.QueryTextChange += HandleQueryTextChange;
                    searchView.ImeOptions = (int)ImeAction.Search;
                    searchView.InputType = (int)InputTypes.TextVariationFilter;
                    searchView.MaxWidth = int.MaxValue;
                }
            }
        }

        void HandleQueryTextChange(object sender, SearchView.QueryTextChangeEventArgs e)
        {
            if (Element is ISearchPage searchPage)
                searchPage.OnSearchBarTextChanged(e?.NewText ?? string.Empty);
        }
    }
}




