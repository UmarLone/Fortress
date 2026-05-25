using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace Fortress.Platforms.Android
{
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
        DataScheme = "com.googleusercontent.apps.830156471419-ib3to0vdg1fvle1lct48l2sp92ksohmv",
        DataPath = "/oauth2redirect")]
    public class GoogleAuthCallbackActivity : WebAuthenticatorCallbackActivity
    {
        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            Intent = intent;
        }
    }
}