using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.Button;
using Microsoft.Extensions.Logging;
using Resource = Microsoft.Maui.Resource;
using View = Android.Views.View;
using Fortress.Mobile.Platforms.Android;

namespace Fortress.Droid.Renderers
{
    /// <summary>
    /// Bottom sheet that warns the user of a high-risk autofill request detected
    /// by the on-device <see cref="Fortress.Mobile.Core.Intelligence.AutofillRiskEngine"/>.
    ///
    /// Presents:
    ///   – The requesting domain
    ///   – The classified risk level (Medium / High)
    ///   – The threat probability as a percentage
    ///   – A human-readable reason string
    ///   – "Fill Anyway" (destructive, outlined) and "Cancel Fill" (primary) buttons
    ///
    /// Fires <see cref="OnFillAnyway"/> if the user explicitly accepts the risk,
    /// or <see cref="OnCancelled"/> if they dismiss or tap Cancel.
    /// </summary>
    public class RiskWarningBottomSheetFragment : BottomSheetDialogFragment
    {
        // ── Events ────────────────────────────────────────────────────────────────
        public event Action? OnFillAnyway;
        public event Action? OnCancelled;

        // ── Payload set by the caller ─────────────────────────────────────────────
        private readonly string _domain;
        private readonly string _riskLevel;       // "Medium" or "High"
        private readonly float  _probability;     // 0–1
        private readonly string _reason;

        // Prevents OnCancel (fired by Dismiss()) from double-invoking OnCancelled
        // when the Cancel button already called it explicitly.
        private bool _cancelFired;

        public RiskWarningBottomSheetFragment(
      string domain,
            string riskLevel,
     float  probability,
    string reason)
        {
            _domain      = domain;
       _riskLevel   = riskLevel;
   _probability = probability;
 _reason      = reason;
        }

        // ── Sheet behaviour ───────────────────────────────────────────────────────
  public override void OnStart()
        {
 base.OnStart();
            if (Dialog is not BottomSheetDialog dlg) return;
      var bs = dlg.FindViewById<FrameLayout>(Resource.Id.design_bottom_sheet);
    if (bs == null) return;
            bs.LayoutParameters.Height = ViewGroup.LayoutParams.WrapContent;
            bs.RequestLayout();
    var beh = BottomSheetBehavior.From(bs);
     beh.State       = BottomSheetBehavior.StateExpanded;
        beh.SkipCollapsed = true;
  beh.Draggable   = true;
        }

    public override Dialog OnCreateDialog(Bundle? savedInstanceState) =>
            new BottomSheetDialog(RequireContext(), Resource.Style.BottomSheetDialogTheme);

        // ── Inflate ───────────────────────────────────────────────────────────────
        public override View OnCreateView(
     LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
    {
     var view = inflater.Inflate(Resource.Layout.bs_risk_warning, container, false);

         // Apply Audiowide font to the FORTRESS hero title
        (view.FindViewWithTag("fortressTitle") as TextView)
        ?.SetTypeface(FontHelper.Audiowide(RequireContext()), Android.Graphics.TypefaceStyle.Bold);

            // ── Subtitle ──────────────────────────────────────────────────────────
   var subtitle = view.FindViewById<TextView>(Resource.Id.riskSubtitle)!;
 subtitle.Text = _riskLevel == "High"
          ? "FORTRESS has detected a high-risk fill request. This domain or application  looks suspicious."
     : "FORTRESS has detected an unusual fill request. Proceed with caution.";

      // ── Detail card ───────────────────────────────────────────────────────
   view.FindViewById<TextView>(Resource.Id.riskDomain)!.Text =
  string.IsNullOrWhiteSpace(_domain) ? "Unknown" : _domain;

            var levelView = view.FindViewById<TextView>(Resource.Id.riskLevel)!;
        levelView.Text = _riskLevel;
      levelView.SetTextColor(_riskLevel == "High"
     ? Android.Graphics.Color.ParseColor("#DC2626")   // red
     : Android.Graphics.Color.ParseColor("#D97706")); // amber

            view.FindViewById<TextView>(Resource.Id.riskProbability)!.Text =
         $"{_probability * 100f:F0}%";

            view.FindViewById<TextView>(Resource.Id.riskReason)!.Text =
    string.IsNullOrWhiteSpace(_reason)
         ? "The fill request has suspicious characteristics."
        : _reason;

            // ── Buttons ───────────────────────────────────────────────────────────
            view.FindViewById<MaterialButton>(Resource.Id.fillAnywayButton)!.Click += (_, _) =>
      {
          Dismiss();
    _ = LogRiskEventAsync(filled: true);
           OnFillAnyway?.Invoke();
       };

        view.FindViewById<MaterialButton>(Resource.Id.cancelRiskButton)!.Click += (_, _) =>
            {
        _cancelFired = true;   // mark before Dismiss() triggers OnCancel
                Dismiss();
  _ = LogRiskEventAsync(filled: false);
 OnCancelled?.Invoke();
       };

         Cancelable = true;
            return view;
        }

        public override void OnCancel(IDialogInterface dialog)
      {
            base.OnCancel(dialog);
            // Only fire if the Cancel button didn't already handle it
        if (!_cancelFired)
    OnCancelled?.Invoke();
      }

        // ── Factory helper ────────────────────────────────────────────────────────
        /// <summary>Persists a risk-warning activity log entry.</summary>
 private async Task LogRiskEventAsync(bool filled)
        {
   try
   {
    var processor = Shiny.Hosting.Host.GetService<IEventLogProcessor>();
   if (processor == null) return;

      await processor.ProcessEventLogAsync(new EventLog
       {
 EventType       = filled
         ? (int)EventLogType.AutofillWarnedRisk   // user filled despite warning
        : (int)EventLogType.AutofillBlockedRisk, // user cancelled
    CredentialLabel = _domain,
         Detail      = $"Risk: {_riskLevel} ({_probability * 100f:F0}%) – {_reason}",
     });
   }
        catch (Exception ex)
    {
     Shiny.Hosting.Host.GetService<ILogger<RiskWarningBottomSheetFragment>>()
      ?.LogWarning(ex, "[RiskWarning] Failed to persist activity log");
  }
 }

        /// <summary>
        /// Builds a human-readable reason string from the raw risk feature values
        /// so the user understands <em>why</em> the fill was flagged.
        /// </summary>
    public static string BuildReason(
  bool domainMismatch,
         bool hasPunycode,
    bool isWebView,
    bool isNewDevice,
            bool urgentText,
  bool hasHyphen)
        {
      var reasons = new List<string>();

  if (domainMismatch)  reasons.Add("the domain does not match your saved credential");
            if (hasPunycode)     reasons.Add("the domain uses punycode encoding (possible homoglyph attack)");
       if (isWebView)       reasons.Add("the request came from a WebView inside an unknown app");
            if (isNewDevice)     reasons.Add("this is an unrecognised device");
            if (urgentText)      reasons.Add("the form contains urgent language");
      if (hasHyphen)       reasons.Add("the domain contains a suspicious hyphen pattern");

   return reasons.Count == 0
  ? "Multiple suspicious signals were detected."
        : "Flagged because " + string.Join(", ", reasons) + ".";
        }
    }
}
