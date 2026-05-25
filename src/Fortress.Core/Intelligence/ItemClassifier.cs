using Fortress.Core.Models;
using Fortress.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Fortress.Core.Intelligence
{
    public sealed class ItemClassifier : IItemClassifier
    {
     private readonly ILogger<ItemClassifier>? _logger;

  internal static readonly HashSet<string> _criticalDomains = new(StringComparer.OrdinalIgnoreCase)
        {
   "gmail.com","outlook.com","hotmail.com","yahoo.com","icloud.com","protonmail.com",
    "proton.me","fastmail.com","zoho.com","tutanota.com",
     "chase.com","bankofamerica.com","wellsfargo.com","citibank.com","barclays.com",
   "hsbc.com","lloydsbank.com","santander.com","pnc.com","usbank.com",
  "tdbank.com","capitalone.com","discover.com","ally.com","schwab.com",
    "fidelity.com","vanguard.com","etrade.com","paypal.com","stripe.com",
  "square.com","wise.com","revolut.com","coinbase.com","binance.com",
  "kraken.com","blockchain.com","metamask.io","ledger.com","gemini.com",
    "google.com","microsoft.com","apple.com","auth0.com","okta.com",
   "aws.amazon.com","cloud.google.com","portal.azure.com","digitalocean.com",
  "cloudflare.com","facebook.com","instagram.com","twitter.com","x.com","linkedin.com",
     };

        private static readonly string[] _criticalKeywords =
 [
    "bank","banking","finance","payment","payroll","wallet","crypto",
   "bitcoin","ethereum","exchange","brokerage","investment","tax",
   "email","mail","gmail","icloud","outlook",
  "admin","administrator","root","superuser","sudo",
  "ssh","vpn","server","database","db","aws","azure","gcp","cloud",
    "recovery","backup code","2fa","two-factor","master password",
    ];

     private static readonly (string[] keywords, string tag)[] _tagRules =
   [
  (["bank","finance","payment","payroll"],    "finance"),
    (["crypto","bitcoin","ethereum","wallet","exchange"],"crypto"),
   (["email","mail","gmail","outlook","icloud"],        "email"),
    (["social","facebook","instagram","twitter","linkedin","reddit","tiktok"],"social"),
    (["work","office","corporate","company","hr","jira","slack"],   "work"),
        (["shopping","amazon","ebay","shopify","store","shop"], "shopping"),
        (["gaming","steam","playstation","xbox","nintendo"],    "gaming"),
    (["streaming","netflix","spotify","hulu","disney","youtube"],"entertainment"),
  (["dev","developer","github","gitlab","npm","docker"],   "developer"),
   (["health","medical","hospital","pharmacy","doctor"],    "health"),
  (["travel","airline","hotel","booking","airbnb"],    "travel"),
   (["admin","root","server","vpn","ssh","cloud","aws","azure"],   "admin"),
        ];

        private static readonly (string[] keywords, string icon)[] _iconRules =
   [
    (["bank","finance","paypal","stripe"],    "icon_bank"),
     (["crypto","bitcoin","ethereum","wallet"],"icon_crypto"),
   (["email","mail","gmail","outlook"],      "icon_email"),
  (["social","facebook","instagram","twitter","linkedin"],"icon_social"),
    (["shop","amazon","ebay","store"],        "icon_shopping"),
   (["game","steam","playstation","xbox"],   "icon_gaming"),
   (["github","gitlab","dev","docker"],      "icon_code"),
     (["cloud","aws","azure","gcp","server"],  "icon_cloud"),
(["health","medical","hospital"],         "icon_health"),
 (["travel","airline","hotel","booking"],  "icon_travel"),
        ];

  public ItemClassifier(ILogger<ItemClassifier>? logger = null) => _logger = logger;

        public ItemClassification Classify(string domain, string title)
  {
     var dl = (domain ?? string.Empty).ToLowerInvariant();
 var tl = (title  ?? string.Empty).ToLowerInvariant();
  var combined = $"{dl} {tl}";

     bool isCritical = false;
  string? reason = null;

  var baseDomain = ExtractBase(dl);
    if (!string.IsNullOrEmpty(baseDomain) && _criticalDomains.Contains(baseDomain))
        { isCritical = true; reason = $"{baseDomain} is a high-value account."; }

    if (!isCritical)
      foreach (var kw in _criticalKeywords)
    if (combined.Contains(kw, StringComparison.Ordinal))
         { isCritical = true; reason = $"Keyword \"{kw}\" detected."; break; }

    return new ItemClassification
  {
   IsCritical = isCritical, CriticalReason = reason,
    SuggestedTags = BuildTags(combined),
    SuggestedIconKey = PickIcon(combined),
    SuggestedLoginType = InferLoginType(dl),
     };
 }

  public ItemClassification ClassifyCard(string label) => new()
   {
    IsCritical = true, CriticalReason = "Credit cards are high-value financial items.",
     SuggestedIconKey = "icon_bank", SuggestedTags = ["finance", "card"],
  };

    public ItemClassification ClassifyIdentity(string firstName, string lastName, string email)
       {
            var tags = new List<string> { "identity" };
     if ((email ?? string.Empty).Contains('@') && !IsPersonalEmail(email!)) tags.Add("work");
     return new ItemClassification { IsCritical = false, SuggestedIconKey = "icon_person", SuggestedTags = tags.AsReadOnly() };
        }

  internal static HashSet<string> CriticalDomainSet => _criticalDomains;

   private static List<string> BuildTags(string combined)
  {
   var tags = new HashSet<string>(StringComparer.Ordinal);
    foreach (var (kw, tag) in _tagRules)
     if (kw.Any(k => combined.Contains(k, StringComparison.Ordinal)))
   tags.Add(tag);
  return tags.ToList();
 }

        private static string PickIcon(string combined)
   {
 foreach (var (kw, icon) in _iconRules)
  if (kw.Any(k => combined.Contains(k, StringComparison.Ordinal)))
 return icon;
  return "icon_key";
     }

  private static LoginType InferLoginType(string domain) =>
    (domain.StartsWith("androidapp://") || domain.StartsWith("iosapp://"))
  ? LoginType.PhoneApp : LoginType.Web;

        private static string? ExtractBase(string host)
  {
  if (DomainName.TryParse(host, out var dn)) return dn?.BaseDomain?.ToLowerInvariant();
    var parts = host.Split('.');
   return parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : host;
   }

     private static readonly HashSet<string> _personalDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "gmail.com","yahoo.com","outlook.com","hotmail.com","icloud.com","protonmail.com","proton.me" };

    private static bool IsPersonalEmail(string email)
    {
   var at = email.IndexOf('@');
   return at >= 0 && _personalDomains.Contains(email[(at + 1)..]);
     }
}
}
