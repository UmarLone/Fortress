using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fortress.Mobile.Core.Intelligence
{
    /// <summary>
/// Offline item classifier – assigns criticality, icon, and tags using
/// keyword lists and domain heuristics. No network calls.
/// </summary>
    public sealed class ItemClassifier : IItemClassifier
    {
    private readonly ILogger<ItemClassifier>? _logger;

        // ── Critical domain list ──────────────────────────────────────────────────
        // Domains whose accounts should always be flagged as critical.
    private static readonly HashSet<string> _criticalDomains = new(StringComparer.OrdinalIgnoreCase)
        {
     // Email
         "gmail.com","outlook.com","hotmail.com","yahoo.com","icloud.com",
 "protonmail.com","proton.me","fastmail.com","zoho.com","tutanota.com",
   // Banking / Finance
      "chase.com","bankofamerica.com","wellsfargo.com","citibank.com",
         "barclays.com","hsbc.com","lloydsbank.com","santander.com",
     "pnc.com","usbank.com","tdbank.com","capitalone.com","discover.com",
      "ally.com","schwab.com","fidelity.com","vanguard.com","etrade.com",
            "paypal.com","stripe.com","square.com","wise.com","revolut.com",
       // Crypto
      "coinbase.com","binance.com","kraken.com","blockchain.com",
       "metamask.io","ledger.com","trezor.io","gemini.com","bitfinex.com",
    // Identity providers / SSO
            "google.com","microsoft.com","apple.com","auth0.com","okta.com",
    "accounts.google.com","login.microsoftonline.com",
       // Cloud / infra
            "aws.amazon.com","console.aws.amazon.com","cloud.google.com",
        "portal.azure.com","digitalocean.com","cloudflare.com",
            // Social (high-value targets)
   "facebook.com","instagram.com","twitter.com","x.com","linkedin.com",
        };

  // ── Critical title/label keywords ────────────────────────────────────────
     private static readonly string[] _criticalKeywords =
        {
            "bank","banking","finance","payment","payroll","wallet","crypto",
            "bitcoin","ethereum","exchange","brokerage","investment","tax",
            "email","mail","gmail","icloud","outlook",
            "admin","administrator","root","superuser","sudo",
"ssh","vpn","server","database","db","aws","azure","gcp","cloud",
      "recovery","backup code","2fa","two-factor","master password",
        };

        // ── Tag keyword ? tag label mapping ──────────────────────────────────────
    private static readonly (string[] keywords, string tag)[] _tagRules =
     {
   (new[]{"bank","finance","payment","payroll"},             "finance"),
         (new[]{"crypto","bitcoin","ethereum","wallet","exchange"},"crypto"),
          (new[]{"email","mail","gmail","outlook","icloud"},        "email"),
(new[]{"social","facebook","instagram","twitter","x.com","linkedin","reddit","tiktok"}, "social"),
     (new[]{"work","office","corporate","company","hr","jira","confluence","slack"}, "work"),
     (new[]{"shopping","amazon","ebay","shopify","store","shop"}, "shopping"),
     (new[]{"gaming","steam","playstation","xbox","nintendo","game"}, "gaming"),
          (new[]{"streaming","netflix","spotify","hulu","disney","youtube"}, "entertainment"),
      (new[]{"dev","developer","github","gitlab","bitbucket","npm","docker"}, "developer"),
       (new[]{"health","medical","hospital","pharmacy","doctor","insurance"}, "health"),
        (new[]{"travel","airline","hotel","booking","airbnb","flight"}, "travel"),
     (new[]{"admin","root","server","vpn","ssh","cloud","aws","azure"}, "admin"),
        };

        // ── Icon key mapping ──────────────────────────────────────────────────────
        private static readonly (string[] keywords, string icon)[] _iconRules =
   {
          (new[]{"bank","finance","paypal","stripe"},    "icon_bank"),
            (new[]{"crypto","bitcoin","ethereum","wallet"},"icon_crypto"),
            (new[]{"email","mail","gmail","outlook"},      "icon_email"),
       (new[]{"social","facebook","instagram","twitter","x.com","linkedin"}, "icon_social"),
 (new[]{"shop","amazon","ebay","store"},        "icon_shopping"),
     (new[]{"game","steam","playstation","xbox"},   "icon_gaming"),
 (new[]{"github","gitlab","dev","docker"},      "icon_code"),
            (new[]{"cloud","aws","azure","gcp","server"},  "icon_cloud"),
      (new[]{"health","medical","hospital"},         "icon_health"),
            (new[]{"travel","airline","hotel","booking"},  "icon_travel"),
        };

      public ItemClassifier(ILogger<ItemClassifier>? logger = null) => _logger = logger;

        // ── IItemClassifier ───────────────────────────────────────────────────────
        /// <inheritdoc />
        public ItemClassification Classify(string domain, string title)
    {
   var domainLower = (domain ?? string.Empty).ToLowerInvariant();
        var titleLower  = (title  ?? string.Empty).ToLowerInvariant();
            var combined    = $"{domainLower} {titleLower}";

 // Critical check
            bool isCritical = false;
    string? criticalReason = null;

            // 1. Known critical domain
          var baseDomain = ExtractBaseDomain(domainLower);
     if (!string.IsNullOrEmpty(baseDomain) && _criticalDomains.Contains(baseDomain))
  {
                isCritical = true;
 criticalReason = $"{baseDomain} is a high-value account (email, finance, or identity provider).";
}

          // 2. Critical keyword in title
            if (!isCritical)
            {
            foreach (var kw in _criticalKeywords)
   {
      if (combined.Contains(kw, StringComparison.Ordinal))
            {
   isCritical = true;
      criticalReason = $"Title/domain contains high-risk keyword \"{kw}\".";
        break;
        }
          }
 }

            // Tags
            var tags = BuildTags(combined);

       // Icon
            var icon = PickIcon(combined);

            // LoginType hint
            var loginType = InferLoginType(domainLower);

            _logger?.LogDebug("ItemClassifier: domain={D} critical={C} tags={T}",
         domainLower, isCritical, string.Join(",", tags));

   return new ItemClassification
  {
   IsCritical    = isCritical,
                CriticalReason    = criticalReason,
          SuggestedTags     = tags,
        SuggestedIconKey  = icon,
           SuggestedLoginType = loginType,
  };
        }

        /// <inheritdoc />
        public ItemClassification ClassifyCard(string label)
        {
       var lower = (label ?? string.Empty).ToLowerInvariant();
            return new ItemClassification
    {
             IsCritical       = true,   // credit cards are always sensitive
 CriticalReason   = "Credit cards are high-value financial items.",
 SuggestedIconKey = "icon_bank",
  SuggestedTags    = new[] { "finance", "card" },
    };
        }

    /// <inheritdoc />
    public ItemClassification ClassifyIdentity(string firstName, string lastName, string email)
        {
            var tags = new List<string> { "identity" };
       bool isWork = (email ?? string.Empty).Contains("@")
         && !IsPersonalEmail(email!);
            if (isWork) tags.Add("work");

          return new ItemClassification
            {
    IsCritical  = false,
                SuggestedIconKey = "icon_person",
   SuggestedTags    = tags.AsReadOnly(),
            };
        }

   // ── Helpers ───────────────────────────────────────────────────────────────
        private static List<string> BuildTags(string combined)
  {
     var tags = new HashSet<string>(StringComparer.Ordinal);
 foreach (var (keywords, tag) in _tagRules)
   if (keywords.Any(k => combined.Contains(k, StringComparison.Ordinal)))
    tags.Add(tag);
            return tags.ToList();
  }

     private static string PickIcon(string combined)
{
          foreach (var (keywords, icon) in _iconRules)
     if (keywords.Any(k => combined.Contains(k, StringComparison.Ordinal)))
      return icon;
            return "icon_key";   // default
        }

        private static LoginType InferLoginType(string domain)
        {
   if (domain.StartsWith("androidapp://") || domain.StartsWith("iosapp://"))
   return LoginType.PhoneApp;
       if (domain.Contains(".") && !domain.StartsWith("//"))
        return LoginType.Web;
            return LoginType.Web;
        }

      private static string? ExtractBaseDomain(string host)
 {
        if (string.IsNullOrEmpty(host)) return null;
      if (Utilities.DomainName.TryParse(host, out var dn))
                return dn.BaseDomain?.ToLowerInvariant();
  var parts = host.Split('.');
      return parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : host;
        }

        private static readonly HashSet<string> _personalEmailDomains = new(StringComparer.OrdinalIgnoreCase)
        {
       "gmail.com","yahoo.com","outlook.com","hotmail.com","icloud.com",
            "protonmail.com","proton.me","fastmail.com","zoho.com","tutanota.com",
        };

  private static bool IsPersonalEmail(string email)
        {
            var at = email.IndexOf('@');
        if (at < 0) return false;
     return _personalEmailDomains.Contains(email[(at + 1)..]);
        }

    /// <summary>Exposed for use by VaultHealthCalculator without duplicating the list.</summary>
    internal static HashSet<string> CriticalDomainSet => _criticalDomains;
    }
}
