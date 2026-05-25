using Microsoft.Extensions.Logging;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Intelligence;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Three-tier voice command parser.
    /// <list type="number">
    ///   <item>Tier 1 — Fast keyword matching (zero latency).</item>
    ///   <item>Tier 2 — ML.NET SDCA multiclass classifier (~80 KB model).</item>
    ///   <item>Tier 3 — Semantic TF-IDF cosine similarity fallback (handles paraphrases).</item>
    /// </list>
    /// </summary>
    public sealed class VoiceCommandParser
    {
        private readonly ILogger<VoiceCommandParser>? _logger;
     private readonly MlNetIntentClassifier _mlClassifier;
      private readonly SemanticIntentFallback _semanticFallback;

     public VoiceCommandParser(
     MlNetIntentClassifier mlClassifier,
       ILogger<VoiceCommandParser>? logger = null)
        {
            _mlClassifier = mlClassifier;
          _semanticFallback = new SemanticIntentFallback(null);
            _logger = logger;
    }

 public VoiceCommandResult Parse(string transcript)
        {
  if (string.IsNullOrWhiteSpace(transcript))
        return VoiceCommandResult.Empty;

            var lower = transcript.ToLowerInvariant().Trim()
  .Replace('\u2019', '\'')
  .Replace('\u2018', '\'')
         .Replace('\u02bc', '\'')
    .Replace("what's", "whats")
.Replace("what\u2019s", "whats")
          .Replace("what is", "whats");

            _logger?.LogDebug("VoiceCommandParser: parsing \"{T}\"", lower);

 // ── Wake word stripping ─────────────────────────────────────────
      // When users say "Hey Fortress, take a note" or "Fortress, show my passwords"
            // strip the wake word prefix so the intent matcher sees the real command.
       // If ONLY the wake word was said (nothing after), treat as Greeting.
          var stripped = StripWakeWord(lower);
     if (stripped != lower)
            {
         _logger?.LogInformation("VoiceCommandParser: stripped wake word → \"{S}\"", stripped);
      if (string.IsNullOrWhiteSpace(stripped))
      {
                  _logger?.LogInformation("Tier0/WakeWord → Greeting (bare wake word)");
            return Make(VoiceCommandIntent.Greeting, transcript);
         }
 lower = stripped;
   }

            // Strip politeness/filler prefixes so "Can you show my passwords please" →
            // "show my passwords" which the keyword tier can match.
            lower = StripFillerPrefix(lower);

        // 1️⃣ Keyword pass — instant, zero allocation
var kw = TryKeywordMatch(lower, transcript);
            if (kw.IsRecognised)
 {
    _logger?.LogInformation("Tier1/Keyword → {I}", kw.Intent);
 return kw;
   }

            // 2️⃣ ML.NET SDCA classifier
      var (mlIntent, mlConfidence) = _mlClassifier.Predict(lower);
      if (mlIntent != VoiceCommandIntent.Unknown)
            {
  _logger?.LogInformation("Tier2/ML.NET → {I} ({C:P0})", mlIntent, mlConfidence);
         return Make(mlIntent, transcript);
            }

            // 3️⃣ Semantic TF-IDF cosine fallback
            var (semIntent, semScore) = _semanticFallback.Predict(lower);
       if (semIntent != VoiceCommandIntent.Unknown)
      {
  _logger?.LogInformation("Tier3/Semantic → {I} ({S:F3})", semIntent, semScore);
 return Make(semIntent, transcript);
     }

    _logger?.LogWarning("VoiceCommandParser: no match for \"{T}\"", transcript);
      return new VoiceCommandResult(VoiceCommandIntent.Unknown, transcript, new Dictionary<string, string>());
        }

        // ── Wake word detection & stripping ──────────────────────────────────────

 /// <summary>
        /// Strips common wake word prefixes like "hey fortress", "ok fortress",
     /// "fortress" from the beginning of the transcript.
   /// Returns the original string unchanged if no wake word is detected.
        /// </summary>
        private static string StripWakeWord(string text)
        {
    ReadOnlySpan<string> prefixes =
  [
         "hey fortress ",
           "hello fortress ",
                "hi fortress ",
      "ok fortress ",
            "okay fortress ",
                "yo fortress ",
         "fortress ",
    ];

          foreach (var prefix in prefixes)
       {
              if (text.StartsWith(prefix, StringComparison.Ordinal))
             return text[prefix.Length..].TrimStart();
            }

            // Also handle trailing comma/pause: "hey fortress, take a note"
            foreach (var prefix in prefixes)
            {
    var trimmed = prefix.TrimEnd();
             var commaPrefix = trimmed + ", ";
                if (text.StartsWith(commaPrefix, StringComparison.Ordinal))
        return text[commaPrefix.Length..].TrimStart();
          var dotPrefix = trimmed + ". ";
      if (text.StartsWith(dotPrefix, StringComparison.Ordinal))
          return text[dotPrefix.Length..].TrimStart();
            }

       return text;
    }

        // ── Filler / politeness prefix stripping ─────────────────────────────────
        // Removes leading courtesy phrases so "Can you please show my passwords"
        // becomes "show my passwords" and the keyword tier can match it.
        private static string StripFillerPrefix(string text)
        {
            ReadOnlySpan<string> fillers =
            [
                "can you please ", "can you ", "could you please ", "could you ",
                "please ", "i'd like to ", "i would like to ", "i want to ",
                "i need to ", "i need you to ", "help me ", "help me to ",
                "let me ", "let me see ", "let me view ",
                "would you ", "would you please ", "would you mind ",
                "hey can you ", "hey could you ",
                "i'd love to ", "i'd really like to ",
                "is it possible to ", "how do i ", "how can i ",
                "what about ", "just ", "quickly ",
            ];

            foreach (var f in fillers)
                if (text.StartsWith(f, StringComparison.Ordinal))
                    text = text[f.Length..].TrimStart();

            // Strip trailing politeness ("please", "for me", "now", "thanks")
            ReadOnlySpan<string> trailers =
            [
                " please", " for me", " right now", " now", " thanks", " thank you",
                " asap", " quickly", " immediately",
            ];
            foreach (var t in trailers)
                if (text.EndsWith(t, StringComparison.Ordinal))
                    text = text[..^t.Length].TrimEnd();

            return text;
        }

        // ── Tier 1 — Keyword matching ────────────────────────────────────────────

      private static VoiceCommandResult TryKeywordMatch(string lower, string raw)
        {
            // ── Conversational intents (checked first — very specific) ───────

  if (ContainsAny(lower, "help", "what can you do", "what can i say",
     "what are the commands", "what commands", "how do i use",
    "show me commands", "voice commands", "what can i ask",
     "how does this work", "what do you do"))
   return Make(VoiceCommandIntent.Help, raw);

            // Greeting — only match actual greetings, NOT bare "hey" / "yo"
         if (ContainsAny(lower, "hi there", "good morning",
     "good afternoon", "good evening", "howdy",
      "hello there", "greetings"))
       return Make(VoiceCommandIntent.Greeting, raw);

            // Only match these short greetings if they're the ENTIRE transcript
            if (lower is "hello" or "hi" or "hey" or "hey there" or "yo" or "howdy"
 or "hey fortress" or "hello fortress" or "hi fortress")
  return Make(VoiceCommandIntent.Greeting, raw);

            if (ContainsAny(lower, "thank", "thanks", "thank you", "cheers",
   "appreciate", "nice one", "good job", "well done"))
     return Make(VoiceCommandIntent.Thanks, raw);

            // ── Navigation intents ───────────────────────────────────────────

   if (ContainsAny(lower, "open passwords", "show passwords", "my passwords",
     "go to passwords", "view passwords", "open logins", "show logins", "my logins",
         "show me my passwords", "show me my logins", "see my passwords", "see my logins",
     "list my passwords", "list my logins", "where are my passwords",
         "find my passwords", "display my passwords", "pull up my passwords",
       "take me to passwords", "take me to my passwords", "go to my passwords",
   "go to my logins", "browse my passwords",
   "saved passwords", "saved logins", "stored passwords", "stored logins",
   "all my passwords", "all my logins", "all passwords", "all logins",
   "password list", "login list", "my accounts", "my credentials",
   "check my passwords", "check my logins", "access my passwords",
   "get my passwords", "get my logins", "passwords please", "logins please"))
     return Make(VoiceCommandIntent.OpenPasswords, raw);

            if (ContainsAny(lower, "open cards", "show cards", "my cards", "open credit cards",
"show credit cards", "my credit cards", "view cards", "go to cards",
   "show me my cards", "see my cards", "list my cards",
         "show me my credit cards", "display my cards", "pull up my cards",
     "where are my cards", "take me to cards", "take me to my cards",
                "go to my cards", "browse my cards", "view my cards"))
           return Make(VoiceCommandIntent.OpenCreditCards, raw);

            if (ContainsAny(lower, "open authenticators", "show authenticators", "my authenticators",
       "go to authenticators", "view authenticators", "open 2fa", "show 2fa", "my 2fa codes",
 "show me my authenticators", "see my authenticators", "list my authenticators",
     "show me my 2fa", "see my 2fa", "show my totp", "where are my authenticators",
        "take me to authenticators", "pull up my authenticators", "two factor codes",
              "open my 2fa", "go to my authenticators", "show me 2fa codes",
    "display my authenticators", "browse my authenticators"))
      return Make(VoiceCommandIntent.OpenAuthenticators, raw);

            if (ContainsAny(lower, "open notes", "show notes", "my notes", "open secure notes",
    "show secure notes", "my secure notes", "view notes", "go to notes",
        "show me my notes", "see my notes", "list my notes",
       "open my notes", "open my secure notes", "where are my notes",
      "take me to notes", "take me to my notes", "pull up my notes",
"display my notes", "browse my notes", "go to my notes",
                "show me my secure notes", "view my notes", "view my secure notes"))
  return Make(VoiceCommandIntent.OpenSecureNotes, raw);

     if (ContainsAny(lower, "open identities", "show identities", "my identities",
           "go to identities", "view identities", "open identity", "show identity",
    "show me my identities", "see my identities", "list my identities",
  "where are my identities", "take me to identities", "pull up my identities",
      "display my identities", "open my identities", "go to my identities",
   "open contacts", "show contacts", "my contacts", "show me my contacts"))
   return Make(VoiceCommandIntent.OpenIdentities, raw);

        if (ContainsAny(lower, "open groups", "show groups", "my groups",
       "go to groups", "view groups",
          "show me my groups", "see my groups", "list my groups",
     "where are my groups", "take me to groups", "open my groups"))
                return Make(VoiceCommandIntent.OpenGroups, raw);

            if (ContainsAny(lower, "open settings", "go to settings", "show settings",
     "app settings", "vault settings",
                "show me settings", "go to my settings", "open preferences",
      "open the settings", "take me to settings", "navigate to settings",
    "settings menu", "open app settings", "view settings"))
 return Make(VoiceCommandIntent.OpenSettings, raw);

        // ── Smart vault queries ─────────────────────────────────────────

            if (ContainsAny(lower, "how many passwords", "how many logins", "how many accounts",
      "number of passwords", "count my passwords", "total passwords",
           "how many credentials", "password count",
      "how many passwords do i have", "how many logins do i have",
          "tell me how many passwords", "how many passwords have i saved",
              "what is my password count", "how many passwords are in my vault",
      "how many sites", "how many websites", "password total", "total logins"))
       return Make(VoiceCommandIntent.HowManyPasswords, raw);

          if (ContainsAny(lower, "how many cards", "how many credit cards", "number of cards",
             "count my cards", "total cards", "how many payment cards",
      "how many cards do i have", "how many credit cards do i have",
  "tell me how many cards", "what is my card count",
     "how many cards are in my vault", "how many cards have i saved"))
            return Make(VoiceCommandIntent.HowManyCards, raw);

       if (ContainsAny(lower, "lock vault", "lock my vault", "lock app", "lock now",
      "lock fortress", "lock the app", "lock everything",
     "lock fortress", "lock the vault", "lock up my vault",
        "secure the vault", "secure my vault", "lock it", "lock it up",
     "lock my app", "put the vault on lock", "secure the app"))
  return Make(VoiceCommandIntent.LockVault, raw);

            if (ContainsAny(lower, "vault summary", "vault overview", "what do i have",
         "summary of my vault", "what's in my vault", "whats in my vault",
          "vault stats", "tell me about my vault",
          "what is in my vault", "give me a summary", "give me a vault summary",
    "summarise my vault", "summarize my vault", "overview of my vault",
          "what do i have stored", "vault statistics", "what is in my fortress",
                "what have i got in my vault", "show me my vault stats",
       "tell me my vault contents", "how many things are in my vault",
        "show vault overview"))
      return Make(VoiceCommandIntent.VaultSummary, raw);

     if (ContainsAny(lower, "attack surface", "blast radius", "how exposed",
         "how at risk", "show my exposure", "compromise chains",
   "accounts share passwords", "accounts are linked",
  "accounts linked together", "show my attack surface",
      "what is my attack surface", "password clusters",
       "security exposure", "show compromise chains",
     "which accounts are linked"))
    return Make(VoiceCommandIntent.AttackSurface, raw);

            // ── Intelligence / health intents ────────────────────────────────

      if (ContainsAny(lower, "vault health", "my health", "security score",
           "how secure", "how safe", "health check", "check my vault",
     "health report", "how is my vault health", "how healthy is my vault",
        "check my vault health", "show my vault health", "what is my vault health",
            "what is my security score", "is my vault secure", "check security score",
        "how strong are my passwords", "check my password security"))
          return Make(VoiceCommandIntent.ShowVaultHealth, raw);

          if (ContainsAny(lower, "risky accounts", "at risk", "vulnerable accounts",
    "problem accounts", "accounts at risk", "security issues",
        "unsafe accounts", "accounts need attention", "what accounts are risky",
      "which accounts are at risk", "which accounts are risky",
    "show risky accounts", "list risky accounts", "which logins are unsafe"))
                return Make(VoiceCommandIntent.ShowRiskyAccounts, raw);

    if (ContainsAny(lower, "reused password", "duplicate password", "same password",
                "passwords reused", "repeated password",
                "reused passwords", "any passwords reused", "am i reusing",
                "do i reuse", "check for reused", "find reused",
             "show reused passwords", "which passwords are reused",
             "do i have reused passwords", "list reused passwords"))
       return Make(VoiceCommandIntent.ShowReusedPasswords, raw);

            if (ContainsAny(lower, "weak password", "weak passwords", "poor password",
   "bad password", "improve password", "strengthen password",
         "show weak passwords", "which passwords are weak",
    "do i have weak passwords", "find weak passwords",
             "list weak passwords", "check for weak", "any passwords weak",
         "show me weak passwords", "my bad passwords"))
      return Make(VoiceCommandIntent.ShowWeakPasswords, raw);

 if (ContainsAny(lower, "breached", "breach", "hacked password", "compromised password",
 "data breach", "haveibeenpwned", "pwned",
       "compromised passwords", "passwords leaked", "have i been breached",
        "passwords in breach", "are my passwords leaked", "compromised accounts",
      "hacked accounts", "are any of my accounts hacked",
  "check if my passwords are compromised"))
           return Make(VoiceCommandIntent.ShowBreachedAccounts, raw);

if (ContainsAny(lower, "missing 2fa", "missing two factor", "no two factor",
      "need 2fa", "add 2fa", "enable 2fa", "list missing", "no authenticator",
      "without 2fa", "accounts missing 2fa", "accounts without two factor",
          "which accounts are missing two factor", "show accounts without 2fa",
           "which sites need 2fa", "find accounts missing 2fa",
   "show missing two factor", "accounts without two step"))
             return Make(VoiceCommandIntent.ListMissing2FA, raw);

   if (ContainsAny(lower, "generate password", "create password", "make password",
           "random password", "strong password", "new password for me", "password generator",
            "generate a password", "create a strong password", "open password generator",
             "make a new password", "suggest a password", "create a secure password",
      "i need a new password", "i need a strong password", "make me a password",
                "help me create a password"))
     return Make(VoiceCommandIntent.GeneratePassword, raw);

    if (ContainsAny(lower, "sync now", "sync vault", "backup now", "update sync",
        "force sync", "synchronise", "synchronize",
       "sync my vault", "back up my vault", "backup my vault",
     "upload my vault", "start a sync", "do a sync",
      "sync to cloud", "update cloud", "refresh my vault",
  "do a backup", "backup my vault now"))
          return Make(VoiceCommandIntent.SyncNow, raw);

            if (ContainsAny(lower, "fortress mode", "enable fortress", "lock down",
         "maximum security", "full protection",
  "activate fortress", "turn on fortress", "start fortress"))
     return Make(VoiceCommandIntent.EnableFortressMode, raw);

        // ── Add intents ─────────────────────────────────────────────────

      // Voice journal (before general "note" keywords)
            if (ContainsAny(lower, "write a note", "record a note", "voice journal",
     "journal entry", "record my thoughts", "dictate a note",
      "write a journal", "record a journal", "take a note",
     "capture a thought", "start journaling", "voice note",
   "log a thought", "record a moment", "capture a moment",
   "dictate a journal", "save my thoughts", "record what i say",
           "write down what i say", "hey fortress write", "hey fortress record",
           "hey fortress journal", "quick note", "memo to self",
    "i want to record a note", "let me dictate a note", "i want to journal",
  "new journal entry", "capture my thoughts", "speak a note",
                "record a voice memo", "fortress take a note", "fortress write this down",
        "speak my journal", "i have something to record", "lets journal",
   "log my day", "start voice recording", "voice memo"))
    return Make(VoiceCommandIntent.VoiceJournal, raw);

     // AddCreditCard
         if (ContainsAny(lower,
             "add card", "new card", "store card", "add a card",
           "save my card", "add my card", "store my card",
       "add payment card", "save payment card", "new payment method",
    "add a credit card", "save a credit card", "add new card",
 "add credit card", "add debit card", "save credit card",
          "store credit card", "add a new card"))
       return Make(VoiceCommandIntent.AddCreditCard, raw);

    // AddAuthenticator
         if (ContainsAny(lower,
                "add authenticator", "new authenticator",
        "add two factor", "save 2fa", "save my 2fa", "set up two factor",
                "set up 2fa", "add otp", "store totp", "add a new authenticator",
                "add 2fa", "add a 2fa", "new 2fa", "store 2fa",
 "add verification code", "new verification code"))
  return Make(VoiceCommandIntent.AddAuthenticator, raw);

       if (ContainsAny(lower, "secure note", "secret note", "private note",
    "add note", "new note", "store note",
       "add a secure note", "save a note", "create a note",
          "add a note", "encrypted note", "confidential note",
     "save private note", "store a note", "add note to vault"))
           return Make(VoiceCommandIntent.AddSecureNote, raw);

 // AddAddress
            if (ContainsAny(lower,
   "add address", "new address", "store address",
     "save my address", "add my address",
       "add home address", "add work address",
    "add shipping address", "add billing address",
          "store my address", "save address", "add a new address"))
  return Make(VoiceCommandIntent.AddAddress, raw);

            // AddIdentity
        if (ContainsAny(lower,
         "add person", "new person", "add identity", "new identity",
                "add contact", "new contact", "add profile",
   "save my identity", "add a person", "add a new identity",
          "add personal details", "store identity", "save identity",
    "add a new contact", "store my identity"))
     return Make(VoiceCommandIntent.AddIdentity, raw);

 // AddPassword — ONLY match explicit add/save/store phrases
      if (ContainsAny(lower,
     "add login", "new login", "add password", "new password",
                "add a password", "save a password", "save my password",
    "store my password", "add a login", "save a login",
       "add credentials", "save credentials", "store credentials",
       "website password", "app password",
      "remember my password", "put my password in the vault",
        "i need to save a login", "i need to add a login",
 "i want to add a password", "store a login",
                "save login for", "add my account", "save my account",
       "add a new login", "add a new password", "save new password",
         "store a new password", "store a new login"))
   return Make(VoiceCommandIntent.AddPassword, raw);

            return VoiceCommandResult.Empty;
      }

        private static bool ContainsAny(string text, params string[] keywords)
  => keywords.Any(k => text.Contains(k, StringComparison.Ordinal));

   private static VoiceCommandResult Make(VoiceCommandIntent intent, string raw,
  Dictionary<string, string>? entities = null)
            => new(intent, raw, entities ?? new Dictionary<string, string>());
 }
}
