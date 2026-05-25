using Fortress.Mobile.Core.Models;
namespace Fortress.Mobile.Core.Services
{
    public interface ICredentialResolver
    {
        Task<List<CredentialView>> GetMatchingCredentialsAsync(string url, List<CipherType> includeOtherTypes = null);
        /// <summary>Returns every credential in the vault, ordered by domain — used for the "Browse all" fallback.</summary>
        Task<List<CredentialView>> GetAllCredentialsAsync();
        /// <summary>Returns all credit cards as <see cref="CredentialView"/> — used by the autofill card flow.</summary>
        Task<List<CredentialView>> GetCardCredentialsAsync();
        /// <summary>Returns all identities as <see cref="CredentialView"/> — used by the autofill identity flow.</summary>
        Task<List<CredentialView>> GetIdentityCredentialsAsync();
    }
}