using System.Reflection;

namespace Fortress.Core.Utilities
{
    public class DomainName
    {
    public string SubDomain { get; }
        public string Domain { get; }
        public string Tld { get; }
        public string BaseDomain => $"{Domain}.{Tld}";

        private DomainName(string tld, string sld, string subDomain)
      {
         Tld = tld;
 Domain = sld;
        SubDomain = subDomain;
        }

        public static bool TryParse(string domainString, out DomainName? result)
        {
            result = null;
   try
       {
         ParseDomainName(domainString, out var tld, out var sld, out var sub);
                if (string.IsNullOrEmpty(sld)) return false;
                result = new DomainName(tld, sld, sub);
            return true;
            }
    catch { return false; }
        }

        public static bool TryParseBaseDomain(string domainString, out string? result)
        {
            var ok = TryParse(domainString, out var dn);
            result = dn?.BaseDomain;
            return ok;
        }

        private static void ParseDomainName(string domainString,
            out string tld, out string sld, out string subDomain)
  {
            domainString = domainString.ToLowerInvariant().Trim();
         tld = string.Empty;
            sld = string.Empty;
  subDomain = string.Empty;

            if (string.IsNullOrWhiteSpace(domainString))
   throw new ArgumentException("Domain cannot be blank.");

            var rule = TLDRulesCache.Instance.FindMatchingRule(domainString);
 if (rule == null)
{
                // Fallback: treat last two labels as domain.tld
      var fallbackParts = domainString.Split('.');
                if (fallbackParts.Length >= 2)
        {
     tld = fallbackParts[^1];
       sld = fallbackParts[^2];
           if (fallbackParts.Length > 2)
      subDomain = string.Join(".", fallbackParts[..^2]);
  }
 return;
  }

        int tldIndex;
            string tempSubAndDomain;

            switch (rule.Type)
    {
    case TLDRule.RuleType.Wildcard:
     tldIndex = domainString.LastIndexOf("." + rule.Name);
           tempSubAndDomain = domainString[..tldIndex];
 tldIndex = tempSubAndDomain.LastIndexOf(".");
           tempSubAndDomain = domainString[..tldIndex];
      tld = domainString[(tldIndex + 1)..];
     break;
   case TLDRule.RuleType.Exception:
  tldIndex = domainString.LastIndexOf(".");
         tempSubAndDomain = domainString[..tldIndex];
            tld = domainString[(tldIndex + 1)..];
        break;
       default: // Normal
   tldIndex = domainString.LastIndexOf("." + rule.Name);
     tempSubAndDomain = domainString[..tldIndex];
      tld = domainString[(tldIndex + 1)..];
        break;
   }

            var remaining = tempSubAndDomain.Split('.').ToList();
   if (remaining.Count > 0)
         {
          sld = remaining[^1];
   if (remaining.Count > 1)
   subDomain = string.Join(".", remaining[..^1]);
            }
 }

  // ── TLD Rule ──────────────────────────────────────────────────────────
     public class TLDRule : IComparable<TLDRule>
        {
       public string Name { get; }
            public RuleType Type { get; }

            public TLDRule(string ruleInfo)
            {
           if (ruleInfo.StartsWith("*"))
     { Type = RuleType.Wildcard; Name = ruleInfo[2..]; }
    else if (ruleInfo.StartsWith("!"))
          { Type = RuleType.Exception; Name = ruleInfo[1..]; }
                else
    { Type = RuleType.Normal; Name = ruleInfo; }
   }

     public int CompareTo(TLDRule? other) => Name.CompareTo(other?.Name);

            public enum RuleType { Normal, Wildcard, Exception }
      }

        // ── TLD Rules Cache ───────────────────────────────────────────────────
      private sealed class TLDRulesCache
    {
  private static volatile TLDRulesCache? _instance;
            private static readonly object _sync = new();

          private readonly Dictionary<TLDRule.RuleType, Dictionary<string, TLDRule>> _rules;

  private TLDRulesCache() => _rules = LoadRules();

            public static TLDRulesCache Instance
       {
       get
       {
  if (_instance != null) return _instance;
        lock (_sync) { return _instance ??= new TLDRulesCache(); }
          }
   }

       public TLDRule? FindMatchingRule(string domain)
    {
        var parts = domain.Split('.').Reverse().ToList();
    var check = string.Empty;
  var matches = new List<TLDRule>();

      foreach (var part in parts)
      {
          check = string.IsNullOrEmpty(check) ? part : $"{part}.{check}";
    foreach (var ruleType in Enum.GetValues<TLDRule.RuleType>())
           if (_rules[ruleType].TryGetValue(check, out var rule))
           matches.Add(rule);
 }

                return matches.OrderByDescending(r => r.Name.Length).FirstOrDefault();
   }

         private static Dictionary<TLDRule.RuleType, Dictionary<string, TLDRule>> LoadRules()
       {
         var result = new Dictionary<TLDRule.RuleType, Dictionary<string, TLDRule>>();
      foreach (var rt in Enum.GetValues<TLDRule.RuleType>())
            result[rt] = new Dictionary<string, TLDRule>(StringComparer.OrdinalIgnoreCase);

        var lines = ReadRuleLines();
    foreach (var line in lines.Where(l => !l.StartsWith("//") && !string.IsNullOrWhiteSpace(l)))
     {
   var rule = new TLDRule(line.Trim());
    result[rule.Type][rule.Name] = rule;
       }
    return result;
     }

         private static IEnumerable<string> ReadRuleLines()
        {
  // Try embedded resource first (Bit.Core compatible path)
     var asm = typeof(TLDRulesCache).GetTypeInfo().Assembly;
     var resourceName = asm.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("public_suffix_list.dat",
       StringComparison.OrdinalIgnoreCase));

              if (resourceName != null)
       {
           using var stream = asm.GetManifestResourceStream(resourceName)!;
   using var reader = new StreamReader(stream);
        string? line;
   while ((line = reader.ReadLine()) != null)
  yield return line;
                 yield break;
}

                // Fallback: common TLDs hardcoded so the parser still works
       // without the embedded resource file
   var fallback = new[]
             {
              "com","net","org","edu","gov","io","co","uk","co.uk","org.uk",
            "me.uk","de","fr","es","it","nl","be","at","ch","pl","ru","cn",
  "jp","au","com.au","net.au","org.au","ca","mx","br","com.br",
     "in","us","info","biz","name","mobi","app","dev","ai","tv"
             };
              foreach (var t in fallback) yield return t;
            }
   }
    }
}
