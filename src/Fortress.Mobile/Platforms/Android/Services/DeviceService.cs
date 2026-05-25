using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Common;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Views.Autofill;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Bit.Droid.Autofill;
using Controls.UserDialogs.Maui;

// Firebase.Messaging removed — push notifications not in use
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Droid.Utilities;
using ZXing;
using ZXing.Common;
using static Android.Provider.Settings;
using Application = Android.App.Application;

namespace Fortress.Droid.Services
{
    public class DeviceService : IDeviceServices
    {
        private ApplicationState ApplicationState { get; set; }
        private Toast _toast;
        private readonly IUserDialogs _dialogService;
        private readonly Lazy<PendingIntent> _clearClipboardPendingIntent;
        private readonly IAppInfo _appInfo;
        public DeviceService(IUserDialogs dialogService, IAppInfo appInfo)
        {
            _dialogService = dialogService;
            _appInfo = appInfo;

            _clearClipboardPendingIntent = new Lazy<PendingIntent>(() =>
               PendingIntent.GetBroadcast(Application.Context,
                                          0,
                                          new Intent(Application.Context, typeof(ClearClipboardAlarmReceiver)),
                                          AndroidHelpers.AddPendingIntentMutabilityFlag(PendingIntentFlags.UpdateCurrent, false)));
        }
        public ApplicationState GetApplicationState() => ApplicationState;


        public string GetAppVersion() 
        {
            try
            {
              return  _appInfo.VersionString;
            }
            catch (Exception)
            {

                
            }
          return  string.Empty;
        }
        public bool AutofillAccessibilityServiceRunning()
        {
            var enabledServices = Secure.GetString(Application.Context.ContentResolver,
                Secure.EnabledAccessibilityServices);
            return Application.Context.PackageName != null &&
                   (enabledServices?.Contains(Application.Context.PackageName) ?? false);
        }
        public async Task<bool> LaunchApp(string appName)
        {
            appName = appName.Replace("androidapp://", string.Empty);
           return await Launcher.OpenAsync(appName);
        }
        public async Task OpenAppStore()
        {
            string playStoreUri = $"market://details?id={AutofillConstants.AppPackageName}";

            await Launcher.OpenAsync(new Uri(playStoreUri));

        }
        public void CloseApplication()
        {
            try
            {
                var activity = Platform.CurrentActivity;
                Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
            }
            catch (Exception ex)
            {

            }

        }
        public void DisableAutofillService()
        {
            try
            {
                var activity = Platform.CurrentActivity;
                var type = Java.Lang.Class.FromType(typeof(AutofillManager));
                var manager = activity.GetSystemService(type) as AutofillManager;
                manager.DisableAutofillServices();
            }
            catch { }
        }
        public void OpenAccessibilitySettings()
        {
            try
            {
                var activity = Platform.CurrentActivity;
                var intent = new Intent(ActionAccessibilitySettings);
                activity.StartActivity(intent);
            }
            catch { }
        }
        public void OpenAutofillSettings()
        {
            var activity = Platform.CurrentActivity;
            try
            {
                var intent = new Intent(ActionRequestSetAutofillService);
                intent.SetData(Android.Net.Uri.Parse("package:com.fortress.app"));
                activity.StartActivity(intent);
            }
            catch (ActivityNotFoundException)
            {
                var alertBuilder = new AlertDialog.Builder(activity);
                alertBuilder.SetMessage("Enabled Autofill for Fortress");
                alertBuilder.SetCancelable(true);
                alertBuilder.SetPositiveButton("OK", (sender, args) =>
                {
                    (sender as AlertDialog)?.Cancel();
                });
                alertBuilder.Create().Show();
            }
        }
        public void OpenAccessibilityOverlayPermissionSettings()
        {
            var activity = Platform.CurrentActivity;
            try
            {
                var intent = new Intent(ActionManageOverlayPermission);
                intent.SetData(Android.Net.Uri.Parse("package:com.fortress.app"));
                activity.StartActivity(intent);
            }
            catch (ActivityNotFoundException)
            {
                // can't open overlay permission management, fall back to app settings
                var intent = new Intent(ActionApplicationDetailsSettings);
                intent.SetData(Android.Net.Uri.Parse("package:com.fortress.app"));
                activity.StartActivity(intent);
            }
            catch
            {
                var alertBuilder = new AlertDialog.Builder(activity);
                alertBuilder.SetMessage("Go to Autofill Settings");
                alertBuilder.SetCancelable(true);
                alertBuilder.SetPositiveButton("OK", (sender, args) =>
                {
                    (sender as AlertDialog)?.Cancel();
                });
                alertBuilder.Create().Show();
            }
        } 

