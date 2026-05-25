using Android.App;
using Android.App.Assist;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.Autofill;
using AndroidX.AppCompat.App;
using Bit.Droid.Autofill;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.EventAggregators;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Core.Utilities;
using Fortress.Droid.Renderers;
using Fortress.Droid.Utilities;
using Fortress.Mobile;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace com.fortress.app
{
    [Activity(
        NoHistory = true,
      LaunchMode = LaunchMode.SingleTop,
        Theme = "@style/Theme.Fortress.Autofill.Transparent"
    )]
    public class AutofillActivity : AppCompatActivity
    {
        private IList<CredentialView> _credentials;
        private AutofillBottomSheetFragment _sheet;
        private Timer _otpTimer;
        private bool _fillingInProgress;

        // Set to true when the user explicitly accepted the risk warning.
        // Passed to BuildDataset to bypass the ML risk gate for this fill only.
        private bool _skipRiskCheck;

        // Intent extras saved for after unlock
        private string _autofillFrameworkName;
        private string _autofillFrameworkUri;
        private string _autofillFrameworkUsername;
        private string _autofillFrameworkPassword;
        private FillRequestType _autofillFrameworkFillType;

        protected async override void OnCreate(Bundle savedInstanceState)
        {
            Intent?.Validate();
            base.OnCreate(savedInstanceState);

            if (!PreferenceWrapper.Instance.IsScreenCaptureEnabled)
                Window.AddFlags(WindowManagerFlags.Secure);
            else
                Window.ClearFlags(WindowManagerFlags.Secure);

            var isAutofillContext = Intent.GetBooleanExtra("autofill", false);
            var isSaveContext = Intent.GetBooleanExtra("autofillFrameworkSave", false);

            if (!isAutofillContext) return;

            // ── Risk-warning intercept ────────────────────────────────────────────
            // When BuildWarningDataset detected a high-risk fill it re-launches this
            // activity with riskWarning=true. Show the warning sheet; the user can
            // choose "Fill Anyway" (proceeds to normal autofill sheet) or "Cancel".
            if (Intent.GetBooleanExtra("riskWarning", false))
            {
                ShowRiskWarningSheet();
                return;
            }

            #region PassKeys
            // ── PASSKEY REGISTRATION ──────────────────────────────────────────
            if (Intent.GetBooleanExtra("credentialManagerSave", false) &&
                 Intent.GetBooleanExtra("passkeyFlow", false))
            {
                var requestJson = Intent.GetStringExtra("passkeyRequestJson");
                await HandlePasskeyRegistrationAsync(requestJson);
                return;
            }

            // ── PASSKEY ASSERTION ─────────────────────────────────────────────
            if (Intent.GetBooleanExtra("credentialManagerFlow", false) &&
              Intent.GetBooleanExtra("passkeyFlow", false))
            {
                var passkeyId = Intent.GetStringExtra("passkeyId");
                var assertionJson = Intent.GetStringExtra("passkeyAssertionRequestJson");
                await HandlePasskeyAssertionAsync(passkeyId, assertionJson);
                return;
            }

            #endregion
            // ── Legacy autofill / password credential-manager flow ────────────
            _autofillFrameworkName = Intent.GetStringExtra("autofillFrameworkName");
            _autofillFrameworkUri = Intent.GetStringExtra("autofillFrameworkUri");
            _autofillFrameworkUsername = Intent.GetStringExtra("autofillFrameworkUsername");
            _autofillFrameworkPassword = Intent.GetStringExtra("autofillFrameworkPassword");
            _autofillFrameworkFillType = (FillRequestType)Intent.GetIntExtra("autofillFrameworkFillType", 1);

            bool isLocked =
                PreferenceWrapper.Instance.IsApplicationLocked &&
                (PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
                 PreferenceWrapper.Instance.IsPinUnlockEnabled);

            if (isLocked)
            {
                // ── Show inline unlock sheet — no round-trip to MainActivity ──
                ShowUnlockSheet(_autofillFrameworkName, isSaveContext);
                return;
            }

            await ShowAutofillSheetAsync(isSaveContext);
        }


        // ── Unlock sheet ──────────────────────────────────────────────────────

        private void ShowUnlockSheet(string appName, bool isSaveContext)
        {
            var unlockSheet = new UnlockBottomSheetFragment(appName);
            unlockSheet.OnUnlocked += async () => await ShowAutofillSheetAsync(isSaveContext);
            unlockSheet.OnCancelled += () => { SetResult(Result.Canceled); FinishAndRemoveTask(); };
            unlockSheet.Show(SupportFragmentManager, "unlock_sheet");
        }

        // ── Risk warning sheet ────────────────────────────────────────────────────
        // Shown when the ML risk engine flagged the fill request as high-risk.
        // The user can review the reason and choose to fill anyway or cancel.

        private void ShowRiskWarningSheet()
        {
            var domain = Intent.GetStringExtra("riskDomain") ?? string.Empty;
            var levelStr = Intent.GetStringExtra("riskLevel") ?? "High";
            var probability = Intent.GetFloatExtra("riskProbability", 0.9f);

            var reason = RiskWarningBottomSheetFragment.BuildReason(
        domainMismatch: Intent.GetBooleanExtra("riskDomainMismatch", true),
       hasPunycode: Intent.GetBooleanExtra("riskHasPunycode", false),
          isWebView: Intent.GetBooleanExtra("riskIsWebView", false),
                 isNewDevice: Intent.GetBooleanExtra("riskIsNewDevice", false),
            urgentText: Intent.GetBooleanExtra("riskUrgentText", false),
           hasHyphen: Intent.GetBooleanExtra("riskHasHyphen", false));

            var sheet = new RiskWarningBottomSheetFragment(domain, levelStr, probability, reason);

            // Guard: OnCancelled can fire twice — once from the button click (which
            // calls Dismiss()) and again from OnCancel (triggered by that Dismiss).
            // A second call after the activity is already finishing throws an exception.
            var cancelHandled = false;
            void HandleCancel()
            {
                if (cancelHandled) return;
                cancelHandled = true;
                // Reply to the autofill framework with Canceled + an empty intent so
                // Android knows the authentication was definitively rejected and does
                // not re-surface the warning dataset chip.
                SetResult(Result.Canceled, new Intent());
                FinishAndRemoveTask();
            }

            sheet.OnFillAnyway += async () =>
          {
              // Restore autofill context from the intent extras that
              // BuildWarningDataset forwarded when it re-launched this activity.
              _autofillFrameworkName = domain;
              _autofillFrameworkUri = Intent.GetStringExtra("autofillFrameworkUri") ?? string.Empty;
              _autofillFrameworkFillType = (FillRequestType)Intent.GetIntExtra("autofillFrameworkFillType", 1);

              var credIdStr = Intent.GetStringExtra("credentialId") ?? string.Empty;
              if (Guid.TryParse(credIdStr, out var credId))
              {
                  try
                  {
                      var resolver = Shiny.Hosting.Host.GetService<CredentialResolver>();
                      var crypto = Shiny.Hosting.Host.GetService<CryptographyService>();
                      CredentialView credential = null;

                      switch (_autofillFrameworkFillType)
                      {
                          case FillRequestType.Card:
                              var cards = await resolver.GetCardCredentialsAsync();
                              credential = cards.FirstOrDefault(c => c.Id == credId);
                              if (credential != null && !string.IsNullOrEmpty(credential.Meta))
                              {
                                  try
                                  {
                                      var meta = System.Text.Json.JsonSerializer.Deserialize<CardAutofillMeta>(credential.Meta);
                                      if (meta != null)
                                      {
                                          if (!string.IsNullOrEmpty(meta.Number))
                                          { var r = await crypto.Decrypt(meta.Number); if (r.Succeeded) meta.Number = r.Data; }
                                          if (!string.IsNullOrEmpty(meta.Cvv))
                                          { var r = await crypto.Decrypt(meta.Cvv); if (r.Succeeded) meta.Cvv = r.Data; }
                                          credential.Meta = System.Text.Json.JsonSerializer.Serialize(meta);
                                      }
                                  }
                                  catch { }
                              }
                              break;

                          case FillRequestType.Identity:
                              var identities = await resolver.GetIdentityCredentialsAsync();
                              credential = identities.FirstOrDefault(c => c.Id == credId);
                              break;

                          default:
                              var all = await resolver.GetAllCredentialsAsync();
                              credential = all.FirstOrDefault(c => c.Id == credId);
                              if (credential != null)
                              {
                                  if (!string.IsNullOrEmpty(credential.Password))
                                  {
                                      var d = await crypto.Decrypt(credential.Password);
                                      if (d.Succeeded) credential.Password = d.Data;
                                  }
                                  // Decrypt the OTP secret so TriggerAutofill can generate the code
                                  if (credential.HasOtp && !string.IsNullOrEmpty(credential.Data))
                                  {
                                      var d = await crypto.Decrypt(credential.Data);
                                      if (d.Succeeded) credential.Data = d.Data;
                                  }
                              }
                              break;
                      }

                      if (credential != null)
                      {
                          _skipRiskCheck = true;
                          // ── Persist acceptance so this pair is never flagged again ──
   PreferenceWrapper.Instance.AcceptAutofillRisk(
         credential.Id,
       _autofillFrameworkUri ?? string.Empty);
 TriggerAutofill(credential, this);
       return;
                      }
                  }
                  catch (Exception ex)
                  {
                      Shiny.Hosting.Host.GetService<ILogger<AutofillActivity>>()
                        ?.LogWarning(ex, "[RiskWarning] Failed to load credential for direct fill — falling back to sheet");
                  }
              }

              await ShowAutofillSheetAsync(false);
          };

            sheet.OnCancelled += HandleCancel;

            sheet.Show(SupportFragmentManager, "risk_warning_sheet");
        }

        // ── Autofill sheet ────────────────────────────────────────────────────

        private async Task ShowAutofillSheetAsync(bool isSaveContext)
        {
            var request = new RequestingApplication
            {
                IsFillContext = true,
                IsAddOrSaveContext = isSaveContext,
                Name = _autofillFrameworkName,
                Package = _autofillFrameworkUri,
                Password = _autofillFrameworkPassword,
                Username = _autofillFrameworkUsername,
                FillRequestType = _autofillFrameworkFillType
            };

            _credentials = await LoadCredentialsAsync(request.Package);
            _sheet = new AutofillBottomSheetFragment(_credentials, request);
            _sheet.OnSelected += OnCredentialSelected;
            _sheet.OnAddNew += OnAddNewCredential;
            _sheet.OnLoadAll += OnLoadAll;
            _sheet.OnBlockSite += OnBlockSite;
            _sheet.Show(SupportFragmentManager, "autofill_sheet");
            StartOtpTimerIfNeeded();
        }

        // ── "Block this site" ─────────────────────────────────────────────

        private void OnBlockSite(object sender, string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return;
            PreferenceWrapper.Instance.BlockAutofillUri(uri);
            RunOnUiThread(() =>
                       {
                           Android.Widget.Toast.MakeText(this,
             $"Autofill blocked for {uri.Replace("androidapp://", "").Replace("https://", "").Replace("http://", "")}",
                Android.Widget.ToastLength.Short)?.Show();
                       });
            SetResult(Result.Canceled);
            FinishAndRemoveTask();
        }

        // ── "All" tab — load the full type-filtered list on demand ───────────────

        private async void OnLoadAll(object sender, BrowseCategory category)
        {
            try
            {
                var resolver = Shiny.Hosting.Host.GetService<CredentialResolver>();
                var crypto = Shiny.Hosting.Host.GetService<CryptographyService>();

                List<CredentialView> filtered;

                switch (category)
                {
                    case BrowseCategory.Cards:
                        // Fetch all cards, decrypt number+CVV
                        var cards = await resolver.GetCardCredentialsAsync();
                        var cardTasks = cards.Select(async c =>
                         {
                             if (string.IsNullOrEmpty(c.Meta)) return c;
                             try
                             {
                                 var meta = System.Text.Json.JsonSerializer.Deserialize<CardAutofillMeta>(c.Meta);
                                 if (meta != null)
                                 {
                                     if (!string.IsNullOrEmpty(meta.Number))
                                     { var r = await crypto.Decrypt(meta.Number); if (r.Succeeded) meta.Number = r.Data; }
                                     if (!string.IsNullOrEmpty(meta.Cvv))
                                     { var r = await crypto.Decrypt(meta.Cvv); if (r.Succeeded) meta.Cvv = r.Data; }
                                     c.Meta = System.Text.Json.JsonSerializer.Serialize(meta);
                                 }
                             }
                             catch { }
                             return c;
                         });
                        filtered = (await Task.WhenAll(cardTasks)).ToList();
                        break;

                    case BrowseCategory.Identities:
                        filtered = await resolver.GetIdentityCredentialsAsync();
                        break;

                    default: // Logins
                        var raw = await resolver.GetAllCredentialsAsync();
                        var loginTasks = raw.Select(async c =>
                             {
                                 var d = await crypto.Decrypt(c.Data);
                                 if (d.Succeeded) c.Data = d.Data;
                                 return c;
                             });
                        var all = (await Task.WhenAll(loginTasks)).ToList();
                        filtered = all.Where(c => c.CredentialType is "Web" or "Otp"
                  or "PhoneApplication" or "Application").ToList();

                        // Prime OTP fields
                        foreach (var c in filtered.Where(x => x.HasOtp))
                        {
                            try
                            {
                                var totp = OtpHelper.GenerateOtp(c.Data);
                                c.Code = totp.Code; c.Progress = totp.RemainingSeconds;
                                c.Duration = c.Duration > 0 ? c.Duration : 30;
                            }
                            catch { }
                        }
                        break;
                }

                RunOnUiThread(() => _sheet?.DeliverAllCredentials(filtered));
            }
            catch (Exception ex)
            {
                Shiny.Hosting.Host.GetService<ILogger<AutofillActivity>>()
             .LogError($"OnLoadAll failed: {ex.Message}");
            }
        }

        // ── Add new credential from autofill context ──────────────────────

        private void OnAddNewCredential(object sender, EventArgs e)
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            intent.PutExtra("autofillFrameworkAddNew", true);
            intent.PutExtra("autofillFrameworkName", _autofillFrameworkName ?? string.Empty);
            intent.PutExtra("autofillFrameworkUri", _autofillFrameworkUri ?? string.Empty);
            // ← pass fill type so the main app can route to the right Add page
            intent.PutExtra("autofillFrameworkFillType", (int)_autofillFrameworkFillType);
            StartActivity(intent);
            SetResult(Result.Canceled);
            FinishAndRemoveTask();
        }

        // ── Credential selected ───────────────────────────────────────────

        private void OnCredentialSelected(object sender, CredentialView credential) =>
         TriggerAutofill(credential, this);

        private async void TriggerAutofill(CredentialView credential, Activity activity)
        {
            // Prevent re-entry if a fill is already in progress
          if (_fillingInProgress) return;

            try
       {
                if (activity == null) return;

    if (!(activity.Intent?.GetBooleanExtra(AutofillConstants.AutofillFramework, false) ?? false))
         return;

      if (credential == null)
    {
       activity.SetResult(Result.Canceled);
       activity.FinishAndRemoveTask();
 return;
  }

          // ── Per-credential auth guard ─────────────────────────────────
    if (credential.RequireAuthBeforeFill &&
   (PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
 PreferenceWrapper.Instance.IsPinUnlockEnabled))
   {
         var authTcs = new TaskCompletionSource<bool>();
      RunOnUiThread(() =>
        {
              var unlockSheet = new UnlockBottomSheetFragment(credential.Domain);
      unlockSheet.OnUnlocked += () => authTcs.TrySetResult(true);
   unlockSheet.OnCancelled += () => authTcs.TrySetResult(false);
       unlockSheet.Show(SupportFragmentManager, "credential_auth_sheet");
         });
           var authPassed = await authTcs.Task;
           if (!authPassed) return;
             }

       // ── Accessibility flow: no AssistStructure available ──────────
   // Copy credentials to clipboard instead of field-filling.
     var isAccessibilityFlow = activity.Intent?.GetBooleanExtra("accessibilityFlow", false) ?? false;

         var structure = activity.Intent.GetParcelableExtra(
    AutofillManager.ExtraAssistStructure) as AssistStructure;

     if (structure == null && isAccessibilityFlow)
          {
    _fillingInProgress = true;
    if (_sheet != null) _sheet.FillCommitted = true;
        StopOtpTimer();

      var svc = Shiny.Hosting.Host.ServiceProvider.GetService<IDeviceServices>();
   var accCrypto = Shiny.Hosting.Host.GetService<CryptographyService>();

   // Decrypt password if needed
   var isAccLogin = credential.CredentialType is "Web" or "Otp" or "PhoneApplication" or "Application";
  if (isAccLogin && !string.IsNullOrEmpty(credential.Password))
 {
 var decrypted = await accCrypto.Decrypt(credential.Password);
         if (decrypted.Succeeded) credential.Password = decrypted.Data;
        }

        // Copy password to clipboard
         if (!string.IsNullOrEmpty(credential.Password))
  {
   await svc.CopyToClipboard(credential.Password, "Password copied",
  PreferenceWrapper.Instance.ClearClipboardTimeout, isSensitive: true);
   }

   // Handle OTP
    if (credential.HasOtp)
      {
        if (!string.IsNullOrEmpty(credential.Data))
        {
  var d = await accCrypto.Decrypt(credential.Data);
        if (d.Succeeded) credential.Data = d.Data;
      }
   var otp = OtpHelper.GenerateOtp(credential.Data).Code;
    if (!string.IsNullOrEmpty(otp))
          {
      await Shiny.Hosting.Host.GetService<IEventLogProcessor>()
      .ProcessEventLogAsync(new EventLog
   {
   EventType = (int)EventLogType.OtpCopied,
      CredentialId = credential.Id,
  CredentialLabel = credential.Domain,
      Detail = _autofillFrameworkUri,
       });
      }
      }

   var toastMsg = !string.IsNullOrEmpty(credential.Username)
    ? $"Password copied for {credential.Username} — paste into the app"
      : "Password copied — paste into the app";
        svc.Toast(toastMsg);

   _ = OnCredentialUsed(credential);

   activity.SetResult(Result.Ok);
  activity.FinishAndRemoveTask();
      return;
         }

      if (structure == null)
      {
           activity.SetResult(Result.Canceled);
      activity.FinishAndRemoveTask();
                return;
  }

    var parser = new Parser(structure, activity.ApplicationContext);
                parser.Parse();

                if (!parser.FieldCollection?.Fields?.Any() ?? true)
                {
                    System.Diagnostics.Debug.WriteLine("[AutofillActivity] TriggerAutofill: no fields — cancelling");
                    activity.SetResult(Result.Canceled);
                    activity.FinishAndRemoveTask();
                    return;
                }

                var isLoginCredential = credential.CredentialType is "Web" or "Otp" or "PhoneApplication" or "Application";
                if (isLoginCredential && string.IsNullOrWhiteSpace(parser.Uri))
                {
                    System.Diagnostics.Debug.WriteLine("[AutofillActivity] TriggerAutofill: login credential but URI is empty — cancelling");
                    activity.SetResult(Result.Canceled);
                    activity.FinishAndRemoveTask();
                    return;
                }

                var crypto = Shiny.Hosting.Host.GetService<CryptographyService>();
                if (isLoginCredential && !string.IsNullOrEmpty(credential.Password))
                {
                    var decrypted = await crypto.Decrypt(credential.Password);
                    if (decrypted.Succeeded) credential.Password = decrypted.Data;
                }

                // ── Build dataset — skip ML risk gate if user already accepted warning ──
                var (dataset, isWarningDataset) = await new AutofillBuilder().BuildDataset(
                          activity, parser.FieldCollection, credential, parser,
                    skipRiskCheck: _skipRiskCheck);

                if (dataset == null)
                {
                    System.Diagnostics.Debug.WriteLine("[AutofillActivity] TriggerAutofill: BuildDataset returned null — cancelling");
                    activity.SetResult(Result.Canceled);
                    activity.FinishAndRemoveTask();
                    return;
                }

                // ── Warning dataset: hand it to Android and stop here ─────────
                // BuildWarningDataset produced a chip that re-launches this activity
                // with riskWarning=true. Do NOT copy OTP, log use, or mark committed.
                if (isWarningDataset)
                {
                    var warnIntent = new Intent();
                    warnIntent.PutExtra(AutofillManager.ExtraAuthenticationResult, dataset);
                    activity.SetResult(Result.Ok, warnIntent);
                    activity.FinishAndRemoveTask();
                    return;
                }

                // ── Real fill ─────────────────────────────────────────────────
                _fillingInProgress = true;
                if (_sheet != null) _sheet.FillCommitted = true;
                StopOtpTimer();

                if (credential.HasOtp)
                {
                    var otp = OtpHelper.GenerateOtp(credential.Data).Code;
                    if (!string.IsNullOrEmpty(otp))
                    {
                        var svc = Shiny.Hosting.Host.ServiceProvider.GetService<IDeviceServices>();
                        await svc.CopyToClipboard(otp, "OTP copied to clipboard",
                         PreferenceWrapper.Instance.ClearClipboardTimeout);
                        svc.Toast("OTP copied to clipboard");
                        await Shiny.Hosting.Host.GetService<IEventLogProcessor>()
                         .ProcessEventLogAsync(new EventLog
                         {
                             EventType = (int)EventLogType.OtpCopied,
                             CredentialId = credential.Id,
                             CredentialLabel = credential.Domain,
                             Detail = _autofillFrameworkUri,
                         });
                    }
                }

                _ = OnCredentialUsed(credential);

                var replyIntent = new Intent();
                replyIntent.PutExtra(AutofillManager.ExtraAuthenticationResult, dataset);
                activity.SetResult(Result.Ok, replyIntent);
                activity.FinishAndRemoveTask();
            }
            catch (Exception ex)
            {
                Shiny.Hosting.Host.GetService<ILogger<AutofillActivity>>()
                     .LogError($"TriggerAutofill failed: {ex.Message}");
            }
        }

        // ── Event log ────────────────────────────────────────────────────

        private async Task OnCredentialUsed(CredentialView dto)
        {
            try
            {
                var type = (CredentialType)Enum.Parse(typeof(CredentialType), dto.CredentialType);
                var eventType = type == CredentialType.PhoneApplication
                    ? (int)EventLogType.PhonePasswordUsed
                     : (int)EventLogType.WebCredentialUsed;

                await Shiny.Hosting.Host.GetService<IEventLogProcessor>()
     .ProcessEventLogAsync(new EventLog
     {
         EventType = eventType,
         CredentialId = dto.Id,
         CredentialLabel = dto.Domain,
         Detail = _autofillFrameworkUri,
     });
            }
            catch (Exception ex)
            {
                Shiny.Hosting.Host.GetService<ILogger<AutofillActivity>>()
                     .LogInformation($"Event log error: {ex.Message}");
            }
        }

        // ── Credential loading ────────────────────────────────────────────

        private async Task<IList<CredentialView>> LoadCredentialsAsync(string package)
        {
            var resolver = Shiny.Hosting.Host.GetService<CredentialResolver>();
            var crypto = Shiny.Hosting.Host.GetService<CryptographyService>();

            // ── Route to the correct store based on what the form needs ──────────
            List<CredentialView> result;
            switch (_autofillFrameworkFillType)
            {
                case FillRequestType.Card:
                    result = await resolver.GetCardCredentialsAsync();
                    // Decrypt number + CVV so BuildDataset can write them directly
                    var cardTasks = result.Select(async c =>
            {
                if (string.IsNullOrEmpty(c.Meta)) return c;
                try
                {
                    var meta = System.Text.Json.JsonSerializer.Deserialize<CardAutofillMeta>(c.Meta);
                    if (meta == null) return c;
                    if (!string.IsNullOrEmpty(meta.Number))
                    {
                        var r = await crypto.Decrypt(meta.Number);
                        if (r.Succeeded) meta.Number = r.Data;
                    }
                    if (!string.IsNullOrEmpty(meta.Cvv))
                    {
                        var r = await crypto.Decrypt(meta.Cvv);
                        if (r.Succeeded) meta.Cvv = r.Data;
                    }
                    c.Meta = System.Text.Json.JsonSerializer.Serialize(meta);
                }
                catch { /* malformed meta — leave as-is */ }
                return c;
            });
                    result = (await Task.WhenAll(cardTasks)).ToList();
                    break;

                case FillRequestType.Identity:
                    result = await resolver.GetIdentityCredentialsAsync();
                    break;

                default: // Login
                    var creds = await resolver.GetMatchingCredentialsAsync(package);
                    var loginTasks = (creds ?? new List<CredentialView>()).Select(async c =>
               {
                   var decrypted = await crypto.Decrypt(c.Data);
                   if (decrypted.Succeeded) c.Data = decrypted.Data;
                   return c;
               });
                    result = (await Task.WhenAll(loginTasks)).ToList();

                    // Prime OTP fields
                    foreach (var c in result.Where(x => x.HasOtp))
                    {
                        try
                        {
                            var totp = OtpHelper.GenerateOtp(c.Data);
                            c.Code = totp.Code;
                            c.Progress = totp.RemainingSeconds;
                            c.Duration = c.Duration > 0 ? c.Duration : 30;
                        }
                        catch { }
                    }
                    break;
            }

            return result;
        }

        // ── OTP ticker ───────────────────────────────────────────────────

        /// <summary>
        /// Ticks OTP for every credential the sheet currently has — both
        /// the matched list and the lazily-loaded "All" list.
        /// </summary>
        private void TickOtp()
        {
            var lists = new List<IList<CredentialView>>();
            if (_credentials != null) lists.Add(_credentials);
            if (_sheet != null)
            {
                var allCreds = _sheet.AllCredentials;
                if (allCreds != null) lists.Add(allCreds);
            }

            bool anyOtp = false;
            foreach (var list in lists)
                foreach (var c in list.Where(x => x.HasOtp))
                {
                    anyOtp = true;
                    try
                    {
                        var totp = OtpHelper.GenerateOtp(c.Data);
                        c.Progress = totp.RemainingSeconds;
                        c.Code = totp.Code;
                        // Duration stays as-is — it was set on first prime and doesn't change
                    }
                    catch { /* ignore bad secrets */ }
                }

            if (anyOtp) _sheet?.NotifyCredentialsChanged();
        }

        private void StartOtpTimerIfNeeded()
        {
            // Start the timer whenever there are any OTP credentials anywhere,
            // not just in the match list.
            StopOtpTimer();
            _otpTimer = new Timer(_ => RunOnUiThread(TickOtp), null, 0, 1000);
        }

        private void StopOtpTimer()
        {
            _otpTimer?.Dispose();
            _otpTimer = null;
        }

        // ── Back press ───────────────────────────────────────────────────

        public override async void OnBackPressed()
        {
            // If a fill is already being committed, ignore the back press entirely.
            if (_fillingInProgress) return;

            if (BottomSheetManager.IsSheetOpen)
            {
                var sheet = BottomSheetManager.CurrentSheet;
                if (sheet != null)
                {
                    await sheet.DismissAsync();
                    BottomSheetManager.Clear();
                    return;
                }
            }
            base.OnBackPressed();
            Finish();
        }

        protected override void OnDestroy()
        {
            StopOtpTimer();
            base.OnDestroy();
        }
        #region PassKeys
        // ── PASSKEY REGISTRATION ──────────────────────────────────────────────
        // Receives the FIDO2 PublicKeyCredentialCreationOptions JSON from the
        // website/app, generates an ECDSA P-256 keypair, stores the PasskeyItem
        // in the vault, then returns the PublicKeyCredential JSON to Android so
        // the Credential Manager can relay it back to the caller.

        private async Task HandlePasskeyRegistrationAsync(string? requestJson)
        {
            var log = Shiny.Hosting.Host.GetService<ILogger<AutofillActivity>>();
            try
            {
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    log?.LogWarning("[Passkey] Registration: requestJson is empty");
                    SetResult(Result.Canceled);
                    FinishAndRemoveTask();
                    return;
                }

                // ── 1. Parse creation options ─────────────────────────────────
                using var doc = JsonDocument.Parse(requestJson);
                var root = doc.RootElement;

                var rpId = root.TryGetProperty("rp", out var rp) ? rp.TryGetProperty("id", out var rpIdEl) ? rpIdEl.GetString() ?? string.Empty : string.Empty : string.Empty;
                var rpName = root.TryGetProperty("rp", out var rp2) ? rp2.TryGetProperty("name", out var rpNameEl) ? rpNameEl.GetString() ?? rpId : rpId : rpId;
                var challengeB64 = root.TryGetProperty("challenge", out var chEl) ? chEl.GetString() ?? string.Empty : string.Empty;
                var userName = root.TryGetProperty("user", out var uEl) ? uEl.TryGetProperty("name", out var unEl) ? unEl.GetString() ?? string.Empty : string.Empty : string.Empty;
                var userDisplayName = root.TryGetProperty("user", out var uEl2) ? uEl2.TryGetProperty("displayName", out var udEl) ? udEl.GetString() ?? userName : userName : userName;
                var userHandleB64 = root.TryGetProperty("user", out var uEl3) ? uEl3.TryGetProperty("id", out var uhEl) ? uhEl.GetString() ?? string.Empty : string.Empty : string.Empty;

                if (string.IsNullOrEmpty(rpId) || string.IsNullOrEmpty(challengeB64))
                {
                    log?.LogWarning("[Passkey] Registration: missing rpId or challenge");
                    SetResult(Result.Canceled);
                    FinishAndRemoveTask();
                    return;
                }

                // ── 2. Generate ECDSA P-256 keypair ───────────────────────────
                using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var privateKeyDer = ecdsa.ExportPkcs8PrivateKey();          // PKCS#8 DER
                var publicKeyDer = ecdsa.ExportSubjectPublicKeyInfo();   // SPKI DER

                // ── 3. Build credential ID (random 32 bytes) ──────────────────
                var credIdBytes = RandomNumberGenerator.GetBytes(32);
                var credIdB64 = Base64UrlEncode(credIdBytes);

                // ── 4. Build COSE public key (ES256, algorithm -7) ────────────
                var ecParams = ecdsa.ExportParameters(false);
                var coseKey = BuildCoseKey(ecParams);      // CBOR Map

                // ── 5. Build authenticatorData ─────────────────────────────────
                //   rpIdHash (32) | flags (1) | signCount (4) | AAGUID (16) |
                //   credIdLen (2) | credId (N) | cosePublicKey
                var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
                const byte flags = 0x41;  // UP (user present) | AT (attested credential data)
                uint signCount = 0;
                var authData = BuildAuthenticatorData(rpIdHash, flags, signCount,
                credIdBytes, coseKey);

                // ── 6. Build attestation object (none format) ─────────────────
                var attObj = BuildAttestationObject(authData);       // CBOR

                // ── 7. Encrypt private key and persist PasskeyItem ────────────
                var crypto = Shiny.Hosting.Host.GetService<CryptographyService>();
                var privateKeyB64 = Convert.ToBase64String(privateKeyDer);
                var encResult = await crypto.Encrypt(privateKeyB64);

                var storage = Shiny.Hosting.Host.GetService<IDataStorageService>();
                var passkey = new PasskeyItem
                {
                    RpId = rpId,
                    RpName = rpName,
                    UserHandle = userHandleB64,
                    UserName = userName,
                    UserDisplayName = userDisplayName,
                    CredentialId = credIdB64,
                    EncryptedPrivateKey = encResult.Succeeded ? encResult.Data : privateKeyB64,
                    PublicKeyCose = Convert.ToBase64String(coseKey),
                    Algorithm = -7,
                    SignCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                await storage.SavePasskeyItemAsync(passkey);
                log?.LogInformation("[Passkey] Registered passkey for {RpId} / {User}", rpId, userName);

                // Log PasskeyRegistered
                try
                {
                    await Shiny.Hosting.Host.GetService<IEventLogProcessor>()
                    .ProcessEventLogAsync(new EventLog
                    {
                        EventType = (int)EventLogType.PasskeyRegistered,
                        CredentialLabel = rpName,
                        Detail = $"User: {userName}",
                    });
                }
                catch { /* never fail registration */ }

                // ── 8. Build PublicKeyCredential JSON response ────────────────
                var clientDataJson = BuildClientDataJson("webauthn.create", challengeB64, rpId);
                var clientDataJsonB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(clientDataJson));

                var responseJson = new
                {
                    id = credIdB64,
                    rawId = credIdB64,
                    type = "public-key",
                    response = new
                    {
                        clientDataJSON = clientDataJsonB64,
                        attestationObject = Base64UrlEncode(attObj),
                    }
                };
                var responseJsonStr = JsonSerializer.Serialize(responseJson);

                // ── 9. Return to Android Credential Manager ───────────────────
                var replyIntent = new Intent();
                replyIntent.PutExtra(
                  AndroidX.Credentials.Provider.CredentialProviderService.ExtraCreateCredentialResponse,
                      responseJsonStr);
                SetResult(Result.Ok, replyIntent);
                Finish();
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "[Passkey] Registration failed");
                SetResult(Result.Canceled);
                FinishAndRemoveTask();
            }
        }

        // ── PASSKEY ASSERTION ─────────────────────────────────────────────────
        // Loads the stored PasskeyItem, decrypts the private key, signs the
        // FIDO2 challenge and returns the assertion JSON to Android.

        private async Task HandlePasskeyAssertionAsync(string? passkeyIdStr, string? assertionRequestJson)
        {
            var log = Shiny.Hosting.Host.GetService<ILogger<AutofillActivity>>();
            try
            {
                if (string.IsNullOrWhiteSpace(passkeyIdStr) ||
    !Guid.TryParse(passkeyIdStr, out var passkeyGuid))
                {
                    log?.LogWarning("[Passkey] Assertion: invalid passkeyId '{Id}'", passkeyIdStr);
                    SetResult(Result.Canceled);
                    FinishAndRemoveTask();
                    return;
                }

                // ── 1. Load PasskeyItem from vault ────────────────────────────
                var storage = Shiny.Hosting.Host.GetService<IDataStorageService>();
                var passkey = await storage.GetPasskeyItemAsync(passkeyGuid);
                if (passkey == null)
                {
                    log?.LogWarning("[Passkey] Assertion: passkey {Id} not found", passkeyGuid);
                    SetResult(Result.Canceled);
                    FinishAndRemoveTask();
                    return;
                }

                // ── 2. Parse assertion challenge ──────────────────────────────
                string challengeB64 = string.Empty;
                string rpId = passkey.RpId;

                if (!string.IsNullOrWhiteSpace(assertionRequestJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(assertionRequestJson);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("challenge", out var ch)) challengeB64 = ch.GetString() ?? string.Empty;
                        if (root.TryGetProperty("rpId", out var rp)) rpId = rp.GetString() ?? rpId;
                    }
                    catch { /* use passkey's stored rpId */ }
                }

                if (string.IsNullOrEmpty(challengeB64))
                {
                    // Fallback — generate a random challenge
                    // (real assertion always provides one; this guards against missing extras)
                    challengeB64 = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
                }

                // ── 3. Decrypt private key ────────────────────────────────────
                var crypto = Shiny.Hosting.Host.GetService<CryptographyService>();
                var decResult = await crypto.Decrypt(passkey.EncryptedPrivateKey);
                var privateKeyB64 = decResult.Succeeded ? decResult.Data : passkey.EncryptedPrivateKey;
                var privateKeyDer = Convert.FromBase64String(privateKeyB64);

                // ── 4. Build clientDataJSON ───────────────────────────────────
                var clientDataJson = BuildClientDataJson("webauthn.get", challengeB64, rpId);
                var clientDataJsonBytes = Encoding.UTF8.GetBytes(clientDataJson);
                var clientDataJsonB64 = Base64UrlEncode(clientDataJsonBytes);

                // ── 5. Build authenticatorData (no attested credential data) ──
                var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
                const byte flags = 0x01;  // UP (user present) only
                var newSignCount = passkey.SignCount + 1;
                var authData = BuildAuthenticatorData(rpIdHash, flags, (uint)newSignCount,
                null, null);

                // ── 6. Sign: SHA-256(authData ++ SHA-256(clientDataJSON)) ──────
                var clientDataHash = SHA256.HashData(clientDataJsonBytes);
                var sigTarget = new byte[authData.Length + clientDataHash.Length];
                Buffer.BlockCopy(authData, 0, sigTarget, 0, authData.Length);
                Buffer.BlockCopy(clientDataHash, 0, sigTarget, authData.Length, clientDataHash.Length);

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportPkcs8PrivateKey(privateKeyDer, out _);
                // WebAuthn requires DER-encoded signature (not IEEE P1363)
                var signatureDer = ecdsa.SignData(sigTarget, HashAlgorithmName.SHA256,
                        DSASignatureFormat.Rfc3279DerSequence);

                // ── 7. Build assertion JSON response ──────────────────────────
                var responseJson = new
                {
                    id = passkey.CredentialId,
                    rawId = passkey.CredentialId,
                    type = "public-key",
                    response = new
                    {
                        clientDataJSON = clientDataJsonB64,
                        authenticatorData = Base64UrlEncode(authData),
                        signature = Base64UrlEncode(signatureDer),
                        userHandle = passkey.UserHandle,
                    }
                };
                var responseJsonStr = JsonSerializer.Serialize(responseJson);

                // ── 8. Persist updated sign count ─────────────────────────────
                await storage.IncrementPasskeySignCountAsync(passkeyGuid);

                // Log PasskeyUsed
                try
                {
                    await Shiny.Hosting.Host.GetService<IEventLogProcessor>()
                  .ProcessEventLogAsync(new EventLog
                  {
                      EventType = (int)EventLogType.PasskeyUsed,
                      CredentialLabel = passkey.RpName,
                      Detail = $"User: {passkey.UserName}, signCount: {newSignCount}",
                  });
                }
                catch { /* never fail assertion */ }

                // ── 9. Return to Android Credential Manager
                var replyIntent = new Intent();
                replyIntent.PutExtra(
            AndroidX.Credentials.Provider.CredentialProviderService.ExtraBeginGetCredentialResponse,
     responseJsonStr);
                SetResult(Result.Ok, replyIntent);
                Finish();

                log?.LogInformation("[Passkey] Assertion complete for {RpId} signCount={Count}",
             rpId, newSignCount);
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "[Passkey] Assertion failed");
                SetResult(Result.Canceled);
                FinishAndRemoveTask();
            }
        }

        // ── FIDO2 / CBOR helpers ──────────────────────────────────────────────

        /// <summary>
        /// Builds the authenticatorData byte string per the WebAuthn spec §6.1.
        /// <paramref name="credIdBytes"/> and <paramref name="coseKey"/> are only
        /// included during registration (AT flag set); pass null for assertion.
        /// </summary>
        private static byte[] BuildAuthenticatorData(
               byte[] rpIdHash, byte flags, uint signCount,
            byte[]? credIdBytes, byte[]? coseKey)
        {
            using var ms = new MemoryStream();
            ms.Write(rpIdHash, 0, 32);      // rpIdHash  32 bytes
            ms.WriteByte(flags);            // flags      1 byte
                                            // signCount big-endian 4 bytes
            ms.WriteByte((byte)(signCount >> 24));
            ms.WriteByte((byte)(signCount >> 16));
            ms.WriteByte((byte)(signCount >> 8));
            ms.WriteByte((byte)(signCount));

            if (credIdBytes != null && coseKey != null)
            {
                // AAGUID — 16 zero bytes (self-attested / none format)
                ms.Write(new byte[16], 0, 16);
                // credentialIdLength (big-endian uint16)
                var len = (ushort)credIdBytes.Length;
                ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)(len));
                ms.Write(credIdBytes, 0, credIdBytes.Length);
                ms.Write(coseKey, 0, coseKey.Length);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Encodes the EC public key as a COSE_Key CBOR map (ES256 / P-256).
        /// Map: { 1:2, 3:-7, -1:1, -2:x, -3:y }
        /// </summary>
        private static byte[] BuildCoseKey(ECParameters p)
        {
            var x = p.Q.X!;
            var y = p.Q.Y!;
            using var ms = new MemoryStream();

            // CBOR map with 5 entries: 0xa5
            ms.WriteByte(0xa5);
            WriteCborInt(ms, 1); WriteCborInt(ms, 2);    // kty: EC2
            WriteCborInt(ms, 3); WriteCborInt(ms, -7);   // alg: ES256
            WriteCborInt(ms, -1); WriteCborInt(ms, 1);    // crv: P-256
            WriteCborInt(ms, -2); WriteCborBytes(ms, x);  // x
            WriteCborInt(ms, -3); WriteCborBytes(ms, y);  // y

            return ms.ToArray();
        }

        /// <summary>
        /// Wraps authenticatorData in a minimal CBOR attestation object
        /// with "none" format and empty attStmt.
        /// { "fmt":"none", "attStmt":{}, "authData":<bytes> }
        /// </summary>
        private static byte[] BuildAttestationObject(byte[] authData)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0xa3);     // map(3)
            WriteCborText(ms, "fmt"); WriteCborText(ms, "none");
            WriteCborText(ms, "attStmt"); ms.WriteByte(0xa0);  // empty map
            WriteCborText(ms, "authData"); WriteCborBytes(ms, authData);
            return ms.ToArray();
        }

        /// <summary>
        /// Builds the minimal clientDataJSON string required by WebAuthn.
        /// </summary>
        private static string BuildClientDataJson(string type, string challengeB64, string origin)
        {
            // origin must be a scheme+host — passkeys.io → https://passkeys.io
            var originUri = origin.StartsWith("http") ? origin : $"https://{origin}";
            return JsonSerializer.Serialize(new
            {
                type,
                challenge = challengeB64,
                origin = originUri,
                crossOrigin = false,
            });
        }

        // ── CBOR primitives ───────────────────────────────────────────────────

        private static void WriteCborInt(Stream s, int v)
        {
            if (v >= 0)
            {
                if (v <= 23) { s.WriteByte((byte)v); }
                else if (v <= 0xff) { s.WriteByte(0x18); s.WriteByte((byte)v); }
                else { s.WriteByte(0x19); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
            }
            else
            {
                // Negative integers: major type 1, value = -1 - n
                int n = -1 - v;
                if (n <= 23) { s.WriteByte((byte)(0x20 | n)); }
                else if (n <= 0xff) { s.WriteByte(0x38); s.WriteByte((byte)n); }
                else { s.WriteByte(0x39); s.WriteByte((byte)(n >> 8)); s.WriteByte((byte)n); }
            }
        }

        private static void WriteCborBytes(Stream s, byte[] data)
        {
            // Major type 2 (byte string)
            if (data.Length <= 23) { s.WriteByte((byte)(0x40 | data.Length)); }
            else if (data.Length <= 0xff) { s.WriteByte(0x58); s.WriteByte((byte)data.Length); }
            else { s.WriteByte(0x59); s.WriteByte((byte)(data.Length >> 8)); s.WriteByte((byte)data.Length); }
            s.Write(data, 0, data.Length);
        }

        private static void WriteCborText(Stream s, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            // Major type 3 (text string)
            if (bytes.Length <= 23) { s.WriteByte((byte)(0x60 | bytes.Length)); }
            else if (bytes.Length <= 0xff) { s.WriteByte(0x78); s.WriteByte((byte)bytes.Length); }
            else { s.WriteByte(0x79); s.WriteByte((byte)(bytes.Length >> 8)); s.WriteByte((byte)bytes.Length); }
            s.Write(bytes, 0, bytes.Length);
        }

        /// <summary>RFC 4648 §5 Base64url without padding.</summary>
        private static string Base64UrlEncode(byte[] data) =>
                Convert.ToBase64String(data)
               .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        #endregion
    }
}
