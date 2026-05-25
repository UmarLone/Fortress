using Fortress.Helpers;
using Fortress.Services;
using System.Globalization;
using SimpleBase;

using System.Security.Cryptography;
using System.Text;
using HashAlgorithm = Fortress.Mobile.Core.Models.HashAlgorithm;
using Fortress.Mobile.Core.Utilities;

namespace Fortress.Extensions
{
    public enum GenerationMethod
    {
        Time, Counter
    }
    public static class ContainerRegistrationExtensions
    {

        //public static void RegisterViewsAndViewModelsByConvention(this IContainerRegistry containerRegistry)
        //{
        //    containerRegistry.RegisterForNavigation<NavigationPage>();
        //    var pages = GetClasses<IRegisterablePage>();
        //    foreach (var page in pages)
        //        containerRegistry.RegisterForNavigation(page, page.Name);
        //}
        private static IEnumerable<Type> GetClasses<T>()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.IsClass && typeof(T).IsAssignableFrom(p));
        }
    }
    public class Totp : HmacOtp, IGenerator
    {
        private readonly int _period;

        public Totp(string secret, int period,HashAlgorithm algorithm, int digits) : base(secret, algorithm, digits)
        {
            _period = period;
        }

        private byte[] GetCounterBytes(long counter)
        {
            var window = counter / _period;
            return ByteUtil.GetBigEndianBytes(window);
        }

        protected virtual string Finalise(int material)
        {
            return Truncate(material);
        }

        public string Compute(long counter)
        {
            var material = base.Compute(GetCounterBytes(counter));
            return Finalise(material);
        }

    }
    public abstract class HmacOtp : IDisposable
    {
        private readonly HMAC _hmac;
        private readonly int _digits;

        protected HmacOtp(string secret, HashAlgorithm algorithm, int digits)
        {
            _digits = digits;

            var secretBytes = SimpleBase.Base32.Rfc4648.Decode(secret).ToArray();
            _hmac = algorithm switch
            {
                HashAlgorithm.Sha1 => new HMACSHA1(secretBytes),
                HashAlgorithm.Sha256 => new HMACSHA256(secretBytes),
                HashAlgorithm.Sha512 => new HMACSHA512(secretBytes),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
            };
        }

        protected int Compute(byte[] counter)
        {
            var hash = _hmac.ComputeHash(counter);
            var lastIndex = hash.Length - 1;
            var offset = hash[lastIndex] & 0xF;

            return BitConverter.ToInt32(new byte[] { hash[offset], hash[offset + 1], hash[offset + 2], hash[offset + 3] }, 0) & 0x7FFFFFFF;
        }


        protected string Truncate(int material)
        {
            var otp = material % Math.Pow(10, _digits);
            var code = otp.ToString(CultureInfo.InvariantCulture).PadLeft(_digits, '0');

            return code;
        }

        public void Dispose()
        {
            _hmac?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
    public class SteamOtp : Totp
    {
        public const int Digits = 5;
        private const int Period = 30;
        private const HashAlgorithm Algorithm = HashAlgorithm.Sha1;
        private const string Alphabet = "23456789BCDFGHJKMNPQRTVWXY";

        public SteamOtp(string secret) : base(secret, Period, Algorithm, Digits) { }

        protected override string Finalise(int material)
        {
            var builder = new StringBuilder(Digits);

            for (var i = 0; i < Digits; i++)
            {
                builder.Append(Alphabet[material % Alphabet.Length]);
                material /= Alphabet.Length;
            }

            return builder.ToString();
        }
    }
    public class Hotp : HmacOtp, IGenerator
    {
        public Hotp(string secret, HashAlgorithm algorithm, int digits) : base(secret, algorithm, digits) { }

        public string Compute(long counter)
        {
            var counterBytes = ByteUtil.GetBigEndianBytes(counter);
            var material = base.Compute(counterBytes);
            return Truncate(material);
        }
    }
}
