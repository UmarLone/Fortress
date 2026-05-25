using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech;
using Fortress.Mobile.Core.Contracts;
using Microsoft.Extensions.Logging;
using OperationCanceledException = Android.OS.OperationCanceledException;

namespace Fortress.Droid.Services
{
    /// <summary>
    /// Android speech-to-text using the platform SpeechRecognizer API.
    /// Works fully on-device when offline recognition is available; falls back to
    /// Google Speech Services when online — no third-party API key needed.
    /// </summary>
    [Android.Runtime.Preserve(AllMembers = true)]
    public sealed class AndroidSpeechToTextService : Java.Lang.Object, ISpeechToTextService,
        IRecognitionListener
    {
        private readonly ILogger<AndroidSpeechToTextService> _logger;
        private SpeechRecognizer? _recognizer;
        private TaskCompletionSource<string?>? _tcs;
        private TaskCompletionSource<bool>? _readyTcs;   // signals OnReadyForSpeech
        private CancellationTokenRegistration _ctr;
        private Action? _onReady;
        private Action<string>? _onPartialResult;

        public AndroidSpeechToTextService(ILogger<AndroidSpeechToTextService> logger)
        {
            _logger = logger;
        }

        // ── ISpeechToTextService ─────────────────────────────────────────────────

        public async Task<bool> RequestPermissionAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            return status == PermissionStatus.Granted;
        }

        public async Task<string?> ListenAsync(
            CancellationToken cancellationToken = default,
            Action? onReady = null,
            Action<string>? onPartialResult = null)
        {
            _onReady = onReady;
            _onPartialResult = onPartialResult;

            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                _logger.LogError("AndroidSTT: Platform.CurrentActivity is null");
                return null;
            }

            bool available = false;
            await MainThread.InvokeOnMainThreadAsync(
                 () => available = SpeechRecognizer.IsRecognitionAvailable(activity));
            if (!available)
            {
                _logger.LogWarning("AndroidSTT: SpeechRecognizer not available");
                return null;
            }

            _tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _ctr = cancellationToken.Register(() =>
            {
                _logger.LogDebug("AndroidSTT: cancellation requested");
                MainThread.BeginInvokeOnMainThread(() => _recognizer?.StopListening());
                _readyTcs?.TrySetCanceled();
                _tcs?.TrySetResult(null);
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Always use the current Activity — SpeechRecognizer needs a live window token
                _recognizer = SpeechRecognizer.CreateSpeechRecognizer(activity);
                _recognizer!.SetRecognitionListener(this);

                var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel,
                    RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
                intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, 3000L);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, 3000L);
                // Prefer on-device recognition — avoids network permission issues
                intent.PutExtra(RecognizerIntent.ExtraPreferOffline, true);
                intent.PutExtra(RecognizerIntent.ExtraCallingPackage, activity.PackageName);

                _logger.LogInformation("AndroidSTT: StartListening");
                _recognizer.StartListening(intent);
            });

            // Wait for OnReadyForSpeech — reduced to 4 s; fire onReady callback
            // even on timeout so the UI never stays stuck on "Initialising…"
            try
            {
                await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
                _logger.LogInformation("AndroidSTT: mic is live");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("AndroidSTT: ready timeout — firing onReady callback anyway");
                // Fire the callback so the UI shows "Listening…" even if OnReadyForSpeech was late
                if (_onReady != null)
                    MainThread.BeginInvokeOnMainThread(_onReady);
            }
            catch (OperationCanceledException) { /* handled below */ }

            try
            {
                var result = await _tcs.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
                _logger.LogInformation("AndroidSTT: result = \"{T}\"", result ?? "(null)");
                return result;
            }
            catch (TimeoutException)   { _logger.LogWarning("AndroidSTT: 20 s timeout"); return null; }
            catch (OperationCanceledException) { _logger.LogInformation("AndroidSTT: cancelled"); return null; }
            finally
            {
                _ctr.Dispose();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _recognizer?.Destroy();
                    _recognizer = null;
                });
            }
        }

        // ── IRecognitionListener ─────────────────────────────────────────────────

        public void OnReadyForSpeech(Bundle? @params)
        {
            _logger.LogInformation("AndroidSTT: ready for speech — speak now");
           _readyTcs?.TrySetResult(true);
         // Fire the callback on the main thread so the VM can update StatusText safely
         if (_onReady != null)
            MainThread.BeginInvokeOnMainThread(_onReady);
        }

        public void OnBeginningOfSpeech() =>
       _logger.LogInformation("AndroidSTT: 🎤 speech detected");

        public void OnEndOfSpeech() =>
            _logger.LogInformation("AndroidSTT: speech ended");

        public void OnResults(Bundle? results)
        {
            var matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            var text = matches?.FirstOrDefault();
            _logger.LogInformation("AndroidSTT: OnResults → \"{Text}\"", text ?? "(null)");
            _tcs?.TrySetResult(text);
        }

        public void OnError([GeneratedEnum] SpeechRecognizerError error)
        {
            _logger.LogWarning("AndroidSTT: OnError = {Error} ({Code})", error, (int)error);

   // ERROR_NO_MATCH (7) and ERROR_SPEECH_TIMEOUT (6) are normal end-of-speech
            // conditions without a debugger. Treat them as empty results, not failures.
   // ERROR_RECOGNIZER_BUSY (8) means we need to destroy and retry.
            if (error == SpeechRecognizerError.RecognizerBusy)
            {
   MainThread.BeginInvokeOnMainThread(() =>
                {
  _recognizer?.Destroy();
          _recognizer = null;
     });
       }

        _readyTcs?.TrySetResult(false);   // unblock ready wait on any error
     _tcs?.TrySetResult(null);
    }

        public void OnPartialResults(Bundle? partialResults)
        {
  var matches = partialResults?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
          var partial = matches?.FirstOrDefault();
            if (!string.IsNullOrEmpty(partial) && _onPartialResult != null)
     MainThread.BeginInvokeOnMainThread(() => _onPartialResult(partial));
        }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEvent(int eventType, Bundle? @params) { }
        public void OnRmsChanged(float rmsdB) { }
    }
}
