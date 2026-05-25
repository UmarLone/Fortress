using Fortress.Mobile.Core.Models;
using Fortress.Extensions;
using SimpleBase;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using HashAlgorithm = Fortress.Mobile.Core.Models.HashAlgorithm;
namespace Fortress.Helpers
{
    public class AuthenticatorHelper
    {
        public const HashAlgorithm DefaultAlgorithm = HashAlgorithm.Sha1;
        public const int IssuerMaxLength = 32;
        public const int UsernameMaxLength = 40;
        private static IGenerator _generator;
        private static long _lastCounter;
        private static string _code;
        public static Authenticator FromOtpAuthUri(string uri)
        {
            var uriMatch = Regex.Match(Uri.UnescapeDataString(uri), @"^otpauth:\/\/([a-z]+)\/([^?]*)(.*)$");

            if (!uriMatch.Success)
                throw new ArgumentException("URI is not valid");

            // Get the issuer and username if possible
            var issuerUsername = uriMatch.Groups[2].Value;
            var issuerUsernameMatch = Regex.Match(issuerUsername, @"^(.*?):(.*)$");

            var queryString = uriMatch.Groups[3].Value;

            var argMatches = Regex.Matches(queryString, "([^?=&]+)(=([^&]*))?");
            var args = new Dictionary<string, string>();

            foreach (Match match in argMatches)
            {
                if (!args.ContainsKey(match.Groups[1].Value))
                    args.Add(match.Groups[1].Value, match.Groups[3].Value);
            }

            string issuer;
            string username;

            if (issuerUsernameMatch.Success)
            {
                var issuerValue = issuerUsernameMatch.Groups[1].Value;
                var usernameValue = issuerUsernameMatch.Groups[2].Value;

                if (issuerValue == "")
                {
                    issuer = usernameValue;
                    username = null;
                }
                else
                {
                    issuer = issuerValue;
                    username = usernameValue;
                }
            }
            else
            {
                if (args.ContainsKey("issuer"))
                {
                    issuer = args["issuer"];
                    username = issuerUsername;
                }
                else
                {
                    issuer = uriMatch.Groups[2].Value;
                    username = null;
                }
            }

            var type = uriMatch.Groups[1].Value switch
            {
                "totp" when issuer == "Steam" || args.ContainsKey("steam") => AuthenticatorType.SteamOtp,
                "totp" => AuthenticatorType.Totp,
                "hotp" => AuthenticatorType.Hotp,
                _ => throw new ArgumentException("Unknown type")
            };

            var algorithm = DefaultAlgorithm;

            if (args.ContainsKey("algorithm") && type != AuthenticatorType.SteamOtp)
                algorithm = args["algorithm"].ToUpper() switch
                {
                    "SHA1" =>  HashAlgorithm.Sha1,
                    "SHA256" => HashAlgorithm.Sha256,
                    "SHA512" => HashAlgorithm.Sha512,
                    _ => throw new ArgumentException("Unknown algorithm")
                };

            var digits = type.GetDefaultDigits();
            if (args.ContainsKey("digits") && !Int32.TryParse(args["digits"], out digits))
                throw new ArgumentException("Digits parameter cannot be parsed.");

            var period = type.GetDefaultPeriod();
            if (args.ContainsKey("period") && !Int32.TryParse(args["period"], out period))
                throw new ArgumentException("Period parameter cannot be parsed.");

            var counter = 0;
            if (type == AuthenticatorType.Hotp && args.ContainsKey("counter") && !Int32.TryParse(args["counter"], out counter))
                throw new ArgumentException("Counter parameter cannot be parsed.");

            if (counter < 0)
                throw new ArgumentException("Counter cannot be negative.");

            if (!args.ContainsKey("secret"))
                throw new ArgumentException("Secret parameter is required.");


            var secret = CleanSecret(args["secret"], type);

            var auth = new Authenticator
            {
                Id = Guid.NewGuid(),
                Secret = secret,
                Issuer = issuer.Trim().Truncate(IssuerMaxLength),
                Username = username?.Trim().Truncate(UsernameMaxLength),
                //Icon = icon,
                Type = type,
                Algorithm = algorithm,
                Digits = digits,
                Period = period,
                Counter = counter,
            };
            bool IsValid()
            {
                var isValid = !string.IsNullOrEmpty(auth.Issuer) && IsValidSecret(auth.Secret, auth.Type) && auth.Digits >= auth.Type.GetMinDigits() && auth.Digits <= auth.Type.GetMaxDigits();

                if (auth.Type.GetGenerationMethod() == GenerationMethod.Time)
                    isValid = isValid && auth.Period > 0;

                return isValid;
            }
            if (!IsValid())
                throw new ArgumentException("Authenticator is invalid");
            return auth;
        }
        public static string ProcessCode(Authenticator authenticator)
        {
            _generator ??= authenticator.Type switch
            {
                AuthenticatorType.Totp => new Extensions.Totp(authenticator.Secret, authenticator.Period, authenticator.Algorithm, authenticator.Digits),
                AuthenticatorType.Hotp => new Extensions.Hotp(authenticator.Secret, authenticator.Algorithm, authenticator.Digits),
                AuthenticatorType.MobileOtp => new MobileOtp(authenticator.Secret, authenticator.Digits),
                AuthenticatorType.SteamOtp => new SteamOtp(authenticator.Secret),
                _ => throw new ArgumentException("Unknown authenticator type.")
            };

            switch (authenticator.Type.GetGenerationMethod())
            {
                case GenerationMethod.Time:
                    _code = _generator.Compute(authenticator.Counter);
                    break;

                case GenerationMethod.Counter when _lastCounter == authenticator.Counter:
                    return _code;

                case GenerationMethod.Counter:
                    {
                        _code = _generator.Compute(authenticator.Counter);
                        _lastCounter = authenticator.Counter;
                        break;
                    }
            }

            return _code;
        }
        public static string GetCode(Authenticator authenticator)
        {
            long counter;

            switch (authenticator.Type.GetGenerationMethod())
            {
                case GenerationMethod.Time:
                    {
                        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        counter = now - now % authenticator.Period;
                        break;
                    }

                case GenerationMethod.Counter:
                    counter = authenticator.Counter;
                    break;

                default:
                    throw new ArgumentException("Unknown generation method");
            }

            return ProcessCode(authenticator);
        }
        public static string GetAuthUri(Authenticator authenticator)
        {
            var type = authenticator.Type switch
            {
                AuthenticatorType.Hotp => "hotp",
                AuthenticatorType.Totp => "totp",
                AuthenticatorType.SteamOtp => "totp",
                _ => throw new NotSupportedException("Unsupported authenticator type.")
            };

            var issuerUsername = String.IsNullOrEmpty(authenticator.Username) ? authenticator.Issuer : $"{authenticator.Issuer}:{authenticator.Username}";

            var uri = new StringBuilder(
                $"otpauth://{type}/{Uri.EscapeDataString(issuerUsername)}?secret={authenticator.Secret}&issuer={Uri.EscapeDataString(authenticator.Issuer)}");

            if (authenticator.Algorithm != DefaultAlgorithm)
            {
                var algorithmName = authenticator.Algorithm switch
                {
                    HashAlgorithm.Sha1 => "SHA1",
                    HashAlgorithm.Sha256 => "SHA256",
                    HashAlgorithm.Sha512 => "SHA512",
                    _ => throw new ArgumentOutOfRangeException(nameof(HashAlgorithm))
                };
                uri.Append($"&algorithm={algorithmName}");
            }

            if (authenticator.Digits != authenticator.Type.GetDefaultDigits())
                uri.Append($"&digits={authenticator.Digits}");

            if (authenticator.Type == AuthenticatorType.Totp && authenticator.Period != authenticator.Type.GetDefaultPeriod())
                uri.Append($"&period={authenticator.Period}");

            if (authenticator.Type == AuthenticatorType.Hotp)
                uri.Append($"&counter={authenticator.Counter}");

            if (authenticator.Type == AuthenticatorType.SteamOtp && authenticator.Issuer != "Steam")
                uri.Append("&steam");

            return uri.ToString();
        }

