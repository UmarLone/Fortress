using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fortress.ViewModels.PopupPagesViewModels
{
    /// <summary>
    /// ViewModel for the voice listening bottom sheet.
    /// Shows a waveform animation while recording and surfaces status text.
    /// The sheet resolves to a <see cref="VoiceCommandResult"/> when dismissed.
    /// </summary>
    public sealed class VoiceListeningSheetViewModel : BottomSheetViewModelBase, IPostShowInitialize
    {
        private readonly IVoiceCommandService _voiceCommandService;
        private readonly IServiceProvider _services;
        private readonly ILogger<VoiceListeningSheetViewModel> _logger;

        private CancellationTokenSource? _cts;

        // ── Bindable state ────────────────────────────────────────────────────────

        private string _statusText = "Hold on…";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isListening;
        public bool IsListening
        {
            get => _isListening;
            set
            {
                SetProperty(ref _isListening, value);
                RaisePropertyChanged(nameof(IsProcessing));
            }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        private bool _showResult;
        public bool ShowResult
        {
            get => _showResult;
            set { SetProperty(ref _showResult, value); RaisePropertyChanged(nameof(ShowCommandResult)); }
        }

        /// <summary>Shows the command result badge only when not in dictation mode.</summary>
        public bool ShowCommandResult => _showResult && IsCommandMode;

        private string _resultText = string.Empty;
        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        private string _transcriptText = string.Empty;
        /// <summary>Live partial transcript shown while the mic is active.</summary>
        public string TranscriptText
        {
            get => _transcriptText;
            set => SetProperty(ref _transcriptText, value);
        }

        private bool _hasTranscript;
        /// <summary>True once the first partial word arrives — shows the transcript label.</summary>
        public bool HasTranscript
        {
            get => _hasTranscript;
            set => SetProperty(ref _hasTranscript, value);
        }

        private bool _isDictating;
        /// <summary>True while recording dictation for a note (second STT pass).</summary>
        public bool IsDictating
        {
            get => _isDictating;
            set { SetProperty(ref _isDictating, value); RaisePropertyChanged(nameof(IsCommandMode)); RaisePropertyChanged(nameof(ShowCommandResult)); }
        }

        private bool _isDictationReview;
        /// <summary>True when dictation is complete and awaiting user confirmation.</summary>
        public bool IsDictationReview
        {
            get => _isDictationReview;
            set { SetProperty(ref _isDictationReview, value); RaisePropertyChanged(nameof(IsCommandMode)); RaisePropertyChanged(nameof(ShowCommandResult)); }
        }

        /// <summary>True when in the normal voice-command (non-dictation) state.</summary>
        public bool IsCommandMode => !_isDictating && !_isDictationReview;

        private string _dictationText = string.Empty;
        /// <summary>The fully captured note text, shown in the review panel.</summary>
        public string DictationText
        {
            get => _dictationText;
            set => SetProperty(ref _dictationText, value);
        }

        private bool _resultSuccess;
        public bool ResultSuccess
        {
            get => _resultSuccess;
            set => SetProperty(ref _resultSuccess, value);
        }

        // ── Constructor ───────────────────────────────────────────────────────────

        public VoiceListeningSheetViewModel(
            IVoiceCommandService voiceCommandService,
            IServiceProvider services,
            ILogger<VoiceListeningSheetViewModel> logger)
        {
            _voiceCommandService = voiceCommandService;
            _services = services;
            _logger = logger;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        public override async Task InitializeAsync(object args, string title = null)
        {
      // Sheet is already visible (IPostShowInitialize). Start the mic immediately —
       // AndroidSpeechToTextService now waits internally for OnReadyForSpeech before
         // the platform signals the mic is live, so no fixed delay is needed here.
         await StartListeningAsync();
        }

        // ── Logic ─────────────────────────────────────────────────────────────────

        private async Task StartListeningAsync()
        {
            _cts = new CancellationTokenSource();

    IsListening  = false;
      IsProcessing = true;
  ShowResult   = false;
   TranscriptText = string.Empty;
     HasTranscript  = false;
   StatusText   = "Starting microphone\u2026";

   try
 {
    var result = await _voiceCommandService.ListenAndParseAsync(
    _cts.Token,
        onReady: () =>
                {
       IsListening    = true;
      StatusText     = "Listening\u2026 speak now";
        TranscriptText = string.Empty;
  HasTranscript  = false;
    },
    onPartialResult: partial =>
     {
   TranscriptText = partial;
    HasTranscript  = !string.IsNullOrEmpty(partial);
        });

                IsListening = false;
                IsProcessing = false;

                if (result.IsRecognised && result.Intent == VoiceCommandIntent.VoiceJournal)
                {
                    // Stay on the sheet — switch to dictation mode so the user
                    // can speak their note and see it live before saving.
                    ShowResult = false;
                    await StartDictationAsync();
                    return;
                }

                if (result.IsRecognised)
                {
                    ResultSuccess = true;
                    ResultText = IntentToFriendlyLabel(result.Intent);
                    StatusText = "Got it!";
                    ShowResult = true;

                    // Small pause so the user sees the confirmed result
                    await Task.Delay(900);
                    ReturnResult?.Invoke(result);
                    DismissAction?.Invoke();
                }
                else if (!string.IsNullOrEmpty(result.RawTranscript))
                {
                    // Heard something but couldn't match — pass the raw transcript
                    // through so the router can give a spoken "I didn't understand" hint.
                    ResultSuccess = false;
                    ResultText = $"Heard: \"{result.RawTranscript}\"";
                    StatusText = "Couldn't recognise that command";
                    ShowResult = true;

                    await Task.Delay(2000);
                    // Return the result with Unknown intent but real transcript so
                    // the router speaks its fallback help message.
                    ReturnResult?.Invoke(result);
                    DismissAction?.Invoke();
                }
                else
                {
                    // Nothing heard at all — show a visible result card
                    ResultSuccess = false;
                    ResultText = "Nothing heard";
                    StatusText = "Nothing heard";
                    ShowResult = true;

                    await Task.Delay(2000);
                    ReturnResult?.Invoke(VoiceCommandResult.Empty);
                    DismissAction?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                IsListening = false;
                IsProcessing = false;
                ReturnResult?.Invoke(VoiceCommandResult.Empty);
                DismissAction?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Voice listening sheet error");
                IsListening = false;
                IsProcessing = false;
                StatusText = "An error occurred";
                await Task.Delay(1500);
                ReturnResult?.Invoke(VoiceCommandResult.Empty);
                DismissAction?.Invoke();
            }
        }

        private async Task AwaitExternalListenTaskAsync(Task<VoiceCommandResult> listenTask)
        {
            IsListening = true;
            IsProcessing = true;
            ShowResult = false;
            StatusText = "Listening… speak now";

            try
            {
                var result = await listenTask;

                IsListening = false;
                IsProcessing = false;

                if (result.IsRecognised)
                {
                    ResultSuccess = true;
                    ResultText = IntentToFriendlyLabel(result.Intent);
                    StatusText = "Got it!";
                    ShowResult = true;
                    await Task.Delay(900);
                    ReturnResult?.Invoke(result);
                    DismissAction?.Invoke();
                }
                else if (!string.IsNullOrEmpty(result.RawTranscript))
                {
                    ResultSuccess = false;
                    ResultText = $"Heard: \"{result.RawTranscript}\"";
                    StatusText = "Couldn't recognise that command";
                    ShowResult = true;
                    await Task.Delay(2000);
                    ReturnResult?.Invoke(VoiceCommandResult.Empty);
                    DismissAction?.Invoke();
                }
                else
                {
                    // Nothing heard at all — show a visible result card
                    ResultSuccess = false;
                    ResultText = "Nothing heard";
                    StatusText = "Nothing heard";
                    ShowResult = true;

                    await Task.Delay(2000);
                    ReturnResult?.Invoke(VoiceCommandResult.Empty);
                    DismissAction?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                IsListening = false;
                IsProcessing = false;
                ReturnResult?.Invoke(VoiceCommandResult.Empty);
                DismissAction?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AwaitExternalListenTask error");
                IsListening = false;
                IsProcessing = false;
                StatusText = "An error occurred";
                await Task.Delay(1500);
                ReturnResult?.Invoke(VoiceCommandResult.Empty);
                DismissAction?.Invoke();
            }
        }

        // ── Dictation mode ───────────────────────────────────────────────────────

        private async Task StartDictationAsync()
        {
            IsDictating      = true;
            IsDictationReview = false;
            DictationText    = string.Empty;
            TranscriptText   = string.Empty;
            HasTranscript    = false;
            IsListening      = false;
            IsProcessing     = true;
            StatusText       = "Starting mic\u2026";

            _cts = new CancellationTokenSource();

            try
            {
                var stt = _services.GetRequiredService<ISpeechToTextService>();

                if (!await stt.RequestPermissionAsync())
                {
                    IsDictating  = false;
                    IsProcessing = false;
                    StatusText   = "Microphone permission denied";
                    return;
                }

                var text = await stt.ListenAsync(
                    _cts.Token,
                    onReady: () =>
                    {
                        IsListening  = true;
                        IsProcessing = false;
                        StatusText   = "Listening\u2026 speak your note";
                    },
                    onPartialResult: partial =>
                    {
                        TranscriptText = partial;
                        HasTranscript  = !string.IsNullOrEmpty(partial);
                    });

                IsListening = false;
                IsDictating = false;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    DictationText  = text;
                    TranscriptText = string.Empty;
                    HasTranscript  = false;
                    StatusText     = "Here\u2019s what I heard \u2014 save it?";
                    IsDictationReview = true;
                }
                else
                {
                    StatusText = "Didn\u2019t catch anything \u2014 tap \u201cTry Again\u201d";
                    IsDictationReview = true; // show panel with empty text + Try Again
                }
            }
            catch (OperationCanceledException)
            {
                IsListening  = false;
                IsDictating  = false;
                IsProcessing = false;
                // If cancelled from dictation review, reset to command mode
                IsDictationReview = false;
                ReturnResult?.Invoke(VoiceCommandResult.Empty);
                DismissAction?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dictation recording failed");
                IsListening       = false;
                IsDictating       = false;
                IsProcessing      = false;
                IsDictationReview = false;
                StatusText        = "Recording failed — please try again";
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        private DelegateCommand? _cancelCommand;
        public DelegateCommand CancelCommand =>
            _cancelCommand ??= new DelegateCommand(ExecuteCancel);

        private void ExecuteCancel()
        {
            _cts?.Cancel();
            IsListening       = false;
            IsProcessing      = false;
            IsDictating       = false;
            IsDictationReview = false;
            TranscriptText    = string.Empty;
            HasTranscript     = false;
            StatusText        = "Cancelled";
            ReturnResult?.Invoke(VoiceCommandResult.Empty);
            DismissAction?.Invoke();
        }

        private DelegateCommand? _confirmNoteCommand;
        public DelegateCommand ConfirmNoteCommand =>
            _confirmNoteCommand ??= new DelegateCommand(ExecuteConfirmNote);

        private void ExecuteConfirmNote()
        {
            var entities = new Dictionary<string, string> { ["dictation"] = DictationText };
            ReturnResult?.Invoke(new VoiceCommandResult(VoiceCommandIntent.VoiceJournal, DictationText, entities));
            DismissAction?.Invoke();
        }

        private DelegateCommand? _redoNoteCommand;
        public DelegateCommand RedoNoteCommand =>
            _redoNoteCommand ??= new DelegateCommand(async () =>
            {
                IsDictationReview = false;
                await StartDictationAsync();
            });

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string IntentToFriendlyLabel(VoiceCommandIntent intent) => intent switch
        {
  // Add intents
        VoiceCommandIntent.AddPassword      => "Adding a Password →",
       VoiceCommandIntent.AddCreditCard    => "Adding a Credit Card →",
       VoiceCommandIntent.AddAuthenticator => "Adding an Authenticator →",
    VoiceCommandIntent.AddIdentity      => "Adding an Identity →",
        VoiceCommandIntent.AddSecureNote    => "Adding a Secure Note →",
            VoiceCommandIntent.AddAddress       => "Adding an Address →",
        VoiceCommandIntent.VoiceJournal     => "Starting Voice Journal 🎙️",
        // Navigation intents
      VoiceCommandIntent.OpenPasswords      => "Opening Passwords →",
       VoiceCommandIntent.OpenCreditCards    => "Opening Credit Cards →",
       VoiceCommandIntent.OpenAuthenticators => "Opening Authenticators →",
     VoiceCommandIntent.OpenSecureNotes => "Opening Secure Notes →",
       VoiceCommandIntent.OpenIdentities    => "Opening Identities →",
       VoiceCommandIntent.OpenGroups        => "Opening Groups →",
       VoiceCommandIntent.OpenSettings      => "Opening Settings →",
  // Smart queries — spoken response shown directly
        VoiceCommandIntent.HowManyPasswords  => "Checking password count…",
      VoiceCommandIntent.HowManyCards       => "Checking card count…",
    VoiceCommandIntent.VaultSummary => "Reading vault summary…",
      VoiceCommandIntent.AttackSurface => "Checking attack surface…",
    VoiceCommandIntent.LockVault           => "Locking vault…",
 // Health intents
  VoiceCommandIntent.ShowVaultHealth     => "Checking vault health…",
    VoiceCommandIntent.GeneratePassword    => "Opening Password Generator →",
       VoiceCommandIntent.SyncNow            => "Syncing vault…",
       _ => "Processing…"
     };
    }
}
