namespace Fortress.Mobile.Core.Intelligence
{
    internal static class AutofillRiskTrainingData
    {
      public static IEnumerable<LabelledAutofillRiskInput> Build() =>
Safe_ExactMatchTrustedDevice()
   .Concat(Safe_SubdomainTrustedApp())
      .Concat(Safe_KnownFormTrustedHistory())
     .Concat(Safe_OtpOnKnownSite())
  .Concat(Suspicious_PhishingDomainNewDevice())
   .Concat(Suspicious_PunycodeDomainWebView())
   .Concat(Suspicious_UrgentFormUnknownApp())
 .Concat(Suspicious_MismatchedDomainHighRisk());

  private static IEnumerable<LabelledAutofillRiskInput> Safe_ExactMatchTrustedDevice() =>
  [
   Row(false, 1,0,0,0,10,2,1,1,0,1,0,0,0,9, 20,1,1),
    Row(false, 1,0,0,0,12,2,1,1,0,1,0,0,0,14,15,1,1),
    Row(false, 1,0,0,0,9, 3,1,1,1,1,0,0,0,11,30,1,1),
    Row(false, 1,0,0,0,14,2,1,0,0,1,0,0,0,16,8, 1,1),
    Row(false, 1,0,0,0,11,4,1,1,0,1,0,0,0,10,50,1,1),
   Row(false, 1,0,0,0,10,2,1,1,0,0,0,0,0,8, 5, 1,1),
     Row(false, 1,0,0,0,10,2,1,1,0,1,0,0,0,13,12,1,1),
  ];

  private static IEnumerable<LabelledAutofillRiskInput> Safe_SubdomainTrustedApp() =>
        [
      Row(false, 0,1,0,0,15,2,1,1,0,1,0,0,0,10,7, 1,1),
   Row(false, 0,1,0,0,18,3,1,1,0,1,0,0,0,15,4, 1,0),
  Row(false, 0,1,0,1,16,2,1,1,0,0,0,0,0,12,9, 1,1),
  Row(false, 0,1,0,0,13,2,1,0,0,1,0,0,0,9, 22,1,1),
    Row(false, 0,1,0,0,20,4,1,1,1,1,0,0,0,11,3, 1,1),
   Row(false, 0,1,0,0,14,2,1,1,0,1,0,0,0,17,18,1,1),
     Row(false, 0,1,0,0,12,2,1,1,0,1,0,0,0,14,11,0,1),
  ];

 private static IEnumerable<LabelledAutofillRiskInput> Safe_KnownFormTrustedHistory() =>
      [
     Row(false, 1,0,0,0,11,2,1,1,0,1,0,0,0,22,40,1,1),
   Row(false, 1,0,0,0,11,2,1,1,0,1,0,0,0,7, 35,1,1),
    Row(false, 0,1,0,0,13,3,1,1,0,1,0,1,0,10,25,1,1),
    Row(false, 1,0,0,0,10,2,0,1,1,1,0,0,0,9, 60,1,1),
    Row(false, 1,0,0,0,12,2,1,1,0,1,0,0,0,15,14,1,1),
     Row(false, 1,0,0,0,9, 2,1,1,0,1,0,0,0,18,10,1,1),
    Row(false, 1,0,0,0,10,2,1,1,0,1,0,0,0,19,7, 1,1),
  ];

 private static IEnumerable<LabelledAutofillRiskInput> Safe_OtpOnKnownSite() =>
  [
     Row(false, 1,0,0,0,10,1,0,0,1,1,0,0,1,8, 0, 1,0),
  Row(false, 1,0,0,0,12,1,0,0,1,1,0,0,0,10,2, 1,1),
    Row(false, 0,1,0,0,15,1,0,0,1,1,0,0,0,14,5, 1,1),
     Row(false, 1,0,0,0,11,1,0,1,1,1,0,0,0,9, 10,1,1),
   Row(false, 1,0,0,0,9, 1,0,0,1,0,0,0,0,11,1, 1,1),
  ];

