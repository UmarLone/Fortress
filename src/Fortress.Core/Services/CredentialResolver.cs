using Fortress.Core.Contracts;
using Fortress.Core.Models;
using Fortress.Core.Utilities;
using System.Text.Json;

namespace Fortress.Core.Services
{
    public interface ICredentialResolver
    {
        Task<List<CredentialView>> GetMatchingCredentialsAsync(string url);
        Task<List<CredentialView>> GetAllCredentialsAsync();
        Task<List<CredentialView>> GetCardCredentialsAsync();
        Task<List<CredentialView>> GetIdentityCredentialsAsync();
        Task<List<PasskeyItem>> GetPasskeyCredentialsAsync(string? rpId = null);
    }

    public class CredentialResolver : ICredentialResolver
    {
        private readonly string[] _ignoredTerms =
    ["com","net","org","android","io","co","uk","au","nz","fr","de",
  "tv","info","app","apps","eu","me","dev","jp","mobile","www"];

        private readonly IDataStorageService _storage;
        private readonly FortressPreferenceWrapper _prefs;
        private const string MatchAlgorithm = "N-Gram";
        private const int NGramN = 2;

        public CredentialResolver(IDataStorageService storage, FortressPreferenceWrapper prefs)
        {
            _storage = storage;
            _prefs = prefs;
        }

        public async Task<List<CredentialView>> GetMatchingCredentialsAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return new();
            var domain = CoreHelpers.GetDomain(url);
            var items = await _storage.GetLoginItemsAsync(
          x => x.LoginType == LoginType.Web || x.LoginType == LoginType.PhoneApp);
            var matched = RankByDomainMatch(items.ToList(), domain ?? url);
            return matched.Select(MapToView).ToList();
        }

        public async Task<List<CredentialView>> GetAllCredentialsAsync()
        {
            var all = await _storage.GetLoginItemsAsync();
            return all.OrderBy(c => c.Url).Select(MapToView).ToList();
        }

        public async Task<List<CredentialView>> GetCardCredentialsAsync()
        {
            var items = await _storage.GetCreditCardItemsAsync();
            return items.Select(c =>
          {
              var meta = new CardAutofillMeta
              {
                  Number = c.Number,
                  CardholderName = c.CardholderName,
                  ExpMonth = c.ExpiryMonth,
                  ExpYear = c.ExpiryYear,
                  Cvv = c.Cvv,
                  Network = c.CardNetwork,
              };
              return new CredentialView
              {
                  Id = c.Id,
                  Domain = string.IsNullOrWhiteSpace(c.Label) ? c.CardholderName : c.Label,
                  Username = c.CardholderName,
                  CredentialType = "CreditCard",
                  FallbackIcon = "creditcardlogo.png",
                  IconUri = "creditcardlogo.png",
                  Meta = JsonSerializer.Serialize(meta),
                  RequireAuthBeforeFill = c.RequireAuthBeforeFill,
              };
          }).ToList();
        }

        public async Task<List<CredentialView>> GetIdentityCredentialsAsync()
        {
            var items = await _storage.GetIdentityItemsAsync();
            return items.Select(i =>
            {
                var meta = new IdentityAutofillMeta
                {
                    FirstName = i.FirstName,
                    LastName = i.LastName,
                    Email = i.Email,
                    Phone = i.Phone,
                    Address = i.AddressLine1,
                    City = i.City,
                    PostalCode = i.PostalCode,
                    Country = i.Country,
                };
                var name = $"{i.FirstName} {i.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(name)) name = i.Label;
                return new CredentialView
                {
                    Id = i.Id,
                    Domain = name,
                    Username = i.Email,
                    CredentialType = "Address",
                    FallbackIcon = "addresslogo.png",
                    IconUri = "addresslogo.png",
                    Meta = JsonSerializer.Serialize(meta),
                };
            }).ToList();
        }

        public async Task<List<PasskeyItem>> GetPasskeyCredentialsAsync(string? rpId = null)
        {
            if (string.IsNullOrEmpty(rpId))
                return (await _storage.GetPasskeyItemsAsync()).ToList();
            return (await _storage.GetPasskeyItemsByRpIdAsync(rpId)).ToList();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private IEnumerable<LoginItem> RankByDomainMatch(List<LoginItem> items, string inputUrl)
        {
            var threshold = _prefs.MatchThreshold / 100.0;
            return items
  .Select(item => (Score: GetScore(item.Url, inputUrl), Item: item))
  .Where(t => t.Score >= threshold)
       .OrderByDescending(t => t.Score)
   .Select(t => t.Item);
        }

        private double GetScore(string d1, string d2)
       => Calculate(GetParts(d1), GetParts(d2));

        private double Calculate(List<string> p1, List<string> p2)
        {
            if (p1.Count == 0 || p2.Count == 0) return 0;
            double total = 0, max = 0;
            foreach (var a in p1)
            {
                double best = 0;
                foreach (var b in p2)
                {
                    double w = (IsCommon(a) || IsCommon(b)) ? 0.1 : 1.0;
                    double sim = Levenshtein(a, b) * w;
                    if (sim > best) best = sim;
                }
                total += best;
                max += IsCommon(a) ? 0.1 : 1.0;
            }
            return total / max;
        }

        private bool IsCommon(string s) => _ignoredTerms.Contains(s, StringComparer.OrdinalIgnoreCase);

        private List<string> GetParts(string url)
        {
            try { return new UriBuilder(url).Host.Split('.').ToList(); }
            catch { return new List<string>(); }
        }

        private static double Levenshtein(string s, string t)
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

        private static CredentialView MapToView(LoginItem item) => new()
        {
            Id = item.Id,
            Domain = item.Url,
            Username = item.Username,
            CredentialType = item.LoginType.ToString(),
            HasOtp = !string.IsNullOrEmpty(item.OtpSecret),
            RequireAuthBeforeFill = item.RequireAuthBeforeFill,
            IsFavorite = item.IsFavorite,
            Notes = item.Notes,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            PasswordStrengthScore = item.PasswordStrengthScore,
            PasswordStrengthLevel = item.PasswordStrengthLevel,
            IconUri = $"https://hubfunctions-us.azurewebsites.net/api/LogoService/Icon?domain={CoreHelpers.GetDomain(item.Url)}&size=64",
            FallbackIcon = "loginlogo.png",
        };
    }
}
