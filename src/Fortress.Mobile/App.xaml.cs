using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Mappers;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress;
using Fortress.ViewModels;
using Fortress.Views;

namespace Fortress.Mobile
{
    public partial class App : Application
    {
        private System.Timers.Timer _timeoutTimer;
        private readonly bool IsAutofillContext;
        private readonly bool IsNotificationContext;
        private Func<Task> _pendingAction;
        private readonly HashSet<string> _processedNotificationActions = new();
        private readonly object _notificationLock = new();

        public App(bool isAutofillContext = false, bool isNotificationContext = false)
        {
            IsAutofillContext = isAutofillContext;
            IsNotificationContext = isNotificationContext;
            this.InitializeComponent();
            Initialize();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (PreferenceWrapper.Instance.FollowSystemTheme)
            {
                var systemTheme = Application.Current?.RequestedTheme;
                if (systemTheme == AppTheme.Dark)
                    Application.Current.Resources.ApplyDarkTheme();
                else
                    Application.Current.Resources.ApplyLightTheme();
                Fortress.ViewModels.CredentialFilterChip.InvalidateResourceCache();
                return;
            }

            var theme = PreferenceWrapper.Instance.AppTheme;
            if (theme == "Dark")
                Application.Current.Resources.ApplyDarkTheme();
            else
                Application.Current.Resources.ApplyLightTheme();
            Fortress.ViewModels.CredentialFilterChip.InvalidateResourceCache();
        }

        public static void SetTheme(string theme)
        {
            PreferenceWrapper.Instance.AppTheme = theme;
            if (theme == "Dark")
                Application.Current.Resources.ApplyDarkTheme();
            else
                Application.Current.Resources.ApplyLightTheme();
            Fortress.ViewModels.CredentialFilterChip.InvalidateResourceCache();
        }

        public static void ApplySystemTheme()
        {
            var systemTheme = Application.Current?.RequestedTheme;
            if (systemTheme == AppTheme.Dark)
                Application.Current.Resources.ApplyDarkTheme();
            else
                Application.Current.Resources.ApplyLightTheme();
            Fortress.ViewModels.CredentialFilterChip.InvalidateResourceCache();
        }

        #region System 

        protected async override void OnStart()
        {
            base.OnStart();

#if IOS
            // Process any credentials the iOS extension queued for saving
            // while the main app was closed (e.g. user tapped the save prompt
            // inside the extension and the queue was written before the app opened).
            _ = ProcessiOSPendingSavesAsync();
#endif

            if (!IsAutofillContext && !IsNotificationContext)
            {
                if (!PreferenceWrapper.Instance.HasSetupCompleted)
                {
                    var result = await Shiny.Hosting.Host.GetService<INavigationService>()
                       .NavigateAsync($"/{nameof(NavigationPage)}/{nameof(OnboardingPage)}");
                    if (!result.Success)
                    {
                        Shiny.Hosting.Host.GetService<ILogger<App>>().LogError(
                       result.Exception,
                       "Navigation to OnboardingPage failed");
                    }
                    return;
                }

                // Setup is complete — lock the app on every cold start if a lock
                // method is configured, then let CheckAuthentication prompt unlock.
                if (PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
                        PreferenceWrapper.Instance.IsPinUnlockEnabled)
                {
                    PreferenceWrapper.Instance.IsApplicationLocked = true;
                    SyncLockStateToExtension();

                    // Tell UnlockService this is a cold start so it uses absolute
                    // navigation to UnlockPage (not a modal) and navigates to HomePage
                    // inside the unlock callback — no gap for the OS to background the app.
                    Shiny.Hosting.Host.GetService<IUnlockService>().MarkColdStart();
                }

                await CheckAuthentication(true);
            }
        }

        protected async void Initialize()
        {
            // InitializeComponent() already called in constructor - remove duplicate
            var deviceInfo = Shiny.Hosting.Host.GetService<IDeviceServices>();
            deviceInfo.SetApplicationState(ApplicationState.Foreground);

            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
            var ea = Shiny.Hosting.Host.GetService<IEventAggregator>();
            ea.GetEvent<PasswordGeneratorRequestEvent>().Subscribe(OnShowPasswordGenerator);
            ea.GetEvent<AppUnlockedEvent>().Subscribe(OnAppUnlockedAsync);
            ea.GetEvent<AutofillPasswordsEvent>().Subscribe(OnAutofillRequest);
        }


