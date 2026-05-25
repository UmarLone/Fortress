using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Credentials;
using AndroidX.Credentials.Provider;
using Bit.Droid.Autofill;
using Fortress.Mobile;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using ProviderAction = AndroidX.Credentials.Provider.Action;

namespace com.fortress.app
{
    [Android.App.Service(
        Permission = "android.permission.BIND_CREDENTIAL_PROVIDER_SERVICE",
        Exported = true,
        Label = "FORTRESS")]
    [Android.App.IntentFilter(new[] { "android.service.credentials.CredentialProviderService" })]
    [Android.App.MetaData("android.credentials.provider", Resource = "@xml/provider_capabilities")]
    public class FortressCredentialProviderService : CredentialProviderService
    {
 private static ILogger<FortressCredentialProviderService>? Log =>
  Shiny.Hosting.Host.GetService<ILogger<FortressCredentialProviderService>>();

        // Java.Lang.String inherits Java.Lang.Object – satisfies IOutcomeReceiver.OnError
      private static Java.Lang.Object JavaError(string msg) => new Java.Lang.String(msg);

     // ── BeginGetCredential ────────────────────────────────────────────────
  public override void OnBeginGetCredentialRequest(
      BeginGetCredentialRequest request,
      CancellationSignal cancellationSignal,
       IOutcomeReceiver callback)
        {
#if DEBUG
   Android.Widget.Toast.MakeText(this,
       $"[FORTRESS] CredentialProvider: {request.CallingAppInfo?.PackageName ?? "unknown"}",
      Android.Widget.ToastLength.Short)?.Show();
#endif
  try
      {
     Log?.LogDebug("[CredentialProvider] OnBeginGetCredentialRequest");

  if (PreferenceWrapper.Instance.IsApplicationLocked)
        {
      callback.OnResult(new BeginGetCredentialResponse.Builder()
    .AddCredentialEntry(BuildUnlockEntry())
        .Build());
           return;
           }

   Task.Run(async () =>
         {
      try
           {
    var entries = await BuildCredentialEntriesAsync(request);
    var rb = new BeginGetCredentialResponse.Builder();
      foreach (var e in entries) rb.AddCredentialEntry(e);
              rb.AddAction(BuildCreateNewAction());
      callback.OnResult(rb.Build());
        }
          catch (Exception ex)
   {
       Log?.LogError(ex, "[CredentialProvider] BuildCredentialEntries failed");
  callback.OnError(JavaError(ex.Message));
        }
    });
            }
            catch (Exception ex)
   {
      Log?.LogError(ex, "[CredentialProvider] OnBeginGetCredentialRequest threw");
         callback.OnError(JavaError(ex.Message));
     }
        }

        // ── BeginCreateCredential ─────────────────────────────────────────────
   public override void OnBeginCreateCredentialRequest(
   BeginCreateCredentialRequest request,
   CancellationSignal cancellationSignal,
            IOutcomeReceiver callback)
        {
  try
            {
         Log?.LogDebug("[CredentialProvider] OnBeginCreateCredentialRequest type={Type}", request.Type);

     var rb = new BeginCreateCredentialResponse.Builder();

     if (request is BeginCreatePasswordCredentialRequest pwdReq)
       rb.AddCreateEntry(BuildSavePasswordEntry(pwdReq));
       else if (request is BeginCreatePublicKeyCredentialRequest pkReq)
        rb.AddCreateEntry(BuildSavePasskeyEntry(pkReq));
     // else: unknown type – return empty so Android skips us

        callback.OnResult(rb.Build());
 }
            catch (Exception ex)
    {
                Log?.LogError(ex, "[CredentialProvider] OnBeginCreateCredentialRequest threw");
       callback.OnError(JavaError(ex.Message));
   }
        }

  // ── OnClearCredentialStateRequest (required abstract) ─────────────────
        public override void OnClearCredentialStateRequest(
         ProviderClearCredentialStateRequest request,
    CancellationSignal cancellationSignal,
            IOutcomeReceiver callback)
        {
  Log?.LogDebug("[CredentialProvider] OnClearCredentialStateRequest");
            callback.OnResult(null);
   }

