using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace Fortress.Platforms.Android
{
    /// <summary>
    /// Receives the OAuth 2.0 redirect from the system browser after the user
    /// signs in with their Microsoft account.  The scheme must exactly match
    /// the RedirectUri registered in the Azure portal and stored in
    /// appsettings.android.json ? OneDrive:RedirectUri.
    ///
    /// Redirect URI: msalae78027d-8c49-46b2-bbae-2e58a527c678://auth
    /// </summary>
    [Activity(
        NoHistory = true,
        LaunchMode = LaunchMode.SingleTop,
        Exported = true)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[]
        {
   Intent.CategoryDefault,
         Intent.CategoryBrowsable
        },
  DataScheme = "msalae78027d-8c49-46b2-bbae-2e58a527c678",
        DataHost = "auth")]
    public class OneDriveAuthCallbackActivity : WebAuthenticatorCallbackActivity
    {
        protected override void OnNewIntent(Intent? intent)
        {
       base.OnNewIntent(intent);
    Intent = intent;
     }
    }
}