  private static IEnumerable<LabelledAutofillRiskInput> Suspicious_PhishingDomainNewDevice() =>
 [
  Row(true,  0,0,0,1,28,3,1,1,0,0,1,1,1,3, 0,0,0),
   Row(true,  0,0,0,1,32,4,1,1,0,0,1,1,1,2, 0,0,0),
 Row(true,  0,0,0,1,25,2,1,1,0,0,1,0,1,1, 0,0,0),
     Row(true,  0,0,0,1,30,5,1,1,0,0,1,1,1,4, 0,0,0),
  Row(true,  0,0,0,0,24,3,1,1,0,0,1,1,1,23,0,0,0),
     Row(true,  0,0,0,1,22,2,1,1,0,0,1,1,1,3, 0,0,0),
   Row(true,  0,0,0,0,26,3,1,1,0,0,1,1,1,2, 0,0,0),
   Row(true,  0,0,0,1,29,4,1,1,0,0,1,1,1,5, 0,0,0),
  ];

  private static IEnumerable<LabelledAutofillRiskInput> Suspicious_PunycodeDomainWebView() =>
     [
     Row(true,  0,0,1,0,20,2,1,1,0,0,0,1,0,14,0,0,0),
Row(true,  0,0,1,0,22,2,1,1,0,0,1,1,1,11,0,0,0),
 Row(true,  0,0,1,1,18,3,1,1,0,0,1,1,0,10,1,0,0),
   Row(true,  0,0,1,0,24,2,1,0,0,0,0,1,0,16,0,0,0),
     Row(true,  0,0,1,0,21,2,1,1,0,0,1,1,1,3, 0,0,0),
  Row(true,  0,0,1,1,19,3,1,1,0,0,0,1,0,22,0,0,0),
  ];

  private static IEnumerable<LabelledAutofillRiskInput> Suspicious_UrgentFormUnknownApp() =>
 [
   Row(true,  0,0,0,1,20,5,1,1,0,0,1,0,0,13,0,0,0),
    Row(true,  0,0,0,0,22,6,1,1,0,0,1,0,1,21,0,0,0),
   Row(true,  0,1,0,1,18,4,1,1,0,0,1,0,0,9, 0,0,0),
   Row(true,  0,0,0,1,23,5,1,1,1,0,1,0,1,1, 0,0,0),
     Row(true,  0,0,0,0,19,5,1,1,0,0,1,0,0,20,1,0,0),
    Row(true,  0,0,0,1,25,7,1,1,0,0,1,1,0,23,0,0,0),
  ];

  private static IEnumerable<LabelledAutofillRiskInput> Suspicious_MismatchedDomainHighRisk() =>
  [
   Row(true,  0,0,0,1,30,3,1,1,0,0,1,1,0,2, 0,0,0),
     Row(true,  0,0,0,1,27,2,1,1,0,0,0,1,1,4, 0,0,0),
   Row(true,  0,0,1,1,34,4,1,1,0,0,1,1,1,3, 0,0,0),
  Row(true,  0,0,0,0,28,3,1,0,0,0,1,1,1,23,0,0,0),
    Row(true,  0,0,0,1,35,5,1,1,0,0,1,1,1,1, 0,0,0),
  Row(true,  0,0,0,1,29,3,1,1,0,0,1,0,1,5, 0,0,0),
   ];

  private static LabelledAutofillRiskInput Row(
   bool label,
     float domainExact, float subdomain, float puny, float hyphen,
  float domLen, float fields, float pwd, float email, float otp,
  float hashKnown, float urgent, float webview, float newDev,
   float hour, float prevLogins, float trustedApp, float trustedDev) =>
new()
 {
   Label = label,
DomainExactMatch = domainExact,
   SubdomainMatch = subdomain,
    HasPunycode = puny,
      HasHyphen = hyphen,
   DomainLength = domLen,
     FieldCount = fields,
    HasPasswordField = pwd,
   HasEmailHint = email,
    HasOtpHint = otp,
      FormHashKnown = hashKnown,
     SubmitTextUrgent = urgent,
 IsWebView = webview,
     IsNewDevice = newDev,
    HourOfDay = hour,
   PreviousSuccessfulLogins = prevLogins,
  KnownTrustedApp = trustedApp,
  KnownTrustedDevice = trustedDev
  };
    }
}
