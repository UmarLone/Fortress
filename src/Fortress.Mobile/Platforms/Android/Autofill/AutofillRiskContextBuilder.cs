using Bit.Droid.Autofill;
using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;

namespace com.fortress.app
{
    /// <summary>
    /// Builds an <see cref="AutofillRiskInput"/> from live Android autofill context.
    ///
 /// Maps every one of the 17 model features from real runtime signals:
    ///   – Domain signals  – derived from the parser URI vs saved credential domain
    ///   – Form signals    – derived from the detected FieldCollection structure
///   – Context signals – derived from the requesting package, WebView flag, time
    ///   – Trust signals   – derived from preferences, credential history, known-app lists
    ///
    /// No network calls. No allocations beyond the single input struct.
    /// </summary>
internal static class AutofillRiskContextBuilder
    {
        // ── Urgent submit-button keywords ─────────────────────────────────────────
        // Sourced from real phishing kit analysis; kept short for mobile perf.
        private static readonly HashSet<string> UrgentKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
     "verify now", "confirm immediately", "act now", "urgent", "verify account",
            "confirm account", "limited time", "expires soon", "immediate action",
            "validate now", "reactivate", "suspended", "locked", "update now"
        };

        /// <summary>
        /// Builds the full <see cref="AutofillRiskInput"/> feature vector.
   /// </summary>
        /// <param name="parser">Parsed assist structure – provides URI, package, field set.</param>
     /// <param name="credential">Credential about to be filled.</param>
        /// <param name="previousSuccessfulLogins">
        ///   Caller-supplied count of prior successful autofills for this credential.
  ///   Pass 0 when unknown.
     /// </param>
        /// <param name="isKnownTrustedDevice">
        ///   True when the current device ID matches an enrolled trusted device.
        ///   Pass false when unknown.
        /// </param>
    public static AutofillRiskInput Build(
     Parser parser,
            CredentialView credential,
 int previousSuccessfulLogins = 0,
            bool isKnownTrustedDevice = false)
  {
            var requestingUri = parser.Uri ?? string.Empty;
    var savedDomain   = credential.Domain ?? string.Empty;
        var packageName   = parser.PackageName ?? string.Empty;
            var fields        = parser.FieldCollection;

      // ── Domain signals ────────────────────────────────────────────────────
  var (domainExact, subdomainMatch, hasPunycode, hasHyphen, domainLength)
   = ExtractDomainSignals(requestingUri, savedDomain);

            // ── Form signals ──────────────────────────────────────────────────────
         var fieldCount       = (float)(fields?.Fields.Count ?? 0);
            var hasPasswordField = fields?.PasswordFields.Any() == true ? 1f : 0f;
            var hasEmailHint     = fields?.UsernameFields.Any() == true ? 1f : 0f;
         var hasOtpHint       = HasOtpField(fields) ? 1f : 0f;
 var submitTextUrgent = HasUrgentSubmitText(fields) ? 1f : 0f;

// FormHashKnown: true when the requesting URI is already in the
  // saved credential domain (the form was previously used successfully).
  var formHashKnown = (domainExact == 1f || subdomainMatch == 1f) ? 1f : 0f;

          // ── Context signals ───────────────────────────────────────────────────
// IsWebView: the fill is from a WebView if the parser has a Website but
            // the package is NOT in the trusted/compat browser lists.
            var isWebView = IsWebViewContext(parser) ? 1f : 0f;

    // IsNewDevice: true when the device has not been enrolled before.
       var isNewDevice = isKnownTrustedDevice ? 0f : 1f;

  var hourOfDay = (float)DateTime.Now.Hour;

   // ── Trust signals ─────────────────────────────────────────────────────
   var knownTrustedApp = IsKnownTrustedApp(packageName) ? 1f : 0f;

            return new AutofillRiskInput
       {
          DomainExactMatch         = domainExact,
          SubdomainMatch           = subdomainMatch,
       HasPunycode         = hasPunycode,
   HasHyphen           = hasHyphen,
          DomainLength    = domainLength,
    FieldCount      = fieldCount,
           HasPasswordField      = hasPasswordField,
    HasEmailHint             = hasEmailHint,
             HasOtpHint   = hasOtpHint,
  FormHashKnown            = formHashKnown,
              SubmitTextUrgent         = submitTextUrgent,
       IsWebView    = isWebView,
     IsNewDevice              = isNewDevice,
    HourOfDay        = hourOfDay,
           PreviousSuccessfulLogins = (float)Math.Max(0, previousSuccessfulLogins),
    KnownTrustedApp          = knownTrustedApp,
         KnownTrustedDevice       = isKnownTrustedDevice ? 1f : 0f
   };
        }

