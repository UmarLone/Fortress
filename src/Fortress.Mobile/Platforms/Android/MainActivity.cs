using Android.App;
using Android.App.Assist;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Autofill;
using Bit.Droid.Autofill;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Droid.Utilities;
using Newtonsoft.Json;
using Plugin.LocalNotification;
using System.Diagnostics;
using Shiny;
namespace Fortress.Mobile
{
    [Activity(Theme = "@style/MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, Label = "Fortress", ScreenOrientation = ScreenOrientation.Portrait,
           ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    [Register("com.fortress.app.MainActivity")]
    [IntentFilter(
      new[]
        {
  Platform.Intent.ActionAppAction
        },
        Categories = new[]
        {
            Intent.CategoryDefault
        }
    )]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataMimeType = "application/octet-stream",
      DataPathPattern = ".*\\.fortress")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "content",
     DataMimeType = "*/*",
        DataPathPattern = ".*\\.fortress")]
    [IntentFilter(
   new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "file",
        DataMimeType = "*/*",
      DataPathPattern = ".*\\.fortress")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            Intent?.Validate();
            base.OnCreate(savedInstanceState);
            //if (!PreferenceWrapper.Instance.IsScreenCaptureEnabled)
            //    Window.AddFlags(WindowManagerFlags.Secure);
            //else
                Window.ClearFlags(WindowManagerFlags.Secure);

            HandleIntent(Intent);

        }


        private void HandleIntent(Intent intent)
        {
            if (intent == null) return;

            // ── .fortress file opened from another app ────────────────────────
            if (intent.Action == Intent.ActionView && intent.Data != null)
      {
     HandleFortressFileIntent(intent);
       return;
         }

       var isAutofill = intent.GetBooleanExtra("autofill", false);
            var isAddNew   = intent.GetBooleanExtra("autofillFrameworkAddNew", false);
            var isGenerator = intent.GetBooleanExtra("generatorTile", false);

            // ── Password Generator tile ───────────────────────────────────────
            if (isGenerator)
            {
                Shiny.Hosting.Host.GetService<IEventAggregator>()
                    .GetEvent<PasswordGeneratorRequestEvent>()
                    .Publish(string.Empty);
                return;
            }

            // ── Add-new-credential from autofill context ──────────────────────
            if (isAddNew)
            {
                // The domain that the autofill service was requested for
                var domain   = intent.GetStringExtra("autofillFrameworkName") ?? string.Empty;
                var uri      = intent.GetStringExtra("autofillFrameworkUri")  ?? string.Empty;
                var fillType = (CipherType)intent.GetIntExtra("autofillFrameworkFillType", (int)CipherType.Login);

                // Login save fields
                var username = intent.GetStringExtra("autofillFrameworkUsername") ?? string.Empty;
                var password = intent.GetStringExtra("autofillFrameworkPassword") ?? string.Empty;

                // Card save fields (only populated when fillType == Card)
                var cardName     = intent.GetStringExtra("autofillFrameworkCardName")   ?? string.Empty;
                var cardNumber   = intent.GetStringExtra("autofillFrameworkCardNumber")   ?? string.Empty;
                var cardExpMonth = intent.GetStringExtra("autofillFrameworkCardExpMonth") ?? string.Empty;
                var cardExpYear  = intent.GetStringExtra("autofillFrameworkCardExpYear")  ?? string.Empty;
                var cardCode     = intent.GetStringExtra("autofillFrameworkCardCode")     ?? string.Empty;

                var ea = Shiny.Hosting.Host.GetService<IEventAggregator>();
                ea.GetEvent<AutofillPasswordsEvent>().Publish(new RequestingApplication
                {
                    IsFillContext    = false,
                    IsAddOrSaveContext = true,
                    Name         = domain,
                    Package    = uri,
                    FillType       = fillType,
                    Username       = username,
                    Password     = password,
                    // Card fields carried through for the save path
                    CardName     = cardName,
                    CardNumber   = cardNumber,
                    CardExpMonth = cardExpMonth,
                    CardExpYear  = cardExpYear,
                    CardCode     = cardCode,
                });
                return;
            }

            if (isAutofill)
            {
                var json = intent.GetStringExtra("request");
                var request = JsonConvert.DeserializeObject<RequestingApplication>(json);
                Shiny.Hosting.Host.GetService<IEventAggregator>()
                    .GetEvent<AutofillPasswordsEvent>().Publish(request);
            }
        }

        private async void HandleFortressFileIntent(Intent intent)
        {
            try
      {
      var uri = intent.Data;
  if (uri == null) return;

     // Copy the content URI to a temp file the app can read
              var tempPath = System.IO.Path.Combine(FileSystem.CacheDirectory, "received.fortress");
   using (var input = ContentResolver?.OpenInputStream(uri))
     {
     if (input == null) return;
          using var output = System.IO.File.Create(tempPath);
     await input.CopyToAsync(output);
       }

        // Navigate to ReceiveItemPage with the file path
          await MainThread.InvokeOnMainThreadAsync(async () =>
      {
    var nav = Shiny.Hosting.Host.GetService<Prism.Navigation.INavigationService>();
     await nav.NavigateAsync(
    $"/{nameof(Microsoft.Maui.Controls.NavigationPage)}/{nameof(Fortress.Views.HomePage)}/{nameof(Fortress.Views.ReceiveItemPage)}",
            new Prism.Navigation.NavigationParameters { { "filePath", tempPath } });
    });
          }
        catch (Exception ex)
     {
System.Diagnostics.Debug.WriteLine($"HandleFortressFileIntent error: {ex.Message}");
        }
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            if (intent == null)
                return;
           
            HandleIntent(intent);
            LocalNotificationCenter.NotifyNotificationTapped(intent);
             
        }
        public override async void OnBackPressed()
        {
            if (BottomSheetManager.IsSheetOpen)
            {
                var sheet = BottomSheetManager.CurrentSheet;

                if (sheet != null)
                {
                    await sheet.DismissAsync();
                    BottomSheetManager.Clear();
                    return;
                }
            }
            base.OnBackPressed();
        }
        public override void OnRequestPermissionsResult(
            int requestCode,
            string[] permissions,
            [GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
