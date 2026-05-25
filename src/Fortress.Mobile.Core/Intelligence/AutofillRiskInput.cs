using Microsoft.ML.Data;

namespace Fortress.Mobile.Core.Intelligence
{
    public class AutofillRiskInput
    {
        [ColumnName(nameof(DomainExactMatch))]
 public float DomainExactMatch { get; set; }

        [ColumnName(nameof(SubdomainMatch))]
        public float SubdomainMatch { get; set; }

        [ColumnName(nameof(HasPunycode))]
 public float HasPunycode { get; set; }

        [ColumnName(nameof(HasHyphen))]
        public float HasHyphen { get; set; }

    [ColumnName(nameof(DomainLength))]
        public float DomainLength { get; set; }

        [ColumnName(nameof(FieldCount))]
        public float FieldCount { get; set; }

      [ColumnName(nameof(HasPasswordField))]
        public float HasPasswordField { get; set; }

        [ColumnName(nameof(HasEmailHint))]
        public float HasEmailHint { get; set; }

        [ColumnName(nameof(HasOtpHint))]
        public float HasOtpHint { get; set; }

        [ColumnName(nameof(FormHashKnown))]
        public float FormHashKnown { get; set; }

        [ColumnName(nameof(SubmitTextUrgent))]
public float SubmitTextUrgent { get; set; }

        [ColumnName(nameof(IsWebView))]
  public float IsWebView { get; set; }

        [ColumnName(nameof(IsNewDevice))]
        public float IsNewDevice { get; set; }

[ColumnName(nameof(HourOfDay))]
        public float HourOfDay { get; set; }

        [ColumnName(nameof(PreviousSuccessfulLogins))]
     public float PreviousSuccessfulLogins { get; set; }

   [ColumnName(nameof(KnownTrustedApp))]
        public float KnownTrustedApp { get; set; }

        [ColumnName(nameof(KnownTrustedDevice))]
 public float KnownTrustedDevice { get; set; }
    }

    public sealed class LabelledAutofillRiskInput : AutofillRiskInput
    {
[ColumnName("Label")]
        public bool Label { get; set; }
 }

    public sealed class AutofillRiskPrediction
    {
      [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
public float Score { get; set; }

        public AutofillRiskLevel RiskLevel => Probability switch
        {
       >= 0.75f => AutofillRiskLevel.High,
     >= 0.45f => AutofillRiskLevel.Medium,
            _ => AutofillRiskLevel.Low
        };
    }

public enum AutofillRiskLevel
    {
        Low,
        Medium,
   High
    }
}
