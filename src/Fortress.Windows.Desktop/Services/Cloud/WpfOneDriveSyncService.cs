using Fortress.Core.Contracts;
using Fortress.Windows.Desktop.Services.Cloud;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// OneDrive cloud sync for WPF.
    /// Mirrors Fortress.Mobile.Core OneDriveSyncService exactly:
    ///   - credentials via IOptions&lt;OneDriveOptions&gt; (bound from appsettings.json)
    ///   - all URLs/keys from OneDriveConstants
    ///   - token persistence via WpfPreferences (mirrors Preferences.Default)
 ///   - auth via WpfOAuthHelper embedded browser (OAuthBrowserWindow) because
    ///     Microsoft blocks loopback redirects for Entra public-client apps.
    ///     Redirect URI must be: https://login.microsoftonline.com/common/oauth2/nativeclient
    /// </summary>
    public class WpfOneDriveSyncService : ICloudSyncService
    {
        public string ProviderName => "OneDrive";

 private readonly HttpClient _http;
        private readonly OneDriveOptions _options;

   private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

     public WpfOneDriveSyncService(HttpClient http, IOptions<OneDriveOptions> options)
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
   "OneDrive Client ID is not configured. " +
             "Add OneDrive:ClientId and OneDrive:RedirectUri to appsettings.json.");

try
  {
   var (verifier, challenge) = WpfOAuthHelper.GeneratePkce();
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

  var code = await WpfOAuthHelper.AuthorizeAsync(authUri, _options.RedirectUri);
    if (string.IsNullOrEmpty(code)) return false;
     return await ExchangeCodeForTokensAsync(code, verifier);
   }
    catch (TaskCanceledException) { return false; }
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

   public Task SignOutAsync()
  {
       _accessToken = null;
  _refreshToken = null;
     _tokenExpiry = DateTime.MinValue;
  WpfPreferences.Remove(OneDriveConstants.PrefAccessToken);
 WpfPreferences.Remove(OneDriveConstants.PrefRefreshToken);
   WpfPreferences.Remove(OneDriveConstants.PrefTokenExpiry);
  WpfPreferences.Remove(OneDriveConstants.PrefUserEmail);
  WpfPreferences.Remove(OneDriveConstants.PrefUserName);
       WpfPreferences.Remove(OneDriveConstants.PrefLastSyncTime);
       return Task.CompletedTask;
 }

    // ── BACKUP ───────────────────────────────────────────────────────────

  public async Task<CloudSyncResult> UploadBackupAsync(byte[] encryptedData)
        {
       try
       {
  if (!await EnsureValidTokenAsync()) return Fail("Not authenticated.");
  var url = string.Format(OneDriveConstants.BackupFileEndpoint, OneDriveConstants.BackupFileName);
  using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
      req.Content = new ByteArrayContent(encryptedData);
    req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
   var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return Fail($"Upload failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
    var now = DateTime.UtcNow;
  WpfPreferences.Set(OneDriveConstants.PrefLastSyncTime, now.ToString("O"));
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

    public Task<DateTime?> GetLastSyncTimeAsync()
        {
     var raw = WpfPreferences.Get(OneDriveConstants.PrefLastSyncTime);
   if (string.IsNullOrEmpty(raw)) return Task.FromResult<DateTime?>(null);
        return Task.FromResult(DateTime.TryParse(raw, null,
     System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? (DateTime?)dt : null);
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

      public async Task<(string Email, string Name)?> GetUserInfoAsync()
       {
    var email = WpfPreferences.Get(OneDriveConstants.PrefUserEmail);
    var name  = WpfPreferences.Get(OneDriveConstants.PrefUserName);
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
   ? m.GetString() ?? ""
   : doc.RootElement.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() ?? "" : "";
   name = doc.RootElement.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
     WpfPreferences.Set(OneDriveConstants.PrefUserEmail, email);
  WpfPreferences.Set(OneDriveConstants.PrefUserName,  name);
    return (email, name);
   }
  catch { return null; }
  }

    // ── PRIVATE ──────────────────────────────────────────────────────────

 private async Task<bool> ExchangeCodeForTokensAsync(string code, string verifier)
        {
      var body = new FormUrlEncodedContent(new Dictionary<string, string>
      {
     ["client_id"]     = _options.ClientId,
    ["code"]  = code,
      ["redirect_uri"]  = _options.RedirectUri,
    ["grant_type"]    = "authorization_code",
["code_verifier"] = verifier,
      ["scope"]         = OneDriveConstants.Scopes
     });
         var resp = await _http.PostAsync(OneDriveConstants.TokenEndpoint, body);
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
  ["client_id"]     = _options.ClientId,
   ["grant_type"]    = "refresh_token",
       ["refresh_token"] = _refreshToken!,
  ["scope"]         = OneDriveConstants.Scopes
    });
var resp = await _http.PostAsync(OneDriveConstants.TokenEndpoint, body);
  if (!resp.IsSuccessStatusCode) return false;
     var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    _accessToken = doc.RootElement.GetProperty("access_token").GetString();
   _tokenExpiry = DateTime.UtcNow.AddSeconds(
   doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600);
       if (doc.RootElement.TryGetProperty("refresh_token", out var rt)) _refreshToken = rt.GetString();
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
   _accessToken  = WpfPreferences.Get(OneDriveConstants.PrefAccessToken);
      _refreshToken = WpfPreferences.Get(OneDriveConstants.PrefRefreshToken);
var raw = WpfPreferences.Get(OneDriveConstants.PrefTokenExpiry);
      if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
    _tokenExpiry = exp;
    }

  private void SaveTokensToPrefs()
       {
     WpfPreferences.Set(OneDriveConstants.PrefAccessToken,  _accessToken  ?? "");
  WpfPreferences.Set(OneDriveConstants.PrefRefreshToken, _refreshToken ?? "");
  WpfPreferences.Set(OneDriveConstants.PrefTokenExpiry,  _tokenExpiry.ToString("O"));
  }

      private static CloudSyncResult    Fail(string msg)    => new() { Success = false, ErrorMessage = msg };
  private static CloudSyncResult<T> Fail<T>(string msg) => new() { Success = false, ErrorMessage = msg };
  }
}
