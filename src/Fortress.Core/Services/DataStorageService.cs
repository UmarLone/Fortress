using Fortress.Core.Contracts;
using Fortress.Core.Models;
using LiteDB;
using LiteDB.Async;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Fortress.Core.Services
{
    /// <summary>
    /// LiteDB-backed vault storage. No MAUI dependency �
    /// preferences are provided via <see cref="IPreferenceService"/>.
    /// </summary>
    public sealed class DataStorageService : IDataStorageService, IDisposable
    {
        private const string DatabaseName = "Fortress.ldb";
        private const string Authenticators = "authenticators";
        private const string Notifications = "notifications";
        private const string EventLogs = "eventLogs";
        private const string LoginItems = "loginItems";
        private const string CreditCardItems = "creditCardItems";
        private const string IdentityItems = "identityItems";
        private const string SecureNoteItems = "secureNoteItems";
        private const string VaultGroups = "vaultGroups";
        private const string GroupMemberships = "vaultGroupMemberships";
        private const string PasskeyItems = "passkeyItems";
        private const string SecureItems = "secureItems";
        private const string ImportAudits = "importAudits";
        private const string HealthSnapshots = "vaultHealthSnapshots";

        private const string SchemaVersionPref = "pref_dbSchemaVersion";
        private const int CurrentSchemaVersion = 2;
        private const string DbFileKeyPref = "pref_dbFileKey";

        private readonly ILogger<DataStorageService>? _logger;
        private readonly IPreferenceService _prefs;
        private readonly string _databasePath;

        private LiteDatabaseAsync? _database;
        private readonly SemaphoreSlim _openLock = new(1, 1);

        public DataStorageService(IPreferenceService prefs, ILogger<DataStorageService>? logger = null, string? databaseDirectory = null)
        {
            _prefs = prefs;
            _logger = logger;
            _databasePath = databaseDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fortress", "Databases");
        }

        private string DbFilePath => Path.Combine(_databasePath, DatabaseName);

        // ── Open / Close ──────────────────────────────────────────────────────
        private void Open()
        {
            if (_database != null) return;
            _openLock.Wait();
            try { if (_database == null) OpenCore(); }
            finally { _openLock.Release(); }
        }

        private async Task OpenAsync()
        {
            if (_database != null) return;
            await _openLock.WaitAsync().ConfigureAwait(false);
            try { if (_database == null) OpenCore(); }
            finally { _openLock.Release(); }
        }

        private void OpenCore()
        {
            if (!Directory.Exists(_databasePath))
                Directory.CreateDirectory(_databasePath);

            var dbKey = GetOrCreateDbFileKey();

            if (File.Exists(DbFilePath))
            {
                try
                {
                    var verify = new ConnectionString { Filename = DbFilePath, Connection = ConnectionType.Direct, Password = dbKey, ReadOnly = true };
                    using var vdb = new LiteDatabase(verify);
                    _ = vdb.GetCollectionNames();
                }
                catch (LiteException ex) when (ex.Message.Contains("Invalid password", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogWarning("DB file key mismatch � wiping and starting fresh.");
                    SafeDeleteDatabaseFile();
                    _prefs.Remove(DbFileKeyPref);
                    dbKey = GetOrCreateDbFileKey();
                }
            }

            var conn = new ConnectionString { Filename = DbFilePath, Connection = ConnectionType.Direct, Password = dbKey };
            _database = new LiteDatabaseAsync(conn) { CheckpointSize = 1000 };
            var storedVersion = _prefs.Get(SchemaVersionPref, 0);
            if (storedVersion < CurrentSchemaVersion)
            {
                try { _database.UnderlyingDatabase.DropCollection(EventLogs); } catch { }
                _prefs.Set(SchemaVersionPref, CurrentSchemaVersion);
            }
        }

        private string GetOrCreateDbFileKey()
        {
            var key = _prefs.Get(DbFileKeyPref, string.Empty);
            if (!string.IsNullOrEmpty(key)) return key;
            key = Utilities.StringGenerator.GenerateRandomString();
            _prefs.Set(DbFileKeyPref, key);
            return key;
        }

        private void SafeDeleteDatabaseFile()
        {
            foreach (var path in new[] { DbFilePath, DbFilePath + "-log", DbFilePath + "-temp" })
                try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        public void Reopen()
        {
            _openLock.Wait();
            try { _database?.Dispose(); _database = null; }
            finally { _openLock.Release(); }
            Open();
        }

        public void Dispose() => _database?.Dispose();


        public async Task<IEnumerable<Authenticator>> GetAuthenticatorsAsync()
        {
            Open();
            return await _database!.GetCollection<Authenticator>(Authenticators).FindAllAsync();
        }

        public async Task<Authenticator> GetAuthenticatorAsync(Expression<Func<Authenticator, bool>> predicate)
        {
            Open();
            return await _database!.GetCollection<Authenticator>(Authenticators).FindOneAsync(predicate);
        }

        public async Task AddAuthenticatorAsync(Authenticator authenticator)
        {
            Open();
            await _database!.GetCollection<Authenticator>(Authenticators).InsertAsync(authenticator);
        }

        public async Task AddOrUpdateAuthenticatorsAsync(IEnumerable<Authenticator> authenticators)
        {
            Open();
            var col = _database!.GetCollection<Authenticator>(Authenticators);
            var list = authenticators.ToList();
            if (!list.Any()) return;
            var existing = (await col.FindAllAsync()).ToDictionary(a => a.Id);
            var toInsert = list.Where(a => !existing.ContainsKey(a.Id)).ToList();
            var toUpdate = list.Where(a => existing.ContainsKey(a.Id)).ToList();
            if (toInsert.Any()) await col.InsertBulkAsync(toInsert);
            if (toUpdate.Any()) await col.UpdateAsync(toUpdate);
        }

        public async Task DeleteAuthenticatorAsync(Guid id)
        {
            Open();
            await _database!.GetCollection<Authenticator>(Authenticators).DeleteManyAsync(x => x.Id == id);
        }

        // ── Event logs ────────────────────────────────────────────────────────
        public async Task AddEventLogsAsync(IEnumerable<EventLog> eventLogs)
        {
            Open();
            await _database!.GetCollection<EventLog>(EventLogs).InsertAsync(eventLogs);
        }

        public async Task DeleteEventLogsAsync()
        {
            Open();
            await _database!.GetCollection<EventLog>(EventLogs).DeleteAllAsync();
        }

        public async Task<IEnumerable<EventLog>> GetEventLogsAsync()
        {
            Open();
            var col = _database!.GetCollection<EventLog>(EventLogs);
            try { return await col.FindAllAsync(); }
            catch (Exception ex) when (ex is InvalidCastException || ex is LiteException)
            {
                _logger?.LogWarning(ex, "[DataStorageService] EventLog schema mismatch � dropping stale collection");
                try { _database!.UnderlyingDatabase.DropCollection(EventLogs); } catch { }
                return Array.Empty<EventLog>();
            }
        }

        // ── Notifications ─────────────────────────────────────────────────────
        public async Task<UserNotification> GetNotificationAsync(Expression<Func<UserNotification, bool>> predicate)
        {
            Open();
            return await _database!.GetCollection<UserNotification>(Notifications).FindOneAsync(predicate);
        }

        public async Task<IEnumerable<UserNotification>> GetNotificationsAsync()
        {
            Open();
            return await _database!.GetCollection<UserNotification>(Notifications).FindAllAsync();
        }

        public async Task AddNotificationAsync(UserNotification notification)
        {
            Open();
            await _database!.GetCollection<UserNotification>(Notifications).InsertAsync(notification);
        }

        public async Task SetNotificationExpiredAsync(Guid id)
        {
            Open();
            var col = _database!.GetCollection<UserNotification>(Notifications);
            var n = await col.FindOneAsync(x => x.Id == id);
            if (n != null) { n.IsExpired = true; await col.UpdateAsync(n); }
        }

        public async Task SetNotificationsSeenAsync()
        {
            Open();
            var col = _database!.GetCollection<UserNotification>(Notifications);
            var unseen = (await col.FindAsync(n => !n.IsSeen)).ToList();
            if (!unseen.Any()) return;
            foreach (var n in unseen) n.IsSeen = true;
            await col.UpdateAsync(unseen);
        }

        public async Task DeleteNotificationsAsync(IEnumerable<Guid> ids)
        {
            Open();
            var col = _database!.GetCollection<UserNotification>(Notifications);
            var idList = ids.ToList();
            var toDelete = await col.FindAsync(n => idList.Contains(n.Id));
            foreach (var n in toDelete) await col.DeleteAsync(n.Id);
        }

        // ── General ───────────────────────────────────────────────────────────
        public async Task DeleteStorage()
        {
            Open();
            foreach (var name in await _database!.GetCollectionNamesAsync())
                await _database.DropCollectionAsync(name);
        }

        public async Task ClearDataAsync()
        {
            Open();
            foreach (var name in await _database!.GetCollectionNamesAsync())
                await _database.DropCollectionAsync(name);
        }

        // ── LoginItem ─────────────────────────────────────────────────────────
        public async Task<IEnumerable<LoginItem>> GetLoginItemsAsync(Expression<Func<LoginItem, bool>>? predicate = null)
        {
            Open();
            var col = _database!.GetCollection<LoginItem>(LoginItems);
            return predicate is null ? await col.FindAllAsync() : await col.FindAsync(predicate);
        }

        public async Task<LoginItem> GetLoginItemAsync(Expression<Func<LoginItem, bool>> predicate)
        {
            Open();
            return await _database!.GetCollection<LoginItem>(LoginItems).FindOneAsync(predicate);
        }

        public async Task<int> GetLoginItemsCountAsync()
        {
            Open();
            return await _database!.GetCollection<LoginItem>(LoginItems).CountAsync();
        }

        public async Task SaveLoginItemAsync(LoginItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database!.GetCollection<LoginItem>(LoginItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null) await col.InsertAsync(item); else await col.UpdateAsync(item);
        }

        public async Task DeleteLoginItemAsync(Guid id)
        {
            Open();
            await _database!.GetCollection<LoginItem>(LoginItems).DeleteManyAsync(x => x.Id == id);
        }

        // ── CreditCardItem ────────────────────────────────────────────────────
        public async Task<IEnumerable<CreditCardItem>> GetCreditCardItemsAsync()
        {
            Open();
            return await _database!.GetCollection<CreditCardItem>(CreditCardItems).FindAllAsync();
        }

        public async Task<CreditCardItem> GetCreditCardItemAsync(Guid id)
        {
            Open();
            return await _database!.GetCollection<CreditCardItem>(CreditCardItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<int> GetCreditCardItemsCountAsync()
        {
            Open();
            return await _database!.GetCollection<CreditCardItem>(CreditCardItems).CountAsync();
        }

        public async Task SaveCreditCardItemAsync(CreditCardItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database!.GetCollection<CreditCardItem>(CreditCardItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null) await col.InsertAsync(item); else await col.UpdateAsync(item);
        }

        public async Task DeleteCreditCardItemAsync(Guid id)
        {
            Open();
            await _database!.GetCollection<CreditCardItem>(CreditCardItems).DeleteManyAsync(x => x.Id == id);
        }

        // ── IdentityItem ──────────────────────────────────────────────────────
        public async Task<IEnumerable<IdentityItem>> GetIdentityItemsAsync()
        {
            Open();
            return await _database!.GetCollection<IdentityItem>(IdentityItems).FindAllAsync();
        }

        public async Task<IdentityItem> GetIdentityItemAsync(Guid id)
        {
            Open();
            return await _database!.GetCollection<IdentityItem>(IdentityItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<int> GetIdentityItemsCountAsync()
        {
            Open();
            return await _database!.GetCollection<IdentityItem>(IdentityItems).CountAsync();
        }

        public async Task SaveIdentityItemAsync(IdentityItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database!.GetCollection<IdentityItem>(IdentityItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null) await col.InsertAsync(item); else await col.UpdateAsync(item);
        }

        public async Task DeleteIdentityItemAsync(Guid id)
        {
            Open();
            await _database!.GetCollection<IdentityItem>(IdentityItems).DeleteManyAsync(x => x.Id == id);
        }

        // ── SecureNoteItem ────────────────────────────────────────────────────
        public async Task<IEnumerable<SecureNoteItem>> GetSecureNoteItemsAsync()
        {
            Open();
            return await _database!.GetCollection<SecureNoteItem>(SecureNoteItems).FindAllAsync();
        }

        public async Task<SecureNoteItem> GetSecureNoteItemAsync(Guid id)
        {
            Open();
            return await _database!.GetCollection<SecureNoteItem>(SecureNoteItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<int> GetSecureNoteItemsCountAsync()
        {
            Open();
            return await _database!.GetCollection<SecureNoteItem>(SecureNoteItems).CountAsync();
        }

        public async Task SaveSecureNoteItemAsync(SecureNoteItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database!.GetCollection<SecureNoteItem>(SecureNoteItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null) await col.InsertAsync(item); else await col.UpdateAsync(item);
        }

        public async Task DeleteSecureNoteItemAsync(Guid id)
        {
            Open();
            await _database!.GetCollection<SecureNoteItem>(SecureNoteItems).DeleteManyAsync(x => x.Id == id);
        }

        // ── VaultGroup ────────────────────────────────────────────────────────
        public async Task<IEnumerable<VaultGroup>> GetVaultGroupsAsync()
        {
            Open();
            return await _database!.GetCollection<VaultGroup>(VaultGroups).FindAllAsync();
        }

        public async Task SaveVaultGroupAsync(VaultGroup group)
        {
            Open();
            var col = _database!.GetCollection<VaultGroup>(VaultGroups);
            var existing = await col.FindOneAsync(x => x.Id == group.Id);
            if (existing is null) await col.InsertAsync(group); else await col.UpdateAsync(group);
        }

        public async Task DeleteVaultGroupAsync(Guid id)
        {
            Open();
            var col = _database!.GetCollection<VaultGroup>(VaultGroups);
            var item = await col.FindOneAsync(x => x.Id == id);
            if (item is not null) await col.DeleteAsync(item.Id);
            var memCol = _database!.GetCollection<VaultGroupMembership>(GroupMemberships);
            var memberships = await memCol.FindAsync(m => m.GroupId == id);
            foreach (var m in memberships) await memCol.DeleteAsync(m.Id);
        }

        public async Task<IEnumerable<Guid>> GetCredentialIdsInGroupAsync(Guid groupId)
        {
            Open();
            var rows = await _database!.GetCollection<VaultGroupMembership>(GroupMemberships).FindAsync(m => m.GroupId == groupId);
            return rows.Select(m => m.CredentialId).ToList();
        }

        public async Task SetCredentialGroupAsync(Guid groupId, IEnumerable<Guid> credentialIds)
        {
            Open();
            var col = _database!.GetCollection<VaultGroupMembership>(GroupMemberships);
            var existing = await col.FindAsync(m => m.GroupId == groupId);
            foreach (var m in existing) await col.DeleteAsync(m.Id);
            var newRows = credentialIds.Select(cid => new VaultGroupMembership { GroupId = groupId, CredentialId = cid }).ToList();
            if (newRows.Count > 0) await col.InsertBulkAsync(newRows);
        }

        public async Task<Guid?> GetGroupForCredentialAsync(Guid credentialId)
        {
            Open();
            var row = await _database!.GetCollection<VaultGroupMembership>(GroupMemberships).FindOneAsync(m => m.CredentialId == credentialId);
            return row?.GroupId;
        }

        // ── ImportAuditRecord ─────────────────────────────────────────────────
        public async Task SaveImportAuditAsync(ImportAuditRecord record)
        {
            Open();
            await _database!.GetCollection<ImportAuditRecord>(ImportAudits).InsertAsync(record);
        }

        public async Task<IEnumerable<ImportAuditRecord>> GetImportAuditsAsync()
        {
            Open();
            return await _database!.GetCollection<ImportAuditRecord>(ImportAudits).FindAllAsync();
        }

        // ── VaultHealthSnapshot ───────────────────────────────────────────────
        public async Task SaveHealthSnapshotAsync(VaultHealthSnapshot snapshot)
        {
            Open();
            snapshot.RecordedDate = snapshot.RecordedDate.Date;
            var col = _database!.GetCollection<VaultHealthSnapshot>(HealthSnapshots);
            var existing = await col.FindOneAsync(s => s.RecordedDate == snapshot.RecordedDate);
            if (existing is null) await col.InsertAsync(snapshot);
            else { snapshot.Id = existing.Id; await col.UpdateAsync(snapshot); }
        }

        public async Task<IReadOnlyList<VaultHealthSnapshot>> GetHealthHistoryAsync(int days = 30)
        {
            Open();
            var cutoff = DateTime.UtcNow.Date.AddDays(-days);
            var rows = await _database!.GetCollection<VaultHealthSnapshot>(HealthSnapshots).FindAsync(s => s.RecordedDate >= cutoff);
            return rows.OrderBy(s => s.RecordedDate).ToList().AsReadOnly();
        }

        // ── PasskeyItem ────────────────────────────────────────────────────────
        public async Task<IEnumerable<PasskeyItem>> GetPasskeyItemsAsync()
        {
            Open();
            return await _database!.GetCollection<PasskeyItem>(PasskeyItems).FindAllAsync();
        }

        public async Task<PasskeyItem?> GetPasskeyItemAsync(Guid id)
        {
            Open();
            return await _database!.GetCollection<PasskeyItem>(PasskeyItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<IEnumerable<PasskeyItem>> GetPasskeyItemsByRpIdAsync(string rpId)
        {
            Open();
            return await _database!.GetCollection<PasskeyItem>(PasskeyItems).FindAsync(x => x.RpId == rpId);
        }

        public async Task SavePasskeyItemAsync(PasskeyItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database!.GetCollection<PasskeyItem>(PasskeyItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null) await col.InsertAsync(item); else await col.UpdateAsync(item);
        }

        public async Task DeletePasskeyItemAsync(Guid id)
        {
            Open();
            await _database!.GetCollection<PasskeyItem>(PasskeyItems).DeleteManyAsync(x => x.Id == id);
        }

        public async Task IncrementPasskeySignCountAsync(Guid id)
        {
            Open();
            var col = _database!.GetCollection<PasskeyItem>(PasskeyItems);
            var item = await col.FindOneAsync(x => x.Id == id);
            if (item is null) return;
            item.SignCount++;
            item.UpdatedAt = DateTime.UtcNow;
            await col.UpdateAsync(item);
        }

        // ── SecureItem ─────────────────────────────────────────────────────────
        public async Task<IEnumerable<SecureItem>> GetSecureItemsAsync()
        {
            Open();
            return await _database!.GetCollection<SecureItem>(SecureItems).FindAllAsync();
        }

        public async Task<SecureItem?> GetSecureItemAsync(Guid id)
        {
            Open();
            return await _database!.GetCollection<SecureItem>(SecureItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<int> GetSecureItemsCountAsync()
        {
            Open();
            return await _database!.GetCollection<SecureItem>(SecureItems).CountAsync();
        }

        public async Task SaveSecureItemAsync(SecureItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database!.GetCollection<SecureItem>(SecureItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null) await col.InsertAsync(item); else await col.UpdateAsync(item);
        }

        public async Task DeleteSecureItemAsync(Guid id)
        {
            Open();
            await _database!.GetCollection<SecureItem>(SecureItems).DeleteManyAsync(x => x.Id == id);
        }
    }
}
