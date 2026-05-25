using System.Windows.Navigation;

namespace Fortress.Windows.Desktop.Views.Windows
{
    /// <summary>
    /// Embedded WebBrowser OAuth popup used for providers (e.g. OneDrive / Entra)
    /// that block the loopback HTTP listener redirect.
    ///
    /// The window navigates to the authorization URL and monitors every navigation
    /// event. When the browser is redirected to the registered redirect URI the
    /// authorization code is extracted from the query string and the window closes.
    /// </summary>
    public partial class OAuthBrowserWindow
    {
        private readonly string _redirectUriPrefix;

        /// <summary>
        /// The extracted authorization code, or <c>null</c> if the user cancelled.
        /// </summary>
        public string? AuthorizationCode { get; private set; }

        public OAuthBrowserWindow(Uri authUri, string redirectUri)
        {
          _redirectUriPrefix = redirectUri.TrimEnd('/');
            InitializeComponent();
    Loaded += (_, _) => Browser.Navigate(authUri);
        }

        private void Browser_Navigating(object sender, NavigatingCancelEventArgs e)
        {
     var url = e.Uri?.ToString() ?? string.Empty;

      // Intercept the redirect — don't actually navigate to it
        if (!url.StartsWith(_redirectUriPrefix, StringComparison.OrdinalIgnoreCase))
              return;

         e.Cancel = true;
AuthorizationCode = ExtractCode(url);
    DialogResult = AuthorizationCode is not null;
            Close();
        }

    private static string? ExtractCode(string url)
   {
            var query = url.Contains('?') ? url[(url.IndexOf('?') + 1)..] : string.Empty;
            foreach (var pair in query.Split('&'))
            {
    var kv = pair.Split('=', 2);
     if (kv.Length == 2 && kv[0].Equals("code", StringComparison.OrdinalIgnoreCase))
    return Uri.UnescapeDataString(kv[1]);
            }
            return null;
        }
    }
}
