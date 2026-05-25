using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Have I Been Pwned integration.
    ///
    /// PASSWORD CHECK – pure k-anonymity, zero privacy risk:
    ///   SHA-1(password) ? send first 5 hex chars ? check suffix locally.
    ///   The plaintext password and the full hash never leave the device.
    ///
    /// EMAIL CHECK – requires internet + user consent:
    ///   Calls the HIBP v3 breachedaccount API (requires API key).
    ///   Store your key in app preferences as "hibp_api_key".
    ///   Degrades gracefully (returns IsBreached=false) when no key is set.
    /// </summary>
    public sealed class HaveIBeenPwnedService : IHaveIBeenPwnedService
    {
        private readonly HttpClient _http;
        private readonly ILogger<HaveIBeenPwnedService> _logger;

        // HIBP rate-limit guidance: 1 request per 1.5 s for the email endpoint.
        private static readonly TimeSpan EmailRateLimit = TimeSpan.FromMilliseconds(1600);

        // Preference key for the optional HIBP API key (email endpoint only).
        private const string ApiKeyPref = "hibp_api_key";

        public HaveIBeenPwnedService(
      HttpClient httpClient,
                  ILogger<HaveIBeenPwnedService> logger)
        {
            _http = httpClient;
            _logger = logger;
        }

        // ── Password check (k-anonymity) ──────────────────────────────────
        public async Task<HibpPasswordResult> CheckPasswordAsync(
                  string plaintextPassword,
                  CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(plaintextPassword))
                return new HibpPasswordResult { PwnCount = 0 };

            try
            {
                var hash = Sha1Hex(plaintextPassword);
                var prefix = hash[..5];
                var suffix = hash[5..].ToUpperInvariant();

                var url = $"https://api.pwnedpasswords.com/range/{prefix}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("User-Agent", "Fortress-PasswordManager/1.0");

                using var resp = await _http.SendAsync(req, cancellationToken);
                resp.EnsureSuccessStatusCode();

                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                var count = ParsePwnedRangeResponse(body, suffix);

                _logger.LogDebug("HIBP password check: prefix={P} found={C}", prefix, count);
                return new HibpPasswordResult { PwnCount = count };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HIBP password check failed");
                return new HibpPasswordResult { PwnCount = 0 };
            }
        }

        // ── Email check ───────────────────────────────────────────────────
        public async Task<HibpEmailResult> CheckEmailAsync(
            string email,
        CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(email))
                return new HibpEmailResult { Email = email, IsBreached = false };

            var apiKey = Preferences.Default.Get(ApiKeyPref, string.Empty);
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogInformation("HIBP: no API key configured – email check skipped");
                return new HibpEmailResult { Email = email, IsBreached = false };
            }

            try
            {
                var encoded = Uri.EscapeDataString(email);
                var url = $"https://haveibeenpwned.com/api/v3/breachedaccount/{encoded}?truncateResponse=false";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("hibp-api-key", apiKey);
                req.Headers.Add("User-Agent", "Fortress-PasswordManager/1.0");

                using var resp = await _http.SendAsync(req, cancellationToken);

                // 404 = email not found in any breach
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("HIBP email {E}: not breached", MaskEmail(email));
                    return new HibpEmailResult { Email = email, IsBreached = false, CheckedAt = DateTime.UtcNow };
                }

                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                var breaches = ParseBreachNames(json);

                _logger.LogInformation("HIBP email {E}: {N} breach(es)", MaskEmail(email), breaches.Count);
                return new HibpEmailResult
                {
                    Email = email,
                    IsBreached = breaches.Count > 0,
                    BreachCount = breaches.Count,
                    BreachNames = breaches,
                    CheckedAt = DateTime.UtcNow
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HIBP email check failed for {E}", MaskEmail(email));
                return new HibpEmailResult { Email = email, IsBreached = false };
            }
        }

        // ── Bulk email check ──────────────────────────────────────────────
        public async Task<IReadOnlyList<HibpEmailResult>> CheckAllVaultEmailsAsync(
           IEnumerable<string> emails,
                IProgress<(int done, int total)>? onProgress = null,
          CancellationToken cancellationToken = default)
        {
            var list = emails
                  .Where(e => !string.IsNullOrWhiteSpace(e))
         .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            var results = new List<HibpEmailResult>(list.Count);
            int done = 0;

            foreach (var email in list)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await CheckEmailAsync(email, cancellationToken);
                results.Add(result);
                done++;
                onProgress?.Report((done, list.Count));

                // Rate-limit: stay within HIBP's 1 req / 1.5 s guideline
                if (done < list.Count)
                    await Task.Delay(EmailRateLimit, cancellationToken);
            }

            return results.AsReadOnly();
        }

        // ── Private helpers ───────────────────────────────────────────────
        /// <summary>Returns uppercase SHA-1 hex of the UTF-8 encoded input.</summary>
        private static string Sha1Hex(string input)
        {
            var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes); // uppercase, no dashes
        }

        /// <summary>
        /// Parses HIBP range response (newline-separated "SUFFIX:COUNT" pairs)
        /// and returns the pwn count for the given suffix, or 0 if not found.
        /// </summary>
        private static int ParsePwnedRangeResponse(string body, string suffix)
        {
            foreach (var line in body.AsSpan().EnumerateLines())
            {
                int colon = line.IndexOf(':');
                if (colon < 0) continue;

                var lineSuffix = line[..colon];
                if (lineSuffix.Equals(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line[(colon + 1)..], out int count))
                        return count;
                }
            }
            return 0;
        }

        /// <summary>Parses HIBP JSON breach array and extracts breach names.</summary>
        private static IReadOnlyList<string> ParseBreachNames(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var names = new List<string>();
                foreach (var breach in doc.RootElement.EnumerateArray())
                {
                    if (breach.TryGetProperty("Name", out var name))
                        names.Add(name.GetString() ?? string.Empty);
                }
                return names.AsReadOnly();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>Masks an email for safe logging: user@domain ? u***@domain.</summary>
        private static string MaskEmail(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 1) return "***";
            return email[0] + "***" + email[at..];
        }
    }
}

