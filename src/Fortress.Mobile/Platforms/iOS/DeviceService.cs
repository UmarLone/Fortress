using Controls.UserDialogs.Maui;
using AudioToolbox;
using Foundation;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using UIKit;

namespace Fortress.iOS.Services
{
    public class iOSDeviceService : IDeviceServices
    {
        private ApplicationState ApplicationState { get; set; }
        private int _lastClipboardChangeCount;
        private nint _clipboardBackgroundTaskId;
        private NSTimer _clipboardTimer;
        private readonly IAppInfo _appInfo;
        private readonly IPlatform _platform;
        public iOSDeviceService(IPlatform platform, IAppInfo appInfo)
        {
            _platform = platform;
            _appInfo = appInfo;
        }
        public void PlayDefaultNotificationSound()
        {
            // This plays the default system sound for notifications
            SystemSound.FromFile("/System/Library/Audio/UISounds/sms-received1.caf").PlaySystemSound();

            // Or simply use the standard alert sound
            SystemSound.Vibrate.PlaySystemSound();
        }
        public void CloseApplication()
        {
            try
            {
                Thread.CurrentThread.Abort();
            }
            catch (Exception)
            {
            }
        }
        public bool AutofillAccessibilityServiceRunning()
        {
            // iOS doesn't have accessibility-based autofill like Android
            // Return true as AutoFill is handled differently on iOS
            return true;
        }

