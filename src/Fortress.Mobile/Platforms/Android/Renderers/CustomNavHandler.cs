
using Android.Runtime;
using Android.Widget;
using Fortress.Mobile.Core.Contracts;
using Google.Android.Material.AppBar;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Microsoft.Maui.Handlers;

namespace Fortress.Mobile.Platforms.Android.Renderers
{
    public class CustomNavHandler : ToolbarHandler
    {
        MaterialToolbar? _toolbar;

        protected override void ConnectHandler(MaterialToolbar platformView)
        {
            base.ConnectHandler(platformView);

            _toolbar = platformView;

            // 🔥 Fix ALL toolbar icons (back arrow, overflow, menu icons)
            TintBackArrow(platformView);
            platformView.SetNavigationIconTint(Resource.Color.white);
            platformView.SetTitleTextColor(Resource.Color.white);
            TryAddSearchToToolbar(platformView);
        }

        protected override void DisconnectHandler(MaterialToolbar platformView)
        {
            RemoveSearchFromToolbar(platformView);
            _toolbar = null;

            base.DisconnectHandler(platformView);
        }

        private void TryAddSearchToToolbar(MaterialToolbar toolbar)
        {
            if (VirtualView is NavigationPage navPage &&
                navPage.CurrentPage is ISearchPage)
            {
                AddSearchToToolbar(toolbar);
            }
        }

        private void AddSearchToToolbar(MaterialToolbar toolbar)
        {
            toolbar.Menu?.Clear();
            toolbar.InflateMenu(Resource.Menu.MainMenu);

            // 🔥 Tint menu icons white
            for (int i = 0; i < toolbar.Menu.Size(); i++)
            {
                var item1 = toolbar.Menu.GetItem(i);
                item1.Icon?.SetTint(Resource.Color.white);
            }

            var item = toolbar.Menu?.FindItem(Resource.Id.ActionSearch);
            var searchView = item?.ActionView?.JavaCast<SearchView>();

            if (searchView != null)
            {
                // 🔥 Fix search icon inside SearchView
                FixSearchViewTint(searchView);

                searchView.QueryTextChange += OnQueryTextChange;
            }
        }

        private void FixSearchViewTint(SearchView searchView)
        {
            var textViewId = searchView.Context.Resources
                .GetIdentifier("android:id/search_src_text", null, null);
            var textView = searchView.FindViewById<EditText>(textViewId);

            if (textView != null)
            {
                textView.SetTextColor(ColorExtensions.ToAndroid(Color.FromArgb("#fff")));
                textView.SetHintTextColor(ColorExtensions.ToAndroid(Color.FromArgb("#fff")));
            }

            var iconId = searchView.Context.Resources
                .GetIdentifier("android:id/search_mag_icon", null, null);
            var magIcon = searchView.FindViewById<ImageView>(iconId);

            if (magIcon != null)
            {
                magIcon.SetColorFilter(ColorExtensions.ToAndroid(Color.FromArgb("#fff")));
            }

            var closeId = searchView.Context.Resources
                .GetIdentifier("android:id/search_close_btn", null, null);
            var closeIcon = searchView.FindViewById<ImageView>(closeId);

            if (closeIcon != null)
            {
                closeIcon.SetColorFilter(ColorExtensions.ToAndroid(Color.FromArgb("#fff")));
            }
        }
        private void TintBackArrow(MaterialToolbar toolbar)
        {
            try
            {
                var navIcon = toolbar.NavigationIcon;

                if (navIcon != null)
                {
                    navIcon.SetTint(ColorExtensions.ToAndroid(Color.FromArgb("#fff")));
                    toolbar.NavigationIcon = navIcon; // <- important: re-assign!
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Back arrow tint failed: " + ex);
            }
        }
        private void RemoveSearchFromToolbar(MaterialToolbar toolbar)
        {
            var item = toolbar.Menu?.FindItem(Resource.Id.ActionSearch);
            var searchView = item?.ActionView?.JavaCast<SearchView>();

            if (searchView != null)
                searchView.QueryTextChange -= OnQueryTextChange;

            toolbar.Menu?.Clear();
        }

        private void OnQueryTextChange(object? sender, SearchView.QueryTextChangeEventArgs e)
        {
            if (VirtualView is ISearchPage page)
                page.OnSearchBarTextChanged(e?.NewText ?? string.Empty);
        }
    }
}
