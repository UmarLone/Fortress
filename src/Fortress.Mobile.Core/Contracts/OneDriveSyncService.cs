using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Microsoft OneDrive implementation of <see cref="ICloudSyncService"/>.
    /// Uses OAuth 2.0 + PKCE (public-client / installed-app flow) with the
    /// Microsoft identity platform (v2.0 endpoint).  Uploads encrypted vault
    /// backups to the app's dedicated AppRoot folder via Microsoft Graph.
    ///
    /// Scopes requested (least privilege):
    ///   Files.ReadWrite.AppFolder – read/write only inside /Apps/FORTRESS Vault/
    ///   User.Read               – display name + email for the account card
    ///   offline_access          – obtain a refresh token so the user stays connected
    /// </summary>
    public class OneDriveSyncService : ICloudSyncService
    {
        public string ProviderName => "OneDrive";

        private readonly HttpClient _http;
        private readonly OneDriveOptions _options;

        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public OneDriveSyncService(HttpClient httpClient, IOptions<OneDriveOptions> options)
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
             "OneDrive Client ID is not configured. " +
                  "Add OneDrive:ClientId and OneDrive:RedirectUri to " +
             "appsettings.android.json / appsettings.apple.json. " +
           "See OneDriveOptions.cs for step-by-step setup instructions.");

            try
            {
                var (verifier, challenge) = PkceHelper.GeneratePair();
                var scopesEncoded = Uri.EscapeDataString(OneDriveConstants.Scopes);

                var authUri = new Uri(
                  $"{OneDriveConstants.AuthorizationEndpoint}" +
              $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
                 $"&response_type=code" +
                     $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
               $"&scope={scopesEncoded}" +
            $"&code_challenge={challenge}" +
              $"&code_challenge_method=S256" +
                 $"&response_mode=query");

                // MAUI WebAuthenticator opens the system browser – user signs in
                // with their own Microsoft account. No credentials are stored by the app.
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
                return false; // user cancelled the browser
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OneDrive] Auth error: {ex.Message}");
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
            // Microsoft identity platform: simply discard tokens locally.
            // There is no server-side token revocation for public-client apps
            // (the OIDC logout endpoint redirects the browser; inappropriate for mobile).
            _accessToken = null;
            _refreshToken = null;
            _tokenExpiry = DateTime.MinValue;

            Preferences.Default.Remove(OneDriveConstants.PrefAccessToken);
            Preferences.Default.Remove(OneDriveConstants.PrefRefreshToken);
            Preferences.Default.Remove(OneDriveConstants.PrefTokenExpiry);
            Preferences.Default.Remove(OneDriveConstants.PrefUserEmail);
            Preferences.Default.Remove(OneDriveConstants.PrefUserName);
            Preferences.Default.Remove(OneDriveConstants.PrefLastSyncTime);
        }

        // ── BACKUP ───────────────────────────────────────────────────────────
        public async Task<CloudSyncResult> UploadBackupAsync(byte[] encryptedData)
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail("Not authenticated.");

                // Graph PUT to /me/drive/special/approot:/{filename}:/content
                // creates or overwrites the file atomically.
                var url = string.Format(OneDriveConstants.BackupFileEndpoint, OneDriveConstants.BackupFileName);
                using var req = new HttpRequestMessage(HttpMethod.Put, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                req.Content = new ByteArrayContent(encryptedData);
                req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    return Fail($"Upload failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");

                var now = DateTime.UtcNow;
                Preferences.Default.Set(OneDriveConstants.PrefLastSyncTime, now.ToString("O"));
                return new CloudSyncResult { Success = true, SyncTime = now };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        public async Task<CloudSyncResult<byte[]>> DownloadBackupAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail<byte[]>("Not authenticated.");

                var url = string.Format(OneDriveConstants.BackupFileEndpoint, OneDriveConstants.BackupFileName);
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var resp = await _http.SendAsync(req);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return Fail<byte[]>("No backup found in OneDrive.");
                if (!resp.IsSuccessStatusCode)
                    return Fail<byte[]>($"Download failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");

                var data = await resp.Content.ReadAsByteArrayAsync();
                return new CloudSyncResult<byte[]> { Success = true, SyncTime = DateTime.UtcNow, Data = data };
            }
            catch (Exception ex) { return Fail<byte[]>(ex.Message); }
        }

        public async Task<DateTime?> GetLastSyncTimeAsync()
        {
            var raw = Preferences.Default.Get(OneDriveConstants.PrefLastSyncTime, string.Empty);
            if (string.IsNullOrEmpty(raw)) return null;
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
               ? dt : null;
        }

        public async Task<bool> BackupExistsAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return false;

                var url = string.Format(OneDriveConstants.BackupFileMetaEndpoint, OneDriveConstants.BackupFileName);
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

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

                var url = string.Format(OneDriveConstants.DeleteFileEndpoint, OneDriveConstants.BackupFileName);
                using var req = new HttpRequestMessage(HttpMethod.Delete, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.NotFound)
                    return Fail($"Delete failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");

                return new CloudSyncResult { Success = true, SyncTime = DateTime.UtcNow };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        // ── USER INFO ────────────────────────────────────────────────────────
        public async Task<(string Email, string Name)?> GetUserInfoAsync()
        {
            var email = Preferences.Default.Get(OneDriveConstants.PrefUserEmail, string.Empty);
            var name = Preferences.Default.Get(OneDriveConstants.PrefUserName, string.Empty);
            if (!string.IsNullOrEmpty(email)) return (email, name);

            try
            {
                if (!await EnsureValidTokenAsync()) return null;

                using var req = new HttpRequestMessage(HttpMethod.Get, OneDriveConstants.GraphMeEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

                email = doc.RootElement.TryGetProperty("mail", out var m) && m.ValueKind == JsonValueKind.String
     ? m.GetString() ?? string.Empty
           : doc.RootElement.TryGetProperty("userPrincipalName", out var upn)
           ? upn.GetString() ?? string.Empty
       : string.Empty;

                name = doc.RootElement.TryGetProperty("displayName", out var dn)
                 ? dn.GetString() ?? string.Empty
               : string.Empty;

                Preferences.Default.Set(OneDriveConstants.PrefUserEmail, email);
                Preferences.Default.Set(OneDriveConstants.PrefUserName, name);
                return (email, name);
            }
            catch { return null; }
        }

        // ── PRIVATE HELPERS ──────────────────────────────────────────────────
        private async Task<bool> ExchangeCodeForTokensAsync(string code, string verifier)
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["code"] = code,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier,
                ["scope"] = OneDriveConstants.Scopes
            });

            var resp = await _http.PostAsync(OneDriveConstants.TokenEndpoint, body);
            if (!resp.IsSuccessStatusCode) return false;

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            _tokenExpiry = DateTime.UtcNow.AddSeconds(
                doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600);

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
                    ["client_id"] = _options.ClientId,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refreshToken!,
                    ["scope"] = OneDriveConstants.Scopes
                });

                var resp = await _http.PostAsync(OneDriveConstants.TokenEndpoint, body);
                if (!resp.IsSuccessStatusCode) return false;

                var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                _tokenExpiry = DateTime.UtcNow.AddSeconds(
               doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600);

                if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
                    _refreshToken = rt.GetString();

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
            _accessToken = Preferences.Default.Get(OneDriveConstants.PrefAccessToken, string.Empty);
            _refreshToken = Preferences.Default.Get(OneDriveConstants.PrefRefreshToken, string.Empty);
            var raw = Preferences.Default.Get(OneDriveConstants.PrefTokenExpiry, string.Empty);
            if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                _tokenExpiry = exp;
        }

        private void SaveTokensToPrefs()
        {
            Preferences.Default.Set(OneDriveConstants.PrefAccessToken, _accessToken ?? string.Empty);
            Preferences.Default.Set(OneDriveConstants.PrefRefreshToken, _refreshToken ?? string.Empty);
            Preferences.Default.Set(OneDriveConstants.PrefTokenExpiry, _tokenExpiry.ToString("O"));
        }

        private static CloudSyncResult Fail(string msg) => new() { Success = false, ErrorMessage = msg };
        private static CloudSyncResult<T> Fail<T>(string msg) => new() { Success = false, ErrorMessage = msg };
    }
}
