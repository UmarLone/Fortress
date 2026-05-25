using Fortress.Core.Contracts;
using Fortress.Windows.Desktop.Services.Cloud;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Options;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// Google Drive cloud sync for WPF.
    /// Mirrors Fortress.Mobile.Core GoogleDriveSyncService exactly:
    ///   - credentials via IOptions&lt;GoogleDriveOptions&gt; (bound from appsettings.json)
    ///   - all URLs/keys from GoogleDriveConstants
    ///   - token persistence via WpfPreferences (mirrors Preferences.Default)
    ///- auth via WpfOAuthHelper loopback listener (equivalent to WebAuthenticator)
    /// </summary>
    public class WpfGoogleDriveSyncService : ICloudSyncService
    {
        public string ProviderName => "Google Drive";

        private readonly HttpClient _http;
        private readonly GoogleDriveOptions _options;

        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public WpfGoogleDriveSyncService(HttpClient http, IOptions<GoogleDriveOptions> options)
        {
            _http = http;
            _options = options.Value;
            LoadTokensFromPrefs();
        }

        // ── AUTH ─────────────────────────────────────────────────────────────
        public async Task<bool> AuthenticateAsync()
        {
            if (!_options.IsConfigured)
                throw new InvalidOperationException(
          "Google Drive Client ID is not configured. " +
        "Add GoogleDrive:ClientId and GoogleDrive:RedirectUri to appsettings.json.");

            try
            {
                var (verifier, challenge) = WpfOAuthHelper.GeneratePkce();
                var scopes = Uri.EscapeDataString(string.Join(" ", new[]
               {
                      GoogleDriveConstants.DriveFileScope,
                      GoogleDriveConstants.DriveProfileScope,
                      GoogleDriveConstants.DriveEmailScope
                }));

                var authUri = new Uri(
                      $"{GoogleDriveConstants.AuthorizationEndpoint}" +
                             $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
                           $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
                        $"&response_type=code" +
                      $"&scope={scopes}" +
                                   $"&code_challenge={challenge}" +
                        $"&code_challenge_method=S256" +
                   $"&access_type=offline" +
               $"&prompt=consent");

                var code = await WpfOAuthHelper.AuthorizeAsync(authUri, _options.RedirectUri);
                if (string.IsNullOrEmpty(code)) return false;
                return await ExchangeCodeForTokensAsync(code, verifier);
            }
            catch (TaskCanceledException) { return false; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GDrive] Auth error: {ex.Message}");
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
                try { await _http.GetAsync($"{GoogleDriveConstants.RevokeEndpoint}?token={Uri.EscapeDataString(_accessToken)}"); }
                catch { }
            }
            _accessToken = null;
            _refreshToken = null;
            _tokenExpiry = DateTime.MinValue;
            WpfPreferences.Remove(GoogleDriveConstants.PrefAccessToken);
            WpfPreferences.Remove(GoogleDriveConstants.PrefRefreshToken);
            WpfPreferences.Remove(GoogleDriveConstants.PrefTokenExpiry);
            WpfPreferences.Remove(GoogleDriveConstants.PrefUserEmail);
            WpfPreferences.Remove(GoogleDriveConstants.PrefUserName);
            WpfPreferences.Remove(GoogleDriveConstants.PrefLastSyncTime);
        }

        // ── BACKUP ───────────────────────────────────────────────────────────
        public async Task<CloudSyncResult> UploadBackupAsync(byte[] encryptedData)
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail("Not authenticated.");

                var service = BuildDriveService();
                var folderId = await EnsureAppFolderAsync(service);
                var existingId = await FindBackupFileIdAsync(service, folderId);
                using var stream = new MemoryStream(encryptedData);

                if (existingId != null)
                {
                    var req = service.Files.Update(
                  new Google.Apis.Drive.v3.Data.File(), existingId, stream, GoogleDriveConstants.BackupMimeType);
                    req.Fields = "id,modifiedTime";
                    var res = await req.UploadAsync();
                    if (res.Status != UploadStatus.Completed)
                        return Fail(res.Exception?.Message ?? "Upload failed.");
                }
                else
                {
                    var meta = new Google.Apis.Drive.v3.Data.File
                    {
                        Name = GoogleDriveConstants.BackupFileName,
                        Parents = new List<string> { folderId },
                        MimeType = GoogleDriveConstants.BackupMimeType
                    };
                    var req = service.Files.Create(meta, stream, GoogleDriveConstants.BackupMimeType);
                    req.Fields = "id,modifiedTime";
                    var res = await req.UploadAsync();
                    if (res.Status != UploadStatus.Completed)
                        return Fail(res.Exception?.Message ?? "Upload failed.");
                }

                var now = DateTime.UtcNow;
                WpfPreferences.Set(GoogleDriveConstants.PrefLastSyncTime, now.ToString("O"));
                return new CloudSyncResult { Success = true, SyncTime = now };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        public async Task<CloudSyncResult<byte[]>> DownloadBackupAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail<byte[]>("Not authenticated.");
                var service = BuildDriveService();
                var folderId = await EnsureAppFolderAsync(service);
                var fileId = await FindBackupFileIdAsync(service, folderId);
                if (fileId == null) return Fail<byte[]>("No backup found in Google Drive.");
                using var ms = new MemoryStream();
                await service.Files.Get(fileId).DownloadAsync(ms);
                return new CloudSyncResult<byte[]> { Success = true, SyncTime = DateTime.UtcNow, Data = ms.ToArray() };
            }
            catch (Exception ex) { return Fail<byte[]>(ex.Message); }
        }

        public Task<DateTime?> GetLastSyncTimeAsync()
        {
            var raw = WpfPreferences.Get(GoogleDriveConstants.PrefLastSyncTime);
            if (string.IsNullOrEmpty(raw)) return Task.FromResult<DateTime?>(null);
            return Task.FromResult(DateTime.TryParse(raw, null,
                  System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? (DateTime?)dt : null);
        }

        public async Task<bool> BackupExistsAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return false;
                var service = BuildDriveService();
                var folderId = await EnsureAppFolderAsync(service);
                return await FindBackupFileIdAsync(service, folderId) != null;
            }
            catch { return false; }
        }

        public async Task<CloudSyncResult> DeleteBackupAsync()
        {
            try
            {
                if (!await EnsureValidTokenAsync()) return Fail("Not authenticated.");
                var service = BuildDriveService();
                var folderId = await EnsureAppFolderAsync(service);
                var fileId = await FindBackupFileIdAsync(service, folderId);
                if (fileId == null) return Fail("No backup found.");
                await service.Files.Delete(fileId).ExecuteAsync();
                return new CloudSyncResult { Success = true, SyncTime = DateTime.UtcNow };
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        public async Task<(string Email, string Name)?> GetUserInfoAsync()
        {
            var email = WpfPreferences.Get(GoogleDriveConstants.PrefUserEmail);
            var name = WpfPreferences.Get(GoogleDriveConstants.PrefUserName);
            if (!string.IsNullOrEmpty(email)) return (email, name);

            try
            {
                if (!await EnsureValidTokenAsync()) return null;
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var resp = await _http.GetStringAsync(GoogleDriveConstants.UserInfoEndpoint);
                var doc = JsonDocument.Parse(resp);
                email = doc.RootElement.GetProperty("email").GetString() ?? "";
                name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                WpfPreferences.Set(GoogleDriveConstants.PrefUserEmail, email);
                WpfPreferences.Set(GoogleDriveConstants.PrefUserName, name);
                return (email, name);
            }
            catch { return null; }
        }

        // ── PRIVATE ──────────────────────────────────────────────────────────
        private DriveService BuildDriveService() =>
            new(new BaseClientService.Initializer
            {
                HttpClientInitializer = GoogleCredential.FromAccessToken(_accessToken)
          .CreateScoped(GoogleDriveConstants.DriveFileScope),
                ApplicationName = "FORTRESS"
            });

        private async Task<string> EnsureAppFolderAsync(DriveService svc)
        {
            var req = svc.Files.List();
            req.Q = $"mimeType='application/vnd.google-apps.folder' and name='{GoogleDriveConstants.AppFolderName}' and trashed=false";
            req.Fields = "files(id)";
            req.Spaces = "drive";
            var list = await req.ExecuteAsync();
            if (list.Files?.Count > 0) return list.Files[0].Id;
            var created = await svc.Files.Create(new Google.Apis.Drive.v3.Data.File
            {
                Name = GoogleDriveConstants.AppFolderName,
                MimeType = "application/vnd.google-apps.folder"
            }).ExecuteAsync();
            return created.Id;
        }

        private async Task<string?> FindBackupFileIdAsync(DriveService svc, string folderId)
        {
            var req = svc.Files.List();
            req.Q = $"name='{GoogleDriveConstants.BackupFileName}' and '{folderId}' in parents and trashed=false";
            req.Fields = "files(id)";
            var list = await req.ExecuteAsync();
            return list.Files?.Count > 0 ? list.Files[0].Id : null;
        }

        private async Task<bool> ExchangeCodeForTokensAsync(string code, string verifier)
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier
            });
            var resp = await _http.PostAsync(GoogleDriveConstants.TokenEndpoint, body);
            if (!resp.IsSuccessStatusCode) return false;
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            _tokenExpiry = DateTime.UtcNow.AddSeconds(
                     doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600);
            if (doc.RootElement.TryGetProperty("refresh_token", out var rt)) _refreshToken = rt.GetString();
            SaveTokensToPrefs();
            _ = GetUserInfoAsync();
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
                    ["refresh_token"] = _refreshToken!
                });
                var resp = await _http.PostAsync(GoogleDriveConstants.TokenEndpoint, body);
                if (!resp.IsSuccessStatusCode) return false;
                var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                _tokenExpiry = DateTime.UtcNow.AddSeconds(
                  doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600);
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
            _accessToken = WpfPreferences.Get(GoogleDriveConstants.PrefAccessToken);
            _refreshToken = WpfPreferences.Get(GoogleDriveConstants.PrefRefreshToken);
            var raw = WpfPreferences.Get(GoogleDriveConstants.PrefTokenExpiry);
            if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                _tokenExpiry = exp;
        }

        private void SaveTokensToPrefs()
        {
            WpfPreferences.Set(GoogleDriveConstants.PrefAccessToken, _accessToken ?? "");
            WpfPreferences.Set(GoogleDriveConstants.PrefRefreshToken, _refreshToken ?? "");
            WpfPreferences.Set(GoogleDriveConstants.PrefTokenExpiry, _tokenExpiry.ToString("O"));
        }

        private static CloudSyncResult Fail(string msg) => new() { Success = false, ErrorMessage = msg };
        private static CloudSyncResult<T> Fail<T>(string msg) => new() { Success = false, ErrorMessage = msg };
    }
}