        public bool AutofillServiceEnabled(out bool isPackageNameCorrect)
        {
            isPackageNameCorrect = true;
            
            // iOS doesn't provide a direct API to check if our AutoFill extension is enabled
            // We can only check if the extension bundle exists, not if it's enabled in Settings
            // The user must manually enable it in Settings > Passwords > Password Options > AutoFill Passwords
            
            // Check if our autofill extension bundle exists (it's bundled with the app)
            // This just verifies the extension is included, not that it's enabled
            try
            {
                var mainBundle = NSBundle.MainBundle;
                var bundlePath = mainBundle.BundlePath;
                
                if (!string.IsNullOrEmpty(bundlePath))
                {
                    var plugInsPath = System.IO.Path.Combine(bundlePath, "PlugIns");
                    var autofillExtensionPath = System.IO.Path.Combine(plugInsPath, "Fortress.iPhone.Autofill.appex");
                    if (System.IO.Directory.Exists(autofillExtensionPath))
                    {
                       return Shiny.Hosting.Host.GetService<ISharedCredentialWriter>().IsAutofillEnabled();
                         
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking autofill extension: {ex.Message}");
                return false;
            }
        }

        public async Task CopyToClipboard(string value, string message, int expiresInMs = -1, bool isSensitive = true)
        {
            try
            {
                // Set text to clipboard
                await Clipboard.SetTextAsync(value);
                Toast(message); // Display toast message

                // Return if no expiration is set
                if (expiresInMs <= 0)
                {
                    return;
                }

                // Handle background task for clipboard clearing
                HandleBackgroundTask();

                // expiresInMs is actually seconds (from PreferenceWrapper.ClearClipboardTimeout)
                // Convert to milliseconds for the timer
                StartClearClipboardTimer(expiresInMs * 1000);
            }
            catch (Exception ex)
            {
                // Handle or log the exception as needed
                Console.WriteLine($"Error in CopyToClipboard: {ex.Message}");
            }
        }

        private void HandleBackgroundTask()
        {
            if (_clipboardBackgroundTaskId > 0)
            {
                UIApplication.SharedApplication.EndBackgroundTask(_clipboardBackgroundTaskId);
            }
            _clipboardBackgroundTaskId = UIApplication.SharedApplication.BeginBackgroundTask(() =>
            {
                UIApplication.SharedApplication.EndBackgroundTask(_clipboardBackgroundTaskId);
                _clipboardBackgroundTaskId = 0;
            });
        }

        private void StartClearClipboardTimer(int expiresInMs)
        {
            _clipboardTimer?.Invalidate();
            _clipboardTimer = NSTimer.CreateScheduledTimer(TimeSpan.FromMilliseconds(expiresInMs), timer =>
            {
                 MainThread.BeginInvokeOnMainThread( () =>
                {
                    ClearClipboardIfUnchanged();
                });
            });
        }

        private void ClearClipboardIfUnchanged()
        {
            var currentClipboardChangeCount = UIPasteboard.General.ChangeCount;
            if (currentClipboardChangeCount == 0 || _lastClipboardChangeCount == currentClipboardChangeCount)
            {
                UIPasteboard.General.String = string.Empty;
            }
            CleanUpTimerAndBackgroundTask();
        }

        private void CleanUpTimerAndBackgroundTask()
        {
            _clipboardTimer?.Invalidate();
            _clipboardTimer = null;
            if (_clipboardBackgroundTaskId > 0)
            {
                UIApplication.SharedApplication.EndBackgroundTask(_clipboardBackgroundTaskId);
                _clipboardBackgroundTaskId = 0;
            }
        }

        public string DecodeQrCodeImage(string filePath)
        {
            return string.Empty;
        }

        public void DisableAutofillService()
        {

        }

        public bool EnableBluetooth()
        {
            return true;
        }

        public ApplicationState GetApplicationState() => ApplicationState;
        public string GetAppVersion() => _appInfo.VersionString;


        public IEnumerable<string> GetInstalledAppNames()
        {
            // Return an empty list as a default value
            return Enumerable.Empty<string>();
        }


         

        public bool IsBluetoothEnabled()
        {
            // Return a default value indicating Bluetooth is not enabled
            return false;
        }

        public bool IsNotificationSupported()
        {
            // Return a default value indicating notifications are not supported
            return false;
        }

        public bool LaunchApp(string appName)
        {
            // Return a default value indicating app launch is not supported
            return false;
        }
        public async Task OpenAppStore()
        {
            string appStoreUrl = $"itms-apps://itunes.apple.com/app/id1368062993";
            try
            {
                await Launcher.OpenAsync(new Uri(appStoreUrl));
            }
            catch (Exception ex)
            {

            }
        }
        public void OpenAccessibilityOverlayPermissionSettings()
        {
            // Provide a default implementation or do nothing
        }

        public void OpenAccessibilitySettings()
        {
            // iOS doesn't have separate accessibility settings for autofill
            // Open general settings instead
            OpenAppSettings();
        }

        // public void OpenAutofillSettings()
        // {
        //     try
        //     {
        //         // On iOS 16+, the AutoFill Passwords settings are at:
        //         // Settings > Passwords > Password Options
        //         // Try multiple URL schemes as Apple's private URL schemes can change
                
        //         // Try the Password Options path first (where AutoFill providers are enabled)
        //         var urlStrings = new[]
        //         {
        //             "App-Prefs:PASSWORDS&path=PASSWORD_OPTIONS",  // iOS 17+ Password Options
        //             "prefs:root=PASSWORDS&path=PASSWORD_OPTIONS", // Alternative format
        //             "App-Prefs:PASSWORDS",                         // Passwords main screen
        //             "prefs:root=PASSWORDS",                        // Alternative Passwords
        //         };
                
        //         foreach (var urlString in urlStrings)
        //         {
        //             var url = new NSUrl(urlString);
        //             if (url != null && UIApplication.SharedApplication.CanOpenUrl(url))
        //             {
        //                 UIApplication.SharedApplication.OpenUrl(url, new NSDictionary(), (success) =>
        //                 {
        //                     if (!success)
        //                     {
        //                         Console.WriteLine($"Failed to open URL: {urlString}");
        //                     }
        //                 });
        //                 return;
        //             }
        //         }
                
        //         // If none of the password URLs work, open app settings
        //         // User will need to navigate: Settings > Passwords > Password Options > AutoFill Passwords
        //         Console.WriteLine("Could not open Passwords settings directly, opening app settings");
        //         OpenAppSettings();
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"Failed to open autofill settings: {ex.Message}");
        //         OpenAppSettings();
        //     }
        // }
       public void OpenAutofillSettings()
    {
        // Apple has blocked direct access to Password settings via URL schemes in iOS 17+/18+
        // The only reliable approach is to show manual instructions to the user
        ShowManualAutofillInstructions();
    }

      private  void ShowManualAutofillInstructions()
    {
        var alert = UIAlertController.Create(
            "Enable Fortress Autofill",
            "To enable Fortress Autofill:\n\n1. Open Settings\n2. Tap General\n3. Tap Autofill & Passwords\n4. Enable Fortress under \"AutoFill From\"",
            UIAlertControllerStyle.Alert);

        alert.AddAction(UIAlertAction.Create("Open Settings", UIAlertActionStyle.Default, _ =>
        {
            var url = new NSUrl("App-Prefs:");
            UIApplication.SharedApplication.OpenUrl(url, new UIApplicationOpenUrlOptions(), null);
        }));
        
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Cancel, null));