        protected async override void OnResume()
        {
            base.OnResume();
            _timeoutTimer?.Stop();
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
            Shiny.Hosting.Host.GetService<IDeviceServices>().SetApplicationState(ApplicationState.Foreground);
            Shiny.Hosting.Host.GetService<IEventAggregator>().GetEvent<ApplicationStateChangeEvent>().Publish(ApplicationState.Foreground);
            if (PreferenceWrapper.Instance.PreventLocking)
                return;

            await CheckAuthentication(false);
        }
        protected override void OnSleep()
        {
            base.OnSleep();
            Connectivity.ConnectivityChanged -= Connectivity_ConnectivityChanged;
            Shiny.Hosting.Host.GetService<IDeviceServices>().SetApplicationState(ApplicationState.Background);
            Shiny.Hosting.Host.GetService<IEventAggregator>().GetEvent<ApplicationStateChangeEvent>().Publish(ApplicationState.Background);

            // If "Lock on Background" is on, lock immediately without waiting for the timer
            if (PreferenceWrapper.Instance.LockOnBackground &&
                  (PreferenceWrapper.Instance.IsBiometricUnlockEnabled || PreferenceWrapper.Instance.IsPinUnlockEnabled))
            {
                PreferenceWrapper.Instance.IsApplicationLocked = true;
                SyncLockStateToExtension();
                return;  // Skip timer-based locking
            }

            LockApplicationIfNeeded();
        }

        #endregion


        #region Events


