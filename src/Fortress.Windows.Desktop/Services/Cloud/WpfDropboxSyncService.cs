using Fortress.Core.Contracts;
using Fortress.Windows.Desktop.Services.Cloud;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
  /// Dropbox cloud sync for WPF.
    /// Mirrors Fortress.Mobile.Core DropboxSyncService exactly:
    ///   - credentials via IOptions&lt;DropboxOptions&gt; (bound from appsettings.json)
    ///   - all URLs/keys from DropboxConstants
    ///   - token persistence via WpfPreferences (mirrors Preferences.Default)
    /// - auth via WpfOAuthHelper loopback listener (equivalent to WebAuthenticator)
    /// </summary>
    public class WpfDropboxSyncService : ICloudSyncService
    {
  public string ProviderName => "Dropbox";

    private readonly HttpClient _http;
        private readonly DropboxOptions _options;

  private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

   public WpfDropboxSyncService(HttpClient http, IOptions<DropboxOptions> options)
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
         "Dropbox App Key is not configured. " +
        "Add Dropbox:AppKey and Dropbox:RedirectUri to appsettings.json.");

    try
       {
    var (verifier, challenge) = WpfOAuthHelper.GeneratePkce();
    var authUri = new Uri(
       $"{DropboxConstants.AuthorizationEndpoint}" +
  $"?client_id={Uri.EscapeDataString(_options.AppKey)}" +
$"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
 $"&response_type=code" +
  $"&code_challenge={challenge}" +
     $"&code_challenge_method=S256" +
$"&token_access_type=offline");

   var code = await WpfOAuthHelper.AuthorizeAsync(authUri, _options.RedirectUri);
    if (string.IsNullOrEmpty(code)) return false;
     return await ExchangeCodeForTokensAsync(code, verifier);
      }
  catch (TaskCanceledException) { return false; }
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
  using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.RevokeEndpoint);
     req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        req.Content = new StringContent("null", Encoding.UTF8, "application/json");
          await _http.SendAsync(req);
      }
       catch { }
}
       _accessToken = null;
         _refreshToken = null;
          _tokenExpiry = DateTime.MinValue;
  WpfPreferences.Remove(DropboxConstants.PrefAccessToken);
    WpfPreferences.Remove(DropboxConstants.PrefRefreshToken);
   WpfPreferences.Remove(DropboxConstants.PrefTokenExpiry);
       WpfPreferences.Remove(DropboxConstants.PrefUserEmail);
    WpfPreferences.Remove(DropboxConstants.PrefUserName);
 WpfPreferences.Remove(DropboxConstants.PrefLastSyncTime);
       }

    // ── BACKUP ───────────────────────────────────────────────────────────
       public async Task<CloudSyncResult> UploadBackupAsync(byte[] encryptedData)
      {
    try
      {
  if (!await EnsureValidTokenAsync()) return Fail("Not authenticated.");
     using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.UploadEndpoint);
  req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
 req.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new
    {
    path = DropboxConstants.BackupFilePath, mode = "overwrite", autorename = false, mute = true
        }));
     req.Content = new ByteArrayContent(encryptedData);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
   var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return Fail($"Upload failed: {resp.StatusCode}");
     var now = DateTime.UtcNow;
      WpfPreferences.Set(DropboxConstants.PrefLastSyncTime, now.ToString("O"));
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
    req.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new { path = DropboxConstants.BackupFilePath }));
       req.Content = new StringContent(string.Empty, Encoding.UTF8, "application/octet-stream");
  var resp = await _http.SendAsync(req);
 if (!resp.IsSuccessStatusCode) return Fail<byte[]>($"Download failed: {resp.StatusCode}");
     var data = await resp.Content.ReadAsByteArrayAsync();
   return new CloudSyncResult<byte[]> { Success = true, SyncTime = DateTime.UtcNow, Data = data };
    }
   catch (Exception ex) { return Fail<byte[]>(ex.Message); }
        }

      public Task<DateTime?> GetLastSyncTimeAsync()
        {
            var raw = WpfPreferences.Get(DropboxConstants.PrefLastSyncTime);
     if (string.IsNullOrEmpty(raw)) return Task.FromResult<DateTime?>(null);
  return Task.FromResult(DateTime.TryParse(raw, null,
  System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? (DateTime?)dt : null);
  }

     public async Task<bool> BackupExistsAsync()
   {
 try
         {
     if (!await EnsureValidTokenAsync()) return false;
     using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.GetMetadataEndpoint);
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
      req.Content = new StringContent(
       JsonSerializer.Serialize(new { path = DropboxConstants.BackupFilePath }), Encoding.UTF8, "application/json");
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
   JsonSerializer.Serialize(new { path = DropboxConstants.BackupFilePath }), Encoding.UTF8, "application/json");
          var resp = await _http.SendAsync(req);
      if (!resp.IsSuccessStatusCode) return Fail($"Delete failed: {resp.StatusCode}");
   return new CloudSyncResult { Success = true, SyncTime = DateTime.UtcNow };
      }
     catch (Exception ex) { return Fail(ex.Message); }
      }

        public async Task<(string Email, string Name)?> GetUserInfoAsync()
        {
    var email = WpfPreferences.Get(DropboxConstants.PrefUserEmail);
     var name  = WpfPreferences.Get(DropboxConstants.PrefUserName);
          if (!string.IsNullOrEmpty(email)) return (email, name);
   try
            {
                if (!await EnsureValidTokenAsync()) return null;
    using var req = new HttpRequestMessage(HttpMethod.Post, DropboxConstants.CurrentAccountEndpoint);
   req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    req.Content = new StringContent("null", Encoding.UTF8, "application/json");
  var resp = await _http.SendAsync(req);
 if (!resp.IsSuccessStatusCode) return null;
  var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    email = doc.RootElement.GetProperty("email").GetString() ?? "";
       name  = doc.RootElement.TryGetProperty("name", out var n)
      && n.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
  WpfPreferences.Set(DropboxConstants.PrefUserEmail, email);
   WpfPreferences.Set(DropboxConstants.PrefUserName,  name);
        return (email, name);
           }
   catch { return null; }
      }

 // ── PRIVATE ──────────────────────────────────────────────────────────
    private async Task<bool> ExchangeCodeForTokensAsync(string code, string verifier)
     {
    var body = new FormUrlEncodedContent(new Dictionary<string, string>
    {
     ["code"]     = code,
      ["grant_type"]= "authorization_code",
     ["client_id"]     = _options.AppKey,
    ["redirect_uri"]  = _options.RedirectUri,
 ["code_verifier"] = verifier
});
   var resp = await _http.PostAsync(DropboxConstants.TokenEndpoint, body);
            if (!resp.IsSuccessStatusCode) return false;
  var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
   _accessToken = doc.RootElement.GetProperty("access_token").GetString();
   _tokenExpiry = doc.RootElement.TryGetProperty("expires_in", out var exp)
  ? DateTime.UtcNow.AddSeconds(exp.GetInt32()) : DateTime.UtcNow.AddHours(4);
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
      ["grant_type"]    = "refresh_token",
 ["refresh_token"] = _refreshToken!,
  ["client_id"]     = _options.AppKey
      });
   var resp = await _http.PostAsync(DropboxConstants.TokenEndpoint, body);
   if (!resp.IsSuccessStatusCode) return false;
   var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    _accessToken = doc.RootElement.GetProperty("access_token").GetString();
       _tokenExpiry = doc.RootElement.TryGetProperty("expires_in", out var exp)
      ? DateTime.UtcNow.AddSeconds(exp.GetInt32()) : DateTime.UtcNow.AddHours(4);
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
       _accessToken  = WpfPreferences.Get(DropboxConstants.PrefAccessToken);
        _refreshToken = WpfPreferences.Get(DropboxConstants.PrefRefreshToken);
       var raw = WpfPreferences.Get(DropboxConstants.PrefTokenExpiry);
 if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
     _tokenExpiry = exp;
        }

  private void SaveTokensToPrefs()
        {
     WpfPreferences.Set(DropboxConstants.PrefAccessToken,  _accessToken  ?? "");
  WpfPreferences.Set(DropboxConstants.PrefRefreshToken, _refreshToken ?? "");
  WpfPreferences.Set(DropboxConstants.PrefTokenExpiry,  _tokenExpiry.ToString("O"));
      }

      private static CloudSyncResult    Fail(string msg)    => new() { Success = false, ErrorMessage = msg };
    private static CloudSyncResult<T> Fail<T>(string msg) => new() { Success = false, ErrorMessage = msg };
}
}
