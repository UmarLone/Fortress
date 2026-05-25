using Android.App;
using Android.App.Assist;
using Android.App.Slices;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Text;
using Android.Views.Autofill;
using Android.Widget;
using Android.Widget.Inline;
using AndroidX.AutoFill.Inline;
using AndroidX.AutoFill.Inline.V1;
using com.fortress.app;
using FFImageLoading;
using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Newtonsoft.Json;
using Resource = Microsoft.Maui.Resource;
using SaveFlags = Android.Service.Autofill.SaveFlags;

namespace Bit.Droid.Autofill
{
    public class AutofillBuilder
    {
        // ── Risk engine ───────────────────────────────────────────────────────────
        // Single instance per process. TrainModel() runs once on first autofill
        // request via the Lazy initialiser. Subsequent calls reuse the engine.
        private static readonly Lazy<AutofillRiskEngine> _riskEngine = new(() =>
            {
                var engine = new AutofillRiskEngine();
                engine.TrainModel();
                return engine;
            });

        private static AutofillRiskEngine RiskEngine => _riskEngine.Value;

        /// <summary>
        /// Public accessor so <see cref="AutofillService"/> can force a warm-up
        /// probe and confirm training metrics appear in Logcat.
        /// </summary>
        public static AutofillRiskEngine RiskEngineInstance => RiskEngine;

        // Fills whose ML probability meets or exceeds this value are suppressed.
        private const float RiskBlockThreshold = 0.75f;

        // ─────────────────────────────────────────────────────────────────────────
        private static int _pendingIntentId = 0;
        private const string UnlockVault = "Please unlock Fortress";
        private const string OpenFortress = "Open Fortress";

        public static HashSet<string> TrustedBrowsers = new HashSet<string>
        {
            "com.duckduckgo.mobile.android",
            "com.google.android.googlequicksearchbox",
            "org.mozilla.focus",
       "org.mozilla.focus.beta",
  "org.mozilla.focus.nightly",
            "org.mozilla.klar",
        };

        public static HashSet<string> CompatBrowsers = new HashSet<string>
  {
            "alook.browser",
       "alook.browser.google",
        "com.amazon.cloud9",
            "com.android.browser",
            "com.android.chrome",
     "com.android.htmlviewer",
     "com.avast.android.secure.browser",
            "com.avg.android.secure.browser",
        "com.brave.browser",
            "com.brave.browser_beta",
    "com.brave.browser_default",
"com.brave.browser_dev",
            "com.brave.browser_nightly",
     "com.chrome.beta",
  "com.chrome.canary",
  "com.chrome.dev",
    "com.cookiegames.smartcookie",
     "com.cookiejarapps.android.smartcookieweb",
      "com.ecosia.android",
            "com.google.android.apps.chrome",
      "com.google.android.apps.chrome_dev",
   "com.google.android.captiveportallogin",
            "com.jamal2367.styx",
          "com.kiwibrowser.browser",
    "com.kiwibrowser.browser.dev",
      "com.microsoft.emmx",
            "com.microsoft.emmx.beta",
     "com.microsoft.emmx.canary",
            "com.microsoft.emmx.dev",
            "com.mmbox.browser",
            "com.mmbox.xbrowser",
     "com.mycompany.app.soulbrowser",
            "com.naver.whale",
            "com.opera.browser",
   "com.opera.browser.beta",
"com.opera.mini.native",
            "com.opera.mini.native.beta",
            "com.opera.touch",
       "com.qflair.browserq",
    "com.qwant.liberty",
     "com.sec.android.app.sbrowser",
      "com.sec.android.app.sbrowser.beta",
          "com.stoutner.privacybrowser.free",
       "com.stoutner.privacybrowser.standard",
            "com.vivaldi.browser",
          "com.vivaldi.browser.snapshot",
"com.vivaldi.browser.sopranos",
        "com.yandex.browser",
            "com.z28j.feel",
            "idm.internet.download.manager",
          "idm.internet.download.manager.adm.lite",
  "idm.internet.download.manager.plus",
     "io.github.forkmaintainers.iceraven",
            "mark.via",
            "mark.via.gp",
       "net.slions.fulguris.full.download",
            "net.slions.fulguris.full.download.debug",
    "net.slions.fulguris.full.playstore",
      "net.slions.fulguris.full.playstore.debug",
            "org.adblockplus.browser",
       "org.adblockplus.browser.beta",
"org.bromite.bromite",
      "org.bromite.chromium",
            "org.chromium.chrome",
  "org.codeaurora.swe.browser",
  "org.gnu.icecat",
            "org.mozilla.fenix",
       "org.mozilla.fenix.nightly",
            "org.mozilla.fennec_aurora",
            "org.mozilla.fennec_fdroid",
         "org.mozilla.firefox",
     "org.mozilla.firefox_beta",
            "org.mozilla.reference.browser",
            "org.mozilla.rocket",
   "org.torproject.torbrowser",
    "org.torproject.torbrowser_alpha",
     "org.ungoogled.chromium.extensions.stable",
  "org.ungoogled.chromium.stable",
            "us.spotco.fennec_dos",
        };

