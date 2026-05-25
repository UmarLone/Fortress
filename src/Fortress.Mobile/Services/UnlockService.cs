using Fortress.Mobile.Core.Contracts;
using Fortress.ViewModels;
using Fortress.Views;

namespace Fortress.Services
{
    /// <summary>
    /// Manages the app lock/unlock lifecycle.
    ///
    /// The UnlockPage is pushed as a **modal** on top of whatever page the
    /// user was on. When unlock succeeds the modal is dismissed and the user
    /// is returned to exactly where they were — no context is lost.
    ///
    /// On a cold start (no existing page) we navigate directly to HomePage
    /// from within the unlock callback to avoid a race where the OS can
    /// background the app between TCS completion and the caller's navigation.
    /// </summary>
    public sealed class UnlockService : IUnlockService
    {
private readonly INavigationService _navigation;
        private readonly ILogger<UnlockService> _logger;

        private readonly SemaphoreSlim _gate = new(1, 1);
        private TaskCompletionSource<bool>? _unlockTcs;
        private volatile bool _isShowingUnlock;

        /// <summary>
        /// Set by MarkColdStart() before the first unlock request so that
        /// ShowUnlockPageAsync uses absolute navigation instead of a modal,
        /// and the ContinueWith navigates directly to HomePage without any gap.
        /// </summary>
        private volatile bool _coldStartPending;

        /// <summary>
        /// True when the unlock page was shown via absolute navigation
        /// (cold start) rather than as a modal. In that case we navigate
      /// to HomePage directly instead of popping a modal.
        /// </summary>
  private volatile bool _isColdStartNavigation;

        public UnlockService(INavigationService navigation, ILogger<UnlockService> logger)
     {
      _navigation = navigation;
         _logger = logger;
   }

     // ─── Public API ─────────────────────────────────────────────────────

        public void MarkColdStart() => _coldStartPending = true;

        public async Task<bool> RequestUnlockAsync(bool forceRefresh = false)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
  try
          {
 // Already showing — piggyback on the existing TCS.
   if (_isShowingUnlock && !forceRefresh &&
         _unlockTcs is { Task.IsCompleted: false })
    {
        return await _unlockTcs.Task.ConfigureAwait(false);
   }

          // Always create a *fresh* TCS (a completed one must never be reused).
            _unlockTcs = new TaskCompletionSource<bool>(
              TaskCreationOptions.RunContinuationsAsynchronously);
    _isShowingUnlock = true;
            }
            finally
        {
        _gate.Release();
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
{
           await ShowUnlockPageAsync(forceRefresh);
        }
          catch (Exception ex)
          {
    _logger?.LogError(ex, "Unlock navigation failed");
      Complete(false);
        }
          });

