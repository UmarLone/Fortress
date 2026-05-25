using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Fortress.Core.Utilities
{
    public static class TaskExtensions
    {
public static async void FireAndForget(this Task task, Action<Exception>? onException = null)
        {
            try { await task.ConfigureAwait(false); }
  catch (Exception ex) { onException?.Invoke(ex); }
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
    var completed = await Task.WhenAny(task, Task.Delay(-1, cancellationToken));
    if (completed == task) return await task;
 throw new OperationCanceledException(cancellationToken);
        }
    }

    public static class CoreHelpers
    {
        public static readonly string IpRegex =
        "^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\."+
      "(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

        public static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long EpochUtcNow() => (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;

        public static bool InDebugMode()
        {
#if DEBUG
         return true;
#else
            return false;
#endif
        }

        public static string? GetHostname(string uriString) => GetUri(uriString)?.Host;

        public static string? GetHost(string uriString)
  {
    var uri = GetUri(uriString);
            if (uri?.Host == null) return null;
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        public static string? GetDomain(string uriString)
        {
     var uri = GetUri(uriString);
          if (uri == null) return null;
    if (uri.Host == "localhost" || Regex.IsMatch(uri.Host, IpRegex))
       return uri.Host;
    try
      {
      if (DomainName.TryParseBaseDomain(uri.Host, out var baseDomain))
        return baseDomain ?? uri.Host;
            }
       catch { }
      return uri.AbsoluteUri;
        }

        public static Uri? GetUri(string uriString)
        {
        if (string.IsNullOrWhiteSpace(uriString)) return null;
   if (!uriString.Contains("://") && uriString.Contains("."))
                if (Uri.TryCreate("http://" + uriString, UriKind.Absolute, out var uri))
   return uri;
          return Uri.TryCreate(uriString, UriKind.Absolute, out var uri2) ? uri2 : null;
        }

    public static Dictionary<string, string> GetQueryParams(string urlString)
        {
        var dict = new Dictionary<string, string>();
            if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri) ||
    string.IsNullOrWhiteSpace(uri.Query)) return dict;
            foreach (var pair in uri.Query[1..].Split('&'))
         {
    var parts = pair.Split('=');
         if (parts.Length < 1) continue;
   var key = System.Net.WebUtility.UrlDecode(parts[0]).ToLower();
       if (!dict.ContainsKey(key))
              dict[key] = parts.Length > 1 ? System.Net.WebUtility.UrlDecode(parts[1]) : string.Empty;
            }
            return dict;
        }

        public static string SerializeJson(object obj, bool ignoreNulls = false)
        {
    var settings = new JsonSerializerSettings();
     if (ignoreNulls) settings.NullValueHandling = NullValueHandling.Ignore;
        return JsonConvert.SerializeObject(obj, settings);
        }

        public static T? DeserializeJson<T>(string json, bool ignoreNulls = false)
        {
   var settings = new JsonSerializerSettings();
    if (ignoreNulls) settings.NullValueHandling = NullValueHandling.Ignore;
       return JsonConvert.DeserializeObject<T>(json, settings);
     }

     public static string Base64UrlEncode(byte[] input) =>
            Convert.ToBase64String(input).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        public static byte[] Base64UrlDecode(string input)
        {
          var output = input.Replace('-', '+').Replace('_', '/');
     output += (output.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
     return Convert.FromBase64String(output);
        }

      public static T Clone<T>(T obj) =>
       JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj))!;
    }
}
