using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using LiteDB;
using LiteDB.Async;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using Device = Fortress.Mobile.Core.Models.Device;


namespace Fortress.Mobile.Core.Services
{
    public sealed class SqliteStorage : IDataStorageService, IDisposable
    {
        private const string DatabaseName = "Fortress.ldb";
        private const string Users = "users";
        private const string Credentials = "credentials";
        private const string Notifications = "notifications";
        private const string Authenticators = "authenticators";
        private const string EventLogs = "eventLogs";

        // ── New typed collections ──────────────────────────────────────────
        private const string LoginItems = "loginItems";
        private const string CreditCardItems = "creditCardItems";
        private const string IdentityItems = "identityItems";
        private const string SecureNoteItems = "secureNoteItems";
        private const string VaultGroups = "vaultGroups";
        private const string GroupMemberships = "vaultGroupMemberships";
        private const string PasskeyItems = "passkeyItems";
        private const string SecureItems   = "secureItems";   // unified: ID card, passport, DL, SSN, Tax, Wi-Fi, SSH

        // ── Schema versioning ─────────────────────────────────────────────
   // Bump this when any collection's document shape changes incompatibly.
        // On first open after an upgrade the old collection is dropped so
        // LiteDB never tries to cast stale BSON into the new C# model.
        private const string SchemaVersionPref = "pref_dbSchemaVersion";
        private const int    CurrentSchemaVersion = 2; // bumped: EventLog model redesign

        // Separate preference key for the DB file encryption key.
        // This is NEVER the master password — it is a stable random key
        // generated once and never changed, regardless of what the user sets
        // as their master password.
        private const string DbFileKeyPref = "pref_dbFileKey";

        private readonly ILogger<SqliteStorage> _logger;
        private static string DatabasePath => Path.Combine(
       Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Databases");
        private static string DbFilePath => Path.Combine(DatabasePath, DatabaseName);

        private LiteDatabaseAsync _database;
        private readonly SemaphoreSlim _openLock = new SemaphoreSlim(1, 1);

        // Synchronous Open() kept for compatibility with callers that haven't been
        // converted yet — but it now yields the thread via ConfigureAwait(false)
        private void Open()
        {
            if (_database != null) return;   // fast path — no lock needed for reads
            _openLock.Wait();
            try
            {
                if (_database != null) return;
                OpenCore();
            }
            finally { _openLock.Release(); }
        }

        private async Task OpenAsync()
        {
            if (_database != null) return;
            await _openLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_database != null) return;
                OpenCore();
            }
            finally { _openLock.Release(); }
        }

