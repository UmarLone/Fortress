using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Orchestrates the full voice command pipeline:
    /// microphone ? STT ? NLP parser ? structured intent.
    /// </summary>
    public interface IVoiceCommandService
    {
        /// <summary>Requests microphone and speech recognition permissions.</summary>
        Task<bool> RequestPermissionAsync();

        /// <summary>
        /// Start listening and return a parsed <see cref="VoiceCommandResult"/>.
        /// <paramref name="onReady"/> is invoked on the main thread the moment the
        /// microphone is live (i.e. after <c>OnReadyForSpeech</c> fires) so the UI
        /// can switch from "Starting…" to "Speak now" without a fixed delay.
        /// <paramref name="onPartialResult"/> is invoked on the main thread each time
        /// an interim transcript is available so the UI can show live text.
        /// Returns <see cref="VoiceCommandResult.Empty"/> on failure / no match.
        /// </summary>
        Task<VoiceCommandResult> ListenAndParseAsync(
           CancellationToken cancellationToken = default,
           Action? onReady = null,
           Action<string>? onPartialResult = null);
    }
}
