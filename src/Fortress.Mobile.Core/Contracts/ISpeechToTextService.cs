namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Cross-platform speech-to-text contract.
    /// Implementations live in the MAUI platform folders.
    /// </summary>
    public interface ISpeechToTextService
    {
        /// <summary>Returns true when the mic permission has been granted.</summary>
        Task<bool> RequestPermissionAsync();

        /// <summary>
        /// Listen for a single utterance and return the transcript,
        /// or <c>null</c> if recognition failed / was cancelled.
        /// <paramref name="onReady"/> is called (on the main thread) the moment
        /// the platform mic is live – i.e. right after OnReadyForSpeech fires.
        /// <paramref name="onPartialResult"/> is called (on the main thread) each time
        /// a partial/interim transcript is available so the UI can show live text.
        /// </summary>
        Task<string?> ListenAsync(
            CancellationToken cancellationToken = default,
            Action? onReady = null,
            Action<string>? onPartialResult = null);
    }
}
