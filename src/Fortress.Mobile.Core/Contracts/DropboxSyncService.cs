using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Dropbox implementation of ICloudSyncService using OAuth 2.0 + PKCE
    /// (installed-app flow — no client secret required on mobile).
    /// The App key comes from IOptions&lt;DropboxOptions&gt; bound from
    /// appsettings.android.json / appsettings.apple.json.
    /// </summary>
    public class DropboxSyncService : ICloudSyncService
    {
        public string ProviderName => "Dropbox";

        private readonly HttpClient _http;
        private readonly DropboxOptions _options;

        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public DropboxSyncService(HttpClient httpClient, IOptions<DropboxOptions> options)
        {
            _http = httpClient;
            _options = options.Value;
            LoadTokensFromPrefs();
        }

        // ── AUTH ─────────────────────────────────────────────────────────────

        public async Task<bool> AuthenticateAsync()
        {
            if (!_options.IsConfigured)
                throw new InvalidOperationException(
                          "Dropbox App Key is not configured. " +
                     "Add Dropbox:AppKey and Dropbox:RedirectUri to " +
             "appsettings.android.json / appsettings.apple.json. " +
                "See DropboxOptions.cs for step-by-step setup instructions.");

            try
            {
                var (verifier, challenge) = PkceHelper.GeneratePair();

                var authUri = new Uri(
                          $"{DropboxConstants.AuthorizationEndpoint}" +
                       $"?client_id={Uri.EscapeDataString(_options.AppKey)}" +
                    $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
                $"&response_type=code" +
                     $"&code_challenge={challenge}" +
                        $"&code_challenge_method=S256" +
                       $"&token_access_type=offline");   // offline = long-lived refresh token

                var result = await WebAuthenticator.Default.AuthenticateAsync(
           new WebAuthenticatorOptions
           {
               Url = authUri,
               CallbackUrl = new Uri(_options.RedirectUri),
               PrefersEphemeralWebBrowserSession = true
           });

                if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
                    return false;

                return await ExchangeCodeForTokensAsync(code, verifier);
            }
            catch (TaskCanceledException)
            {
                return false; // user cancelled browser
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dropbox] Auth error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            if (string.IsNullOrEmpty(_accessToken)) return false;
            if (DateTime.UtcNow < _tokenExpiry.AddMinutes(-2)) return true;
            if (!string.IsNullOrEmpty(_refreshToken)) return await RefreshAccessTokenAsync();
            return false;
        }

        public async Task SignOutAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken))
            {
                try
                {
                    // Revoke the token via Dropbox auth/token/revoke
                    using var req = new HttpRequestMessage(HttpMethod.Post,
              "https://api.dropboxapi.com/2/auth/token/revoke");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    req.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                    await _http.SendAsync(req);
                }
                catch { /* best-effort */ }
            }

            _accessToken = null;
            _refreshToken = null;
            _tokenExpiry = DateTime.MinValue;

            Preferences.Default.Remove(DropboxConstants.PrefAccessToken);
            Preferences.Default.Remove(DropboxConstants.PrefRefreshToken);
            Preferences.Default.Remove(DropboxConstants.PrefTokenExpiry);
            Preferences.Default.Remove(DropboxConstants.PrefUserEmail);
            Preferences.Default.Remove(DropboxConstants.PrefUserName);
            Preferences.Default.Remove(DropboxConstants.PrefLastSyncTime);
        }

        // ── BACKUP ───────────────────────────────────────────────────────────

        public async Task<CloudSyncResult> UploadBackupAsync(byte[] encryptedData)
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail("Not authenticated.");

                // Dropbox upload — overwrite if exists
                using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.UploadEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var dropboxArg = JsonSerializer.Serialize(new
                {
                    path = DropboxConstants.BackupFilePath,
                    mode = "overwrite",
                    autorename = false,
                    mute = true
                });
                req.Headers.Add("Dropbox-API-Arg", dropboxArg);
                req.Content = new ByteArrayContent(encryptedData);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    return Fail($"Upload failed: {resp.StatusCode}");

                var now = DateTime.UtcNow;
                Preferences.Default.Set(DropboxConstants.PrefLastSyncTime, now.ToString("O"));
                return new CloudSyncResult { Success = true, SyncTime = now };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        public async Task<CloudSyncResult<byte[]>> DownloadBackupAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail<byte[]>("Not authenticated.");

                using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.DownloadEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var dropboxArg = JsonSerializer.Serialize(new { path = DropboxConstants.BackupFilePath });
                req.Headers.Add("Dropbox-API-Arg", dropboxArg);
                req.Content = new StringContent(string.Empty, Encoding.UTF8, "application/octet-stream");

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    return Fail<byte[]>($"Download failed: {resp.StatusCode}");

                var data = await resp.Content.ReadAsByteArrayAsync();
                return new CloudSyncResult<byte[]> { Success = true, SyncTime = DateTime.UtcNow, Data = data };
            }
            catch (Exception ex) { return Fail<byte[]>(ex.Message); }
        }

        public async Task<DateTime?> GetLastSyncTimeAsync()
        {
            var raw = Preferences.Default.Get(DropboxConstants.PrefLastSyncTime, string.Empty);
            if (string.IsNullOrEmpty(raw)) return null;
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
              ? dt : null;
        }

        public async Task<bool> BackupExistsAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return false;

                using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.GetMetadataEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                req.Content = new StringContent(
            JsonSerializer.Serialize(new { path = DropboxConstants.BackupFilePath }),
            Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<CloudSyncResult> DeleteBackupAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail("Not authenticated.");

                using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.DeleteEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                req.Content = new StringContent(
              JsonSerializer.Serialize(new { path = DropboxConstants.BackupFilePath }),
                         Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    return Fail($"Delete failed: {resp.StatusCode}");

                return new CloudSyncResult { Success = true, SyncTime = DateTime.UtcNow };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        // ── USER INFO ────────────────────────────────────────────────────────

        public async Task<(string Email, string Name)?> GetUserInfoAsync()
        {
            var email = Preferences.Default.Get(DropboxConstants.PrefUserEmail, string.Empty);
            var name = Preferences.Default.Get(DropboxConstants.PrefUserName, string.Empty);
            if (!string.IsNullOrEmpty(email)) return (email, name);

            try
            {
                if (!await EnsureValidTokenAsync()) return null;

                using var req = new HttpRequestMessage(HttpMethod.Post,
                    DropboxConstants.CurrentAccountEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                req.Content = new StringContent("null", Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                email = doc.RootElement
                      .GetProperty("email")
                      .GetString() ?? string.Empty;
                name = doc.RootElement.TryGetProperty("name", out var nameEl)
                     && nameEl.TryGetProperty("display_name", out var dn)
                     ? dn.GetString() ?? string.Empty
                       : string.Empty;

                Preferences.Default.Set(DropboxConstants.PrefUserEmail, email);
                Preferences.Default.Set(DropboxConstants.PrefUserName, name);
                return (email, name);
            }
            catch { return null; }
        }

        // ── PRIVATE HELPERS ──────────────────────────────────────────────────

        private async Task<bool> ExchangeCodeForTokensAsync(string code, string verifier)
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["client_id"] = _options.AppKey,
                ["redirect_uri"] = _options.RedirectUri,
                ["code_verifier"] = verifier
            });

            var resp = await _http.PostAsync(DropboxConstants.TokenEndpoint, body);
            if (!resp.IsSuccessStatusCode) return false;

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();

            _tokenExpiry = doc.RootElement.TryGetProperty("expires_in", out var exp)
     ? DateTime.UtcNow.AddSeconds(exp.GetInt32())
              : DateTime.UtcNow.AddHours(4); // Dropbox tokens typically valid ~4 h

            if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
                _refreshToken = rt.GetString();

            SaveTokensToPrefs();
            _ = GetUserInfoAsync(); // eagerly cache display name / email
            return !string.IsNullOrEmpty(_accessToken);
        }

        private async Task<bool> RefreshAccessTokenAsync()
        {
            try
            {
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refreshToken!,
                    ["client_id"] = _options.AppKey
                });

                var resp = await _http.PostAsync(DropboxConstants.TokenEndpoint, body);
                if (!resp.IsSuccessStatusCode) return false;

                var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                _tokenExpiry = doc.RootElement.TryGetProperty("expires_in", out var exp)
            ? DateTime.UtcNow.AddSeconds(exp.GetInt32())
             : DateTime.UtcNow.AddHours(4);

                SaveTokensToPrefs();
                return !string.IsNullOrEmpty(_accessToken);
            }
            catch { return false; }
        }

        private async Task<bool> EnsureValidTokenAsync()
        {
            if (string.IsNullOrEmpty(_accessToken)) return false;
            if (DateTime.UtcNow < _tokenExpiry.AddMinutes(-2)) return true;
            if (!string.IsNullOrEmpty(_refreshToken)) return await RefreshAccessTokenAsync();
            return false;
        }

        private void LoadTokensFromPrefs()
        {
            _accessToken = Preferences.Default.Get(DropboxConstants.PrefAccessToken, string.Empty);
            _refreshToken = Preferences.Default.Get(DropboxConstants.PrefRefreshToken, string.Empty);
            var raw = Preferences.Default.Get(DropboxConstants.PrefTokenExpiry, string.Empty);
            if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                _tokenExpiry = exp;
        }

        private void SaveTokensToPrefs()
        {
            Preferences.Default.Set(DropboxConstants.PrefAccessToken, _accessToken ?? string.Empty);
            Preferences.Default.Set(DropboxConstants.PrefRefreshToken, _refreshToken ?? string.Empty);
            Preferences.Default.Set(DropboxConstants.PrefTokenExpiry, _tokenExpiry.ToString("O"));
        }

        private static CloudSyncResult Fail(string msg) => new() { Success = false, ErrorMessage = msg };
        private static CloudSyncResult<T> Fail<T>(string msg) => new() { Success = false, ErrorMessage = msg };
    }
}