        public static string CleanSecret(string input, AuthenticatorType type)
        {
            if (type.IsHmacBased())
                input = input.ToUpper();

            input = input.Replace(" ", "");
            input = input.Replace("-", "");

            return input;
        }

        public static bool IsValidSecret(string secret, AuthenticatorType type)
        {
            if (String.IsNullOrEmpty(secret))
                return false;

            if (type.IsHmacBased())
            {
                try
                {
                    var output = Base32.Rfc4648.Decode(secret);
                    return output.Length > 0;
                }
                catch
                {
                    return false;
                }
            }

            if (type == AuthenticatorType.MobileOtp)
                return secret.Length >= MobileOtp.SecretMinLength;
            throw new ArgumentOutOfRangeException(nameof(type));
        }
        public static int GetDefaultPeriod(AuthenticatorType type)
        {
            return 30;
        }


    }
    public interface IGenerator
    {
        public string Compute(long counter);
    }
    public class MobileOtp : IGenerator
    {
        public const int SecretMinLength = 16;
        public const int PinLength = 4;

        private readonly string _secret;
        private readonly int _digits;

        public MobileOtp(string secret, int digits)
        {
            _secret = secret;
            _digits = digits;
        }

        public string Compute(long counter)
        {
            var timestamp = counter / 10;
            var material = timestamp + _secret;
            return HashUtil.Md5(material).Truncate(_digits);
        }
    }
    public static class HashUtil
    {
        public static string Sha1(string input)
        {
            var hash = new SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes(input));
            return string.Join("", hash.Select(b => b.ToString("x2")).ToArray());
        }

        public static string Md5(string input)
        {
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));

            var builder = new StringBuilder();
            foreach (var b in hashBytes)
                builder.Append(b.ToString("x2"));

            return builder.ToString();
        }
    }
}