   // ── BuildCredentialEntriesAsync ───────────────────────────────────────
        private async Task<List<CredentialEntry>> BuildCredentialEntriesAsync(
         BeginGetCredentialRequest request)
     {
            var entries = new List<CredentialEntry>();
      var resolver = Shiny.Hosting.Host.GetService<CredentialResolver>();
            if (resolver == null) return entries;

            string? originPackage = null;
            string? rpId = null;
            string? assertionRequestJson = null;   // ? store the full request JSON
         bool wantsPasswords = false;
          bool wantsPasskeys = false;

  foreach (var opt in request.BeginGetCredentialOptions)
   {
       if (opt is BeginGetPasswordOption)
   {
            wantsPasswords = true;
          originPackage = request.CallingAppInfo?.PackageName;
       }
      else if (opt is BeginGetPublicKeyCredentialOption pkOpt)
              {
         wantsPasskeys         = true;
          assertionRequestJson  = pkOpt.RequestJson;   // ? capture
            try
            {
    var json = System.Text.Json.JsonDocument.Parse(pkOpt.RequestJson);
                  rpId = json.RootElement.TryGetProperty("rpId", out var rp)
      ? rp.GetString()
          : request.CallingAppInfo?.PackageName;
                }
         catch { rpId = request.CallingAppInfo?.PackageName; }
            }
            }

    // ── Password entries ──────────────────────────────────────────────
          if (wantsPasswords)
            {
    var pwdOption = request.BeginGetCredentialOptions
       .OfType<BeginGetPasswordOption>()
     .FirstOrDefault();

       if (pwdOption != null)
              {
    var candidates = !string.IsNullOrEmpty(originPackage)
       ? await resolver.GetMatchingCredentialsAsync(originPackage)
   : await resolver.GetAllCredentialsAsync();

        foreach (var cred in candidates.Take(10))
           {
   try
{
  var pi = BuildFillPendingIntent(cred);
                  Java.Lang.ICharSequence username =
      new Java.Lang.String(cred.Username ?? cred.Domain ?? string.Empty);
               entries.Add(new PasswordCredentialEntry.Builder(this, username, pi, pwdOption)
     .SetDisplayName(cred.Domain ?? string.Empty)
               .Build());
  }
   catch (Exception ex)
             {
Log?.LogWarning(ex, "[CredentialProvider] Password entry failed for {D}", cred.Domain);
             }
       }
     }
         }

   // ── Passkey entries ───────────────────────────────────────────────
            if (wantsPasskeys && !string.IsNullOrEmpty(rpId))
{
       var pkOption = request.BeginGetCredentialOptions
      .OfType<BeginGetPublicKeyCredentialOption>()
        .FirstOrDefault();

if (pkOption != null)
  {
            var passkeys = await resolver.GetPasskeyCredentialsAsync(rpId);
       foreach (var pk in passkeys.Take(10))
  {
           try
{
        // ? forward assertionRequestJson so the activity can sign the right challenge
        var pi = BuildPasskeyFillPendingIntent(pk, assertionRequestJson);
     Java.Lang.ICharSequence username =
            new Java.Lang.String(pk.UserDisplayName ?? pk.UserName);
            entries.Add(new PublicKeyCredentialEntry.Builder(this, username, pi, pkOption)
      .SetDisplayName(pk.RpName ?? pk.RpId)
             .Build());
           }
         catch (Exception ex)
      {
       Log?.LogWarning(ex, "[CredentialProvider] Passkey entry failed for {Rp}", pk.RpId);
        }
          }
          }
            }

            return entries;
 }

