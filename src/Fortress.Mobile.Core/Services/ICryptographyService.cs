using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Services
{
    public interface ICryptographyService
    {
        Task<CommandResult<string>> Decrypt(string cipherText);
        Task<CommandResult<string>> Encrypt(string plainText);
        /// <summary>
        /// Evicts the cached derived key so the next operation re-derives it
        /// from the current master password. Call after the master password changes.
        /// </summary>
        void InvalidateKeyCache();
    }
}