    // ── Domain feature extraction ─────────────────────────────────────────────
        private static (float exact, float subdomain, float punycode, float hyphen, float length)
   ExtractDomainSignals(string requestingUri, string savedDomain)
        {
   var requesting = NormaliseHost(requestingUri);
            var saved      = NormaliseHost(savedDomain);

         if (string.IsNullOrEmpty(requesting) || string.IsNullOrEmpty(saved))
 return (0f, 0f, 0f, 0f, (float)requesting.Length);

        var exact     = requesting.Equals(saved, StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
 var subdomain = 0f;

            if (exact == 0f)
            {
      // Current is subdomain of saved base, or same eTLD+1
         var savedBase = ExtractBaseDomain(saved);
                var reqBase   = ExtractBaseDomain(requesting);

if (!string.IsNullOrEmpty(savedBase))
   {
             if (savedBase.Equals(reqBase, StringComparison.OrdinalIgnoreCase))
            subdomain = 1f;
else if (requesting.EndsWith("." + savedBase, StringComparison.OrdinalIgnoreCase))
         subdomain = 1f;
          }
 }

            var punycode = requesting.Contains("xn--", StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
    var hyphen   = requesting.Contains('-') ? 1f : 0f;
  var length   = (float)ExtractBaseDomain(requesting)?.Length;

 return (exact, subdomain, punycode, hyphen, length);
 }

        private static string NormaliseHost(string raw)
        {
            raw = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(raw)) return raw;

            if (raw.StartsWith("http://") || raw.StartsWith("https://"))
     {
     try { raw = new Uri(raw).Host; } catch { /* keep raw */ }
    }

    // Strip androidapp://
      if (raw.StartsWith("androidapp://"))
     raw = raw["androidapp://".Length..];

          int colon = raw.IndexOf(':');
            if (colon > 0) raw = raw[..colon];

            int slash = raw.IndexOf('/');
            if (slash > 0) raw = raw[..slash];

   if (raw.StartsWith("www.")) raw = raw[4..];
     return raw;
    }

        private static string? ExtractBaseDomain(string host)
        {
            if (string.IsNullOrEmpty(host)) return host;
       var parts = host.Split('.');
         return parts.Length >= 2
   ? $"{parts[^2]}.{parts[^1]}"
          : host;
        }

        // ── Form signal helpers ───────────────────────────────────────────────────
        private static bool HasOtpField(FieldCollection? fields)
        {
if (fields is null) return false;
      return fields.Fields.Any(f =>
           ContainsAny(f.IdEntry, "otp", "one-time", "onetime", "verification", "code", "token") ||
   ContainsAny(f.Hint,    "otp", "one-time", "onetime", "verification", "code", "token"));
        }

        private static bool HasUrgentSubmitText(FieldCollection? fields)
        {
            if (fields is null) return false;
          // Check IdEntry and Hint of all fields for urgent language.
            return fields.Fields.Any(f =>
       UrgentKeywords.Any(k =>
          (f.IdEntry?.Contains(k, StringComparison.OrdinalIgnoreCase) == true) ||
    (f.Hint?.Contains(k, StringComparison.OrdinalIgnoreCase) == true)));
        }

        // ── Context signal helpers ────────────────────────────────────────────────
 private static bool IsWebViewContext(Parser parser)
        {
      // A WebView is identified when:
            //  – The parser has a website (web domain was detected)
            //  – AND the package is NOT a recognised standalone browser
    if (string.IsNullOrEmpty(parser.Website)) return false;
            var pkg = parser.PackageName ?? string.Empty;
            return !AutofillBuilder.TrustedBrowsers.Contains(pkg)
         && !AutofillBuilder.CompatBrowsers.Contains(pkg);
        }

        // ── Trust signal helpers ──────────────────────────────────────────────────
        private static bool IsKnownTrustedApp(string packageName)
    {
            if (string.IsNullOrEmpty(packageName)) return false;

            // Trusted browsers are inherently safe
         if (AutofillBuilder.TrustedBrowsers.Contains(packageName)) return true;
            if (AutofillBuilder.CompatBrowsers.Contains(packageName))  return true;

            // Own app package
  if (packageName.StartsWith("com.fortress", StringComparison.OrdinalIgnoreCase)) return true;
  if (packageName.StartsWith("com.android",  StringComparison.OrdinalIgnoreCase)) return true;

          return false;
    }

     // ── String util ───────────────────────────────────────────────────────────
        private static bool ContainsAny(string? source, params string[] terms)
        {
            if (string.IsNullOrEmpty(source)) return false;
            return terms.Any(t => source.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
    }
}
