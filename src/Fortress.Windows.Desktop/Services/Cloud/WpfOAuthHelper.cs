using Fortress.Windows.Desktop.Views.Windows;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Fortress.Windows.Desktop.Services.Cloud
{
    /// <summary>
    /// Desktop OAuth helper.
    ///
    /// Routing:
    ///   Google Drive / Dropbox  ?  system browser + loopback HTTP listener (RFC 8252 �7.3)
    ///     redirect URI must be http://localhost:{port}/
    ///   OneDrive / Entra        ?  embedded WPF WebBrowser popup (OAuthBrowserWindow)
    ///     redirect URI must be the pre-registered Microsoft
    ///     native-client URI (https://login.microsoftonline.com/
    ///        common/oauth2/nativeclient)
    ///
    /// Google Desktop OAuth clients accept ANY http://localhost:{port} without
    /// pre-registration (RFC 8252 �7.3 / Google OAuth 2.0 for desktop apps).
    /// The port in appsettings.json just needs to be a free unprivileged port.
  /// </summary>
    internal static class WpfOAuthHelper
    {
      // How long to wait for the browser callback before giving up
        private static readonly TimeSpan LoopbackTimeout = TimeSpan.FromMinutes(2);

        // ── PKCE ─────────────────────────────────────────────────────────────
   public static (string Verifier, string Challenge) GeneratePkce()
        {
 var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
var verifier = Base64UrlEncode(bytes);
         var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            return (verifier, Base64UrlEncode(hash));
        }

        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

 // ── Public entry point ────────────────────────────────────────────────
        public static Task<string?> AuthorizeAsync(
Uri authUri,
            string redirectUri,
            CancellationToken ct = default)
        {
   // Non-loopback (OneDrive native-client URI) ? embedded browser popup
            if (!redirectUri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
                return AuthorizeViaEmbeddedBrowserAsync(authUri, redirectUri);

     // Loopback (Google / Dropbox) ? system browser + HTTP listener
      return AuthorizeViaLoopbackAsync(authUri, redirectUri, ct);
    }

        // ── Embedded WebBrowser popup (OneDrive) ──────────────────────────────
   private static Task<string?> AuthorizeViaEmbeddedBrowserAsync(
        Uri authUri, string redirectUri)
        {
   var tcs = new TaskCompletionSource<string?>();
    System.Windows.Application.Current.Dispatcher.Invoke(() =>
{
       var win = new OAuthBrowserWindow(authUri, redirectUri);
    win.ShowDialog();
                tcs.SetResult(win.AuthorizationCode);
      });
            return tcs.Task;
      }

    // ── Loopback HTTP listener (Google Drive / Dropbox) ───────────────────
        private static async Task<string?> AuthorizeViaLoopbackAsync(
            Uri authUri, string redirectUri, CancellationToken externalCt)
        {
    // Ensure the prefix ends with exactly one slash (HttpListener requirement)
            var prefix = redirectUri.TrimEnd('/') + "/";

            using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

            try
            {
        listener.Start();
            }
catch (HttpListenerException ex)
         {
          System.Diagnostics.Debug.WriteLine(
     $"[OAuthHelper] Cannot start listener on {prefix}: {ex.Message}");
      return null;
            }

            // Open the system browser (Chrome / Edge / Firefox � not the embedded WebBrowser)
      System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
 {
                FileName = authUri.ToString(),
                UseShellExecute = true
    });

 // Wait up to LoopbackTimeout for the browser to redirect back
    using var timeoutCts = new CancellationTokenSource(LoopbackTimeout);
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(
      externalCt, timeoutCts.Token);
HttpListenerContext ctx;
     try
    {
      var getCtx  = listener.GetContextAsync();
                var timeout = Task.Delay(Timeout.Infinite, linkedCts.Token);
  var winner  = await Task.WhenAny(getCtx, timeout);
    if (winner != getCtx) return null;   // cancelled or timed out
       ctx = await getCtx;
            }
 finally
  {
          listener.Stop();
            }

     // Send a "you can close this tab" page to the browser
 var html = Encoding.UTF8.GetBytes(
          "<html><body style='font-family:sans-serif;text-align:center;margin-top:80px'>" +
    "<h2>Authentication complete</h2>" +
             "<p>You can close this tab and return to Fortress.</p>" +
 "</body></html>");
            ctx.Response.ContentType     = "text/html";
        ctx.Response.ContentLength64 = html.Length;
            await ctx.Response.OutputStream.WriteAsync(html, CancellationToken.None);
            ctx.Response.Close();

   // Extract the authorization code from the callback query string
    var query = ctx.Request.Url?.Query ?? "";
            foreach (var pair in query.TrimStart('?').Split('&'))
         {
       var kv = pair.Split('=', 2);
    if (kv.Length == 2 && kv[0] == "code")
 return Uri.UnescapeDataString(kv[1]);
       }

            return null;
        }
    }
}
