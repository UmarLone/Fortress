namespace Fortress.Mobile.Core.Models
{
    /// <summary>The vault item type the user wants to add or query via voice.</summary>
    public enum VoiceCommandIntent
    {
        Unknown = 0,

        // ── Add intents ──────────────────────────────────────────────────────
        AddPassword    = 1,
        AddCreditCard  = 2,
  AddAuthenticator = 3,
      AddIdentity    = 4,
      AddSecureNote  = 5,
  AddAddress  = 6,

        // ── Vault intelligence / health ──────────────────────────────────────
        ShowVaultHealth    = 10,
   ShowRiskyAccounts  = 11,
        ShowReusedPasswords = 12,
ShowWeakPasswords  = 13,
        GeneratePassword   = 14,
  SyncNow      = 15,
        EnableFortressMode = 16,
        ListMissing2FA     = 17,
     ShowBreachedAccounts = 18,

        // ── Navigation ───────────────────────────────────────────────────────
     OpenPasswords      = 20,
        OpenCreditCards    = 21,
   OpenAuthenticators = 22,
        OpenSecureNotes    = 23,
        OpenIdentities     = 24,
        OpenGroups         = 25,
        OpenSettings     = 26,

        // ── Smart vault queries ──────────────────────────────────────────────
   HowManyPasswords   = 30,
      HowManyCards       = 31,
  LockVault          = 32,
VaultSummary       = 33,
      AttackSurface      = 34,

        // ── Voice journal ────────────────────────────────────────────────────
        VoiceJournal       = 40,

        // ── Conversational ───────────────────────────────────────────────────
 Help             = 50,
  Greeting = 51,
        Thanks             = 52,
    }

    /// <summary>
    /// Structured result produced by the voice command pipeline.
    /// </summary>
    public sealed class VoiceCommandResult
    {
        public static VoiceCommandResult Empty { get; } =
            new VoiceCommandResult(VoiceCommandIntent.Unknown, string.Empty, new Dictionary<string, string>());

        public VoiceCommandIntent Intent { get; }
        public string RawTranscript { get; }
        public IReadOnlyDictionary<string, string> Entities { get; }
        public bool IsRecognised => Intent != VoiceCommandIntent.Unknown;

        /// <summary>
        /// Optional TTS response text – never contains raw secrets.
        /// Populated by <see cref="IVoiceCommandRouter"/> for query intents.
        /// </summary>
        public string? SpokenResponse { get; init; }

        public VoiceCommandResult(
            VoiceCommandIntent intent,
            string rawTranscript,
            Dictionary<string, string> entities)
        {
            Intent = intent;
            RawTranscript = rawTranscript ?? string.Empty;
            Entities = entities ?? new Dictionary<string, string>();
        }
    }
}
