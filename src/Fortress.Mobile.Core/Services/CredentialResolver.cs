using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Mappers;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Utilities;
using System.Text.Json;

namespace Fortress.Mobile.Core.Services
{
    public class CredentialResolver : ICredentialResolver
    {
        private readonly string[] _ignoredSearchTerms = new string[] { "com", "net", "org", "android",
            "io", "co", "uk", "au", "nz", "fr", "de", "tv", "info", "app", "apps", "eu", "me", "dev", "jp", "mobile", "www", "app", "apps", "mobile" };
        private readonly IDataStorageService _dataStorageService;
        private string MATCH_ALGORITHM = "N-Gram";
        private int NGRAM_NUMBER = 2;

        public CredentialResolver(IDataStorageService dataStorageService)
        {
   _dataStorageService = dataStorageService;
        }

public async Task<List<CredentialView>> GetMatchingCredentialsAsync(string url, List<CipherType> includeOtherTypes = null)
        {
    if (string.IsNullOrWhiteSpace(url) && includeOtherTypes == null)
            return null;

    var domainFromUrl = CoreHelpers.GetDomain(url);

         var items = await _dataStorageService.GetLoginItemsAsync(
           x => x.LoginType == LoginType.Web ||
            x.LoginType == LoginType.PhoneApp);

        var matching = RankByDomainMatch(items.ToList(), domainFromUrl).ToList();
            return LoginItemMapper.Map(matching).ToList();
        }

        public async Task<List<CredentialView>> GetAllCredentialsAsync()
        {
        var all = await _dataStorageService.GetLoginItemsAsync();
      return LoginItemMapper.Map(all.OrderBy(c => c.Url)).ToList();
        }

        public async Task<List<CredentialView>> GetCardCredentialsAsync()
        {
            var items = await _dataStorageService.GetCreditCardItemsAsync();
            return items.Select(c =>
         {
    var meta = new CardAutofillMeta
       {
    Number         = c.Number,
             CardholderName = c.CardholderName,
        ExpMonth   = c.ExpiryMonth,
         ExpYear        = c.ExpiryYear,
    Cvv            = c.Cvv,
  Network        = c.CardNetwork,
                };
     return new CredentialView
         {
     Id             = c.Id,
             Domain    = string.IsNullOrWhiteSpace(c.Label) ? c.CardholderName : c.Label,
          Username      = c.CardholderName,
           CredentialType       = "CreditCard",
       FallbackIcon         = "creditcardlogo.png",
       IconUri    = "creditcardlogo.png",
       Meta         = JsonSerializer.Serialize(meta),
         RequireAuthBeforeFill = c.RequireAuthBeforeFill,
              };
            }).ToList();
 }

        public async Task<List<CredentialView>> GetIdentityCredentialsAsync()
   {
    var items = await _dataStorageService.GetIdentityItemsAsync();
            return items.Select(i =>
   {
    var meta = new IdentityAutofillMeta
      {
              FirstName  = i.FirstName,
         LastName   = i.LastName,
  Email      = i.Email,
        Phone      = i.Phone,
                    Address    = i.AddressLine1,
          City       = i.City,
           PostalCode = i.PostalCode,
         Country    = i.Country,
     };
   var displayName = $"{i.FirstName} {i.LastName}".Trim();
          if (string.IsNullOrWhiteSpace(displayName)) displayName = i.Label;
              return new CredentialView
     {
        Id  = i.Id,
Domain         = displayName,
            Username       = i.Email,
     CredentialType = "Address",
       FallbackIcon   = "addresslogo.png",
 IconUri        = "addresslogo.png",
     Meta   = JsonSerializer.Serialize(meta),
           };
   }).ToList();
        }

        public async Task<List<PasskeyItem>> GetPasskeyCredentialsAsync(string? rpId = null)
        {
   var all = await _dataStorageService.GetPasskeyItemsAsync();
 if (string.IsNullOrEmpty(rpId))
  return all.ToList();
    return (await _dataStorageService.GetPasskeyItemsByRpIdAsync(rpId)).ToList();
        }

    // ── Ranking ──────────────────────────────────────────────────────

    private IEnumerable<LoginItem> RankByDomainMatch(List<LoginItem> items, string inputUrl)
      {
            var threshold = PreferenceWrapper.Instance.MatchThreshold / 100.0;
       return items
 .Select(item => (Score: GetDomainMatchScore(item.Url, inputUrl), Item: item))
         .Where(t => t.Score >= threshold)
  .OrderByDescending(t => t.Score)
       .Select(t => t.Item);
        }

        // ...existing domain scoring helpers (unchanged)...
    private double GetDomainMatchScore(string domain1, string domain2)
         => CalculateScore(GetDomainParts(domain1), GetDomainParts(domain2));

        private double CalculateScore(List<string> parts1, List<string> parts2)
  {
            if (parts1.Count == 0 || parts2.Count == 0) return 0;
        double totalSimilarity = 0, maxPossibleScore = 0;
            foreach (var part1 in parts1)
            {
                double maxSim = 0;
    foreach (var part2 in parts2)
      {
             double weight = (IsCommonTLD(part1) || IsCommonTLD(part2)) ? 0.1 : 1.0;
                double sim = MATCH_ALGORITHM == "NGramSimilarity"
       ? NGramSimilarity(part1, part2, NGRAM_NUMBER) * weight
    : LevenshteinDistance(part1, part2) * weight;
 if (sim > maxSim) maxSim = sim;
           }
        totalSimilarity += maxSim;
                maxPossibleScore += IsCommonTLD(part1) ? 0.1 : 1.0;
            }
          return totalSimilarity / maxPossibleScore;
        }

      private bool IsCommonTLD(string part)
    => new HashSet<string>(_ignoredSearchTerms).Contains(part);

 private List<string> GetDomainParts(string url)
 {
            try { return new UriBuilder(url).Host.Split('.').ToList(); }
            catch { return new List<string>(); }
    }

     private double LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
 var d = new int[n + 1, m + 1];
     for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;
      for (int j = 1; j <= m; j++)
 for (int i = 1; i <= n; i++)
             d[i, j] = s[i - 1] == t[j - 1] ? d[i - 1, j - 1]
 : Math.Min(d[i - 1, j] + 1, Math.Min(d[i, j - 1] + 1, d[i - 1, j - 1] + 1));
     return 1.0 - (double)d[n, m] / Math.Max(s.Length, t.Length);
        }

        private double NGramSimilarity(string s1, string s2, int n = 2)
{
   var set1 = new HashSet<string>(GenerateNGrams(s1, n));
     var set2 = new HashSet<string>(GenerateNGrams(s2, n));
          return (2.0 * set1.Intersect(set2).Count()) / (set1.Count + set2.Count);
  }

        private List<string> GenerateNGrams(string input, int n)
        {
   var result = new List<string>();
            for (int i = 0; i <= input.Length - n; i++)
    result.Add(input.Substring(i, n));
   return result;
      }
    }
}
