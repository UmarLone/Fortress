using Android.Content.Res;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.Widget;
using Fortress.Mobile.Core.Contracts;
using Google.Android.Material.AppBar;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Platform = Microsoft.Maui.ApplicationModel.Platform;
using Resource = Microsoft.Maui.Resource;

namespace Fortress.Droid.Services
{
    public static class NavigationPageExtensions
    {
        #region PUBLIC ENTRY

        public static void HookNavigationEvents(IElementHandler handler, NavigationPage nav)
        {
            nav.Pushed += (_, __) => UpdateToolbar(nav);
            nav.Popped += (_, __) => UpdateToolbar(nav);
            nav.PoppedToRoot += (_, __) => UpdateToolbar(nav);
            UpdateToolbar(nav);
        }

        public static void UpdateToolbar(NavigationPage nav)
        {
            if (nav.CurrentPage is ISearchPage)
                AddSearchView(nav);
            else
                RemoveSearchView();
        }

        #endregion


        #region TOOLBAR DISCOVERY (Xamarin-style, reliable)

        public static IEnumerable<MaterialToolbar> GetToolbars(ViewGroup root)
        {
            for (int i = 0; i < root.ChildCount; i++)
            {
                if (root.GetChildAt(i) is MaterialToolbar tb)
                    yield return tb;

                if (root.GetChildAt(i) is ViewGroup group)
                {
                    foreach (var t in GetToolbars(group))
                        yield return t;
                }
            }
        }

        public static MaterialToolbar? GetRealToolbar()
        {
            var activity = Platform.CurrentActivity;
            var root = activity?.Window?.DecorView?.RootView as ViewGroup;
            if (root == null) return null;

            // The LAST MaterialToolbar on screen is ALWAYS the visible top toolbar.
            return GetToolbars(root).LastOrDefault();
        }

        #endregion


        #region SEARCH + COLORING

        public static void AddSearchView(NavigationPage nav)
        {
            var toolbar = GetRealToolbar();
            if (toolbar == null)
            {
                System.Diagnostics.Debug.WriteLine("Toolbar not found.");
                return;
            }

            toolbar.Menu.Clear();
            toolbar.InflateMenu(Resource.Menu.MainMenu);

            // Tint the actual back arrow
            toolbar.Post(() =>
            {
                var navIcon = toolbar.NavigationIcon;
                if (navIcon != null)
                {
                    navIcon.SetTint(Android.Graphics.Color.White);
                    toolbar.NavigationIcon = navIcon;
                  
                }
            });

            var searchItem = toolbar.Menu.FindItem(Resource.Id.ActionSearch);
            var searchView = searchItem?.ActionView as AndroidX.AppCompat.Widget.SearchView;

            if (searchView == null)
            {
                System.Diagnostics.Debug.WriteLine("SearchView NOT FOUND");
                return;
            }

            var white = Android.Graphics.Color.White;

            searchItem.Icon?.SetTint(white);

            ApplySearchTint(searchView, white);

            toolbar.OverflowIcon?.SetTint(white);

            // Search text events
            searchView.QueryTextChange += (s, e) =>
            {
                if (nav.CurrentPage is ISearchPage page)
                    page.OnSearchBarTextChanged(e.NewText ?? string.Empty);
            };
        }


        private static void ApplySearchTint(AndroidX.AppCompat.Widget.SearchView searchView, Android.Graphics.Color white)
        {
            int GetId(string name) => searchView.Context.Resources.GetIdentifier(name, "id", "android");

            var textId = GetId("search_src_text");
            var magId = GetId("search_mag_icon");
            var closeId = GetId("search_close_btn");
            var plateId = GetId("search_plate");

            var searchText = searchView.FindViewById<EditText>(textId);
            var magIcon = searchView.FindViewById<ImageView>(magId);
            var closeBtn = searchView.FindViewById<ImageView>(closeId);
            var plate = searchView.FindViewById(plateId);

            // Device-dependent collapse arrow IDs
            var goId = GetId("search_go_btn");
            var goIcon = searchView.FindViewById<ImageView>(goId);

            var searchBtnId = GetId("search_button");
            var searchButtonIcon = searchView.FindViewById<ImageView>(searchBtnId);

            var upId = GetId("up");
            var upIcon = searchView.FindViewById<ImageView>(upId);

            // Tint text
            searchText?.SetTextColor(white);
            searchText?.SetHintTextColor(ColorStateList.ValueOf(white));

            // Tint icons
            magIcon?.SetColorFilter(white);
            closeBtn?.SetColorFilter(white);

            goIcon?.SetColorFilter(white);
            searchButtonIcon?.SetColorFilter(white);
            upIcon?.SetColorFilter(white);

            // Remove underline
            plate?.SetBackgroundColor(Android.Graphics.Color.Transparent);
        }

        #endregion


        #region REMOVE SEARCH

        public static void RemoveSearchView()
        {
            var toolbar = GetRealToolbar();
            if (toolbar == null) return;

            toolbar.Menu.Clear();
        }

        #endregion
    }
}
