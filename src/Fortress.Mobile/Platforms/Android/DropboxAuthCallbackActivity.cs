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
        DataScheme = "db-dxopwdvjvdc9kt4",
  DataPath = "/2/token")]
    public class DropboxAuthCallbackActivity : WebAuthenticatorCallbackActivity
 {
        protected override void OnNewIntent(Intent? intent)
   {
            base.OnNewIntent(intent);
   Intent = intent;
        }
    }
}
