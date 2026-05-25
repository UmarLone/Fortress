using Fortress.Mobile.Core.Models;

namespace Fortress.Mobile.Core.Contracts
{
    /// <summary>
    /// Flexible vault import engine.
    /// Supports CSV/JSON from Chrome, Edge, Firefox, Safari, Bitwarden,
    /// 1Password, Dashlane and LastPass.
    /// </summary>
    public interface IVaultImportService
    {
        /// <summary>
        /// Reads the file at <paramref name="filePath"/>, auto-detects the format,
        /// maps columns, normalises domains, scores passwords and detects duplicates.
        /// Returns an <see cref="ImportPreview"/> the UI shows to the user.
        /// The file buffer is zeroed and the temp file is deleted after parsing.
        /// Raw credentials are never written to any log.
        /// </summary>
        Task<ImportPreview> PreviewAsync(
               string filePath,
               CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits the candidates in <paramref name="preview"/> to the vault
        /// using the supplied <paramref name="mergeOption"/>.
        /// Persists a non-sensitive <see cref="ImportAuditRecord"/> on completion.
        /// </summary>
        Task<ImportResult> CommitAsync(
       ImportPreview preview,
                DuplicateMergeOption mergeOption,
            CancellationToken cancellationToken = default);

        /// <summary>Returns all non-sensitive import audit records, newest first.</summary>
        Task<IEnumerable<ImportAuditRecord>> GetImportHistoryAsync();

        /// <summary>
        /// Parses an authenticator backup file (Aegis JSON, Raivo JSON,
        /// 2FAS JSON, or a plain-text list of otpauth:// URIs) and returns
        /// a preview of accounts that would be imported.
        /// </summary>
        Task<AuthenticatorImportPreview> PreviewAuthenticatorsAsync(
         string filePath,
   CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the authenticators from <paramref name="preview"/> to the vault,
        /// skipping any that already exist (same issuer + username).
        /// </summary>
        Task<AuthenticatorImportResult> CommitAuthenticatorsAsync(
            AuthenticatorImportPreview preview,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses one or more raw <c>otpauth://</c> URIs (e.g. scanned from
        /// a Google Authenticator QR code export) and saves them to the vault.
        /// </summary>
        Task<AuthenticatorImportResult> ImportAuthenticatorsFromUrisAsync(
IEnumerable<string> uris,
          CancellationToken cancellationToken = default);

      /// <summary>
        /// Parses one or more raw <c>otpauth://</c> or <c>otpauth-migration://</c>
        /// URIs and returns a preview – no data is written to the vault.
   /// Call <see cref="CommitAuthenticatorsAsync"/> with the returned preview to commit.
        /// </summary>
        Task<AuthenticatorImportPreview> ImportAuthenticatorsPreviewFromUrisAsync(
            IEnumerable<string> uris,
            CancellationToken cancellationToken = default);
    }
}