        UIApplication.SharedApplication
            .KeyWindow?
            .RootViewController?
            .PresentViewController(alert, true, null);
    }
        private void OpenAppSettings()
        {
            try
            {
                var url = new NSUrl(UIApplication.OpenSettingsUrlString);
                if (UIApplication.SharedApplication.CanOpenUrl(url))
                {
                    UIApplication.SharedApplication.OpenUrl(url, new NSDictionary(), null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open app settings: {ex.Message}");
            }
        }

        public async Task<string> RequestPushToken()
        {
            CancellationToken cancelToken = new CancellationToken();
            var deviceToken = await RequestRawToken(cancelToken).ConfigureAwait(false);
            var nativeToken = ToPushTokenString(deviceToken);
            // Return a default or placeholder push token
            return nativeToken;
        }
        TaskCompletionSource<NSData> tokenSource;
        protected async Task<NSData> RequestRawToken(CancellationToken cancelToken)
        {
            this.tokenSource = new TaskCompletionSource<NSData>();
            using (var cancelSrc = cancelToken.Register(() => this.tokenSource.TrySetCanceled()))
            {
                await _platform
                   .InvokeOnMainThreadAsync(
                       () => UIApplication
                           .SharedApplication
                           .RegisterForRemoteNotifications()
                   )
                   .ConfigureAwait(false);

                var rawToken = await this.tokenSource.Task.ConfigureAwait(false);
                return rawToken;
            }


        }

        public void SetApplicationState(ApplicationState applicationState) => ApplicationState = applicationState;


        public void SetScreenCaptureAllowed(bool isAllowed)
        {
            // Provide a default implementation or do nothing
        }





        public void Toast(string text, bool longDuration = false)
        {
            // Provide a default implementation for displaying a toast message
            Shiny.Hosting.Host.GetService<IUserDialogs>().ShowToast(text);
        }

        public Task UnregisterPush()
        {
            // Provide a default implementation or do nothing
            return Task.CompletedTask;
        }

        public Task<bool> VerifyBluetoothPermissions()
        {
            // Return a default value indicating Bluetooth permissions are verified
            return Task.FromResult(true);
        }

        public Task<bool> VerifyCameraPermissions()
        {
            // Return a default value indicating camera permissions are verified
            return Task.FromResult(true);
        }

        public Task<bool> VerifyMediaPermissions()
        {
            // Return a default value indicating media permissions are verified
            return Task.FromResult(true);
        }

        public Task<bool> VerifyNetworkPermissions()
        {
            // Return a default value indicating network permissions are verified
            return Task.FromResult(true);
        }

        public Task<bool> VerifyStoragePermissions()
        {
            // Return a default value indicating storage permissions are verified
            return Task.FromResult(true);
        }
        private string ToPushTokenString(NSData deviceToken)
        {
            string token = null;
            if (deviceToken.Length > 0)
            {
                if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
                {
                    var data = deviceToken.ToArray();
                    token = BitConverter
                        .ToString(data)
                        .Replace("-", "")
                        .Replace("\"", "");
                }
                else if (!deviceToken.Description.IsEmpty())
                {
                    token = deviceToken.Description.Trim('<', '>');
                }
            }
            return token;
        }

        public Task<bool> VerifyNotificationPermissions()
        {
            return Task.FromResult(true);
        }

        public Task<bool> VerifyAlarmPermissions()
        {
            return Task.FromResult(true);
        }

        /// <summary>Passkey provider settings — iOS manages this via Settings &gt; Passwords automatically.</summary>
  public void OpenCredentialProviderSettings()
        {
          // On iOS, passkey provider selection is managed by the OS under
  // Settings > Passwords > Password Options > AutoFill From.
      // Reuse the existing autofill settings guidance flow.
            ShowManualAutofillInstructions();
    }

        Task<bool> IDeviceServices.LaunchApp(string appName)
        {
            return Task.FromResult(true);
        }
    }
}