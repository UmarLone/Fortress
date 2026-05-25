using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Have I Been Pwned integration using the k-anonymity model.
  ///
    /// PASSWORD CHECK (k-anonymity – no plaintext ever leaves the device):
    ///   1. SHA-1 hash the plaintext password.
  ///   2. Send only the first 5 hex characters to api.pwnedpasswords.com/range/{prefix}.
    ///   3. HIBP returns all suffixes that match. We check locally whether
  ///      our full hash suffix is in the response.
    ///   Result: the API never sees the password or even the full hash.
    ///
    /// EMAIL CHECK (requires user consent – sends hashed email prefix):
///   Uses the v3 breachedaccount endpoint with truncated response mode.
    ///   Requires an API key for the email endpoint (free tier available).
    /// </summary>
    public interface IHaveIBeenPwnedService
    {
        /// <summary>
        /// Check whether a plaintext password appears in HIBP breach data.
        /// Uses k-anonymity – only a 5-char SHA-1 prefix is sent to the network.
        /// Safe to call for any password; the actual password never leaves the device.
      /// </summary>
    Task<HibpPasswordResult> CheckPasswordAsync(
 string plaintextPassword,
            CancellationToken cancellationToken = default);

        /// <summary>
  /// Check whether an email address appears in known breach databases.
        /// Requires internet access and user consent.
        /// The email is sent to the HIBP breachedaccount endpoint (requires API key).
 /// </summary>
     Task<HibpEmailResult> CheckEmailAsync(
            string email,
   CancellationToken cancellationToken = default);

  /// <summary>
        /// Bulk-check all unique emails found in the vault's identity items.
        /// Progress is reported via <paramref name="onProgress"/> as (checked, total).
     /// Rate-limited internally to respect HIBP's 1 request/1.5 s guideline.
        /// </summary>
 Task<IReadOnlyList<HibpEmailResult>> CheckAllVaultEmailsAsync(
            IEnumerable<string> emails,
         IProgress<(int done, int total)>? onProgress = null,
            CancellationToken cancellationToken = default);
    }
}
