using Fortress.Core.Models;

namespace Fortress.Core.Services
{
    public interface ICryptographyService
    {
        Task<CommandResult<string>> Encrypt(string plainText);
    Task<CommandResult<string>> Decrypt(string cipherText);
        void InvalidateKeyCache();
    }
}