     // ── PendingIntent builders ────────────────────────────────────────────
     private PendingIntent BuildFillPendingIntent(CredentialView cred)
        {
   var intent = new Intent(this, typeof(AutofillActivity));
      intent.PutExtra("autofill", true);
          intent.PutExtra("credentialManagerFlow", true);
            intent.PutExtra("credentialId", cred.Id.ToString());
  intent.PutExtra("autofillFrameworkName", cred.Domain ?? string.Empty);
    intent.PutExtra("autofillFrameworkUri",
                cred.CredentialType is "Web" or "Otp"
           ? $"https://{cred.Domain}"
              : $"androidapp://{cred.Domain}");

            return PendingIntent.GetActivity(
   this,
     cred.Id.GetHashCode() & 0x7FFFFFFF,
    intent,
        PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        }

        private PendingIntent BuildPasskeyFillPendingIntent(PasskeyItem pk, string? assertionRequestJson = null)
        {
    var intent = new Intent(this, typeof(AutofillActivity));
   intent.PutExtra("autofill",           true);
      intent.PutExtra("credentialManagerFlow",  true);
intent.PutExtra("passkeyFlow",   true);
       intent.PutExtra("passkeyId",     pk.Id.ToString());
  intent.PutExtra("autofillFrameworkName",    pk.RpName ?? pk.RpId);
        intent.PutExtra("autofillFrameworkUri",    $"https://{pk.RpId}");

   // Forward the full assertion request JSON so AutofillActivity can
            // extract the live challenge and any allowCredentials constraints.
       if (!string.IsNullOrWhiteSpace(assertionRequestJson))
 intent.PutExtra("passkeyAssertionRequestJson", assertionRequestJson);

           return PendingIntent.GetActivity(
     this,
          pk.Id.GetHashCode() & 0x7FFFFFFF,
     intent,
          PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        }

 // ── Entry builders ────────────────────────────────────────────────────
        private CredentialEntry BuildUnlockEntry()
 {
          var intent = new Intent(this, typeof(AutofillActivity));
            intent.PutExtra("autofill", true);
        intent.PutExtra("credentialManagerFlow", true);

            var pi = PendingIntent.GetActivity(
       this, 9999, intent,
     PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

      // Placeholder option required by PasswordCredentialEntry.Builder
    var opt = new BeginGetPasswordOption(
            new System.Collections.Generic.HashSet<string>(),
  new Bundle(),
    System.Guid.NewGuid().ToString());

  Java.Lang.ICharSequence username = new Java.Lang.String("Unlock FORTRESS to fill");
      return new PasswordCredentialEntry.Builder(this, username, pi, opt)
        .SetDisplayName("FORTRESS Password Manager")
  .Build();
        }

   private ProviderAction BuildCreateNewAction()
   {
        var intent = new Intent(this, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            intent.PutExtra("credentialManagerAddNew", true);

            var pi = PendingIntent.GetActivity(
     this, 10000, intent,
     PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

   // AuthenticationAction doesn't implicitly convert to ProviderAction in the binding
  var action = new AuthenticationAction.Builder(
      title: new Java.Lang.String("Open FORTRESS"),
      pendingIntent: pi)
          .Build();
            return (ProviderAction)(Java.Lang.Object)action;
   }

        private CreateEntry BuildSavePasswordEntry(BeginCreatePasswordCredentialRequest request)
   {
     var intent = new Intent(this, typeof(AutofillActivity));
            intent.PutExtra("credentialManagerSave", true);

      var pi = PendingIntent.GetActivity(
              this, 10001, intent,
          PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

         return new CreateEntry.Builder(
  accountName: new Java.Lang.String("FORTRESS"),
    pendingIntent: pi)
  .SetDescription("Save to FORTRESS Password Manager")
     .Build();
        }

     private CreateEntry BuildSavePasskeyEntry(BeginCreatePublicKeyCredentialRequest request)
{
   var intent = new Intent(this, typeof(AutofillActivity));
     intent.PutExtra("credentialManagerSave", true);
      intent.PutExtra("passkeyFlow", true);
      intent.PutExtra("passkeyRequestJson", request.RequestJson);

   var pi = PendingIntent.GetActivity(
             this, 10002, intent,
    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

    return new CreateEntry.Builder(
     accountName: new Java.Lang.String("FORTRESS"),
        pendingIntent: pi)
    .SetDescription("Save passkey to FORTRESS Password Manager")
     .Build();
        }
    }
}
