using Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Widget;
using Bit.Droid.Autofill;
using Fortress.Mobile;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;

namespace com.fortress.app
{
    [Android.App.Service(Permission = Manifest.Permission.BindAutofillService, Label = "Fortress", Exported = true)]
    [IntentFilter(new string[] { "android.service.autofill.AutofillService" })]
    [MetaData("android.autofill", Resource = "@xml/autofillservice")]
    [Register("com.fortress.app.AutofillService")]
    public class AutofillService : Android.Service.Autofill.AutofillService
    {
        private static ILogger<AutofillService> _log =>
         Shiny.Hosting.Host.GetService<ILogger<AutofillService>>();

        public AutofillService() { }

        public async override void OnFillRequest(FillRequest request, CancellationSignal cancellationSignal, FillCallback callback)
        {
          
            _ = Task.Run(() =>
            {
                try
                {
                    var engine = AutofillBuilder.RiskEngineInstance;
                    _log?.LogInformation("[RiskEngine] Warm-up OK. Type={T}", engine.GetType().Name);
                }
                catch (Exception ex)
                {
                    _log?.LogError(ex, "[RiskEngine] Warm-up failed: {Message}", ex.Message);
                }
            });
            try
            {
                var structure = request.FillContexts?.LastOrDefault()?.Structure;
                if (structure == null)
                {
                    _log?.LogDebug("[Autofill] OnFillRequest: structure is null — skipping");
                    return;
                }

                var parser = new Parser(structure, ApplicationContext);
                parser.Parse();

                _log?.LogDebug(
                    "[Autofill] Parsed — PackageName={PackageName} Website={Website} Uri={Uri} " +
                    "Fields={FieldCount} Hints={Hints} FocusedHints={FocusedHints}",
                  parser.PackageName,
                  parser.Website,
                  parser.Uri,
                  parser.FieldCollection?.Fields?.Count ?? 0,
                  string.Join(",", parser.FieldCollection?.Hints ?? new HashSet<string>()),
                  string.Join(",", parser.FieldCollection?.FocusedHints ?? new HashSet<string>()));
                if (parser.FieldCollection?.Fields != null)
                {
                    foreach (var f in parser.FieldCollection.Fields)
                    {
                        _log?.LogDebug(
                      "[Autofill] Field — IdEntry={IdEntry} Hint={Hint} " +
                      "InputType={InputType} Focused={Focused} AutofillHints={Hints}",
                      f.IdEntry, f.Hint, f.InputType, f.Focused,
                           string.Join(",", f.Hints ?? new List<string>()));
                    }
                }

                var shouldAutofill = await parser.ShouldAutofillAsync();
                _log?.LogDebug("[Autofill] ShouldAutofill={ShouldAutofill}", shouldAutofill);

                if (!shouldAutofill)
                    return;

                var builder = new AutofillBuilder();

                bool isVaultLocked =
                     PreferenceWrapper.Instance.IsApplicationLocked &&
                     (PreferenceWrapper.Instance.IsBiometricUnlockEnabled ||
                            PreferenceWrapper.Instance.IsPinUnlockEnabled);

                _log?.LogDebug("[Autofill] IsUseInlineAutofillEnabled={Inline} IsVaultLocked={Locked}",
                     PreferenceWrapper.Instance.IsUseInlineAutofillEnabled, isVaultLocked);

                     var response = await builder.CreateFillResponseAsync(
                      parser, PreferenceWrapper.Instance.IsUseInlineAutofillEnabled, isVaultLocked, request);

                if (response == null)
                {
                    _log?.LogDebug("[Autofill] CreateFillResponse returned null — no fillable fields matched");
                    return;
                }

                _log?.LogDebug("[Autofill] Responding with fill response");
                callback.OnSuccess(response.Build());
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, "[Autofill] OnFillRequest threw: {Message}", ex.Message);
                callback.OnFailure($"Error while processing autofill request: {ex.Message}");
            }
        }

        public override void OnSaveRequest(SaveRequest request, SaveCallback callback)
        {
            try
            {
                if (PreferenceWrapper.Instance.IsSavePromptDisabled)
                    return;

                var structure = request.FillContexts?.LastOrDefault()?.Structure;
                if (structure == null)
                    return;

                var parser = new Parser(structure, ApplicationContext);
                parser.Parse();

                var savedItem = parser.FieldCollection.GetSavedItem();
                if (savedItem == null)
                {
                    Toast.MakeText(this, "Unable to save this form.", ToastLength.Short).Show();
                    return;
                }

                var intent = new Intent(parser.ApplicationContext, typeof(MainActivity));
                intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);

                // ── Flag that HandleIntent should treat this as a save/add-new ──────
                // Use "autofillFrameworkAddNew" so HandleIntent's existing isAddNew
                // branch fires and publishes AutofillPasswordsEvent with IsAddOrSaveContext=true.
                intent.PutExtra("autofillFrameworkAddNew", true);

                // ── Common extras shared with the add-new path ────────────────────
                // "autofillFrameworkFillType" is the key HandleIntent reads for CipherType.
                intent.PutExtra("autofillFrameworkFillType", (int)savedItem.Type);

                switch (savedItem.Type)
                {
                    case CipherType.Login:
                        intent.PutExtra("autofillFrameworkName", parser.Uri
                            .Replace(Constants.AndroidAppProtocol, string.Empty)
                            .Replace("https://", string.Empty)
                            .Replace("http://", string.Empty));
                        intent.PutExtra("autofillFrameworkUri", parser.Uri);
                        // These are read by App.xaml.cs OnAutofillRequest via RequestingApplication
                        intent.PutExtra("autofillFrameworkUsername", savedItem.Login.Username);
                        intent.PutExtra("autofillFrameworkPassword", savedItem.Login.Password);
                        break;

                    case CipherType.Card:
                        // Reuse the same name/uri keys so the destination page gets something
                        // meaningful in its "domain" nav parameter.
                        intent.PutExtra("autofillFrameworkName", "Credit Card");
                        intent.PutExtra("autofillFrameworkUri", parser.Uri ?? string.Empty);
                        // Card-specific fields — read by HandleIntent and forwarded via
                        // RequestingApplication properties to AddEditCreditCardPage.
                        intent.PutExtra("autofillFrameworkCardName", savedItem.Card.Name ?? string.Empty);
                        intent.PutExtra("autofillFrameworkCardNumber", savedItem.Card.Number ?? string.Empty);
                        intent.PutExtra("autofillFrameworkCardExpMonth", savedItem.Card.ExpMonth ?? string.Empty);
                        intent.PutExtra("autofillFrameworkCardExpYear", savedItem.Card.ExpYear ?? string.Empty);
                        intent.PutExtra("autofillFrameworkCardCode", savedItem.Card.Code ?? string.Empty);
                        break;

                    default:
                        Toast.MakeText(this, "Unable to save this type of form.", ToastLength.Short).Show();
                        return;
                }

                StartActivity(intent);
                callback.OnSuccess();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, "[Autofill] OnSaveRequest threw: {Message}", ex.Message);
            }
        }
    }
}
