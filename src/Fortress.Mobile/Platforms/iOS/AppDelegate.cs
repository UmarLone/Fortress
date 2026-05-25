using Foundation;
using UIKit;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Services;

namespace Fortress.Mobile
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        private bool? _lastKnownAutofillStatus;
        
        protected override MauiApp CreateMauiApp()
            => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            // Configure navigation bar appearance globally for plain back button
            ConfigureNavigationBarAppearance();
            
            // Cache initial autofill status
            try
            {
                var credentialWriter = Shiny.Hosting.Host.GetService<ISharedCredentialWriter>();
                _lastKnownAutofillStatus = credentialWriter?.IsAutofillEnabled();
            }
            catch
            {
                _lastKnownAutofillStatus = null;
            }
            
            return base.FinishedLaunching(application, launchOptions);
        }
        public override void WillTerminate(UIApplication application)
        {
            base.WillTerminate(application);

            if (PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
                     PreferenceWrapper.Instance.IsPinUnlockEnabled)
            {
                PreferenceWrapper.Instance.IsApplicationLocked = true;
                var sharedCredentialWriter = Shiny.Hosting.Host.GetService<ISharedCredentialWriter>();
                sharedCredentialWriter?.SyncLockStateToSharedPreferences();
            }
        }
        public override void WillEnterForeground(UIApplication application)
        {
            base.WillEnterForeground(application);
            
            // Check if autofill status changed while app was in background
            CheckAutofillStatusChange();
        }
        
        private void CheckAutofillStatusChange()
        {
            try
            {
                var credentialWriter = Shiny.Hosting.Host.GetService<ISharedCredentialWriter>();
                if (credentialWriter == null) return;
                
                var currentStatus = credentialWriter.IsAutofillEnabled();
                
                // If status changed, publish event
                if (_lastKnownAutofillStatus != currentStatus)
                {
                    Console.WriteLine($"[AppDelegate] Autofill status changed: {_lastKnownAutofillStatus} -> {currentStatus}");
                    _lastKnownAutofillStatus = currentStatus;
                    
                    var eventAggregator = Shiny.Hosting.Host.GetService<Prism.Events.IEventAggregator>();
                    eventAggregator?.GetEvent<AutofillStatusChangedEvent>().Publish(currentStatus);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppDelegate] CheckAutofillStatusChange error: {ex.Message}");
            }
        }
        
        private void ConfigureNavigationBarAppearance()
        {
            // Set global appearance for navigation bar
            var appearance = new UINavigationBarAppearance();
            appearance.ConfigureWithOpaqueBackground();
            
            // Make back button show only arrow (no text)
            var backAppearance = new UIBarButtonItemAppearance(UIBarButtonItemStyle.Plain);
            backAppearance.Normal.TitleTextAttributes = new NSDictionary<NSString, NSObject>(
                new NSString[] { UIStringAttributeKey.ForegroundColor },
                new NSObject[] { UIColor.Clear }
            );
            appearance.BackButtonAppearance = backAppearance;
            
            // Apply globally
            UINavigationBar.Appearance.StandardAppearance = appearance;
            UINavigationBar.Appearance.ScrollEdgeAppearance = appearance;
            UINavigationBar.Appearance.CompactAppearance = appearance;
            UINavigationBar.Appearance.TintColor = UIColor.White;
            
            // Also set BackButtonDisplayMode to minimal by default
            // This removes the circle/pill background on iOS 14+
            if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
            {
                UIBarButtonItem.Appearance.SetBackButtonBackgroundImage(
                    new UIImage(), 
                    UIControlState.Normal, 
                    UIBarMetrics.Default);
            }
        }

        [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
        public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
            => global::Shiny.Hosting.Host.Lifecycle.OnRegisteredForRemoteNotifications(deviceToken);

        [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
        public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
            => global::Shiny.Hosting.Host.Lifecycle.OnFailedToRegisterForRemoteNotifications(error);

        [Export("application:didReceiveRemoteNotification:fetchCompletionHandler:")]
        public void DidReceiveRemoteNotification(UIApplication application, NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
            => global::Shiny.Hosting.Host.Lifecycle.OnDidReceiveRemoteNotification(userInfo, completionHandler);

        [Export("application:openURL:options:")]
        public bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            try
            {
                Console.WriteLine($"[AppDelegate] OpenUrl called with: {url}");

                // Handle .fortress file opens
                if (url.IsFileUrl && url.PathExtension?.ToLower() == "fortress")
                {
                    HandleFortressFile(url);
                    return true;
                }

                if (url.Scheme?.ToLower() == "fortress")
                {
                    HandleFortressUrl(url);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppDelegate] OpenUrl error: {ex.Message}");
            }
            
            return false;
        }

        private async void HandleFortressFile(NSUrl url)
        {
            try
            {
                // Copy to app sandbox so we can read it after the URL is released
                var tempPath = System.IO.Path.Combine(FileSystem.CacheDirectory, "received.fortress");
                var sourcePath = url.Path;
                if (string.IsNullOrEmpty(sourcePath)) return;

                // Start secure access for files from outside the sandbox
                var accessed = url.StartAccessingSecurityScopedResource();
                try
                {
                    System.IO.File.Copy(sourcePath, tempPath, overwrite: true);
                }
                finally
                {
                    if (accessed) url.StopAccessingSecurityScopedResource();
                }

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
                Console.WriteLine($"[AppDelegate] HandleFortressFile error: {ex.Message}");
            }
        }

        private void HandleFortressUrl(NSUrl url)
        {
            try
            {
                var host = url.Host ?? string.Empty;
                var query = url.Query ?? string.Empty;
                
                Console.WriteLine($"[AppDelegate] Handling Fortress URL - Host: {host}, Query: {query}");
                
                // Parse query parameters
                var queryParams = ParseQueryString(query);
                
                var request = new RequestingApplication
                {
                    Package = queryParams.GetValueOrDefault("domain", string.Empty),
                    Name = queryParams.GetValueOrDefault("name", string.Empty),
                    Username = queryParams.GetValueOrDefault("username", string.Empty),
                    Password = queryParams.GetValueOrDefault("password", string.Empty)
                };
                
                // Determine action based on URL
                var action = queryParams.GetValueOrDefault("action", host);
                
                switch (action.ToLower())
                {
                    case "add":
                    case "save":
                        request.IsAddOrSaveContext = true;
                        request.IsFillContext = false;
                        break;
                    case "unlock":
                    case "autofill":
                    default:
                        request.IsFillContext = true;
                        request.IsAddOrSaveContext = false;
                        break;
                }
                
                Console.WriteLine($"[AppDelegate] Publishing AutofillPasswordsEvent - Action: {action}, Domain: {request.Package}");
                
                // Publish event for the app to handle
                var eventAggregator = Shiny.Hosting.Host.GetService<Prism.Events.IEventAggregator>();
                eventAggregator?.GetEvent<AutofillPasswordsEvent>().Publish(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppDelegate] HandleFortressUrl error: {ex.Message}");
            }
        }
        
        private Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            if (string.IsNullOrEmpty(query)) return result;
            
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = Uri.UnescapeDataString(keyValue[0]);
                    var value = Uri.UnescapeDataString(keyValue[1]);
                    result[key] = value;
                }
            }
            
            return result;
        }
    }
}