            return await _unlockTcs.Task.ConfigureAwait(false);
     }

        // ─── Show the page ──────────────────────────────────────────────────

        private async Task ShowUnlockPageAsync(bool forceRefresh)
        {
 var mainPage = Application.Current?.MainPage;

            // ── 1. Cold start — App.OnStart called MarkColdStart() ──────────
            // Prism's CreateWindow navigates to PlaceholderPage before OnStart fires,
            // so mainPage is never null on cold start. We rely on the explicit flag
            // set by App.OnStart rather than inferring from the page type.
            bool coldStart = mainPage is null || _coldStartPending;
            _coldStartPending = false; // consume the flag regardless

            if (coldStart)
            {
    _isColdStartNavigation = true;

    var result = await _navigation.NavigateAsync(
     $"/{nameof(NavigationPage)}/{nameof(UnlockPage)}");

     if (!result.Success)
                {
         _logger?.LogError(result.Exception,
"Cold-start navigation to UnlockPage failed");
   Complete(false);
   return;
       }

                await WireUpViewModelAsync();
       return;
 }

            _isColdStartNavigation = false;

       // ── 2. Already showing the UnlockPage ───────────────────────────
            if (IsUnlockPageVisible())
{
           if (forceRefresh)
       {
        await DismissUnlockPageAsync();
       // Fall through to push a fresh one.
         }
        else
     {
     // Re-wire in case the VM was recycled by Prism.
          await WireUpViewModelAsync();
    return;
           }
       }

    // ── 3. Push UnlockPage as a modal ───────────────────────────────
       var modalParams = new NavigationParameters
   {
    { KnownNavigationParameters.UseModalNavigation, true }
    };

       var navResult = await _navigation.NavigateAsync(
 nameof(UnlockPage), modalParams);

 if (!navResult.Success)
            {
        _logger?.LogWarning(navResult.Exception,
             "Modal push failed — falling back to absolute navigation");

        _isColdStartNavigation = true;
          navResult = await _navigation.NavigateAsync(
  $"/{nameof(NavigationPage)}/{nameof(UnlockPage)}");

       if (!navResult.Success)
            {
       _logger?.LogError(navResult.Exception,
     "Absolute fallback also failed");
         Complete(false);
        return;
   }
      }

            await WireUpViewModelAsync();
        }

        // ─── Wire up the ViewModel's TCS ────────────────────────────────────

        private async Task WireUpViewModelAsync()
        {
         UnlockPageViewModel? vm = null;

     // Give Prism a few frames to finish wiring the BindingContext.
    for (int i = 0; i < 15 && vm is null; i++)
  {
  vm = FindUnlockViewModel();
      if (vm is null)
        await Task.Delay(50);
         }

    if (vm?.UnlockResult is null)
            {
    _logger?.LogWarning(
              "UnlockPageViewModel not found after retries — completing false");
      Complete(false);
            return;
         }

            // Capture whether this was a cold-start at the time we wired up,
            // so the ContinueWith closure uses the correct value.
            bool wasColdStart = _isColdStartNavigation;

_ = vm.UnlockResult.Task.ContinueWith(async t =>
    {
      bool success = t.IsCompletedSuccessfully && t.Result;

       if (success && wasColdStart)
  {
     // Cold start: navigate to HomePage directly on the main
     // thread so there is no gap for the OS to background the
        // app between TCS completion and navigation.
 await MainThread.InvokeOnMainThreadAsync(async () =>
      {
  try
          {
          var navResult = await _navigation.NavigateAsync(
            $"/{nameof(NavigationPage)}/{nameof(HomePage)}?AutoFillCheck=true");
 if (!navResult.Success)
     _logger?.LogError(navResult.Exception,
"Cold-start post-unlock navigation to HomePage failed");
     }
          catch (Exception ex)
     {
     _logger?.LogError(ex,
              "Cold-start post-unlock navigation threw");
  }
      });
    }
     else if (success)
                {
    // Resume: pop the modal so the user lands back where they were.
        await MainThread.InvokeOnMainThreadAsync(DismissUnlockPageAsync);
 }

    Complete(success);
    }, TaskScheduler.Default);
      }

  // ─── Dismiss helpers ────────────────────────────────────────────────

        private async Task DismissUnlockPageAsync()
        {
      try
      {
var mainPage = Application.Current?.MainPage;

            // Check modals first — Prism wraps them in a NavigationPage.
      if (mainPage?.Navigation?.ModalStack?.Count > 0)
   {
        var topModal = mainPage.Navigation.ModalStack[^1];
          if (IsUnlockPage(topModal))
      {
          // Use Prism's GoBack first — it handles its own wrapper.
      var goBackResult = await _navigation.GoBackAsync();
    if (goBackResult.Success)
        return;

_logger?.LogWarning(goBackResult.Exception,
      "Prism GoBack failed — trying raw PopModalAsync");
          await mainPage.Navigation.PopModalAsync(animated: false);
               return;
          }
                }

         // Not a modal — absolute navigation fallback.
             if (mainPage is NavigationPage navPage &&
                  navPage.CurrentPage is UnlockPage)
  {
   await _navigation.NavigateAsync(
    $"/{nameof(NavigationPage)}/{nameof(HomePage)}");
        }
       }
            catch (Exception ex)
 {
                _logger?.LogWarning(ex, "DismissUnlockPageAsync error — forcing HomePage");
     try
     {
     await _navigation.NavigateAsync(
             $"/{nameof(NavigationPage)}/{nameof(HomePage)}");
       }
      catch { /* last resort — avoid crash */ }
            }
        }

        // ─── State management ───────────────────────────────────────────────

        private void Complete(bool result)
        {
            _unlockTcs?.TrySetResult(result);
    _isShowingUnlock = false;
        }

      // ─── Detection helpers ──────────────────────────────────────────────

        private static bool IsUnlockPageVisible()
        {
    var mainPage = Application.Current?.MainPage;
  if (mainPage is null) return false;

 if (mainPage.Navigation?.ModalStack?.Count > 0)
{
       var topModal = mainPage.Navigation.ModalStack[^1];
      if (IsUnlockPage(topModal)) return true;
            }

          if (mainPage is NavigationPage nav)
   return nav.CurrentPage is UnlockPage;

        return mainPage is UnlockPage;
        }

        private static bool IsUnlockPage(Page page) => page switch
   {
      UnlockPage => true,
    NavigationPage np => np.CurrentPage is UnlockPage,
            _ => false,
        };


    private static UnlockPageViewModel? FindUnlockViewModel()
    {
            var mainPage = Application.Current?.MainPage;
  if (mainPage is null) return null;

            // 1. Modals (most likely after a modal push).
      if (mainPage.Navigation?.ModalStack?.Count > 0)
            {
    var topModal = mainPage.Navigation.ModalStack[^1];

       if (topModal is NavigationPage mnp && mnp.CurrentPage is UnlockPage mu)
   return mu.BindingContext as UnlockPageViewModel;

           if (topModal is UnlockPage mu2)
           return mu2.BindingContext as UnlockPageViewModel;
            }

          // 2. Navigation stack (cold-start absolute nav).
        if (mainPage is NavigationPage nav && nav.CurrentPage is UnlockPage su)
    return su.BindingContext as UnlockPageViewModel;

            // 3. Direct page.
          if (mainPage is UnlockPage du)
                return du.BindingContext as UnlockPageViewModel;

      return null;
  }
    }
}
