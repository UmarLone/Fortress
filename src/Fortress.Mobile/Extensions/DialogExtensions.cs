
using Fortress.Mobile.Core.Models;

namespace Fortress.Extensions
{
    public static class PageExtensions
    {
        public static async Task TraverseNavigationRecursivelyAsync(this Page page, Func<Page, Task> actionOnPage)
        {
            if (page?.Navigation?.ModalStack != null)
            {
                foreach (var p in page.Navigation.ModalStack)
                {
                    if (p is NavigationPage modalNavPage)
                    {
                        await TraverseNavigationStackRecursivelyAsync(modalNavPage.CurrentPage, actionOnPage);
                    }
                    else
                    {
                        await TraverseNavigationStackRecursivelyAsync(p, actionOnPage);
                    }
                }
            }

            await TraverseNavigationStackRecursivelyAsync(page, actionOnPage);
        }

        private static async Task TraverseNavigationStackRecursivelyAsync(this Page page, Func<Page, Task> actionOnPage)
        {
            if (page is MultiPage<Page> multiPage && multiPage.Children != null)
            {
                foreach (var p in multiPage.Children)
                {
                    await TraverseNavigationStackRecursivelyAsync(p, actionOnPage);
                }
            }

            if (page is NavigationPage && page.Navigation != null)
            {
                if (page.Navigation.NavigationStack != null)
                {
                    foreach (var p in page.Navigation.NavigationStack)
                    {
                        await TraverseNavigationStackRecursivelyAsync(p, actionOnPage);
                    }
                }
            }

            await actionOnPage(page);
        }
    }
    public static class DialogExtensions
    {

        //public static IDisposable ToastError(this IUserDialogs userDialogs, string message, TimeSpan duration)
        //{
        //    var config = new ToastConfig(message)
        //    {
        //        BackgroundColor = Color.FromHex("#e35130"),
        //        MessageTextColor = Color.White,
        //        Duration = duration,
        //        Icon = "error.png",
        //    };
        //    return userDialogs.Toast(config);
        //}
        //public static IDisposable ToastSuccess(this IUserDialogs userDialogs, string message, TimeSpan duration)
        //{
        //    var config = new ToastConfig(message)
        //    {
        //        BackgroundColor = Color.FromHex("#39bd76"),
        //        MessageTextColor = Color.White,
        //        Duration = duration,
        //        Icon = "check.png"

        //    };
        //    return userDialogs.Toast(config);
        //}

        //public static IDisposable ToastWarning(this IUserDialogs userDialogs, string message, TimeSpan duration)
        //{
        //    var config = new ToastConfig(message)
        //    {
        //        BackgroundColor = Color.FromHex("#e89e1e"),
        //        MessageTextColor = Color.White,
        //        Duration = duration,
        //        Icon = "warning.png"
        //    };
        //    return userDialogs.Toast(config);
        //}
        //public static IDisposable ToastInfo(this IUserDialogs userDialogs, string message, TimeSpan duration)
        //{
        //    var config = new ToastConfig(message)
        //    {
        //        BackgroundColor = Color.FromHex("#39abbd"),
        //        MessageTextColor = Color.White,
        //        Duration = duration,
        //        Icon = "info.png"
        //    };
        //    return userDialogs.Toast(config);
        //}
    }
}
