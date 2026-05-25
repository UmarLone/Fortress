namespace Fortress.Mobile.Adapters
{
    /// <summary>
    /// Marker interface — VMs that implement this are initialized AFTER ShowAsync
    /// (e.g. voice sheet that starts the microphone in InitializeAsync).
    /// </summary>
    public interface IPostShowInitialize { }

    public class BottomSheetService : IBottomSheetService
    {
        private readonly IServiceProvider _services;

        public BottomSheetService(IServiceProvider services)
        {
            _services = services;
        }

        public Task<T> ShowAsync<TSheet, TViewModel, T>(object args = null, string title = null)
      where TSheet : The49.Maui.BottomSheet.BottomSheet
      where TViewModel : BottomSheetViewModelBase
        {
            var tcs = new TaskCompletionSource<T>();
            var sheet = _services.GetRequiredService<TSheet>();
            var vm = _services.GetRequiredService<TViewModel>();

            sheet.BindingContext = vm;
            BottomSheetManager.SetCurrent(sheet);
            bool resultReturned = false;

            sheet.Dismissed += (s, e) =>
            {
                if (!resultReturned && !tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(default(T));
                }
                BottomSheetManager.Clear();
            };

            vm.DismissAction = async () =>
            {
                await sheet.DismissAsync();
            };

            vm.ReturnResult = r =>
            {
                resultReturned = true;
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult((T)r);
            };

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // For sheets that start async work in InitializeAsync (e.g. voice listening),
                // we must show the sheet first, then initialize. For data-bound menu sheets
                // we initialize first so ContentDetent measures the correct height.
                // The VM signals which mode it needs via IPostShowInitialize.
                bool postShow = vm is IPostShowInitialize;

                if (!postShow)
                {
                    try
                    {
                        await vm.InitializeAsync(args, title);
                    }
                    catch (Exception ex)
                    {
                        if (!tcs.Task.IsCompleted)
                            tcs.TrySetException(ex);
                        return;
                    }

                    // Force a layout pass so ContentDetent measures the fully-populated
                    // BindableLayout before ShowAsync is called.
                    sheet.Layout(new Rect(0, 0, Application.Current.MainPage.Width, double.PositiveInfinity));
                }

                await sheet.ShowAsync();

                if (postShow)
                {
                    try
                    {
                        await vm.InitializeAsync(args, title);
                    }
                    catch (Exception ex)
                    {
                        if (!tcs.Task.IsCompleted)
                            tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }


    }

    public static class BottomSheetManager
    {
        public static bool IsSheetOpen { get; private set; }
        public static The49.Maui.BottomSheet.BottomSheet CurrentSheet { get; private set; }

        public static void SetCurrent(The49.Maui.BottomSheet.BottomSheet sheet)
        {
            CurrentSheet = sheet;
            IsSheetOpen = sheet != null;
        }

        public static void Clear()
        {
            CurrentSheet = null;
            IsSheetOpen = false;
        }
    }

}
