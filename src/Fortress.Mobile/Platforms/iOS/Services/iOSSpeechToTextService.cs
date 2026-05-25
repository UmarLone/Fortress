using AVFoundation;
using Foundation;
using Fortress.Mobile.Core.Contracts;
using Microsoft.Extensions.Logging;
using Speech;

namespace Fortress.iOS.Services
{
    /// <summary>
    /// iOS/macOS speech-to-text using the native <see cref="SFSpeechRecognizer"/> API.
    /// Supports on-device recognition (iOS 13+ with on-device model) and falls back
    /// to server-side recognition — no third-party API key required.
    /// </summary>
    public sealed class iOSSpeechToTextService : ISpeechToTextService
    {
        private readonly ILogger<iOSSpeechToTextService> _logger;

        public iOSSpeechToTextService(ILogger<iOSSpeechToTextService> logger)
        {
            _logger = logger;
        }

        // ── ISpeechToTextService ─────────────────────────────────────────────────
        private Task<bool> RequestMicrophonePermissionAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            AVAudioSession.SharedInstance().RequestRecordPermission(granted =>
            {
                tcs.TrySetResult(granted);
            });

            return tcs.Task;
        }
        public async Task<bool> RequestPermissionAsync()
        {
            // Microphone
            var micStatus = await RequestMicrophonePermissionAsync();
            if (!micStatus)
            {
                _logger.LogWarning("iOS microphone permission denied");
                return false;
            }

            // Speech recognition
            var tcs = new TaskCompletionSource<bool>();
            SFSpeechRecognizer.RequestAuthorization(status =>
     {
         tcs.TrySetResult(status == SFSpeechRecognizerAuthorizationStatus.Authorized);
     });

            var granted = await tcs.Task;
            if (!granted) _logger.LogWarning("iOS speech recognition permission denied");
            return granted;
        }

        public async Task<string?> ListenAsync(CancellationToken cancellationToken = default)
        {
            // Permission is verified by VoiceCommandService before calling here.
            // Re-checking here caused a second permission dialog on iOS.
            var recognizer = new SFSpeechRecognizer(NSLocale.CurrentLocale);
            if (recognizer == null || !recognizer.Available)
            {
                _logger.LogWarning("SFSpeechRecognizer not available");
                return null;
            }

            // Use on-device mode when available (iOS 13+)
            if (recognizer.SupportsOnDeviceRecognition)
                recognizer.DefaultTaskHint = SFSpeechRecognitionTaskHint.Dictation;

            var audioEngine = new AVAudioEngine();
            var request = new SFSpeechAudioBufferRecognitionRequest
            {
                ShouldReportPartialResults = true,
                // On-device only when supported
                RequiresOnDeviceRecognition = recognizer.SupportsOnDeviceRecognition
            };

            // Configure audio session
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Record,
                    AVAudioSessionCategoryOptions.DefaultToSpeaker,
           out _);
            session.SetActive(true, out _);

            var inputNode = audioEngine.InputNode;
            var format = inputNode.GetBusOutputFormat(0);
            inputNode.InstallTapOnBus(0, 1024, format, (buffer, _) =>
            {
                request.Append(buffer);
            });

            audioEngine.Prepare();
            audioEngine.StartAndReturnError(out _);

            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            string? lastPartial = null;

            SFSpeechRecognitionTask? task = recognizer.GetRecognitionTask(request, (result, error) =>
        {
            if (error != null)
            {
                _logger.LogWarning("iOS STT error: {Err}", error.LocalizedDescription);
                tcs.TrySetResult(null);
                return;
            }
            if (result != null)
            {
                lastPartial = result.BestTranscription?.FormattedString;
                if (result.Final)
                {
                    _logger.LogDebug("iOS STT final result: \"{Text}\"", lastPartial);
                    tcs.TrySetResult(lastPartial);
                }
            }
        });

            using var reg = cancellationToken.Register(() =>
              {
                  task?.Cancel();
                  tcs.TrySetResult(null);
              });

            try
            {
                // 7 s silence timeout: if no final result in 7 s, return whatever partial we have
                return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(7), cancellationToken);
            }
            catch (TimeoutException)
            {
                task?.Cancel();
                _logger.LogInformation("iOS STT: no final result in 7s, returning partial: \"{P}\"", lastPartial);
                return lastPartial; // may be null — caller handles that
            }
            catch (OperationCanceledException) { return null; }
            finally
            {
                audioEngine.Stop();
                inputNode.RemoveTapOnBus(0);
                request.EndAudio();
                session.SetActive(false, out _);
            }
        }

        public async Task<string?> ListenAsync(
   CancellationToken cancellationToken = default,
   Action? onReady = null,
            Action<string>? onPartialResult = null)
     {
            var recognizer = new SFSpeechRecognizer(NSLocale.CurrentLocale);
        if (recognizer == null || !recognizer.Available)
         {
    _logger.LogWarning("SFSpeechRecognizer not available");
       return null;
  }

    if (recognizer.SupportsOnDeviceRecognition)
       recognizer.DefaultTaskHint = SFSpeechRecognitionTaskHint.Dictation;

   var audioEngine = new AVAudioEngine();
    var request = new SFSpeechAudioBufferRecognitionRequest
  {
   ShouldReportPartialResults = true,
   RequiresOnDeviceRecognition = recognizer.SupportsOnDeviceRecognition
          };

        var session = AVAudioSession.SharedInstance();
           session.SetCategory(AVAudioSessionCategory.Record,
     AVAudioSessionCategoryOptions.DefaultToSpeaker, out _);
 session.SetActive(true, out _);

    var inputNode = audioEngine.InputNode;
  var format    = inputNode.GetBusOutputFormat(0);
     inputNode.InstallTapOnBus(0, 1024, format, (buffer, _) => request.Append(buffer));

   audioEngine.Prepare();
    audioEngine.StartAndReturnError(out _);

    // Fire onReady now — on iOS the audio engine starting IS the "mic live" signal
 if (onReady != null)
     MainThread.BeginInvokeOnMainThread(onReady);

  var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            string? lastPartial = null;

            SFSpeechRecognitionTask? task = recognizer.GetRecognitionTask(request, (result, error) =>
        {
  if (error != null)
         {
  _logger.LogWarning("iOS STT error: {Err}", error.LocalizedDescription);
 tcs.TrySetResult(null);
  return;
  }
     if (result != null)
            {
     lastPartial = result.BestTranscription?.FormattedString;
     if (!string.IsNullOrEmpty(lastPartial) && onPartialResult != null)
          MainThread.BeginInvokeOnMainThread(() => onPartialResult(lastPartial));
   if (result.Final)
        {
         _logger.LogDebug("iOS STT final result: \"{Text}\"", lastPartial);
        tcs.TrySetResult(lastPartial);
         }
            }
     });

            using var reg = cancellationToken.Register(() =>
              {
                  task?.Cancel();
                  tcs.TrySetResult(null);
              });

            try
            {
                // 7 s silence timeout: if no final result in 7 s, return whatever partial we have
                return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(7), cancellationToken);
            }
            catch (TimeoutException)
            {
                task?.Cancel();
                _logger.LogInformation("iOS STT: no final result in 7s, returning partial: \"{P}\"", lastPartial);
                return lastPartial; // may be null — caller handles that
            }
            catch (OperationCanceledException) { return null; }
            finally
            {
                audioEngine.Stop();
                inputNode.RemoveTapOnBus(0);
                request.EndAudio();
                session.SetActive(false, out _);
            }
        }
    }
}
