using Windows.Security.Credentials.UI;

namespace Fortress.Windows.Desktop.Services
{
    public enum BiometricAvailability { Available, NotAvailable, NotEnrolled }
    public enum BiometricVerificationResult { Verified, Failed, Canceled, NotAvailable }

    public interface IBiometricService
    {
        Task<BiometricAvailability> CheckAvailabilityAsync();
        Task<BiometricVerificationResult> RequestVerificationAsync(string message);
    }

    public sealed class BiometricService : IBiometricService
    {
     public async Task<BiometricAvailability> CheckAvailabilityAsync()
        {
            try
            {
    var result = await UserConsentVerifier.CheckAvailabilityAsync();
                return result == UserConsentVerifierAvailability.Available
          ? BiometricAvailability.Available
 : BiometricAvailability.NotAvailable;
  }
      catch { return BiometricAvailability.NotAvailable; }
 }

   public async Task<BiometricVerificationResult> RequestVerificationAsync(string message)
 {
            try
            {
    var result = await UserConsentVerifier.RequestVerificationAsync(message);
         return result switch
         {
        UserConsentVerificationResult.Verified => BiometricVerificationResult.Verified,
           UserConsentVerificationResult.Canceled => BiometricVerificationResult.Canceled,
        _ => BiometricVerificationResult.Failed
             };
    }
            catch { return BiometricVerificationResult.NotAvailable; }
     }
    }
}
