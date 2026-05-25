using Fortress.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Fortress.Core.Intelligence
{
    /// <summary>
    /// Offline domain-risk analyser: exact/subdomain matching, typosquat
    /// detection, homoglyph attack detection, phishing URL scoring.
    /// No network calls. Deterministic.
    /// </summary>
  public sealed class DomainRiskAnalyzer : IDomainRiskAnalyzer
{
  private readonly ILogger<DomainRiskAnalyzer>? _logger;
     private readonly PhishingUrlScorer _phishingScorer;

    private static readonly HashSet<string> _brandDomains = new(StringComparer.OrdinalIgnoreCase)
     {
     "google.com","gmail.com","youtube.com","microsoft.com","live.com","outlook.com",
  "hotmail.com","office.com","onedrive.com","apple.com","icloud.com",
           "facebook.com","instagram.com","whatsapp.com","twitter.com","x.com",
    "linkedin.com","reddit.com","tiktok.com","amazon.com","paypal.com","ebay.com",
   "netflix.com","spotify.com","discord.com","slack.com","zoom.us",
"github.com","gitlab.com","stackoverflow.com",
    "chase.com","bankofamerica.com","wellsfargo.com","barclays.com","hsbc.com",
     "coinbase.com","binance.com","dropbox.com","notion.so",
   };

        private static readonly Dictionary<char, char> _homoglyphs = new()
    {
         ['0']='o',['1']='l',['|']='l',['!']='i',['3']='e',
   ['4']='a',['5']='s',['6']='g',['7']='t',['8']='b',['@']='a',['$']='s',
   };

        public DomainRiskAnalyzer(ILogger<DomainRiskAnalyzer>? logger = null)
        {
    _logger = logger;
   _phishingScorer = new PhishingUrlScorer(null);
        }

  public DomainRiskResult GetRisk(string savedDomain, string currentDomain)
 {
  if (string.IsNullOrWhiteSpace(savedDomain) || string.IsNullOrWhiteSpace(currentDomain))
     return new DomainRiskResult
    {
    MatchType = DomainMatchType.Mismatch, RiskLevel = DomainRiskLevel.High,
       SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
    Reason = "One or both domains are empty — cannot assess safety.",
     };

     // ML phishing check first
    var phishing = _phishingScorer.Score(currentDomain);
     if (phishing.IsSuspicious)
  return new DomainRiskResult
       {
  MatchType = DomainMatchType.Similar, RiskLevel = DomainRiskLevel.High,
  SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
     Reason = $"Possible phishing site: {phishing.Explanation} (confidence {phishing.Probability:P0}).",
  PhishingProbability = phishing.Probability,
     };

   var saved   = NormaliseHost(savedDomain);
     var current = NormaliseHost(currentDomain);
     _logger?.LogDebug("DomainRiskAnalyzer: saved={S} current={C}", saved, current);

         // 1. Exact full-host match
  if (saved.Equals(current, StringComparison.OrdinalIgnoreCase))
  return DomainRiskResult.Safe(saved, current);

         var savedBase   = ExtractBaseDomain(saved);
   var currentBase = ExtractBaseDomain(current);

         // 2. Exact eTLD+1 match
   if (!string.IsNullOrEmpty(savedBase) &&
    savedBase.Equals(currentBase, StringComparison.OrdinalIgnoreCase))
  return DomainRiskResult.Safe(savedBase, currentBase);

  // 3. Current is a subdomain of saved base — safe
  if (!string.IsNullOrEmpty(savedBase) &&
   current.EndsWith("." + savedBase, StringComparison.OrdinalIgnoreCase))
  return new DomainRiskResult
       {
  MatchType = DomainMatchType.Subdomain, RiskLevel = DomainRiskLevel.Safe,
   SuggestedAction = AutofillSuggestedAction.AllowAutofill,
     Reason = $"{current} is a subdomain of {savedBase}.",
    SavedBaseDomain = savedBase, CurrentBaseDomain = currentBase ?? current,
        };

   // 4. Saved is a subdomain of current — caution
  if (!string.IsNullOrEmpty(currentBase) &&
  saved.EndsWith("." + currentBase, StringComparison.OrdinalIgnoreCase))
     return new DomainRiskResult
    {
   MatchType = DomainMatchType.Similar, RiskLevel = DomainRiskLevel.Caution,
    SuggestedAction = AutofillSuggestedAction.RequireConfirm,
  Reason = $"Your saved domain {saved} is a subdomain of the current site {current}. Confirm before filling.",
  SavedBaseDomain = savedBase ?? saved, CurrentBaseDomain = currentBase,
          };

         // 5. Homoglyph attack
 var savedNorm   = ApplyHomoglyphs(savedBase   ?? saved);
    var currentNorm = ApplyHomoglyphs(currentBase ?? current);
      if (!savedNorm.Equals(currentNorm, StringComparison.OrdinalIgnoreCase) &&
           savedNorm.Length > 3 && currentNorm.Length > 3 &&
     Levenshtein(savedNorm, currentNorm) == 0)
  return new DomainRiskResult
      {
     MatchType = DomainMatchType.Similar, RiskLevel = DomainRiskLevel.High,
  SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
       Reason = $"Possible homoglyph attack: '{current}' looks like '{saved}' but uses visually similar characters.",
    SavedBaseDomain = savedBase ?? saved, CurrentBaseDomain = currentBase ?? current,
  };

         // 6. Typosquat: Levenshtein ? 2
        var baseToCheck = savedBase ?? saved;
        if (!string.IsNullOrEmpty(baseToCheck))
  {
    int dist = Levenshtein(baseToCheck, currentBase ?? current);
  if (dist > 0 && dist <= 2)
        {
   bool isBrand = _brandDomains.Contains(baseToCheck) || _brandDomains.Contains(currentBase ?? current);
     return new DomainRiskResult
      {
   MatchType = DomainMatchType.Similar,
         RiskLevel = isBrand ? DomainRiskLevel.High : DomainRiskLevel.Caution,
      SuggestedAction = isBrand
   ? AutofillSuggestedAction.BlockUntilConfirm
    : AutofillSuggestedAction.RequireConfirm,
  Reason = isBrand
  ? $"Possible typosquat: '{current}' is very similar to '{saved}' (edit distance {dist})."
    : $"'{current}' is very similar to your saved domain '{saved}'. Please verify before filling.",
     SavedBaseDomain = savedBase ?? saved, CurrentBaseDomain = currentBase ?? current,
         };
  }
  }

  // 7. Mismatch
 return new DomainRiskResult
    {
  MatchType = DomainMatchType.Mismatch, RiskLevel = DomainRiskLevel.High,
  SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
  Reason = $"'{current}' does not match your saved domain '{saved}'.",
     SavedBaseDomain = savedBase ?? saved, CurrentBaseDomain = currentBase ?? current,
    };
    }

    private static string NormaliseHost(string raw)
    {
  raw = raw.Trim().ToLowerInvariant();
 if (string.IsNullOrEmpty(raw)) return raw;
        if (raw.StartsWith("http://") || raw.StartsWith("https://"))
     try { raw = new Uri(raw).Host; } catch { }
     int idx = raw.IndexOf(':'); if (idx > 0) raw = raw[..idx];
  idx = raw.IndexOf('/'); if (idx > 0) raw = raw[..idx];
     if (raw.StartsWith("www.")) raw = raw[4..];
   return raw;
    }

   private static string? ExtractBaseDomain(string host)
        {
  if (DomainName.TryParse(host, out var dn)) return dn?.BaseDomain?.ToLowerInvariant();
    var parts = host.Split('.');
  return parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : host;
 }

  private static string ApplyHomoglyphs(string input)
  {
 if (string.IsNullOrEmpty(input)) return input;
    var sb = new System.Text.StringBuilder(input.Length);
    foreach (char c in input.ToLowerInvariant())
     sb.Append(_homoglyphs.TryGetValue(c, out var canon) ? canon : c);
  return sb.ToString().Replace("rn", "m");
   }

  internal static int Levenshtein(string a, string b)
        {
   if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
   if (string.IsNullOrEmpty(b)) return a.Length;
  if (a.Length > b.Length) (a, b) = (b, a);
  int[] prev = Enumerable.Range(0, a.Length + 1).ToArray();
  int[] curr = new int[a.Length + 1];
  for (int j = 1; j <= b.Length; j++)
    {
   curr[0] = j;
    for (int i = 1; i <= a.Length; i++)
    {
 int cost = a[i - 1] == b[j - 1] ? 0 : 1;
    curr[i] = Math.Min(Math.Min(prev[i] + 1, curr[i - 1] + 1), prev[i - 1] + cost);
          }
    (prev, curr) = (curr, prev);
       }
  return prev[a.Length];
        }
    }
}
