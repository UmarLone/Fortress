using OtpNet;
using System;

namespace Fortress.Mobile.Core.Utilities
{
    public static class OtpHelper
    {
        public static OtpResult GenerateOtp(string secret)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(secret))
                {
                    return new OtpResult("", 0);
                }
                var secretKey = Base32Encoding.ToBytes(secret.Replace(" ", string.Empty));
                var totp = new Totp(secretKey);
                return new OtpResult(totp.ComputeTotp(), totp.RemainingSeconds(DateTime.UtcNow));
                
            }
            catch (Exception)
            {
 
            }
            return new OtpResult("", 0);
             
        }
    }
    public sealed class OtpResult
    {
        public string Code { get; } = string.Empty;
        public int RemainingSeconds { get;}
        public OtpResult(string code, int remainingSeconds)
        {
            Code = code;
            RemainingSeconds = remainingSeconds;
        }
    }
}