        private async void OnAutofillRequest(RequestingApplication request)
        {
            //  NavigationService.InitiailizePageStack();

            async Task Redirect()
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var type = request.Package?.Contains("https") == true ||
                        request.Package?.Contains("http") == true
                     ? CredentialType.Web
                       : CredentialType.PhoneApplication;

                    // ── Add / Save context ────────────────────────────────────────
                    if (request.IsAddOrSaveContext)
                    {
                        var navParams = new NavigationParameters
              {
         { "domain",        request.Name     ?? string.Empty },
        { "username",   request.Username ?? string.Empty },
        { "password",        request.Password ?? string.Empty },
         { "finishAfterSave", true }
  };

                        if (request.FillType == Fortress.Mobile.Core.Models.CipherType.Card)
                        {
                            // Pre-populate card fields captured from the save request
                            if (!string.IsNullOrEmpty(request.CardName))
                                navParams.Add("cardName", request.CardName);
                            if (!string.IsNullOrEmpty(request.CardNumber))
                                navParams.Add("cardNumber", request.CardNumber);
                            if (!string.IsNullOrEmpty(request.CardExpMonth))
                                navParams.Add("cardExpMonth", request.CardExpMonth);
                            if (!string.IsNullOrEmpty(request.CardExpYear))
                                navParams.Add("cardExpYear", request.CardExpYear);
                            if (!string.IsNullOrEmpty(request.CardCode))
                                navParams.Add("cardCode", request.CardCode);

                            await Shiny.Hosting.Host.GetService<INavigationService>()
                                     .NavigateAsync(
                               $"/{nameof(NavigationPage)}/{nameof(HomePage)}/{nameof(AddEditCreditCardPage)}",
                               navParams);
                        }
                        else if (request.FillType == Fortress.Mobile.Core.Models.CipherType.Identity)
                        {
                            await Shiny.Hosting.Host.GetService<INavigationService>()
                     .NavigateAsync(
                            $"/{nameof(NavigationPage)}/{nameof(HomePage)}/{nameof(AddEditIdentityPage)}",
                  navParams);
                        }
                        else
                        {
                            var storage = Shiny.Hosting.Host.GetService<IDataStorageService>();
                            var existing = await storage.GetLoginItemAsync(x =>
                           x.Url.Contains(request.Name ?? string.Empty) &&
                !string.IsNullOrEmpty(x.Username) &&
                !string.IsNullOrEmpty(request.Username) &&
                         x.Username.ToLower() == request.Username.ToLower());

                            if (existing != null)
                                navParams.Add("credential", LoginItemMapper.Map(existing));

                            await Shiny.Hosting.Host.GetService<INavigationService>()
                                .NavigateAsync(
                           $"/{nameof(NavigationPage)}/{nameof(HomePage)}/{nameof(AddEditCredentialPage)}",
                         navParams);
                        }
                        return;
                    }

                    // ── Fill context ──────────────────────────────────────────────
                    {
                        var storage = Shiny.Hosting.Host.GetService<IDataStorageService>();
                        var filler = await storage.GetLoginItemAsync(x =>
                     x.Url.Contains(request.Name ?? string.Empty) &&
                       !string.IsNullOrEmpty(x.Username) &&
                                     !string.IsNullOrEmpty(request.Username) &&
                           x.Username.ToLower() == request.Username.ToLower());

                        var navP = new NavigationParameters
               {
     { "type",       type            },
       { "packageName",     request.Package },
   { "finishAfterSave", false     },
    { "username",   request.Username },
                  { "password",      request.Password }
      };

                        if (filler != null)
                            navP.Add("Credential", LoginItemMapper.Map(filler));
                    }
                });
            }

            // Save pending action
            _pendingAction = async () => await Redirect();

            // Authenticate
            bool authenticated = await AuthenticateAsync();
            if (!authenticated)
            {
                _pendingAction = null;
                return;
            }

            // User is authenticated and unlocked → run Autofill immediately
            var action = _pendingAction;
            _pendingAction = null;
            if (action != null)
                await action();
        }
        private async void OnAppUnlockedAsync(bool _)
        {

            if (_pendingAction != null)
            {
                var action = _pendingAction;
                _pendingAction = null;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await action();
                });
                return;
            }
            //if (_pendingPush == null && _pendingAction == null)
            //{
            //    await MainThread.InvokeOnMainThreadAsync(async () =>
            //    {
            //        await Shiny.Hosting.Host.GetService<INavigationService>().NavigateAsync($"/{nameof(NavigationPage)}/{nameof(HomePage)}");
            //    });
            //}
        }
        private async void OnShowPasswordGenerator(string obj)
            => await Shiny.Hosting.Host.GetService<INavigationService>()
    .NavigateAsync($"/{nameof(NavigationPage)}/{nameof(HomePage)}/{nameof(PasswordGeneratorPage)}");


        private async void OnProfileDeleted(string message)
        {
            var vm = Shiny.Hosting.Host.GetService<MenuPageViewModel>();
            Shiny.Hosting.Host.GetService<IDeviceServices>().CloseApplication();
        }
        #endregion

        #region Authentication
        private async Task<bool> AuthenticateAsync()
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var unlockService = Shiny.Hosting.Host.GetService<IUnlockService>();

                if (PreferenceWrapper.Instance.IsApplicationLocked &&
     (PreferenceWrapper.Instance.IsBiometricUnlockEnabled || PreferenceWrapper.Instance.IsPinUnlockEnabled))
                {
                    bool unlocked = await unlockService.RequestUnlockAsync();
                    if (!unlocked)
                        return false;
                }
                return true;
            });
        }

        /// <summary>
        /// Checks whether the user needs to authenticate and optionally
        /// navigates to the home page.
        /// 
        /// <paramref name="redirect"/>:
        ///   true  = cold start → navigate to HomePage after unlock.
        /// false = resume from background → just unlock (modal overlay),
        ///     the user returns to wherever they were.
        /// </summary>
        private async Task CheckAuthentication(bool redirect)
        {
            bool authenticated = await AuthenticateAsync();
            if (!authenticated)
                return;

            if (redirect)
            {
// Cold start: The UnlockService already navigated to HomePage
           // directly from its ContinueWith callback when it detected a
      // cold-start unlock. Only navigate here if we somehow aren't
    // on HomePage yet (e.g. no lock was configured).
         var mainPage = Application.Current?.MainPage;
         bool alreadyOnHome = mainPage is NavigationPage np &&
          np.CurrentPage is HomePage;
     if (!alreadyOnHome)
           {
           await Shiny.Hosting.Host.GetService<INavigationService>()
            .NavigateAsync($"/{nameof(NavigationPage)}/{nameof(HomePage)}?AutoFillCheck=true");
}
      }
       // On resume (redirect == false): the unlock modal was popped by
    // UnlockService, restoring the previous page. No navigation needed
// unless the stack is completely empty (shouldn't happen normally).
            else
  {
        var mainPage = Application.Current?.MainPage;
          var stack = mainPage?.Navigation?.NavigationStack;
     if (mainPage is null || stack is null || stack.Count == 0)
                {
        await Shiny.Hosting.Host.GetService<INavigationService>()
        .NavigateAsync($"/{nameof(NavigationPage)}/{nameof(HomePage)}");
  }
   }
      }

        private void LockApplicationIfNeeded()
        {
            // 1) If user has no unlock mechanism, don't lock via timer at all.
            if (!PreferenceWrapper.Instance.IsBiometricUnlockEnabled &&
                !PreferenceWrapper.Instance.IsPinUnlockEnabled)
                return;

            // 2) Minimum lock timeout is 1 minute (60 s). Any stored value below
            //    that (e.g. the old "Immediately" = 0) is clamped up to 60 s.
            int timeoutSeconds = Math.Max(60, PreferenceWrapper.Instance.LockTimeout);

            // 3) Cancel any prior timer before creating a new one.
            try
            {
                _timeoutTimer?.Stop();
                _timeoutTimer?.Dispose();
            }
            catch { /* swallow */ }
            finally
            {
                _timeoutTimer = null;
            }

            _timeoutTimer = new System.Timers.Timer(timeoutSeconds * 1000.0)
            {
                AutoReset = false
            };

            _timeoutTimer.Elapsed += (s, e) =>
            {
                // Runs on a background thread; we're only toggling a flag, so this is safe.
                PreferenceWrapper.Instance.IsApplicationLocked = true;
                SyncLockStateToExtension();

                try
                {
                    _timeoutTimer?.Stop();
                    _timeoutTimer?.Dispose();
                }
                catch { /* ignore */ }
                finally
                {
                    _timeoutTimer = null;
                }
            };

            _timeoutTimer.Start();
        }

        /// <summary>
        /// Syncs the lock state to shared storage for iOS extension access
        /// </summary>
        private void SyncLockStateToExtension()
        {
            try
            {
                var sharedCredentialWriter = Shiny.Hosting.Host.GetService<ISharedCredentialWriter>();
                sharedCredentialWriter?.SyncLockStateToSharedPreferences();
            }
            catch
            {
                // Service not available on this platform - ignore
            }
        }

        #endregion

        private void Connectivity_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            try
            {
                // Internet connection state - Hub connection event removed
                Shiny.Hosting.Host.GetService<IEventAggregator>().GetEvent<InternetConnectionStateEvent>().Publish(e.NetworkAccess == NetworkAccess.Internet);
            }
            catch { }
        }