        public static HashSet<string> BlacklistedUris = new HashSet<string>
        {
  "androidapp://android",
     "androidapp://com.android.settings",
            "androidapp://com.fortress",
     "androidapp://com.oneplus.applocker",
            "androidapp://com.fortress.app",
    };

        // ── BuildDataset ──────────────────────────────────────────────────────────
        public async Task<(Dataset Dataset, bool IsWarning)> BuildDataset(
            Context context, FieldCollection fields, CredentialView credential,
            Parser parser = null, InlinePresentationSpec inlinePresentationSpec = null,
            bool skipRiskCheck = false)
        {
            bool isWarningDataset = false;

            var logo = await GetIcon(context, credential);

            var isCreditCard = credential.CredentialType == nameof(CredentialType.CreditCard) ||
                         credential.CredentialType == "CreditCard";

            var displayTitle = credential.Domain;
            var displaySubtitle = credential.Username;

            if (isCreditCard && !string.IsNullOrEmpty(credential.Data))
            {
                try
                {
                    var meta = JsonConvert.DeserializeObject<CardAutofillMeta>(credential.Data);
                    if (meta != null && !string.IsNullOrEmpty(meta.Number))
                    {
                        displayTitle = string.IsNullOrWhiteSpace(meta.CardholderName) ? "Credit Card" : meta.CardholderName;
                        displaySubtitle = MaskCardNumber(meta.Number);
                    }
                    else
                    {
                        var legacy = JsonConvert.DeserializeObject<CreditCardData>(credential.Data);
                        displayTitle = legacy?.Name ?? "Credit Card";
                        displaySubtitle = MaskCardNumber(legacy?.Number);
                    }
                }
                catch
                {
                    displayTitle = "Credit Card";
                    displaySubtitle = "";
                }
            }

            var overlayPresentation = BuildOverlayPresentation(
          displayTitle, displaySubtitle, logo, context, Android.Views.ViewStates.Visible);

            // Scale icon for crisp inline chip rendering
            var inlineIcon = ScaleIconForInline(logo, context);
            var inlinePresentation = BuildInlinePresentation(
         inlinePresentationSpec, displayTitle, displaySubtitle,
    Icon.CreateWithBitmap(inlineIcon ?? logo), null, context);

            var datasetBuilder = new Dataset.Builder(overlayPresentation);
            if (inlinePresentation != null)
                datasetBuilder.SetInlinePresentation(inlinePresentation);

            if (!fields?.Fields.Any() ?? true)
                return (null, isWarningDataset);

            var setValues = false;

            // ── Credit card ───────────────────────────────────────────────────────
            if (fields.FillableForCard && (credential.CredentialType == "CreditCard"
       || credential.CredentialType == nameof(CredentialType.CreditCard)))
            {
                var cardJson = !string.IsNullOrEmpty(credential.Meta) ? credential.Meta : credential.Data;
                setValues = ApplyCreditCardValues(fields, datasetBuilder, cardJson);
            }
            // ── Identity ──────────────────────────────────────────────────────────
            else if (fields.FillableForIdentity && (credential.CredentialType == "Identity"
          || credential.CredentialType == "Address"))
            {
                setValues = ApplyIdentityValues(fields, datasetBuilder, credential);
            }
            // ── Login ─────────────────────────────────────────────────────────────
            else if (fields.FillableForLogin)
            {
                foreach (var f in fields.PasswordFields)
                {
                    var val = FilledItem.ApplyValue(f, credential.Password);
                    if (val != null) { setValues = true; datasetBuilder.SetValue(f.AutofillId, val); }
                }
                foreach (var f in fields.UsernameFields)
                {
                    var val = FilledItem.ApplyValue(f, credential.Username);
                    if (val != null) { setValues = true; datasetBuilder.SetValue(f.AutofillId, val); }
                }
            }

            if (!setValues)
            {
                System.Diagnostics.Debug.WriteLine(
                $"?? BuildDataset: No values set for credential type={credential.CredentialType}, " +
             $"FillableForCard={fields.FillableForCard}, FillableForLogin={fields.FillableForLogin}");
                return (null, isWarningDataset);
            }

            // ── ML Risk Gate ──────────────────────────────────────────────────────
            try
            {
                // ── Persistent allowlist: user already accepted this pair ──────────
                var riskParser2 = parser ?? new Parser(null!, context);
                if (!skipRiskCheck &&
        PreferenceWrapper.Instance.IsAutofillRiskAccepted(credential.Id, riskParser2.Uri ?? string.Empty))
                {
                    System.Diagnostics.Debug.WriteLine(
               $"[RiskEngine] Skipping risk check for '{credential.Domain}' — on accepted allowlist.");
                    skipRiskCheck = true;
                }

                if (!skipRiskCheck)
                {
                    var riskParser = parser ?? new Parser(null!, context);
                    var riskInput = AutofillRiskContextBuilder.Build(riskParser, credential);
                    var risk = RiskEngine.Predict(riskInput);

                    System.Diagnostics.Debug.WriteLine(
                     $"[RiskEngine] domain={credential.Domain} " +
                       $"requestingUri={riskParser.Uri} " +
                       $"prob={risk.Probability:P0} level={risk.RiskLevel} " +
                             $"suspicious={risk.PredictedLabel}");

                    if (risk.Probability >= RiskBlockThreshold)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"⚠ [RiskEngine] Fill BLOCKED for '{credential.Domain}' " +
                          $"(probability={risk.Probability:P0})");
                        isWarningDataset = true;
                        var warningDs = BuildWarningDataset(context, fields, credential, risk, riskInput,
                         parser?._structure);
                        return (warningDs, true);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                $"[RiskEngine] Skipping risk check for '{credential.Domain}' — user accepted warning.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                   $"⚠ [RiskEngine] Prediction error — allowing fill: {ex.Message}");
            }

            return (datasetBuilder.Build(), isWarningDataset);
        }

