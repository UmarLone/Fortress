using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Mappers
{
    /// <summary>
    /// Maps between <see cref="LoginItem"/> (storage) and <see cref="CredentialView"/> (UI).
    /// Replaces the old CredentialMapper that worked on the legacy Credential model.
    /// </summary>
  public static class LoginItemMapper
    {
     public static CredentialView Map(LoginItem item) => new()
   {
      Id             = item.Id,
     Domain  = FormatDomain(item),
        Username       = item.Username,
   Password    = item.Password,
  Data           = item.OtpSecret ?? string.Empty,
      HasOtp         = !string.IsNullOrWhiteSpace(item.OtpSecret),
      CredentialType = ToCredentialTypeString(item.LoginType),
  IconUri        = GetIcon(item),
    FallbackIcon   = GetFallbackIcon(item),
      RequireAuthBeforeFill = item.RequireAuthBeforeFill,
    CreatedAt   = item.CreatedAt,
        UpdatedAt      = item.UpdatedAt,
        PasswordStrengthScore = item.PasswordStrengthScore,
   PasswordStrengthLevel = item.PasswordStrengthLevel,
        Notes          = item.Notes ?? string.Empty,
        IsFavorite     = item.IsFavorite,
    };

        public static IEnumerable<CredentialView> Map(IEnumerable<LoginItem> items)
       => items.Select(Map);

        public static LoginItem Map(CredentialView view, LoginItem? existing = null)
        {
        var item = existing ?? new LoginItem { Id = view.Id };
         item.Label       = view.Domain;
   item.Url         = view.Domain;
            item.Username    = view.Username ?? string.Empty;
            item.Password    = view.Password ?? string.Empty;
       item.OtpSecret   = view.HasOtp ? view.Data : null;
 item.LoginType   = ToLoginType(view.CredentialType);
            return item;
   }

      // ── Helpers ───────────────────────────────────────────────────────

        private static string FormatDomain(LoginItem item)
    {
            var url = item.Url;
 if (string.IsNullOrEmpty(url)) return item.Label;
  try
      {
    if (item.LoginType is LoginType.Web &&
          Uri.TryCreate(url, UriKind.Absolute, out var uri))
        return uri.Host;

    if (item.LoginType == LoginType.PhoneApp &&
              (url.Contains("androidapp://") || url.Contains("iosapp://")) &&
       Uri.TryCreate(url, UriKind.Absolute, out var appUri))
   return appUri.Host;
   }
        catch { }
     return string.IsNullOrEmpty(item.Label) ? url : item.Label;
        }

        private static string GetIcon(LoginItem item) => item.LoginType switch
        {
          LoginType.Web =>
         $"https://hubfunctions-us.azurewebsites.net/api/LogoService/Icon?domain={FixUrl(item.Url)}&size=64",
   LoginType.PhoneApp =>
     $"https://hubfunctions-us.azurewebsites.net/api/LogoService/Icon?domain={item.Url}&size=64",
 LoginType.DesktopApp => "applicationlogo.png",
            _ => "gklogoblue64.png"
  };

        private static string GetFallbackIcon(LoginItem item) => item.LoginType switch
        {
            LoginType.Web     => "weblogo.png",
         LoginType.PhoneApp => "phoneapplogo.png",
    LoginType.DesktopApp => "applicationlogo.png",
            _ => "gklogoblue64.png"
        };

        private static string ToCredentialTypeString(LoginType t) => t switch
        {
            LoginType.PhoneApp   => "PhoneApplication",
     LoginType.DesktopApp => "Application",
       _      => "Web"
  };

        private static LoginType ToLoginType(string? credentialType) => credentialType switch
     {
      "PhoneApplication" => LoginType.PhoneApp,
            "Application"=> LoginType.DesktopApp,
   _        => LoginType.Web
     };

        private static string FixUrl(string url)
     {
  if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
         (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        url = "https://" + url;
            return url;
        }
    }
}

