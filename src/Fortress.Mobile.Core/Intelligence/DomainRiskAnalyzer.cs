using Fortress.Mobile.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Fortress.Mobile.Core.Intelligence
{
    /// <summary>
    /// Offline domain-risk analyser implementing:
    /// <list type="bullet">
    ///   <item>eTLD+1 extraction and exact / subdomain matching</item>
    ///   <item>Levenshtein-distance typosquat detection (distance ? 2)</item>
    ///   <item>Homoglyph attack detection (0?o, 1?l/i, rn?m, etc.)</item>
    ///   <item>Sibling-domain / parent-domain direction checks</item>
    /// </list>
    /// No network calls. Deterministic. Safe for offline-first use.
    /// </summary>
    public sealed class DomainRiskAnalyzer : IDomainRiskAnalyzer
    {
        private readonly ILogger<DomainRiskAnalyzer>? _logger;
    private readonly PhishingUrlScorer _phishingScorer;

   // Well-known brand base-domains used for typosquat comparison.
    // Extend as needed – keep lowercase, eTLD+1 only.
        private static readonly HashSet<string> _brandDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            "google.com","gmail.com","youtube.com","googledrive.com",
 "microsoft.com","live.com","outlook.com","hotmail.com","office.com","onedrive.com",
    "apple.com","icloud.com",
            "facebook.com","instagram.com","whatsapp.com","messenger.com",
            "twitter.com","x.com","linkedin.com","reddit.com","tiktok.com",
          "amazon.com","aws.amazon.com","paypal.com","ebay.com","shopify.com",
     "netflix.com","spotify.com","discord.com","slack.com","zoom.us",
            "github.com","gitlab.com","bitbucket.org","stackoverflow.com",
            "chase.com","bankofamerica.com","wellsfargo.com","citibank.com","barclays.com","hsbc.com",
    "pnc.com","usbank.com","tdbank.com","capitalone.com","discover.com",
            "ally.com","schwab.com","fidelity.com","vanguard.com",
            "coinbase.com","binance.com","kraken.com","blockchain.com","metamask.io",
            "dropbox.com","box.com","notion.so","atlassian.com","jira.com",
        };

        // Homoglyph substitution table – maps confusable chars to a canonical form.
        private static readonly Dictionary<char, char> _homoglyphs = new()
        {
            ['0'] = 'o', ['1'] = 'l', ['|'] = 'l', ['!'] = 'i',
       ['3'] = 'e', ['4'] = 'a', ['5'] = 's', ['6'] = 'g',
   ['7'] = 't', ['8'] = 'b', ['@'] = 'a', ['$'] = 's',
 };

        public DomainRiskAnalyzer(ILogger<DomainRiskAnalyzer>? logger = null)
        {
            _logger = logger;
      _phishingScorer = new PhishingUrlScorer(null);
        }

      // ── Public API ────────────────────────────────────────────────────────────
        /// <inheritdoc />
        public DomainRiskResult GetRisk(string savedDomain, string currentDomain)
{
          if (string.IsNullOrWhiteSpace(savedDomain) || string.IsNullOrWhiteSpace(currentDomain))
    {
   return new DomainRiskResult
      {
   MatchType       = DomainMatchType.Mismatch,
   RiskLevel     = DomainRiskLevel.High,
     SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
     Reason    = "One or both domains are empty – cannot assess safety.",
                };
   }

            // ── NEW: ML phishing check on the current domain first ────────────────
       var phishing = _phishingScorer.Score(currentDomain);
      if (phishing.IsSuspicious)
            {
      _logger?.LogWarning(
       "DomainRiskAnalyzer: phishing signal on '{D}' ({E})",
         currentDomain, phishing.Explanation);

        return new DomainRiskResult
     {
        MatchType       = DomainMatchType.Similar,
        RiskLevel  = DomainRiskLevel.High,
       SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
         Reason          = $"Possible phishing site: {phishing.Explanation} (confidence {phishing.Probability:P0}).",
           PhishingProbability = phishing.Probability,
      };
  }
            // ─────────────────────────────────────────────────────────────────────
            var saved   = NormaliseHost(savedDomain);
  var current = NormaliseHost(currentDomain);

            _logger?.LogDebug("DomainRiskAnalyzer: saved={S} current={C}", saved, current);

         // ── 1. Exact full-host match ──────────────────────────────────────────
            if (saved.Equals(current, StringComparison.OrdinalIgnoreCase))
          return DomainRiskResult.Safe(saved, current);

  var savedBase   = ExtractBaseDomain(saved);
            var currentBase = ExtractBaseDomain(current);

            // ── 2. Exact eTLD+1 match (different subdomains) ─────────────────────
            if (!string.IsNullOrEmpty(savedBase) &&
          savedBase.Equals(currentBase, StringComparison.OrdinalIgnoreCase))
                return DomainRiskResult.Safe(savedBase, currentBase);

            // ── 3. Current is a subdomain of saved base – generally safe ─────────
  if (!string.IsNullOrEmpty(savedBase) &&
           current.EndsWith("." + savedBase, StringComparison.OrdinalIgnoreCase))
       {
  return new DomainRiskResult
       {
          MatchType       = DomainMatchType.Subdomain,
  RiskLevel       = DomainRiskLevel.Safe,
          SuggestedAction = AutofillSuggestedAction.AllowAutofill,
        Reason          = $"{current} is a subdomain of {savedBase}.",
        SavedBaseDomain   = savedBase,
       CurrentBaseDomain = currentBase,
     };
    }

            // ── 4. Saved is a subdomain of current base – caution ─────────────────
          if (!string.IsNullOrEmpty(currentBase) &&
            saved.EndsWith("." + currentBase, StringComparison.OrdinalIgnoreCase))
      {
      return new DomainRiskResult
           {
          MatchType  = DomainMatchType.Similar,
            RiskLevel    = DomainRiskLevel.Caution,
        SuggestedAction = AutofillSuggestedAction.RequireConfirm,
            Reason          = $"Your saved domain {saved} is a subdomain of the current site {current}. Confirm before filling.",
            SavedBaseDomain   = savedBase,
         CurrentBaseDomain = currentBase,
         };
   }

            // ── 5. Homoglyph attack ───────────────────────────────────────────────
            var savedNorm   = ApplyHomoglyphs(savedBase   ?? saved);
        var currentNorm = ApplyHomoglyphs(currentBase ?? current);

     if (!savedNorm.Equals(currentNorm, StringComparison.OrdinalIgnoreCase) &&
          savedNorm.Length > 3 && currentNorm.Length > 3 &&
    Levenshtein(savedNorm, currentNorm) == 0)
 {
    // Normalised versions match but original forms differ ? homoglyph
     return new DomainRiskResult
 {
   MatchType       = DomainMatchType.Similar,
        RiskLevel       = DomainRiskLevel.High,
    SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
      Reason          = $"Possible homoglyph attack: '{current}' looks like '{saved}' but uses visually similar characters.",
                    SavedBaseDomain   = savedBase ?? saved,
            CurrentBaseDomain = currentBase ?? current,
        };
        }

 // ── 6. Typosquat: Levenshtein distance ? 2 against known brands ──────
            var basesToCheck = new[] { savedBase ?? saved };
       foreach (var baseD in basesToCheck)
            {
 if (string.IsNullOrEmpty(baseD)) continue;
    int dist = Levenshtein(baseD, currentBase ?? current);
  if (dist > 0 && dist <= 2)
         {
      // Only warn if one of the domains is a known brand
          bool savedIsBrand   = _brandDomains.Contains(baseD);
 bool currentIsBrand = _brandDomains.Contains(currentBase ?? current);

       if (savedIsBrand || currentIsBrand)
        {
         return new DomainRiskResult
   {
       MatchType       = DomainMatchType.Similar,
      RiskLevel = DomainRiskLevel.High,
  SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
      Reason   = $"Possible typosquat: '{current}' is very similar to '{saved}' (edit distance {dist}). This may be a phishing site.",
          SavedBaseDomain   = savedBase ?? saved,
            CurrentBaseDomain = currentBase ?? current,
        };
      }

    // Non-brand near match – still worth a caution
  return new DomainRiskResult
        {
       MatchType       = DomainMatchType.Similar,
  RiskLevel    = DomainRiskLevel.Caution,
    SuggestedAction = AutofillSuggestedAction.RequireConfirm,
        Reason          = $"'{current}' is very similar to your saved domain '{saved}'. Please verify before filling.",
     SavedBaseDomain   = savedBase ?? saved,
       CurrentBaseDomain = currentBase ?? current,
       };
      }
            }

            // ── 7. Complete mismatch ──────────────────────────────────────────────
       return new DomainRiskResult
            {
          MatchType = DomainMatchType.Mismatch,
    RiskLevel= DomainRiskLevel.High,
         SuggestedAction = AutofillSuggestedAction.BlockUntilConfirm,
    Reason   = $"'{current}' does not match your saved domain '{saved}'.",
         SavedBaseDomain   = savedBase ?? saved,
  CurrentBaseDomain = currentBase ?? current,
     };
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        /// <summary>Extract host from a raw string that may or may not contain a scheme.</summary>
        private static string NormaliseHost(string raw)
        {
            raw = raw.Trim().ToLowerInvariant();
   if (string.IsNullOrEmpty(raw)) return raw;

  // Strip scheme if present
    if (raw.StartsWith("http://") || raw.StartsWith("https://"))
          {
     try { raw = new Uri(raw).Host; } catch { /* keep as-is */ }
            }
            // Strip port
   int colonIdx = raw.IndexOf(':');
     if (colonIdx > 0) raw = raw[..colonIdx];
    // Strip trailing slash / path
    int slashIdx = raw.IndexOf('/');
            if (slashIdx > 0) raw = raw[..slashIdx];
     // Strip leading "www."
      if (raw.StartsWith("www.")) raw = raw[4..];
 return raw;
        }

  /// <summary>Extract eTLD+1 using the existing DomainName parser.</summary>
    private static string? ExtractBaseDomain(string host)
        {
 if (DomainName.TryParse(host, out var dn))
   return dn.BaseDomain?.ToLowerInvariant();
  // Fallback: last two labels
            var parts = host.Split('.');
       return parts.Length >= 2
? string.Join(".", parts[^2], parts[^1])
   : host;
        }

     /// <summary>Replace confusable characters with canonical equivalents.</summary>
        private static string ApplyHomoglyphs(string input)
  {
          if (string.IsNullOrEmpty(input)) return input;
          var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input.ToLowerInvariant())
 sb.Append(_homoglyphs.TryGetValue(c, out var canon) ? canon : c);

  // "rn" ? "m" multi-char substitution
         return sb.ToString().Replace("rn", "m");
    }

        /// <summary>Standard iterative Levenshtein distance – O(n*m) time, O(min(n,m)) space.</summary>
        internal static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
   if (string.IsNullOrEmpty(b)) return a.Length;

            if (a.Length > b.Length) (a, b) = (b, a); // a is always shorter

            int[] prev = Enumerable.Range(0, a.Length + 1).ToArray();
          int[] curr = new int[a.Length + 1];

         for (int j = 1; j <= b.Length; j++)
       {
         curr[0] = j;
    for (int i = 1; i <= a.Length; i++)
     {
    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[i] = Math.Min(
       Math.Min(prev[i] + 1, curr[i - 1] + 1),
     prev[i - 1] + cost);
   }
      (prev, curr) = (curr, prev);
        }
            return prev[a.Length];
        }
}
}