        // ── CreateFillResponse ────────────────────────────────────────────────────
        public FillResponse.Builder CreateFillResponse(Parser parser, bool inlineAutofillEnabled,
      bool isvaultLocked, FillRequest fillRequest = null)
        {
            // Fire-and-forget is not possible here — the callback expects a sync result.
            // We call the async version synchronously via .GetAwaiter().GetResult() since
            // OnFillRequest already runs on a background thread (not the UI thread).
            return CreateFillResponseAsync(parser, inlineAutofillEnabled, isvaultLocked, fillRequest)
 .GetAwaiter().GetResult();
        }

        public async Task<FillResponse.Builder> CreateFillResponseAsync(Parser parser, bool inlineAutofillEnabled,
    bool isvaultLocked, FillRequest fillRequest = null)
        {
            IList<InlinePresentationSpec> inlinePresentationSpecs = null;
            var inlineMaxSuggestedCount = 0;

            if (inlineAutofillEnabled && fillRequest != null && (int)Build.VERSION.SdkInt >= 30)
            {
                var inlineSuggestionsRequest = fillRequest.InlineSuggestionsRequest;
                inlineMaxSuggestedCount = inlineSuggestionsRequest?.MaxSuggestionCount ?? 0;
                inlinePresentationSpecs = inlineSuggestionsRequest?.InlinePresentationSpecs;
            }

            System.Diagnostics.Debug.WriteLine(
              $"[Autofill] CreateFillResponseAsync — inlineEnabled={inlineAutofillEnabled} " +
                $"vaultLocked={isvaultLocked} " +
      $"inlineMaxCount={inlineMaxSuggestedCount} " +
                $"specsCount={inlinePresentationSpecs?.Count ?? 0} " +
             $"uri={parser.Uri}");

            var responseBuilder = new FillResponse.Builder();

            // ── Try to add rich per-credential inline datasets ────────────────
            // Like Bitwarden/Dashlane, show each matched credential as a separate
            // suggestion chip in the keyboard's inline autofill bar.
            bool addedRichDatasets = false;

            // Also check the actual vault lock state — even though the caller may
            // pass isvaultLocked=false, the vault may still be locked.
            var isActuallyLocked = isvaultLocked || PreferenceWrapper.Instance.IsApplicationLocked;

            if (!isActuallyLocked &&
                  inlinePresentationSpecs != null &&
               inlineMaxSuggestedCount > 0)
            {
                try
                {
                    // Services are registered as interfaces — resolve by interface
                    var resolver = Shiny.Hosting.Host.GetService<ICredentialResolver>();
                    var crypto = Shiny.Hosting.Host.GetService<ICryptographyService>();

                    System.Diagnostics.Debug.WriteLine(
               $"[Autofill] DI resolution — resolver={resolver != null} crypto={crypto != null}");

                    if (resolver != null && crypto != null)
                    {
                        List<CredentialView> matched = null;
                        var fillType = CipherType.Login;
                        if (parser.FieldCollection.FillableForCard)
                            fillType = CipherType.Card;
                        else if (parser.FieldCollection.FillableForIdentity)
                            fillType = CipherType.Identity;

                        System.Diagnostics.Debug.WriteLine(
                           $"[Autofill] Loading credentials — fillType={fillType} uri={parser.Uri}");

                        switch (fillType)
                        {
                            case CipherType.Card:
                                var cards = await resolver.GetCardCredentialsAsync();
                                var cardTasks = cards.Select(async c =>
                                 {
                                     if (string.IsNullOrEmpty(c.Meta)) return c;
                                     try
                                     {
                                         var meta = Newtonsoft.Json.JsonConvert.DeserializeObject<CardAutofillMeta>(c.Meta);
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
                                matched = (await Task.WhenAll(cardTasks)).ToList();
                                break;

                            case CipherType.Identity:
                                matched = await resolver.GetIdentityCredentialsAsync();
                                break;

                            default:
                                var creds = await resolver.GetMatchingCredentialsAsync(parser.Uri);
                                var loginTasks = (creds ?? new List<CredentialView>()).Select(async c =>
                        {
                            // Decrypt the OTP data
                            if (!string.IsNullOrEmpty(c.Data))
                            {
                                var d = await crypto.Decrypt(c.Data);
                                if (d.Succeeded) c.Data = d.Data;
                            }
                            // Decrypt the password — required for inline fill
                            if (!string.IsNullOrEmpty(c.Password))
                            {
                                var p = await crypto.Decrypt(c.Password);
                                if (p.Succeeded) c.Password = p.Data;
                            }
                            return c;
                        });
                                matched = (await Task.WhenAll(loginTasks)).ToList();
                                break;
                        }

                        System.Diagnostics.Debug.WriteLine(
                            $"[Autofill] Matched credentials: {matched?.Count ?? 0}");

                        if (matched != null && matched.Count > 0)
                        {
                            // Reserve the last inline spec slot for the "Open Fortress" fallback if there's room
                            var maxInline = inlineMaxSuggestedCount > 1
                                    ? Math.Min(matched.Count, inlineMaxSuggestedCount - 1)
                                    : Math.Min(matched.Count, inlineMaxSuggestedCount);
                            var specIndex = 0;

                            System.Diagnostics.Debug.WriteLine(
                         $"[Autofill] Building {maxInline} inline datasets (max={inlineMaxSuggestedCount}, specs={inlinePresentationSpecs.Count})");

                            for (int i = 0; i < maxInline; i++)
                            {
                                var credential = matched[i];
                                // Reuse the last available spec when we run out of unique ones
                                var spec = inlinePresentationSpecs[Math.Min(specIndex, inlinePresentationSpecs.Count - 1)];

                                try
                                {
                                    var (dataset, isWarning) = await BuildDataset(
                              parser.ApplicationContext, parser.FieldCollection, credential,
                                       parser, spec, skipRiskCheck: false);

                                    if (dataset != null)
                                    {
                                        responseBuilder.AddDataset(dataset);
                                        addedRichDatasets = true;
                                        specIndex++; // Only advance on successful dataset
                                        System.Diagnostics.Debug.WriteLine(
                                  $"[Autofill] ✓ Added inline dataset: {credential.Domain} / {credential.Username}");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine(
                                          $"[Autofill] ✗ BuildDataset returned null for: {credential.Domain}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[Autofill] Failed to build inline dataset for '{credential.Domain}': {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                      "[Autofill] ✗ Could not resolve CredentialResolver or CryptographyService from DI");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
               $"[Autofill] Rich inline datasets failed — falling back to generic: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine(
          $"[Autofill] addedRichDatasets={addedRichDatasets}, adding fallback 'Open Fortress' chip");

            // ── Always add the "Open Fortress" / "Unlock" fallback as the last chip ──
            // This ensures there's always at least one suggestion, and it opens
            // the full autofill bottom sheet for browsing all credentials.
            responseBuilder.AddDataset(GetDataset(
    parser.ApplicationContext, parser.FieldCollection, parser.Uri,
        isActuallyLocked, inlinePresentationSpecs, parser._structure));

            if (!PreferenceWrapper.Instance.IsSavePromptDisabled)
                AddSaveInfo(parser, fillRequest, responseBuilder, parser.FieldCollection);

            responseBuilder.SetIgnoredIds(parser.FieldCollection.IgnoreAutofillIds.ToArray());
            return responseBuilder;
        }
        // ── BuildWarningDataset ───────────────────────────────────────────────────
        // Called when the ML risk engine scores a fill >= 0.75.
        // Instead of silently returning null, we surface a warning suggestion.
        // Tapping it re-launches AutofillActivity with riskWarning=true so the
        // user sees RiskWarningBottomSheetFragment and can choose to fill anyway.
        private Dataset BuildWarningDataset(
         Context context,
         FieldCollection fields,
                   CredentialView credential,
                   AutofillRiskPrediction risk,
                   AutofillRiskInput riskInput,
             AssistStructure structure = null)
        {
            var view = new RemoteViews(context.PackageName, Resource.Layout.fillrequesttemplate);
            view.SetTextViewText(Resource.Id.FillRequestTitle, "Suspicious application — tap to review");
            view.SetTextViewText(Resource.Id.FillRequestSubtitle, "Autofill vault manager");

            var intent = new Intent(context, typeof(AutofillActivity));
            intent.PutExtra("autofill", true);
            intent.PutExtra(AutofillConstants.AutofillFramework, true);
            intent.PutExtra("riskWarning", true);
            intent.PutExtra("riskLevel", risk.RiskLevel.ToString());
            intent.PutExtra("riskProbability", risk.Probability);
            intent.PutExtra("riskDomain", credential.Domain ?? string.Empty);
            intent.PutExtra("credentialId", credential.Id.ToString());
            intent.PutExtra("autofillFrameworkUri", credential.Domain ?? string.Empty);
            intent.PutExtra("autofillFrameworkName", credential.Domain ?? string.Empty);
            // Pass fill type so ShowAutofillSheetAsync routes correctly after "Fill Anyway"
            intent.PutExtra("autofillFrameworkFillType",
                 fields.FillableForCard ? (int)CipherType.Card :
              fields.FillableForIdentity ? (int)CipherType.Identity :
                     (int)CipherType.Login);
            // Feature flags so the warning sheet builds a human-readable reason
            intent.PutExtra("riskDomainMismatch", riskInput.DomainExactMatch < 0.5f && riskInput.SubdomainMatch < 0.5f);
            intent.PutExtra("riskHasPunycode", riskInput.HasPunycode > 0.5f);
            intent.PutExtra("riskIsWebView", riskInput.IsWebView > 0.5f);
            intent.PutExtra("riskIsNewDevice", riskInput.IsNewDevice > 0.5f);
            intent.PutExtra("riskUrgentText", riskInput.SubmitTextUrgent > 0.5f);
            intent.PutExtra("riskHasHyphen", riskInput.HasHyphen > 0.5f);

            // Forward the AssistStructure so TriggerAutofill can parse fields
            // after the user taps "Fill Anyway".
            if (structure != null)
                intent.PutExtra(AutofillManager.ExtraAssistStructure, structure);

            var pendingIntent = PendingIntent.GetActivity(
           context, ++_pendingIntentId, intent,
              PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var datasetBuilder = new Dataset.Builder(view);
            datasetBuilder.SetAuthentication(pendingIntent?.IntentSender);

            foreach (var autofillId in fields.AutofillIds)
                datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));

            return datasetBuilder.Build();
        }

        // ── GetDataset (locked-vault / Open Fortress fallback) ─────────────────────────────────
        private Dataset GetDataset(Context context, FieldCollection fields, string uri,
            bool isvaultLocked, IList<InlinePresentationSpec> inlinePresentationSpecs = null,
    AssistStructure structure = null)
        {
            var view = new RemoteViews(context.PackageName, Resource.Layout.fillrequesttemplate);

            var intent = new Intent(context, typeof(AutofillActivity));
            intent.PutExtra("autofill", true);
            intent.PutExtra(AutofillConstants.AutofillFramework, true);
            intent.PutExtra("isVaultLocked", isvaultLocked);
            intent.PutExtra("autofillFrameworkName",
         uri.Replace(Constants.AndroidAppProtocol, string.Empty)
             .Replace("https://", string.Empty)
                .Replace("http://", string.Empty));
            intent.PutExtra("autofillFrameworkUri", uri);

            if (structure != null)
                intent.PutExtra(AutofillManager.ExtraAssistStructure, structure);

            if (fields.FillableForCard)
                intent.PutExtra("autofillFrameworkFillType", (int)CipherType.Card);
            else if (fields.FillableForIdentity)
                intent.PutExtra("autofillFrameworkFillType", (int)CipherType.Identity);
            else if (fields.FillableForLogin)
                intent.PutExtra("autofillFrameworkFillType", (int)CipherType.Login);
            else
                return null;

            var pendingIntent = PendingIntent.GetActivity(context, ++_pendingIntentId, intent,
        PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var title = isvaultLocked ? UnlockVault : OpenFortress;
            view.SetTextViewText(Resource.Id.FillRequestTitle, title);
            view.SetTextViewText(Resource.Id.FillRequestSubtitle, "Autofill vault manager");

            var icon = GetDefaultIcon(context);
            var scaledIcon = ScaleIconForInline(icon, context);
            var inlinePresentation = BuildInlinePresentation(
              inlinePresentationSpecs?.Last(), title, string.Empty,
          Icon.CreateWithBitmap(scaledIcon ?? icon), pendingIntent, context);

            var datasetBuilder = new Dataset.Builder(view);
            if (inlinePresentation != null)
                datasetBuilder.SetInlinePresentation(inlinePresentation);

            datasetBuilder.SetAuthentication(pendingIntent?.IntentSender);

            foreach (var autofillId in fields.AutofillIds)
                datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));

            return datasetBuilder.Build();
        }

        // ── Inline icon sizing ───────────────────────────────────────────────────
        // Android inline suggestions render icons at ~24–32dp. Scaling the bitmap
        // down avoids OOM on some keyboards and ensures the chip looks crisp.
        private const int InlineIconSizeDp = 32;

        private static Bitmap ScaleIconForInline(Bitmap source, Context context)
        {
            if (source == null) return null;
            var density = context.Resources?.DisplayMetrics?.Density ?? 2.5f;
            var sizePx = (int)(InlineIconSizeDp * density);
            if (source.Width == sizePx && source.Height == sizePx)
                return source;
            return Bitmap.CreateScaledBitmap(source, sizePx, sizePx, true);
        }

        // ── Presentations ─────────────────────────────────────────────────────────
        public static RemoteViews BuildOverlayPresentation(string text, string subtext,
       Bitmap iconId, Context context, Android.Views.ViewStates viewStates)
        {
            var view = new RemoteViews(context.PackageName, Resource.Layout.fillItemlayout);
            view.SetTextViewText(Resource.Id.Domain, text);
            view.SetTextViewText(Resource.Id.Username, subtext);
            view.SetImageViewBitmap(Resource.Id.icon, iconId);
            view.SetViewVisibility(Resource.Id.icon, viewStates);

            var intent = new Intent(context, typeof(AutofillActivity));
            intent.PutExtra("autofill", true);
            intent.PutExtra(AutofillConstants.AutofillFramework, true);

            var pendingIntent = PendingIntent.GetActivity(context, 0, intent,
  PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            view.SetOnClickPendingIntent(Resource.Id.icon, pendingIntent);
            return view;
        }

        public InlinePresentation BuildInlinePresentation(InlinePresentationSpec inlinePresentationSpec,
 string text, string subtext, Icon iconId, PendingIntent pendingIntent, Context context)
        {
            if ((int)Build.VERSION.SdkInt < 30 || inlinePresentationSpec == null)
                return null;

            pendingIntent ??= PendingIntent.GetService(context, 0, new Intent(),
                       PendingIntentFlags.OneShot | PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var slice = CreateInlinePresentationSlice(
inlinePresentationSpec, text, subtext, iconId, "Autofill option", pendingIntent, context);

            return slice != null ? new InlinePresentation(slice, inlinePresentationSpec, false) : null;
        }

        private Slice CreateInlinePresentationSlice(InlinePresentationSpec inlinePresentationSpec,
             string text, string subtext, Icon iconId, string contentDescription,
            PendingIntent pendingIntent, Context context)
        {
            var imeStyle = inlinePresentationSpec.Style;
            if (!UiVersions.GetVersions(imeStyle).Contains(UiVersions.InlineUiVersion1))
                return null;

            var contentBuilder = InlineSuggestionUi.NewContentBuilder(pendingIntent)
                 .SetContentDescription(contentDescription);

            if (!string.IsNullOrWhiteSpace(text)) contentBuilder.SetTitle(text);
            if (!string.IsNullOrWhiteSpace(subtext)) contentBuilder.SetSubtitle(subtext);
            if (iconId != null) contentBuilder.SetStartIcon(iconId);

            return contentBuilder.Build().JavaCast<InlineSuggestionUi.Content>()?.Slice;
        }

        // ── SaveInfo ──────────────────────────────────────────────────────────────
        public void AddSaveInfo(Parser parser, FillRequest fillRequest,
                 FillResponse.Builder responseBuilder, FieldCollection fields)
        {
            bool? compatRequest = null;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q && fillRequest != null)
                compatRequest = (fillRequest.Flags | FillRequest.FlagCompatibilityModeRequest) == fillRequest.Flags;

            var compatBrowser = compatRequest ?? CompatBrowsers.Contains(parser.PackageName);
            if (compatBrowser && fields.SaveType == SaveDataType.Password)
                return;

            var requiredIds = fields.GetRequiredSaveFields();
            if (fields.SaveType == SaveDataType.Generic || requiredIds.Length == 0)
                return;

            var saveBuilder = new SaveInfo.Builder(fields.SaveType, requiredIds);
            var optionalIds = fields.GetOptionalSaveIds();
            if (optionalIds.Length > 0)
                saveBuilder.SetOptionalIds(optionalIds);

            if (compatBrowser)
                saveBuilder.SetFlags(SaveFlags.SaveOnAllViewsInvisible);

            responseBuilder.SetSaveInfo(saveBuilder.Build());
        }

        // ── Icons ────────────────────────────────────────────────────────────────

        private Bitmap GetDefaultIcon(Context context)
        {
            var drawable = context.GetDrawable(Resource.Drawable.applogo);
            var bitmapDrawable = (BitmapDrawable)drawable;
            return bitmapDrawable.Bitmap;
        }

        private async Task<Bitmap> GetIcon(Context context, CredentialView credential)
        {
            try
            {
                var icon = await ImageService.Instance.LoadUrl(credential.IconUri)
                       .AsBitmapDrawableAsync(ImageService.Instance);
                return icon.Bitmap;
            }
            catch
            {
                var icon = await ImageService.Instance.LoadFileFromApplicationBundle(credential.FallbackIcon)
               .AsBitmapDrawableAsync(ImageService.Instance);
                return icon.Bitmap;
            }
        }

        // ── Credit card fill ──────────────────────────────────────────────────────
        private bool ApplyCreditCardValues(FieldCollection fields, Dataset.Builder datasetBuilder, string cardDataJson)
        {
            System.Diagnostics.Debug.WriteLine($"?? ApplyCreditCardValues: JSON={cardDataJson}");

            if (string.IsNullOrEmpty(cardDataJson))
                return false;

            try
            {
                CardAutofillMeta? meta = null;
                CreditCardData? legacy = null;

                try { meta = JsonConvert.DeserializeObject<CardAutofillMeta>(cardDataJson); } catch { }

                string cardNumber = meta?.Number ?? string.Empty;
                string cardholderName = meta?.CardholderName ?? string.Empty;
                string cvv = meta?.Cvv ?? string.Empty;
                string expMonth = meta?.ExpMonth ?? string.Empty;
                string expYear = meta?.ExpYear ?? string.Empty;

                if (string.IsNullOrEmpty(cardNumber) && string.IsNullOrEmpty(expMonth))
                {
                    try { legacy = JsonConvert.DeserializeObject<CreditCardData>(cardDataJson); } catch { }

                    if (legacy != null)
                    {
                        cardNumber = legacy.Number ?? string.Empty;
                        cardholderName = legacy.Name ?? string.Empty;
                        cvv = legacy.Cvv ?? string.Empty;
                        (expMonth, expYear) = ParseExpiry(legacy.Expiry ?? string.Empty);
                    }
                }

                if (expYear.Length == 2)
                    expYear = "20" + expYear;

                System.Diagnostics.Debug.WriteLine(
                        $"   Card: Name={cardholderName}, Number={cardNumber}, CVV={cvv}, Month={expMonth}, Year={expYear}");

                var setValues = false;

                foreach (var field in fields.CreditCardFields)
                {
                    if (FieldIsCreditCardNumberByHtml(field))
                    {
                        var val = FilledItem.ApplyValue(field, cardNumber);
                        if (val != null) { datasetBuilder.SetValue(field.AutofillId, val); setValues = true; }
                    }
                    else if (FieldIsCvvByHtml(field))
                    {
                        var val = FilledItem.ApplyValue(field, cvv);
                        if (val != null) { datasetBuilder.SetValue(field.AutofillId, val); setValues = true; }
                    }
                    else if (FieldIsExpiryMonthByHtml(field))
                    {
                        var val = FilledItem.ApplyValue(field, expMonth, monthValue: true);
                        if (val != null) { datasetBuilder.SetValue(field.AutofillId, val); setValues = true; }
                    }
                    else if (FieldIsExpiryYearByHtml(field))
                    {
                        var val = FilledItem.ApplyValue(field, expYear);
                        if (val != null) { datasetBuilder.SetValue(field.AutofillId, val); setValues = true; }
                    }
                    else if (FieldIsCardNameByHtml(field))
                    {
                        var val = FilledItem.ApplyValue(field, cardholderName);
                        if (val != null) { datasetBuilder.SetValue(field.AutofillId, val); setValues = true; }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"?? ApplyCreditCardValues: setValues={setValues}");
                return setValues;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? ApplyCreditCardValues: {ex.Message}");
                return false;
            }
        }

        // ── HTML field detection helpers ──────────────────────────────────────────
        private bool FieldIsCreditCardNumberByHtml(Field f)
        {
            var htmlName = GetHtmlAttribute(f, "name");
            var uaHints = GetHtmlAttribute(f, "ua-autofill-hints");
            return htmlName == "cc-number" || uaHints?.Contains("CREDIT_CARD_NUMBER") == true;
        }

        private bool FieldIsCvvByHtml(Field f)
        {
            var htmlName = GetHtmlAttribute(f, "name");
            var uaHints = GetHtmlAttribute(f, "ua-autofill-hints");
            return htmlName == "cc-csc" || uaHints?.Contains("CREDIT_CARD_VERIFICATION_CODE") == true;
        }

        private bool FieldIsExpiryMonthByHtml(Field f)
        {
            var htmlName = GetHtmlAttribute(f, "name");
            var uaHints = GetHtmlAttribute(f, "ua-autofill-hints");
            return htmlName == "cc-exp-month" || uaHints?.Contains("CREDIT_CARD_EXP_MONTH") == true;
        }

        private bool FieldIsExpiryYearByHtml(Field f)
        {
            var htmlName = GetHtmlAttribute(f, "name");
            var uaHints = GetHtmlAttribute(f, "ua-autofill-hints");
            return htmlName == "cc-exp-year"
       || uaHints?.Contains("CREDIT_CARD_EXP_4_DIGIT_YEAR") == true
      || uaHints?.Contains("CREDIT_CARD_EXP_2_DIGIT_YEAR") == true;
        }

        private bool FieldIsCardNameByHtml(Field f)
        {
            var htmlName = GetHtmlAttribute(f, "name");
            var uaHints = GetHtmlAttribute(f, "ua-autofill-hints");
            return htmlName == "cc-name"
           || uaHints?.Contains("CREDIT_CARD_NAME") == true
          || uaHints?.Contains("CREDIT_CARD_NAME_FULL") == true;
        }

        // ── Identity fill ─────────────────────────────────────────────────────────
        private bool ApplyIdentityValues(FieldCollection fields, Dataset.Builder datasetBuilder,
        CredentialView credential)
        {
            IdentityAutofillMeta? id = null;
            if (!string.IsNullOrEmpty(credential.Meta))
            {
                try { id = JsonConvert.DeserializeObject<IdentityAutofillMeta>(credential.Meta); } catch { }
            }

            var firstName = id?.FirstName ?? credential.Username ?? string.Empty;
            var lastName = id?.LastName ?? string.Empty;
            var middleName = id?.MiddleName ?? string.Empty;
            var email = id?.Email ?? credential.Password ?? string.Empty;
            var phone = id?.Phone ?? string.Empty;
            var address = id?.Address ?? string.Empty;
            var address2 = id?.Address2 ?? string.Empty;
            var city = id?.City ?? string.Empty;
            var state = id?.State ?? string.Empty;
            var postalCode = id?.PostalCode ?? string.Empty;

            var setValues = false;

            void TrySet(List<Field> fieldList, string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                foreach (var f in fieldList)
                {
                    var val = FilledItem.ApplyValue(f, value);
                    if (val != null) { datasetBuilder.SetValue(f.AutofillId, val); setValues = true; }
                }
            }

            TrySet(fields.IdentityFirstNameFields, firstName);
            TrySet(fields.IdentityLastNameFields, lastName);
            TrySet(fields.IdentityMiddleNameFields, middleName);
            TrySet(fields.IdentityEmailFields, email);
            TrySet(fields.IdentityPhoneFields, phone);
            TrySet(fields.IdentityAddressFields, address);
            TrySet(fields.IdentityAddress2Fields, address2);
            TrySet(fields.IdentityCityFields, city);
            TrySet(fields.IdentityStateFields, state);
            TrySet(fields.IdentityPostalFields, postalCode);

            return setValues;
        }

        // ── Shared helpers ────────────────────────────────────────────────────────
        private (string month, string year) ParseExpiry(string expiry)
        {
            if (string.IsNullOrWhiteSpace(expiry)) return ("", "");
            var parts = expiry.Split('/');
            if (parts.Length != 2) return ("", "");
            var month = parts[0].Trim();
            var year = parts[1].Trim();
            if (year.Length == 2) year = "20" + year;
            return (month, year);
        }

        private string MaskCardNumber(string cardNumber)
        {
            if (string.IsNullOrWhiteSpace(cardNumber)) return "";
            var cleaned = cardNumber.Replace(" ", "").Replace("-", "");
            return cleaned.Length < 4 ? "****" : "**** **** **** " + cleaned[^4..];
        }

        private string GetHtmlAttribute(Field f, string attributeName)
        {
            if (f.HtmlInfo?.Attributes == null) return null;
            foreach (var attr in f.HtmlInfo.Attributes)
            {
                var key = attr.First as Java.Lang.String;
                var val = attr.Second as Java.Lang.String;
                if (key != null && val != null &&
           key.ToString().Equals(attributeName, StringComparison.OrdinalIgnoreCase))
                    return val.ToString();
            }
            return null;
        }
    } // end AutofillBuilder

    internal class CreditCardData
    {
        public string Name { get; set; }
        public string Number { get; set; }
        public string Cvv { get; set; }
        public string Expiry { get; set; }
    }
}