        public async Task CopyToClipboard(string value, string message, int expiresInMs = -1, bool isSensitive = true)
        {
            try
            {
                // Xamarin.Essentials.Clipboard currently doesn't support the IS_SENSITIVE flag for API 33+
                if ((int)Build.VERSION.SdkInt < 33)
                {
                    await Clipboard.SetTextAsync(value);
                }
                else
                {
                    CopyToClipboard(value, isSensitive);
                }

                if (expiresInMs > 0 && CanUseAlarm())
                    ClearClipboardAlarmAsync(expiresInMs);
            }
            catch (Java.Lang.SecurityException ex) when (ex.Message.Contains("does not belong to"))
            {
                // #1962 Just ignore, the content is copied either way but there is some app interfering in the process
                // that the OS catches and just throws this exception.
            }
        }
        private void CopyToClipboard(string text, bool isSensitive = true)
        {
            var clipboardManager = Application.Context.GetSystemService(Context.ClipboardService) as ClipboardManager;
            var clipData = ClipData.NewPlainText("FORTRESS", text);
            if (isSensitive)
            {
                clipData.Description.Extras ??= new PersistableBundle();
                clipData.Description.Extras.PutBoolean("android.content.extra.IS_SENSITIVE", true);
            }
            clipboardManager.PrimaryClip = clipData;
        }
        private void ClearClipboardAlarmAsync(int expiresInSeconds)
        {
            if (expiresInSeconds <= 0)
            {
                return;
            }

            // Calculate the trigger time in seconds from the current time
            var triggerSeconds = Java.Lang.JavaSystem.CurrentTimeMillis() / 1000 + expiresInSeconds;

            var alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;

            // Set the alarm using triggerSeconds
            alarmManager.Set(AlarmType.Rtc, triggerSeconds * 1000, _clearClipboardPendingIntent.Value);
        }
        public void SetScreenCaptureAllowed(bool isAllowed)
        {
            var activity = Platform.CurrentActivity;
            if (isAllowed)
            {
                activity.RunOnUiThread(() => activity.Window.ClearFlags(WindowManagerFlags.Secure));
                return;
            }
            activity.RunOnUiThread(() => activity.Window.AddFlags(WindowManagerFlags.Secure));
        }
       
         
        public void Toast(string text, bool longDuration = false)
        {
            if (_toast != null)
            {
                _toast.Cancel();
                _toast.Dispose();
                _toast = null;
            }
            _toast = Android.Widget.Toast.MakeText(Platform.CurrentActivity, text,
                longDuration ? ToastLength.Long : ToastLength.Short);
            _toast.Show();
        }
        public bool AutofillServiceEnabled(out bool isPackageNameCorrect)
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                isPackageNameCorrect = false;
                return false;
            }
            try
            {
                var activity = Platform.CurrentActivity;
                var afm = (AutofillManager)activity.GetSystemService(
                    Java.Lang.Class.FromType(typeof(AutofillManager)));
                isPackageNameCorrect = afm.AutofillServiceComponentName!=null && afm.AutofillServiceComponentName.PackageName == AutofillConstants.AppPackageName;

                return afm.IsEnabled && afm.HasEnabledAutofillServices;
            }
            catch (Exception ex)
            {
                isPackageNameCorrect = false;

                return false;
            }
        }

        public string DecodeQrCodeImage(string filePath)
        {
            Bitmap bitmap = BitmapFactory.DecodeFile(filePath);
            byte[] rgbBytes = GetRgbBytes(bitmap);

            var bin = new HybridBinarizer(new RGBLuminanceSource(rgbBytes, bitmap.Width, bitmap.Height));
            var i = new BinaryBitmap(bin);
            var reader = new MultiFormatReader();
            var result = reader.decode(i);

            if (result != null)
            {
                return result.Text;
            }
            return null;
        }

        private static byte[] GetRgbBytes(Bitmap image)
        {
            var rgbBytes = new List<byte>();
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var c = new Android.Graphics.Color(image.GetPixel(x, y));
                    rgbBytes.AddRange(new[] { c.R, c.G, c.B });
                }
            }
            return rgbBytes.ToArray();
        }


        public bool SupportsAutofillService()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                return false;
            }
            try
            {
                var activity = Platform.CurrentActivity;
                var type = Java.Lang.Class.FromType(typeof(AutofillManager));
                var manager = activity.GetSystemService(type) as AutofillManager;
                return manager.IsAutofillSupported;
            }
            catch
            {
                return false;
            }
        }
        public bool IsNotificationSupported()
        {
            return GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(Application.Context) == ConnectionResult.Success;
        }


        public void SetApplicationState(ApplicationState applicationState) => ApplicationState = applicationState;

        public async Task<bool> VerifyStoragePermissions()
        {
            try
            {
                var storageRead = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                var storageWrite = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if ((storageRead == PermissionStatus.Denied || storageRead == PermissionStatus.Disabled) ||
                    (storageWrite == PermissionStatus.Denied || storageWrite == PermissionStatus.Disabled))
                {
                    var confirmResult = await _dialogService.ConfirmAsync("We need storage access to save and read the data", "Storage Permissions", "Yes", "No");
                    if (!confirmResult)
                    {
                        return false;
                    }
                    var readState = await Permissions.RequestAsync<Permissions.StorageRead>();
                    var writeState = await Permissions.RequestAsync<Permissions.StorageWrite>();
                    if (readState != PermissionStatus.Granted || writeState != PermissionStatus.Granted)
                    {
                        return false;
                    }
                }
                return true;

            }
            catch (System.Exception)
            {

            }
            return false;
        }
        public void PlayDefaultNotificationSound()
        {
            var uri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            var ringtone = RingtoneManager.GetRingtone(Android.App.Application.Context, uri);
            ringtone.Play();
        }
        public async Task<bool> VerifyCameraPermissions()
        {
            try
            {
                var cameraPermission = await Permissions.CheckStatusAsync<Permissions.Camera>();


                if (cameraPermission == PermissionStatus.Denied || cameraPermission == PermissionStatus.Disabled)
                {

                    var cameraState = await Permissions.RequestAsync<Permissions.Camera>();
                    return cameraState == PermissionStatus.Granted;
                }
                return cameraPermission == PermissionStatus.Granted;
            }
            catch (System.Exception)
            {
            }
            return false;
        }
        public Task<bool> VerifyAlarmPermissions()
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                {
                    var alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
                    if (!alarmManager.CanScheduleExactAlarms())
                    {

                        var intent = new Intent();
                        intent.SetAction("android.settings.REQUEST_SCHEDULE_EXACT_ALARM");
                        intent.AddCategory(Intent.CategoryDefault);
                        intent.SetData(Android.Net.Uri.Parse("package:" + Application.Context.PackageName));
                        intent.SetFlags(ActivityFlags.NewTask);
                        Application.Context.StartActivity(intent);
                        return Task.FromResult(false);
                    }
                    return Task.FromResult(true);
                }

                //var activity = CrossCurrentActivity.Current.Activity as FormsAppCompatActivity;

                //// Check if permission is not granted, then request it
                //if (ContextCompat.CheckSelfPermission(CrossCurrentActivity.Current.AppContext, Manifest.Permission.ScheduleExactAlarm) != Permission.Granted)
                //{
                //    ActivityCompat.RequestPermissions(activity, new String[] { Manifest.Permission.ScheduleExactAlarm }, 3);
                //    return Task.FromResult(false); // Permission not yet granted, wait for user response
                //}

                //// Permission already granted
                return Task.FromResult(false);
            }
            catch (System.Exception ex)
            {
                // Log or handle the exception
                Console.WriteLine("Exception occurred while verifying alarm permissions: " + ex);
                return Task.FromResult(false); // Return false indicating permission not granted due to exception
            }
        }
        private bool CanUseAlarm()
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                {
                    var alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
                    return alarmManager.CanScheduleExactAlarms();
                }
            }
            catch (Exception)
            {

                
            }
            return false;
        }
        public async Task<bool> VerifyNetworkPermissions()
        {
            try
            {
                var networkPermission = await Permissions.CheckStatusAsync<Permissions.NetworkState>();
                if (networkPermission == PermissionStatus.Denied || networkPermission == PermissionStatus.Disabled)
                {
                    var confirmResult = await _dialogService.ConfirmAsync("We need access to internet in order to check updates", "Internet Permissions", "Yes", "No");
                    if (!confirmResult)
                    {
                        return false;
                    }
                    var networkState = await Permissions.RequestAsync<Permissions.NetworkState>();
                    if (networkState != PermissionStatus.Granted)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (System.Exception)
            {
            }
            return false;
        }
        public async Task<bool> VerifyMediaPermissions()
        {
            try
            {
                var mediaPermission = await Permissions.CheckStatusAsync<Permissions.Media>();

                if (mediaPermission == PermissionStatus.Denied || mediaPermission == PermissionStatus.Disabled)
                {
                    var confirmResult = await _dialogService.ConfirmAsync("We need access to media to read file", "Internet Permissions", "Yes", "No");
                    if (!confirmResult)
                    {
                        return false;
                    }
                    var networkState = await Permissions.RequestAsync<Permissions.Media>();
                    if (networkState != PermissionStatus.Granted)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (System.Exception)
            {
            }
            return false;
        }
        public Task<bool> VerifyNotificationPermissions()
        {
            try
            {
                var activity = Platform.CurrentActivity;

                if (ContextCompat.CheckSelfPermission(activity, Manifest.Permission.PostNotifications) != Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(activity, new String[] { Manifest.Permission.PostNotifications }, 3);
                    return Task.FromResult(false);
                }
                return Task.FromResult(true);
            }
            catch (System.Exception)
            {
            }
            return Task.FromResult(false);
        }

        public IEnumerable<string> GetInstalledAppNames()
        {
            var installedApplications = Android.App.Application.Context.PackageManager.GetInstalledApplications(PackageInfoFlags.MetaData);
            return installedApplications.Where(app => (app.Flags & ApplicationInfoFlags.System) == 0).Select(x => x.PackageName);
        }

        /// <summary>
        /// Opens Android 14+ Credential Provider settings so the user can
        /// choose FORTRESS as the default passkey provider.
        /// Falls back to app settings on older Android versions.
        /// </summary>
        public void OpenCredentialProviderSettings()
        {
            try
            {
                var activity = Platform.CurrentActivity;
                if ((int)Build.VERSION.SdkInt >= 34) // Android 14 = API 34
                {
                    var intent = new Intent("android.settings.CREDENTIAL_PROVIDER");
                    intent.AddFlags(ActivityFlags.NewTask);
                    activity.StartActivity(intent);
                }
                else
                {
                    // Android 9-13: Autofill service settings is the closest equivalent
                    var intent = new Intent(ActionRequestSetAutofillService);
                    intent.SetData(Android.Net.Uri.Parse("package:com.fortress.app"));
                    activity.StartActivity(intent);
                }
            }
            catch (ActivityNotFoundException)
            {
                // Ultimate fallback: open app details
                var intent = new Intent(ActionApplicationDetailsSettings);
                intent.SetData(Android.Net.Uri.Parse("package:com.fortress.app"));
                Platform.CurrentActivity.StartActivity(intent);
            }
            catch { }
        }

        public bool IsBluetoothEnabled()
        {
            BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            return bluetoothAdapter.IsEnabled;
        }

        public bool EnableBluetooth()
        {
            BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            if (bluetoothAdapter.IsEnabled)
                return true;

            return bluetoothAdapter.Enable();
        }

        public Task<string> RequestPushToken()
        {
    // Firebase/push removed — not supported
  return Task.FromResult(string.Empty);
        }

        public Task UnregisterPush()
        {
      // Firebase/push removed — not supported
          return Task.CompletedTask;
        }
    }

}