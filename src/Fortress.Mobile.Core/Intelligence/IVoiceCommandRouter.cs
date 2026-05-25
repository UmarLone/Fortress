using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Intelligence
{
    /// <summary>
    /// Routes a parsed <see cref="VoiceCommandResult"/> to a ViewModel action
    /// and produces a spoken TTS response (no secrets in responses).
    /// </summary>
    public interface IVoiceCommandRouter
    {
    /// <summary>
     /// Execute the action for <paramref name="result"/> using
        /// <paramref name="context"/> and populate <see cref="VoiceCommandResult.SpokenResponse"/>.
        /// </summary>
        Task<VoiceCommandResult> RouteAsync(
       VoiceCommandResult result,
 IVoiceCommandContext context,
    CancellationToken cancellationToken = default);
    }

    /// <summary>
 /// Context provided by the ViewModel so the router can query vault data
    /// without taking a dependency on the full DI container.
    /// </summary>
    public interface IVoiceCommandContext
    {
    /// <summary>Navigate to a named page with optional parameters.</summary>
      Task NavigateAsync(string page, IDictionary<string, object>? parameters = null);

        /// <summary>Get the latest pre-computed vault health result (never null after first load).</summary>
   Task<VaultHealthResult> GetVaultHealthAsync();

        /// <summary>Get total number of passwords stored in the vault.</summary>
        Task<int> GetPasswordCountAsync();

        /// <summary>Get total number of credit cards stored in the vault.</summary>
      Task<int> GetCardCountAsync();

      /// <summary>Lock the app immediately.</summary>
   Task LockAsync();

        /// <summary>True when the user has configured at least one unlock method (PIN or biometric).</summary>
      bool IsLockConfigured { get; }

        /// <summary>Show a toast message on the UI thread.</summary>
  void ShowToast(string message);

        /// <summary>Trigger a cloud sync (no-op if sync is disabled).</summary>
     Task TriggerSyncAsync();

     /// <summary>Speak text aloud using TTS (no secrets).</summary>
        Task SpeakAsync(string text);

        /// <summary>
        /// Listen for free-form dictation (no intent parsing) and return the
/// raw transcript. Used by the Voice Journal flow after the initial
        /// "record a note" intent is recognised.
      /// </summary>
        Task<string?> ListenForDictationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Save a voice journal entry as an encrypted SecureNoteItem.
  /// Returns the ID of the saved item.
        /// </summary>
        Task<Guid> SaveJournalEntryAsync(string content);
    }
}
