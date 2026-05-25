using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Fortress.Mobile.Core.Utilities
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Fires a task and ignores any exception.
        /// See http://stackoverflow.com/a/22864616/344182
        /// </summary>
        /// <param name="task">The task to be forgotten.</param>
        /// <param name="onException">Action to be called on exception.</param>
        public static async void FireAndForget(this Task task, Action<Exception> onException = null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // LoggerHelper.LogEvenIfCantBeResolved(ex);
                onException?.Invoke(ex);
            }
        }
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(-1, cancellationToken));
            if (completedTask == task)
            {
                return await task;  // Task completed successfully
            }
            else
            {
                throw new OperationCanceledException(cancellationToken);  // Task was canceled
            }
        }
    }
    public static class CoreHelpers
    {
        public static readonly string IpRegex =
            "^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\." +
            "(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\." +
            "(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\." +
            "(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

        public static readonly string TldEndingRegex =
            ".*\\.(com|net|org|edu|uk|gov|ca|de|jp|fr|au|ru|ch|io|es|us|co|xyz|info|ly|mil)$";

        public static readonly DateTime Epoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long EpocUtcNow()
        {
            return (long)(DateTime.UtcNow - Epoc).TotalMilliseconds;
        }

        public static bool InDebugMode()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        public static string GetHostname(string uriString)
        {
            var uri = GetUri(uriString);
            return string.IsNullOrEmpty(uri?.Host) ? null : uri.Host;
        }

        public static string GetHost(string uriString)
        {
            var uri = GetUri(uriString);
            if (!string.IsNullOrEmpty(uri?.Host))
            {
                if (uri.IsDefaultPort)
                {
                    return uri.Host;
                }
                else
                {
                    return string.Format("{0}:{1}", uri.Host, uri.Port);
                }
            }
            return null;
        }

        public static string GetDomain(string uriString)
        {
            var uri = GetUri(uriString);
            if (uri == null)
            {
                return null;
            }

            if (uri.Host == "localhost" || Regex.IsMatch(uri.Host, IpRegex))
            {
                return uri.Host;
            }
            try
            {
                if (DomainName.TryParseBaseDomain(uri.Host, out var baseDomain))
                {
                    return baseDomain ?? uri.Host;
                }
            }
            catch { }
            return uri.AbsoluteUri;
        }

        public static Uri GetUri(string uriString)
        {
            if (string.IsNullOrWhiteSpace(uriString))
            {
                return null;
            }
            var hasHttpProtocol = uriString.StartsWith("http://") || uriString.StartsWith("https://");
            if (!hasHttpProtocol && !uriString.Contains("://") && uriString.Contains("."))
            {
                if (Uri.TryCreate("http://" + uriString, UriKind.Absolute, out var uri))
                {
                    return uri;
                }
            }
            if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri2))
            {
                return uri2;
            }
            return null;
        }
 
       

        public static Dictionary<string, string> GetQueryParams(string urlString)
        {
            var dict = new Dictionary<string, string>();
            if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Query))
            {
                return dict;
            }
            var pairs = uri.Query.Substring(1).Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length < 1)
                {
                    continue;
                }
                var key = System.Net.WebUtility.UrlDecode(parts[0]).ToLower();
                if (!dict.ContainsKey(key))
                {
                    dict.Add(key, parts[1] == null ? string.Empty : System.Net.WebUtility.UrlDecode(parts[1]));
                }
            }
            return dict;
        }

        public static string SerializeJson(object obj, bool ignoreNulls = false)
        {
            var jsonSerializationSettings = new JsonSerializerSettings();
            if (ignoreNulls)
            {
                jsonSerializationSettings.NullValueHandling = NullValueHandling.Ignore;
            }
            return JsonConvert.SerializeObject(obj, jsonSerializationSettings);
        }

        public static string SerializeJson(object obj, JsonSerializerSettings jsonSerializationSettings)
        {
            return JsonConvert.SerializeObject(obj, jsonSerializationSettings);
        }

        public static T DeserializeJson<T>(string json, bool ignoreNulls = false)
        {
            var jsonSerializationSettings = new JsonSerializerSettings();
            if (ignoreNulls)
            {
                jsonSerializationSettings.NullValueHandling = NullValueHandling.Ignore;
            }
            return JsonConvert.DeserializeObject<T>(json, jsonSerializationSettings);
        }

        public static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", string.Empty);
            return output;
        }

        public static byte[] Base64UrlDecode(string input)
        {
            var output = input;
            // 62nd char of encoding
            output = output.Replace('-', '+');
            // 63rd char of encoding
            output = output.Replace('_', '/');
            // Pad with trailing '='s
            switch (output.Length % 4)
            {
                case 0:
                    // No pad chars in this case
                    break;
                case 2:
                    // Two pad chars
                    output += "=="; break;
                case 3:
                    // One pad char
                    output += "="; break;
                default:
                    throw new InvalidOperationException("Illegal base64url string!");
            }
            // Standard base64 decoder
            return Convert.FromBase64String(output);
        }

        public static T Clone<T>(T obj)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj));
        }
    }
}
