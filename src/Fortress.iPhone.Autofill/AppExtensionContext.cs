using System;

namespace Fortress.iPhone.Autofill
{
    public class AppExtensionContext
    {
        public const string iOSAppProtocol = "iosapp://";

        private string? _uriString;

        public Uri? Uri
        {
            get
            {
                if (string.IsNullOrWhiteSpace(UrlString) || !Uri.TryCreate(UrlString, UriKind.Absolute, out Uri? uri))
                {
                    return null;
                }
                return uri;
            }
        }

        public string? UrlString
        {
            get
            {
                return _uriString;
            }
            set
            {
                _uriString = value;
                if (string.IsNullOrWhiteSpace(_uriString))
                {
                    return;
                }
                if (!_uriString.StartsWith(iOSAppProtocol) && _uriString.Contains("."))
                {
                    if (!_uriString.Contains("://") && !_uriString.Contains(" "))
                    {
                        _uriString = string.Concat("http://", _uriString);
                    }
                }
                if (!_uriString.StartsWith("http") && !_uriString.StartsWith(iOSAppProtocol))
                {
                    _uriString = string.Concat(iOSAppProtocol, _uriString);
                }
            }
        }


    }
}