#if IOS
      /// <summary>
        /// Reads the pending-save queue written by the iOS autofill extension
        /// (<c>SharedCredentialStorage.PendingSavesFileName</c>) and opens the
        /// Add/Edit credential page for each queued item so the user can confirm.
        /// The queue is cleared after processing.
        /// </summary>
  private async Task ProcessiOSPendingSavesAsync()
        {
         try
       {
  var sharedWriter = Shiny.Hosting.Host.GetService<ISharedCredentialWriter>();
         if (sharedWriter == null) return;

         var pending = sharedWriter.GetPendingSaveCredentials();
      if (pending == null || pending.Count == 0) return;

      // Authenticate before showing any save UI
      bool authenticated = await AuthenticateAsync();
   if (!authenticated) return;

     foreach (var item in pending)
                {
 await MainThread.InvokeOnMainThreadAsync(async () =>
          {
            var navParams = new NavigationParameters
         {
    { "domain",   item.Domain   },
{ "username", item.Username },
 { "password", item.Password }
      };
        await Shiny.Hosting.Host.GetService<INavigationService>()
                   .NavigateAsync(
            $"/{nameof(NavigationPage)}/{nameof(HomePage)}/{nameof(AddEditCredentialPage)}",
    navParams);
           });

           // One save dialog at a time — break after first so the user
      // is not spammed. Remaining items are processed on next launch.
   break;
      }

     // Remove the item we just handed to the UI
       sharedWriter.ClearFirstPendingSaveCredential();
     }
 catch (Exception ex)
       {
  Shiny.Hosting.Host.GetService<ILogger<App>>()
      ?.LogError(ex, "ProcessiOSPendingSavesAsync failed");
}
        }
#endif
    }
}
