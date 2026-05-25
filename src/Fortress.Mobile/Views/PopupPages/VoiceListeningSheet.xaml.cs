using Fortress.ViewModels.PopupPagesViewModels;

namespace Fortress.Views.PopupPages
{
    public partial class VoiceListeningSheet : The49.Maui.BottomSheet.BottomSheet
    {
        private BoxView? _bar1, _bar2, _bar3, _bar4, _bar5;
        private CancellationTokenSource? _waveCts;

        public VoiceListeningSheet()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                _bar1 = this.FindByName<BoxView>("Bar1");
                _bar2 = this.FindByName<BoxView>("Bar2");
                _bar3 = this.FindByName<BoxView>("Bar3");
                _bar4 = this.FindByName<BoxView>("Bar4");
                _bar5 = this.FindByName<BoxView>("Bar5");

                if (BindingContext is VoiceListeningSheetViewModel vm)
                    vm.PropertyChanged += OnVmPropertyChanged;

                StartWaveformAnimation();
            };

            Dismissed += (_, _) =>
                  {
                      StopWaveformAnimation();
                      if (BindingContext is VoiceListeningSheetViewModel vm)
                      {
                          vm.PropertyChanged -= OnVmPropertyChanged;
                          // If user swipes the sheet away, stop the mic too
                          vm.CancelCommand.Execute();
                      }
                  };
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(VoiceListeningSheetViewModel.IsListening)) return;
            if (BindingContext is VoiceListeningSheetViewModel vm)
            {
                if (vm.IsListening) StartWaveformAnimation();
                else StopWaveformAnimation();
            }
        }

        private void StartWaveformAnimation()
        {
            StopWaveformAnimation();
            if (_bar1 == null) return;

            _waveCts = new CancellationTokenSource();
            var token = _waveCts.Token;

            _ = AnimateBarLoopAsync(_bar1, 400, 0, 14, 36, token);
            _ = AnimateBarLoopAsync(_bar2!, 350, 80, 26, 46, token);
            _ = AnimateBarLoopAsync(_bar3!, 300, 40, 38, 58, token);
            _ = AnimateBarLoopAsync(_bar4!, 350, 120, 26, 46, token);
            _ = AnimateBarLoopAsync(_bar5!, 400, 60, 14, 36, token);
        }

        private void StopWaveformAnimation()
        {
            _waveCts?.Cancel();
            _waveCts = null;
        }

        private static async Task AnimateBarLoopAsync(
            BoxView bar, uint duration, int delayMs,
            double minHeight, double maxHeight,
            CancellationToken token)
        {
            try
            {
                if (delayMs > 0) await Task.Delay(delayMs, token);

                while (!token.IsCancellationRequested)
                {
                    await AnimateHeightAsync(bar, maxHeight, duration, Easing.SinInOut, token);
                    if (token.IsCancellationRequested) break;
                    await AnimateHeightAsync(bar, minHeight, duration, Easing.SinInOut, token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (MainThread.IsMainThread)
                    bar.HeightRequest = minHeight;
                else
                    MainThread.BeginInvokeOnMainThread(() => bar.HeightRequest = minHeight);
            }
        }

        private static Task AnimateHeightAsync(
            BoxView view, double toHeight, uint duration,
            Easing easing, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            MainThread.BeginInvokeOnMainThread(() =>
              {
                  if (token.IsCancellationRequested) { tcs.TrySetCanceled(); return; }

                  var anim = new Animation(
                    v => view.HeightRequest = v,
                    view.HeightRequest,
                    toHeight,
                    easing);

                  anim.Commit(view, "BarWave", length: duration,
                   finished: (_, _) => tcs.TrySetResult(true));
              });

            token.Register(() =>
    {
       MainThread.BeginInvokeOnMainThread(() => view.AbortAnimation("BarWave"));
           tcs.TrySetCanceled();
            });

  return tcs.Task;
   }
    }
}
