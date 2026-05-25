using Fortress.Mobile.Core.Models;
using System.Linq.Expressions;

namespace Fortress.Mobile.Core.Contracts
{
    public interface IDataStorageService
    {
        Task DeleteLoginItemsAsync(IEnumerable<Guid> ids);
        // ── Authenticators ─────────────────────────────────────────────────────
        Task<IEnumerable<Authenticator>> GetAuthenticatorsAsync();
        Task DeleteAuthenticatorAsync(Guid id);
        Task<Authenticator> GetAuthenticatorAsync(Expression<Func<Authenticator, bool>> predicate);
        Task AddAuthenticatorAsync(Authenticator authenticator);
        Task AddOrUpdateAuthenticatorsAsync(IEnumerable<Authenticator> authenticators);

        // ── Event logs ────────────────────────────────────────────────────────
        Task AddEventLogsAsync(IEnumerable<EventLog> eventLogs);
        Task DeleteEventLogsAsync();
        Task<IEnumerable<EventLog>> GetEventLogsAsync();

        // ── Notifications ─────────────────────────────────────────────────────
        Task<UserNotification> GetNotificationAsync(Expression<Func<UserNotification, bool>> predicate);
        Task SetNotificationExpiredAsync(Guid id);
        Task SetNotificationsSeenAsync();
        Task DeleteNotificationsAsync(IEnumerable<Guid> notificationIds);
        Task<IEnumerable<UserNotification>> GetNotificationsAsync();
        Task AddNotificationAsync(UserNotification userNotification);

        // ── General ───────────────────────────────────────────────────────────
        Task DeleteStorage();
        Task ClearDataAsync();

        // ── LoginItem ─────────────────────────────────────────────────────────
        Task<IEnumerable<LoginItem>> GetLoginItemsAsync(Expression<Func<LoginItem, bool>> predicate = null);
        Task<LoginItem> GetLoginItemAsync(Expression<Func<LoginItem, bool>> predicate);
        Task<int> GetLoginItemsCountAsync();
        Task SaveLoginItemAsync(LoginItem item);
        Task DeleteLoginItemAsync(Guid id);

        // ── CreditCardItem ────────────────────────────────────────────────────
        Task<IEnumerable<CreditCardItem>> GetCreditCardItemsAsync();
        Task<CreditCardItem> GetCreditCardItemAsync(Guid id);
        Task<int> GetCreditCardItemsCountAsync();
        Task SaveCreditCardItemAsync(CreditCardItem item);
        Task DeleteCreditCardItemAsync(Guid id);

        // ── IdentityItem ──────────────────────────────────────────────────────
        Task<IEnumerable<IdentityItem>> GetIdentityItemsAsync();
        Task<IdentityItem> GetIdentityItemAsync(Guid id);
        Task<int> GetIdentityItemsCountAsync();
        Task SaveIdentityItemAsync(IdentityItem item);
        Task DeleteIdentityItemAsync(Guid id);

        // ── SecureNoteItem ────────────────────────────────────────────────────
        Task<IEnumerable<SecureNoteItem>> GetSecureNoteItemsAsync();
        Task<SecureNoteItem> GetSecureNoteItemAsync(Guid id);
        Task<int> GetSecureNoteItemsCountAsync();
        Task SaveSecureNoteItemAsync(SecureNoteItem item);
        Task DeleteSecureNoteItemAsync(Guid id);

        // ── VaultGroup ────────────────────────────────────────────────────────
        Task<IEnumerable<VaultGroup>> GetVaultGroupsAsync();
        Task SaveVaultGroupAsync(VaultGroup group);
        Task DeleteVaultGroupAsync(Guid id);
        Task<IEnumerable<Guid>> GetCredentialIdsInGroupAsync(Guid groupId);
        Task SetCredentialGroupAsync(Guid groupId, IEnumerable<Guid> credentialIds);
        Task<Guid?> GetGroupForCredentialAsync(Guid credentialId);

        // ── Import audit ──────────────────────────────────────────────────────
        Task SaveImportAuditAsync(ImportAuditRecord record);
        Task<IEnumerable<ImportAuditRecord>> GetImportAuditsAsync();

        // ── VaultHealthSnapshot (trending) ────────────────────────────────────────
        /// <summary>
        /// Saves a daily health snapshot. If a snapshot for today already
        /// exists it is overwritten so we keep exactly one per calendar day.
        /// </summary>
        Task SaveHealthSnapshotAsync(VaultHealthSnapshot snapshot);

        /// <summary>Returns health snapshots ordered oldest-first, limited to <paramref name="days"/>.</summary>
        Task<IReadOnlyList<VaultHealthSnapshot>> GetHealthHistoryAsync(int days = 30);

        // ── PasskeyItem ────────────────────────────────────────────────────────
        Task<IEnumerable<PasskeyItem>> GetPasskeyItemsAsync();
        Task<PasskeyItem?> GetPasskeyItemAsync(Guid id);
        Task<IEnumerable<PasskeyItem>> GetPasskeyItemsByRpIdAsync(string rpId);
        Task SavePasskeyItemAsync(PasskeyItem item);
        Task DeletePasskeyItemAsync(Guid id);
        Task IncrementPasskeySignCountAsync(Guid id);

        // ── SecureItem (unified: ID card, Passport, DL, SSN, Tax, Wi-Fi, SSH) ──
        Task<IEnumerable<SecureItem>> GetSecureItemsAsync();
        Task<SecureItem?> GetSecureItemAsync(Guid id);
        Task<int> GetSecureItemsCountAsync();
        Task SaveSecureItemAsync(SecureItem item);
        Task DeleteSecureItemAsync(Guid id);
    }
}