        private void OpenCore()
        {
            if (!Directory.Exists(DatabasePath))
                Directory.CreateDirectory(DatabasePath);

            var dbKey = GetOrCreateDbFileKey();

            if (File.Exists(DbFilePath))
            {
                try
                {
                    var verifyConnection = new ConnectionString
                    {
                        Filename = DbFilePath,
                        Connection = ConnectionType.Direct,
                        Password = dbKey,
                        ReadOnly = true
                    };
                    using var verifyDb = new LiteDatabase(verifyConnection);
                    _ = verifyDb.GetCollectionNames();
                }
                catch (LiteException ex) when (ex.Message.Contains("Invalid password", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("DB file key mismatch — wiping and starting fresh.");
                    SafeDeleteDatabaseFile();
                    Preferences.Default.Remove(DbFileKeyPref);
                    dbKey = GetOrCreateDbFileKey();
                }
            }

            var connection = new ConnectionString
            {
                Filename = DbFilePath,
                Connection = ConnectionType.Direct,
                Password = dbKey
            };
            _database = new LiteDatabaseAsync(connection) { CheckpointSize = 1000 };

            // ── One-time schema migration ──────────────────────────────────
            // If the stored schema version is older than CurrentSchemaVersion,
    // drop collections whose document shape has changed so LiteDB never
            // tries to deserialise stale BSON into the new C# models.
            var storedVersion = Preferences.Default.Get(SchemaVersionPref, 0);
      if (storedVersion < CurrentSchemaVersion)
        {
      try
       {
         // v2: EventLog model was redesigned (_id was ObjectId, now Guid;
          //     fields renamed/removed). Old documents cannot be cast.
        _database.UnderlyingDatabase.DropCollection(EventLogs);
   }
      catch { /* collection may not exist on a fresh install — safe to ignore */ }

              Preferences.Default.Set(SchemaVersionPref, CurrentSchemaVersion);
  }
        }
        /// <summary>
        /// Gets or creates the stable database file encryption key.
        /// This is completely separate from the user's master password.
        /// </summary>
        private static string GetOrCreateDbFileKey()
        {
            var key = Preferences.Default.Get(DbFileKeyPref, string.Empty);
            if (!string.IsNullOrEmpty(key))
                return key;

            key = Utilities.StringGenerator.GenerateRandomString();
            Preferences.Default.Set(DbFileKeyPref, key);
            return key;
        }

        private static void SafeDeleteDatabaseFile()
        {
            try
            {
                // LiteDB creates up to 3 files: .ldb, .ldb-log, .ldb-temp
                foreach (var path in new[] { DbFilePath, DbFilePath + "-log", DbFilePath + "-temp" })
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
            catch
            {
                // Best-effort — if we can't delete, the next open attempt will fail again
                // but at least the app won't crash repeatedly
            }
        }

        /// <summary>
        /// Called when the app detects it needs to close and reopen the database
        /// (e.g. after a full data wipe). The DB file key does NOT change.
        /// </summary>
        public void Reopen()
        {
            _openLock.Wait();
            try
            {
                _database?.Dispose();
                _database = null;
            }
            finally
            {
                _openLock.Release();
            }
            Open();
        }

        private void Close()
        {
            _database?.Dispose();
            _database = null;
        }
        public async Task<IEnumerable<Authenticator>> GetAuthenticatorsAsync()
        {
            Open();
            var authenticators = _database.GetCollection<Authenticator>(Authenticators);
            return await authenticators.FindAllAsync();
        }

        public async Task<Authenticator> GetAuthenticatorAsync(Expression<Func<Authenticator, bool>> predicate)
        {
            Open();
            var authenticator = _database.GetCollection<Authenticator>(Authenticators);
            return await authenticator.FindOneAsync(predicate);
        }
        public async Task AddAuthenticatorAsync(Authenticator authenticator)
        {
            Open();
            var authenticatorsCollection = _database.GetCollection<Authenticator>(Authenticators);
            await authenticatorsCollection.InsertAsync(authenticator);

        }
        public async Task AddOrUpdateAuthenticatorsAsync(IEnumerable<Authenticator> authenticators)
        {
            Open();
            var authenticatorsCollection = _database.GetCollection<Authenticator>(Authenticators);
            var authenticatorsList = authenticators.ToList();

            if (!authenticatorsList.Any())
                return;

            var existingAuthenticators = (await authenticatorsCollection.FindAllAsync())
                .ToDictionary(a => a.Id);

            var toInsert = new List<Authenticator>();
            var toUpdate = new List<Authenticator>();

            foreach (var authenticator in authenticatorsList)
            {
                if (!existingAuthenticators.TryGetValue(authenticator.Id, out var existingAuth))
                    toInsert.Add(authenticator);
                else
                {
                    authenticator.Id = existingAuth.Id;
                    toUpdate.Add(authenticator);
                }
            }

            // Batch insert and update for better performance
            if (toInsert.Any())
                await authenticatorsCollection.InsertBulkAsync(toInsert);

            if (toUpdate.Any())
                await authenticatorsCollection.UpdateAsync(toUpdate);
        }
        public async Task DeleteAuthenticatorAsync(Guid id)
        {
            Open();
            await _database.GetCollection<Authenticator>(Authenticators).DeleteManyAsync(x => x.Id == id);
        }

        // ── Removed: AddOrUpdateCredentialAsync, AddOrUpdateCredentialsAsync,
        // DeleteCredentialAsync, DeleteCredentialsAsync, ClearAndAddCredentialsAsync,
        // GetCredentialAsync, GetCredentialsAsync, GetCredentialsPagedAsync,
        // GetCredentialsCountAsync — all callers now use LoginItem methods.
        // The "credentials" LiteDB collection remains on disk for migration only.

        public async Task DeleteNotificationsAsync(IEnumerable<Guid> notificationIds)
        {
            Open();
            var userNotificationsCollection = _database.GetCollection<UserNotification>(Notifications);
            var notificationIdList = notificationIds.ToList();
            var notificationsToDelete = await userNotificationsCollection
                .FindAsync(n => notificationIdList.Contains(n.Id));

            foreach (var notification in notificationsToDelete)
                await userNotificationsCollection.DeleteAsync(notification.Id);
        }
        public async Task SetNotificationExpiredAsync(Guid id)
        {
            Open();
            var userNotificationsCollection = _database.GetCollection<UserNotification>(Notifications);
            var notification = await userNotificationsCollection
                .FindOneAsync(x => x.Id == id);

            if (notification != null)
            {
                notification.IsExpired = true;
                await userNotificationsCollection.UpdateAsync(notification);
            }
        }
        public async Task SetNotificationsSeenAsync()
        {
            Open();
            var userNotificationsCollection = _database.GetCollection<UserNotification>(Notifications);
            var unseenNotifications = (await userNotificationsCollection
                .FindAsync(n => !n.IsSeen)).ToList();

            if (unseenNotifications.Count == 0) return;

            foreach (var notification in unseenNotifications)
                notification.IsSeen = true;

            // Single batch update instead of N individual UpdateAsync calls
            await userNotificationsCollection.UpdateAsync(unseenNotifications);
        }
        public async Task DeleteStorage()
        {
            Open();
            var collectionNames = await _database.GetCollectionNamesAsync();
            foreach (var collectionName in collectionNames)
                await _database.DropCollectionAsync(collectionName);

        }
        public async Task ClearDataAsync()
        {
            Open();
            var collectionNames = await _database.GetCollectionNamesAsync();
            foreach (var collectionName in collectionNames)
                await _database.DropCollectionAsync(collectionName);

        }
        public async Task<UserNotification> GetNotificationAsync(Expression<Func<UserNotification, bool>> predicate)
        {
            Open();
            var userNotificationsCollection = _database.GetCollection<UserNotification>(Notifications);
            return await userNotificationsCollection.FindOneAsync(predicate);
        }
        public async Task<IEnumerable<UserNotification>> GetNotificationsAsync()
        {
            Open();
            var userNotificationsCollection = _database.GetCollection<UserNotification>(Notifications);
            return await userNotificationsCollection.FindAllAsync();
        }
        public async Task AddNotificationAsync(UserNotification userNotification)
        {
            Open();
            var userNotificationsCollection = _database.GetCollection<UserNotification>(Notifications);
            await userNotificationsCollection.InsertAsync(userNotification);
        }

        public void Dispose() => Close();

        public async Task AddEventLogsAsync(IEnumerable<EventLog> eventLogs)
        {
            Open();
            var eventLogsCollection = _database.GetCollection<EventLog>(EventLogs);
            await eventLogsCollection.InsertAsync(eventLogs);
        }
        public async Task DeleteEventLogsAsync()
        {
            Open();
            var eventLogsCollection = _database.GetCollection<EventLog>(EventLogs);
            await eventLogsCollection.DeleteAllAsync();
        }
        public async Task<IEnumerable<EventLog>> GetEventLogsAsync()
        {
            Open();
            var col = _database.GetCollection<EventLog>(EventLogs);
            try
         {
      return await col.FindAllAsync();
     }
       catch (Exception ex) when (ex is InvalidCastException || ex is LiteException)
 {
         // The collection contains documents written by an older schema version.
      // Drop it so the next write starts clean — this is the safety net
// in case the migration in OpenCore didn't run (e.g. the DB was already
            // open when the preference was updated on another thread).
  _logger.LogWarning(ex, "[SqliteStorage] EventLog schema mismatch — dropping stale collection");
    try { _database.UnderlyingDatabase.DropCollection(EventLogs); } catch { }
    return [];
         }
    }

        // ══════════════════════════════════════════════════════════════════
        // LoginItem
        // ══════════════════════════════════════════════════════════════════
        public async Task<IEnumerable<LoginItem>> GetLoginItemsAsync(Expression<Func<LoginItem, bool>> predicate = null)
        {
            Open();
            var col = _database.GetCollection<LoginItem>(LoginItems);
            return predicate is null
           ? await col.FindAllAsync()
         : await col.FindAsync(predicate);
        }

        public async Task<LoginItem> GetLoginItemAsync(Expression<Func<LoginItem, bool>> predicate)
        {
            Open();
            return await _database.GetCollection<LoginItem>(LoginItems).FindOneAsync(predicate);
        }

        public async Task<int> GetLoginItemsCountAsync()
        {
            Open();
            return await _database.GetCollection<LoginItem>(LoginItems).CountAsync();
        }

        public async Task SaveLoginItemAsync(LoginItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database.GetCollection<LoginItem>(LoginItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null)
                await col.InsertAsync(item);
            else
                await col.UpdateAsync(item);
        }

        public async Task DeleteLoginItemAsync(Guid id)
        {
            Open();
            await _database.GetCollection<LoginItem>(LoginItems).DeleteManyAsync(x => x.Id == id);
        }
        public async Task DeleteLoginItemsAsync(IEnumerable<Guid> ids)
        {
            Open();
            await _database.GetCollection<LoginItem>(LoginItems).DeleteManyAsync(x =>  ids.Contains(x.Id));
        }

        // ══════════════════════════════════════════════════════════════════
        // CreditCardItem
        // ══════════════════════════════════════════════════════════════════
        public async Task<IEnumerable<CreditCardItem>> GetCreditCardItemsAsync()
        {
            Open();
            return await _database.GetCollection<CreditCardItem>(CreditCardItems).FindAllAsync();
        }

        public async Task<CreditCardItem> GetCreditCardItemAsync(Guid id)
        {
            Open();
            return await _database.GetCollection<CreditCardItem>(CreditCardItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<int> GetCreditCardItemsCountAsync()
        {
            Open();
            return await _database.GetCollection<CreditCardItem>(CreditCardItems).CountAsync();
        }

        public async Task SaveCreditCardItemAsync(CreditCardItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database.GetCollection<CreditCardItem>(CreditCardItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null)
                await col.InsertAsync(item);
            else
                await col.UpdateAsync(item);
        }

        public async Task DeleteCreditCardItemAsync(Guid id)
        {
            Open();
            await _database.GetCollection<CreditCardItem>(CreditCardItems).DeleteManyAsync(x => x.Id == id);
        }

        // ══════════════════════════════════════════════════════════════════
        // IdentityItem
        // ══════════════════════════════════════════════════════════════════
        public async Task<IEnumerable<IdentityItem>> GetIdentityItemsAsync()
        {
            Open();
            return await _database.GetCollection<IdentityItem>(IdentityItems).FindAllAsync();
        }

        public async Task<IdentityItem> GetIdentityItemAsync(Guid id)
        {
            Open();
            return await _database.GetCollection<IdentityItem>(IdentityItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<int> GetIdentityItemsCountAsync()
        {
            Open();
            return await _database.GetCollection<IdentityItem>(IdentityItems).CountAsync();
        }

        public async Task SaveIdentityItemAsync(IdentityItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database.GetCollection<IdentityItem>(IdentityItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null)
                await col.InsertAsync(item);
            else
                await col.UpdateAsync(item);
        }

        public async Task DeleteIdentityItemAsync(Guid id)
        {
            Open();
            await _database.GetCollection<IdentityItem>(IdentityItems).DeleteManyAsync(x => x.Id == id);
        }

        // ══════════════════════════════════════════════════════════════════
        // SecureNoteItem
        // ══════════════════════════════════════════════════════════════════
        public async Task<IEnumerable<SecureNoteItem>> GetSecureNoteItemsAsync()
        {
            Open();
            return await _database.GetCollection<SecureNoteItem>(SecureNoteItems).FindAllAsync();
        }

        public async Task<SecureNoteItem> GetSecureNoteItemAsync(Guid id)
        {
            Open();
            return await _database.GetCollection<SecureNoteItem>(SecureNoteItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<int> GetSecureNoteItemsCountAsync()
        {
            Open();
            return await _database.GetCollection<SecureNoteItem>(SecureNoteItems).CountAsync();
        }

        public async Task SaveSecureNoteItemAsync(SecureNoteItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database.GetCollection<SecureNoteItem>(SecureNoteItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null)
                await col.InsertAsync(item);
            else
                await col.UpdateAsync(item);
        }

        public async Task DeleteSecureNoteItemAsync(Guid id)
        {
            Open();
            await _database.GetCollection<SecureNoteItem>(SecureNoteItems).DeleteManyAsync(x => x.Id == id);
        }

        // ══════════════════════════════════════════════════════════════════
        // VaultGroup
        // ══════════════════════════════════════════════════════════════════
        public async Task<IEnumerable<VaultGroup>> GetVaultGroupsAsync()
        {
            Open();
            return await _database.GetCollection<VaultGroup>(VaultGroups).FindAllAsync();
        }

        public async Task SaveVaultGroupAsync(VaultGroup group)
        {
            Open();
            var col = _database.GetCollection<VaultGroup>(VaultGroups);
            var existing = await col.FindOneAsync(x => x.Id == group.Id);
            if (existing is null)
                await col.InsertAsync(group);
            else
                await col.UpdateAsync(group);
        }

        public async Task DeleteVaultGroupAsync(Guid id)
        {
            Open();
            // Remove the group document
            var col = _database.GetCollection<VaultGroup>(VaultGroups);
            var item = await col.FindOneAsync(x => x.Id == id);
            if (item is not null)
                await col.DeleteAsync(item.Id);

            // Also wipe all membership rows that reference this group
            var memCol = _database.GetCollection<VaultGroupMembership>(GroupMemberships);
            var memberships = await memCol.FindAsync(m => m.GroupId == id);
            foreach (var m in memberships)
                await memCol.DeleteAsync(m.Id);
        }

        public async Task<IEnumerable<Guid>> GetCredentialIdsInGroupAsync(Guid groupId)
        {
            Open();
            var memCol = _database.GetCollection<VaultGroupMembership>(GroupMemberships);
            var rows = await memCol.FindAsync(m => m.GroupId == groupId);
            return rows.Select(m => m.CredentialId).ToList();
        }

        public async Task SetCredentialGroupAsync(Guid groupId, IEnumerable<Guid> credentialIds)
        {
            Open();
            var memCol = _database.GetCollection<VaultGroupMembership>(GroupMemberships);

            // Wipe current members for this group
            var existing = await memCol.FindAsync(m => m.GroupId == groupId);
            foreach (var m in existing)
                await memCol.DeleteAsync(m.Id);

            // Insert new membership rows
            var newRows = credentialIds
  .Select(cid => new VaultGroupMembership { GroupId = groupId, CredentialId = cid })
       .ToList();
            if (newRows.Count > 0)
                await memCol.InsertBulkAsync(newRows);
        }

        public async Task<Guid?> GetGroupForCredentialAsync(Guid credentialId)
        {
            Open();
            var memCol = _database.GetCollection<VaultGroupMembership>(GroupMemberships);
            var row = await memCol.FindOneAsync(m => m.CredentialId == credentialId);
            return row?.GroupId;
        }

        // ══════════════════════════════════════════════════════════════════
        // ImportAuditRecord  (non-sensitive aggregate metadata only)
        // ══════════════════════════════════════════════════════════════════
        private const string ImportAudits = "importAudits";
        private const string HealthSnapshots = "vaultHealthSnapshots";

        public async Task SaveImportAuditAsync(ImportAuditRecord record)
        {
            Open();
            var col = _database.GetCollection<ImportAuditRecord>(ImportAudits);
            await col.InsertAsync(record);
        }

        public async Task<IEnumerable<ImportAuditRecord>> GetImportAuditsAsync()
        {
            Open();
            var col = _database.GetCollection<ImportAuditRecord>(ImportAudits);
            return await col.FindAllAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        // VaultHealthSnapshot  (trending)
        // ══════════════════════════════════════════════════════════════════
        public async Task SaveHealthSnapshotAsync(VaultHealthSnapshot snapshot)
        {
            Open();
            // Zero out the time component so we store one record per calendar day
            snapshot.RecordedDate = snapshot.RecordedDate.Date;
            var col = _database.GetCollection<VaultHealthSnapshot>(HealthSnapshots);
            // Upsert: one snapshot per day
            var existing = await col.FindOneAsync(s => s.RecordedDate == snapshot.RecordedDate);
            if (existing is null)
                await col.InsertAsync(snapshot);
            else
            {
                snapshot.Id = existing.Id;
                await col.UpdateAsync(snapshot);
            }
        }

        public async Task<IReadOnlyList<VaultHealthSnapshot>> GetHealthHistoryAsync(int days = 30)
        {
            Open();
            var cutoff = DateTime.UtcNow.Date.AddDays(-days);
            var col = _database.GetCollection<VaultHealthSnapshot>(HealthSnapshots);
            var rows = await col.FindAsync(s => s.RecordedDate >= cutoff);
            return rows.OrderBy(s => s.RecordedDate).ToList().AsReadOnly();
        }

        // ══════════════════════════════════════════════════════════════════
        // PasskeyItem
        // ══════════════════════════════════════════════════════════════════
        public async Task<IEnumerable<PasskeyItem>> GetPasskeyItemsAsync()
        {
            Open();
            return await _database.GetCollection<PasskeyItem>(PasskeyItems).FindAllAsync();
        }

        public async Task<PasskeyItem?> GetPasskeyItemAsync(Guid id)
        {
            Open();
            return await _database.GetCollection<PasskeyItem>(PasskeyItems).FindOneAsync(x => x.Id == id);
        }

        public async Task<IEnumerable<PasskeyItem>> GetPasskeyItemsByRpIdAsync(string rpId)
        {
            Open();
            return await _database.GetCollection<PasskeyItem>(PasskeyItems).FindAsync(x => x.RpId == rpId);
        }

        public async Task SavePasskeyItemAsync(PasskeyItem item)
        {
            Open();
            item.UpdatedAt = DateTime.UtcNow;
            var col = _database.GetCollection<PasskeyItem>(PasskeyItems);
            var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null)
                await col.InsertAsync(item);
            else
                await col.UpdateAsync(item);
        }

        public async Task DeletePasskeyItemAsync(Guid id)
        {
            Open();
            await _database.GetCollection<PasskeyItem>(PasskeyItems).DeleteManyAsync(x => x.Id == id);
        }

        public async Task IncrementPasskeySignCountAsync(Guid id)
        {
            Open();
            var col = _database.GetCollection<PasskeyItem>(PasskeyItems);
            var item = await col.FindOneAsync(x => x.Id == id);
            if (item is null) return;
            item.SignCount++;
            item.UpdatedAt = DateTime.UtcNow;
            await col.UpdateAsync(item);
        }

        // ══════════════════════════════════════════════════════════════════
        // SecureItem  (unified: ID card, Passport, Driver's License, SSN,
        //              Tax Number, Wi-Fi, SSH)
        // ══════════════════════════════════════════════════════════════════
     public async Task<IEnumerable<SecureItem>> GetSecureItemsAsync()
        {
    Open();
            return await _database.GetCollection<SecureItem>(SecureItems).FindAllAsync();
        }

        public async Task<SecureItem?> GetSecureItemAsync(Guid id)
        {
            Open();
            return await _database.GetCollection<SecureItem>(SecureItems).FindOneAsync(x => x.Id == id);
        }

  public async Task<int> GetSecureItemsCountAsync()
     {
     Open();
    return await _database.GetCollection<SecureItem>(SecureItems).CountAsync();
    }

        public async Task SaveSecureItemAsync(SecureItem item)
        {
         Open();
            item.UpdatedAt = DateTime.UtcNow;
  var col = _database.GetCollection<SecureItem>(SecureItems);
       var existing = await col.FindOneAsync(x => x.Id == item.Id);
            if (existing is null)
    await col.InsertAsync(item);
   else
     await col.UpdateAsync(item);
 }

        public async Task DeleteSecureItemAsync(Guid id)
        {
          Open();
   await _database.GetCollection<SecureItem>(SecureItems).DeleteManyAsync(x => x.Id == id);
        }

    } // end class SqliteStorage
} // end namespace
