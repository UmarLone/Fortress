using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fortress.Mobile.Core.Intelligence
{
    /// <summary>
    /// Routes voice command intents to ViewModel actions and builds
    /// spoken TTS responses. No secrets are ever included in spoken text.
    /// </summary>
    public sealed class VoiceCommandRouter : IVoiceCommandRouter
    {
  private readonly ILogger<VoiceCommandRouter>? _logger;

        public VoiceCommandRouter(ILogger<VoiceCommandRouter>? logger = null) => _logger = logger;

 /// <inheritdoc />
    public async Task<VoiceCommandResult> RouteAsync(
     VoiceCommandResult result,
      IVoiceCommandContext context,
   CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("VoiceCommandRouter: routing intent {I}", result.Intent);

            string spoken;

         switch (result.Intent)
 {
         // ── Add intents ──────────────────────────────────────────
case VoiceCommandIntent.AddPassword:
   await context.NavigateAsync("AddEditCredentialPage");
    spoken = "Sure, let's add a new login.";
   break;

case VoiceCommandIntent.AddCreditCard:
  await context.NavigateAsync("AddEditCreditCardPage");
    spoken = "Opening the card form for you.";
  break;

    case VoiceCommandIntent.AddAuthenticator:
       await context.NavigateAsync("AddEditAuthenticatorPage");
 spoken = "Let's set up a new authenticator.";
        break;

      case VoiceCommandIntent.AddIdentity:
       case VoiceCommandIntent.AddAddress:
await context.NavigateAsync("AddEditIdentityPage");
  spoken = "Opening identity form.";
        break;

    case VoiceCommandIntent.AddSecureNote:
        await context.NavigateAsync("AddEditSecureItemPage",
  new Dictionary<string, object> { { "itemType", SecureItemType.SecureNote } });
   spoken = "Ready for your secure note.";
  break;

case VoiceCommandIntent.VoiceJournal:
    {
        // If dictation was captured on the sheet (entities["dictation"]), use it directly.
        // Otherwise fall back to listening again here (legacy path).
        string? dictation = null;
        if (result.Entities.TryGetValue("dictation", out var captured) &&
            !string.IsNullOrWhiteSpace(captured))
        {
            dictation = captured;
        }
        else
        {
            await context.SpeakAsync("Go ahead, I'm listening. I'll save whatever you say as an encrypted note in your vault.");
            dictation = await context.ListenForDictationAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(dictation))
        {
            spoken = "I didn't catch anything. Just say \"take a note\" whenever you're ready to try again.";
            break;
        }

        await context.SaveJournalEntryAsync(dictation);
        spoken = "Done — your journal entry is saved and encrypted in your vault.";
        context.ShowToast("Journal entry saved ✓");
        break;
    }

       // ── Intelligence / query intents ──────────────────────────
    case VoiceCommandIntent.ShowVaultHealth:
      {
   var health = await context.GetVaultHealthAsync();
        spoken = $"Your vault health score is {health.Score} percent – that's {health.StatusLabel}. ";
     if (health.WeakPasswordsCount > 0)
       spoken += $"There {(health.WeakPasswordsCount == 1 ? "is" : "are")} {health.WeakPasswordsCount} weak password{(health.WeakPasswordsCount == 1 ? "" : "s")} to strengthen. ";
 else
             spoken += "All your passwords are strong. ";
         if (health.ReusedPasswordsCount > 0)
    spoken += $"{health.ReusedPasswordsCount} {(health.ReusedPasswordsCount == 1 ? "is" : "are")} reused across accounts. ";
  if (health.Missing2FACount > 0)
       spoken += $"{health.Missing2FACount} account{(health.Missing2FACount == 1 ? "" : "s")} still {(health.Missing2FACount == 1 ? "needs" : "need")} two-factor authentication.";
 else
   spoken += "Two-factor coverage looks solid.";
await context.NavigateAsync("VaultHealthPage");
       break;
        }

      case VoiceCommandIntent.ShowRiskyAccounts:
      {
            var health = await context.GetVaultHealthAsync();
   int risky = health.WeakPasswordsCount + health.ReusedPasswordsCount + health.BreachedCount;
         spoken = risky == 0
     ? "Good news – I didn't find any risky accounts in your vault."
          : $"I found {risky} account{(risky == 1 ? "" : "s")} that {(risky == 1 ? "needs" : "need")} attention. Opening the details for you.";
  await context.NavigateAsync("VaultHealthPage");
break;
       }

   case VoiceCommandIntent.ShowReusedPasswords:
    {
 var health = await context.GetVaultHealthAsync();
   spoken = health.ReusedPasswordsCount == 0
        ? "No reused passwords – you're doing great on that front."
 : $"You have {health.ReusedPasswordsCount} password{(health.ReusedPasswordsCount == 1 ? "" : "s")} reused across different accounts. That's risky – if one site gets breached, the others are exposed too.";
        await context.NavigateAsync("VaultHealthPage");
   break;
    }

     case VoiceCommandIntent.ShowWeakPasswords:
    {
 var health = await context.GetVaultHealthAsync();
   spoken = health.WeakPasswordsCount == 0
         ? "All your passwords are strong. Nice work."
 : $"{health.WeakPasswordsCount} password{(health.WeakPasswordsCount == 1 ? "" : "s")} could be cracked quickly. I'd recommend using the generator to replace {(health.WeakPasswordsCount == 1 ? "it" : "them")}.";
   await context.NavigateAsync("VaultHealthPage");
    break;
 }

    case VoiceCommandIntent.ShowBreachedAccounts:
    {
     var health = await context.GetVaultHealthAsync();
 spoken = health.BreachedCount == 0
       ? "None of your passwords were found in known breach databases. That's a clean bill of health."
   : $"Heads up – {health.BreachedCount} password{(health.BreachedCount == 1 ? " was" : "s were")} found in known breach databases. You should change {(health.BreachedCount == 1 ? "it" : "them")} as soon as possible.";
       await context.NavigateAsync("VaultHealthPage");
    break;
      }

      case VoiceCommandIntent.ListMissing2FA:
  {
 var health = await context.GetVaultHealthAsync();
       spoken = health.Missing2FACount == 0
   ? "All your accounts have two-factor authentication. Well done."
     : $"{health.Missing2FACount} account{(health.Missing2FACount == 1 ? " is" : "s are")} missing two-factor authentication. Adding it makes those accounts much harder to break into.";
      await context.NavigateAsync("VaultHealthPage");
         break;
       }

      case VoiceCommandIntent.GeneratePassword:
   await context.NavigateAsync("PasswordGeneratorPage");
    spoken = "Here's the password generator – tap to create a strong one.";
  break;

case VoiceCommandIntent.SyncNow:
      await context.TriggerSyncAsync();
 spoken = "Syncing your vault now.";
      break;

  case VoiceCommandIntent.EnableFortressMode:
         context.ShowToast("Fortress Mode is always active.");
     spoken = "Fortress Mode is already active – your vault is continuously protected by on-device AI threat detection.";
    break;

  // ── Navigation intents ──────────────────────────────────
     case VoiceCommandIntent.OpenPasswords:
        await context.NavigateAsync("CredentialsPage");
spoken = "Here are your passwords.";
      break;

           case VoiceCommandIntent.OpenCreditCards:
 await context.NavigateAsync("CreditCardsPage");
         spoken = "Here are your cards.";
        break;

         case VoiceCommandIntent.OpenAuthenticators:
   await context.NavigateAsync("AuthenticatorsPage");
    spoken = "Here are your authenticator codes.";
 break;

   case VoiceCommandIntent.OpenSecureNotes:
          await context.NavigateAsync("SecureItemsPage");
    spoken = "Here are your secure notes and items.";
                break;

  case VoiceCommandIntent.OpenIdentities:
await context.NavigateAsync("IdentitiesPage");
         spoken = "Here are your saved identities.";
     break;

  case VoiceCommandIntent.OpenGroups:
  await context.NavigateAsync("GroupsPage");
      spoken = "Here are your groups.";
     break;

       case VoiceCommandIntent.OpenSettings:
        await context.NavigateAsync("MenuPage");
          spoken = "Here are your settings.";
  break;

   // ── Smart vault queries ──────────────────────────────────
        case VoiceCommandIntent.HowManyPasswords:
        {
       var count = await context.GetPasswordCountAsync();
          spoken = count == 0
     ? "Your vault is empty – no passwords stored yet. Say \"add a login\" to get started."
    : $"You have {count} password{(count == 1 ? "" : "s")} stored in your vault.";
 break;
   }

      case VoiceCommandIntent.HowManyCards:
      {
       var count = await context.GetCardCountAsync();
      spoken = count == 0
       ? "No credit cards stored yet. Say \"add a card\" to save one."
            : $"You have {count} credit card{(count == 1 ? "" : "s")} in your vault.";
     break;
          }

        case VoiceCommandIntent.LockVault:
   if (!context.IsLockConfigured)
     {
   spoken = "You don't have a lock set up yet. Head to Settings to configure a PIN or biometric lock first.";
 context.ShowToast("Set up a lock in Settings first");
        }
      else
    {
   await context.LockAsync();
     spoken = "Vault locked.";
    }
     break;

            case VoiceCommandIntent.AttackSurface:
     {
      var health = await context.GetVaultHealthAsync();
int surface = health.AttackSurfaceScore;
      spoken = $"Your attack surface score is {surface} out of 100 – {health.AttackSurfaceLabel}. ";
    if (health.CredentialClusters.Count > 0)
     {
    var biggest = health.CredentialClusters[0];
       spoken += $"The biggest risk is: {biggest.RiskNarrative} ";
  }
    if (surface <= 20)
  spoken += "Overall, your vault exposure is well contained.";
      else if (surface <= 45)
        spoken += "There are some exposure risks worth addressing when you get a chance.";
               else
    spoken += "Your vault has significant exposure. I'd prioritise fixing compromise chains and adding two-factor to critical accounts.";
    break;
      }

      case VoiceCommandIntent.VaultSummary:
        {
var health = await context.GetVaultHealthAsync();
         var passwords = await context.GetPasswordCountAsync();
        var cards = await context.GetCardCountAsync();
      spoken = $"Your vault has {passwords} password{(passwords == 1 ? "" : "s")} "
      + $"and {cards} card{(cards == 1 ? "" : "s")}. "
       + $"Vault health is at {health.Score} percent – {health.StatusLabel}.";
   if (health.WeakPasswordsCount > 0)
     spoken += $" {health.WeakPasswordsCount} weak password{(health.WeakPasswordsCount == 1 ? "" : "s")} to fix.";
  if (health.BreachedCount > 0)
   spoken += $" And {health.BreachedCount} breached password{(health.BreachedCount == 1 ? "" : "s")} that {(health.BreachedCount == 1 ? "needs" : "need")} immediate attention.";
       break;
    }

     // ── Conversational intents ──────────────────────────────────
 case VoiceCommandIntent.Help:
       spoken = "Here's what I can help with: "
      + "Check vault health and security scores. "
       + "Find weak, reused, or breached passwords. "
   + "Count your passwords or cards. "
      + "Add new logins, cards, or notes. "
      + "Lock your vault or sync to cloud. "
   + "Generate strong passwords. "
     + "Or start a voice journal – just say \"take a note\". "
     + "What would you like to do?";
    break;

 case VoiceCommandIntent.Greeting:
       {
      var health = await context.GetVaultHealthAsync();
    var count = await context.GetPasswordCountAsync();
     spoken = $"Hey! Your vault has {count} password{(count == 1 ? "" : "s")} "
     + $"and a health score of {health.Score} percent. ";
    if (health.WeakPasswordsCount > 0 || health.ReusedPasswordsCount > 0)
    spoken += "There are a few things to fix – just ask about your vault health for details.";
  else
  spoken += "Everything's looking good. What can I help you with?";
      break;
   }

     case VoiceCommandIntent.Thanks:
       spoken = "You're welcome! Let me know if you need anything else.";
      break;

    default:
  spoken = BuildUnknownResponse(result.RawTranscript);
   _logger?.LogWarning("VoiceCommandRouter: unhandled intent {I} for transcript: {T}", result.Intent, result.RawTranscript);
       break;
     }

 await context.SpeakAsync(spoken);

    return new VoiceCommandResult(result.Intent, result.RawTranscript,
   new Dictionary<string, string>(result.Entities.ToDictionary(k => k.Key, v => v.Value)))
         {
       SpokenResponse = spoken
 };
   }

  // ── Unknown intent helper ───────────────────────────────────────────────
        /// <summary>
      /// Builds a contextual help response when the user's command wasn't
  /// understood, rather than a generic "I didn't understand" message.
        /// Suggests the closest capability based on keywords in the transcript.
    /// </summary>
        private static string BuildUnknownResponse(string transcript)
        {
         var lower = transcript.ToLowerInvariant();

      // If they mentioned something password-related, guide them
     if (ContainsAny(lower, "password", "login", "credential", "account"))
          return "I wasn't sure what to do with that. You can say things like \"show my passwords\", \"how many passwords do I have\", \"show weak passwords\", or \"add a new login\".";

  if (ContainsAny(lower, "card", "payment", "visa", "mastercard"))
     return "I didn't catch that. Try saying \"show my cards\", \"how many cards do I have\", or \"add a credit card\".";

            if (ContainsAny(lower, "note", "journal", "memo", "write", "record", "dictate"))
    return "I didn't quite get that. Try \"take a note\", \"start journaling\", or \"open my notes\".";

            if (ContainsAny(lower, "security", "safe", "secure", "health", "risk", "breach"))
 return "I didn't catch the exact command. You can ask \"how secure is my vault?\", \"show weak passwords\", \"show breached accounts\", or \"what's my attack surface?\".";

   if (ContainsAny(lower, "sync", "backup", "cloud", "upload"))
                return "Try saying \"sync now\" or \"backup my vault\" to start a sync.";

    if (ContainsAny(lower, "lock", "close"))
       return "Say \"lock my vault\" to lock it right now.";

            if (ContainsAny(lower, "setting", "preference", "config", "option"))
         return "Say \"open settings\" to go to your preferences.";

    if (ContainsAny(lower, "help", "what can you do", "what can i say"))
     return "Here's what I can do: check your vault health, show weak or reused passwords, "
      + "count your passwords or cards, add new items, lock your vault, sync to cloud, "
           + "generate a strong password, or start a voice journal. Just ask naturally!";

   // Generic fallback – still more helpful than before
        return "I didn't understand that command. Here are some things you can try: "
          + "\"What's in my vault?\", \"show weak passwords\", \"vault health\", "
     + "\"add a new login\", \"lock my vault\", or \"take a note\".";
        }

        private static bool ContainsAny(string text, params string[] keywords)
            => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
