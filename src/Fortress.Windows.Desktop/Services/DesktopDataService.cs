using Fortress.Core.Contracts;
using Fortress.Core.Models;
using System.Text.Json;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// Pipe-backed implementation of <see cref="IDesktopDataService"/>.
    ///
    /// The desktop app is NOT a peer of the database � it is a client of
    /// Fortress.Service, exactly like the browser extension.  All vault reads
    /// and writes go through the named pipe using the same IPC contract that
    /// the extension uses.  The service owns the LiteDB file and is the only
    /// process that ever opens it.
    ///
    /// Crypto is performed exclusively inside Fortress.Service; this class
    /// passes plaintext to the service (over the local named pipe) and
    /// receives plaintext back.  The pipe itself never leaves the local machine.
    /// </summary>
public sealed class PipeBackedDataService : IDesktopDataService
    {
 private readonly PipeClient _pipe;
 private readonly IDesktopSessionStore _session;

        private static readonly JsonSerializerOptions _json = new()
        {
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public PipeBackedDataService(PipeClient pipe, IDesktopSessionStore session)
      {
       _pipe    = pipe;
 _session = session;
        }

        // ── Token helper ──────────────────────────────────────────────────────
        private string Token => _session.SessionToken ?? string.Empty;

     private void ThrowIfNoToken()
        {
    if (string.IsNullOrEmpty(_session.SessionToken))
         throw new InvalidOperationException(
     "The vault is locked. Unlock Fortress before accessing vault data.");
 }

        // ── Inner payload unwrappers ──────────────────────────────────────────
        private static T UnwrapList<T>(string payload, string key) where T : new()
        {
        using var doc = JsonDocument.Parse(payload);
          if (doc.RootElement.TryGetProperty(key, out var el))
          return el.Deserialize<T>(_json) ?? new T();
            return new T();
     }

        // ── Login items ───────────────────────────────────────────────────────
      public async Task<List<LoginItem>> GetLoginItemsAsync()
     {
            var (r, _) = await _pipe.CallAsync<JsonElement>(
           "GetLoginItems", new { sessionToken = Token });
     if (r is { } el && el.TryGetProperty("items", out var items))
    return items.Deserialize<List<LoginItem>>(_json) ?? new();
            return new();
    }

        public async Task SaveLoginItemAsync(LoginItem item)
      {
            ThrowIfNoToken();
            var (_, err) = await _pipe.CallAsync(
        item.Id == Guid.Empty ? "SaveLoginItem" : "UpdateLogin",
     item.Id == Guid.Empty
            ? (object)new SaveLoginItemRequest
            {
             SessionToken         = Token,
          Item     = item,
   EncryptSensitiveFields = true,
            }
   : new UpdateLoginRequest
               {
SessionToken = Token,
      Id= item.Id,
    Label        = item.Label,
        Url          = item.Url,
       Username   = item.Username,
     Password     = item.Password,
   TotpSecret   = item.OtpSecret,
               Notes    = item.Notes,
               IsFavorite   = item.IsFavorite,
         });
    if (err is not null) throw new InvalidOperationException(err);
        }

        public async Task DeleteLoginItemAsync(Guid id)
        {
     ThrowIfNoToken();
            var (_, err) = await _pipe.CallAsync(
     "DeleteLoginItem", new DeleteItemRequest { SessionToken = Token, Id = id });
 if (err is not null) throw new InvalidOperationException(err);
  }

   // ── Credit cards ──────────────────────────────────────────────────────
        public async Task<List<CreditCardItem>> GetCreditCardsAsync()
        {
            var (r, _) = await _pipe.CallAsync<JsonElement>(
  "GetCreditCards", new { sessionToken = Token });
   if (r is { } el && el.TryGetProperty("items", out var items))
     return items.Deserialize<List<CreditCardItem>>(_json) ?? new();
      return new();
        }

        public async Task SaveCreditCardAsync(CreditCardItem item)
    {
    ThrowIfNoToken();
  string expiry = string.IsNullOrEmpty(item.ExpiryMonth) && string.IsNullOrEmpty(item.ExpiryYear)
       ? string.Empty
                : $"{item.ExpiryMonth}/{item.ExpiryYear}";

       var (_, err) = await _pipe.CallAsync(
  item.Id == Guid.Empty ? "SaveCreditCard" : "UpdateCard",
       item.Id == Guid.Empty
   ? (object)new SaveCreditCardRequest
          {
        SessionToken   = Token,
          Label          = item.Label,
        CardholderName = item.CardholderName,
               Number         = item.Number,
   Expiry         = expiry,
   Cvv        = item.Cvv,
            IsFavorite  = item.IsFavorite,
 Notes          = item.Notes ?? string.Empty,
          }
      : new UpdateCreditCardRequest
            {
              SessionToken   = Token,
               Id   = item.Id,
       Label          = item.Label,
 CardholderName = item.CardholderName,
   Number         = item.Number,
    Expiry         = expiry,
          Cvv            = item.Cvv,
    IsFavorite     = item.IsFavorite,
        Notes       = item.Notes,
       });
            if (err is not null) throw new InvalidOperationException(err);
     }

   public async Task DeleteCreditCardAsync(Guid id)
        {
          ThrowIfNoToken();
            var (_, err) = await _pipe.CallAsync(
                "DeleteCreditCard", new DeleteItemRequest { SessionToken = Token, Id = id });
        if (err is not null) throw new InvalidOperationException(err);
   }

        // ── Identities ────────────────────────────────────────────────────────
        public async Task<List<IdentityItem>> GetIdentitiesAsync()
        {
      var (r, _) = await _pipe.CallAsync<JsonElement>(
        "GetIdentities", new { sessionToken = Token });
            if (r is { } el && el.TryGetProperty("items", out var items))
      return items.Deserialize<List<IdentityItem>>(_json) ?? new();
      return new();
        }

        public async Task SaveIdentityAsync(IdentityItem item)
      {
            ThrowIfNoToken();
          var (_, err) = await _pipe.CallAsync(
             item.Id == Guid.Empty ? "SaveIdentity" : "UpdateIdentity",
   item.Id == Guid.Empty
           ? (object)new SaveIdentityRequest
         {
                SessionToken = Token,
                Label        = item.Label,
                FirstName    = item.FirstName,
                LastName     = item.LastName,
                Email        = item.Email,
                Phone        = item.Phone,
                Company      = item.Company,
                AddressLine1 = item.AddressLine1,
                AddressLine2 = item.AddressLine2,
                City         = item.City,
                State        = item.State,
                Country      = item.Country,
                PostalCode   = item.PostalCode,
                IsFavorite   = item.IsFavorite,
                Notes        = item.Notes ?? string.Empty,
            }
            : new UpdateIdentityRequest
            {
                SessionToken = Token,
                Id           = item.Id,
                Label        = item.Label,
                FirstName    = item.FirstName,
                LastName     = item.LastName,
                Email        = item.Email,
                Phone        = item.Phone,
                Company      = item.Company,
                AddressLine1 = item.AddressLine1,
                AddressLine2 = item.AddressLine2,
                City         = item.City,
                State        = item.State,
                Country      = item.Country,
                PostalCode   = item.PostalCode,
                IsFavorite   = item.IsFavorite,
                Notes        = item.Notes,
            });
     if (err is not null) throw new InvalidOperationException(err);
    }

        public async Task DeleteIdentityAsync(Guid id)
        {
            ThrowIfNoToken();
  var (_, err) = await _pipe.CallAsync(
  "DeleteIdentity", new DeleteItemRequest { SessionToken = Token, Id = id });
        if (err is not null) throw new InvalidOperationException(err);
        }

        // ── Secure items ──────────────────────────────────────────────────────
        public async Task<List<SecureItem>> GetSecureItemsAsync()
    {
            var (r, _) = await _pipe.CallAsync<JsonElement>(
          "GetSecureItems", new { sessionToken = Token });
         if (r is { } el && el.TryGetProperty("items", out var items))
 return items.Deserialize<List<SecureItem>>(_json) ?? new();
return new();
        }

        public async Task SaveSecureItemAsync(SecureItem item)
   {
          ThrowIfNoToken();
       var (_, err) = await _pipe.CallAsync(
         item.Id == Guid.Empty ? "SaveSecureItem" : "UpdateSecureItem",
 item.Id == Guid.Empty
     ? (object)new SaveSecureItemRequest
     {
    SessionToken   = Token,
    ItemType       = item.ItemType.ToString(),
        Label          = item.Label,
  IsFavorite     = item.IsFavorite,
                  Notes        = item.Notes ?? string.Empty,
    FullName       = item.FullName ?? string.Empty,
 DateOfBirth    = item.DateOfBirth ?? string.Empty,
         Nationality    = item.Nationality ?? string.Empty,
          Number         = item.Number ?? string.Empty,
            IssuingCountry = item.IssuingCountry ?? string.Empty,
      IssuedDate     = item.IssuedDate ?? string.Empty,
             ExpiryDate     = item.ExpiryDate ?? string.Empty,
  FirstName      = item.FirstName ?? string.Empty,
         LastName       = item.LastName ?? string.Empty,
           Email          = item.Email ?? string.Empty,
  Phone = item.Phone ?? string.Empty,
  Company        = item.Company ?? string.Empty,
      AddressLine1 = item.AddressLine1 ?? string.Empty,
            AddressLine2   = item.AddressLine2 ?? string.Empty,
       City           = item.City ?? string.Empty,
   State          = item.State ?? string.Empty,
   Country        = item.Country ?? string.Empty,
       PostalCode     = item.PostalCode ?? string.Empty,
               Ssid           = item.Ssid ?? string.Empty,
 WifiSecurity = item.WifiSecurity ?? string.Empty,
                  Password       = item.Password ?? string.Empty,
       Host     = item.Host ?? string.Empty,
                 Port           = item.Port ?? string.Empty,
 Username = item.Username ?? string.Empty,
     SshPassword    = item.SshPassword ?? string.Empty,
     PrivateKey     = item.PrivateKey ?? string.Empty,
  KeyFingerprint = item.KeyFingerprint ?? string.Empty,
 NoteContent    = item.NoteContent ?? string.Empty,
         }
         : new UpdateSecureItemRequest
 {
            SessionToken   = Token,
   Id   = item.Id,
            Label       = item.Label,
       IsFavorite   = item.IsFavorite,
          Notes    = item.Notes,
             FullName       = item.FullName,
     DateOfBirth    = item.DateOfBirth,
        Nationality    = item.Nationality,
 Number         = item.Number,
               IssuingCountry = item.IssuingCountry,
         IssuedDate     = item.IssuedDate,
      ExpiryDate   = item.ExpiryDate,
FirstName      = item.FirstName,
      LastName   = item.LastName,
        Email          = item.Email,
               Phone          = item.Phone,
      Company  = item.Company,
    AddressLine1   = item.AddressLine1,
            AddressLine2   = item.AddressLine2,
     City  = item.City,
                 State          = item.State,
          Country        = item.Country,
          PostalCode     = item.PostalCode,
        Ssid           = item.Ssid,
WifiSecurity   = item.WifiSecurity,
 Password       = item.Password,
                 Host       = item.Host,
            Port     = item.Port,
    Username       = item.Username,
       SshPassword    = item.SshPassword,
       PrivateKey     = item.PrivateKey,
KeyFingerprint = item.KeyFingerprint,
        NoteContent    = item.NoteContent,
      });
        if (err is not null) throw new InvalidOperationException(err);
        }

        public async Task DeleteSecureItemAsync(Guid id)
     {
            ThrowIfNoToken();
     var (_, err) = await _pipe.CallAsync(
  "DeleteSecureItem", new DeleteItemRequest { SessionToken = Token, Id = id });
            if (err is not null) throw new InvalidOperationException(err);
        }

        // ── Authenticators ────────────────────────────────────────────────────
        public async Task<List<Authenticator>> GetAuthenticatorsAsync()
{
       var (r, _) = await _pipe.CallAsync<JsonElement>(
         "GetAuthenticators", new { sessionToken = Token });
 if (r is { } el && el.TryGetProperty("items", out var items))
           return items.Deserialize<List<Authenticator>>(_json) ?? new();
   return new();
        }

        public async Task SaveAuthenticatorAsync(Authenticator item)
        {
          ThrowIfNoToken();
  var (_, err) = await _pipe.CallAsync(
      item.Id == Guid.Empty ? "SaveAuthenticator" : "UpdateAuthenticator",
    item.Id == Guid.Empty
        ? (object)new SaveAuthenticatorRequest
        {
    SessionToken = Token,
Label        = item.Issuer,
   Username     = item.Username,
      Secret       = item.Secret,
              AuthType     = item.Type.ToString().ToUpperInvariant(),
         Digits       = item.Digits,
          Period       = item.Period,
            Algorithm    = item.Algorithm.ToString().ToUpperInvariant(),
   IsFavorite   = item.IsFavorite,
                }
        : new UpdateAuthenticatorRequest
       {
        SessionToken = Token,
        Id        = item.Id,
         Label        = item.Issuer,
 Username     = item.Username,
      Secret    = item.Secret,
          AuthType     = item.Type.ToString().ToUpperInvariant(),
                Digits   = item.Digits,
   Period       = item.Period,
        Algorithm    = item.Algorithm.ToString().ToUpperInvariant(),
     IsFavorite   = item.IsFavorite,
            });
   if (err is not null) throw new InvalidOperationException(err);
}

        public async Task DeleteAuthenticatorAsync(Guid id)
     {
            ThrowIfNoToken();
            var (_, err) = await _pipe.CallAsync(
       "DeleteAuthenticator", new DeleteItemRequest { SessionToken = Token, Id = id });
            if (err is not null) throw new InvalidOperationException(err);
        }

        // ── Passkeys ──────────────────────────────────────────────────────────
     public async Task<List<PasskeyItem>> GetPasskeysAsync()
      {
      // Service does not expose a GetPasskeys method yet � return empty.
    await Task.CompletedTask;
            return new();
      }

     // ── Groups ────────────────────────────────────────────────────────────
        public async Task<List<VaultGroup>> GetGroupsAsync()
        {
   // Groups are not yet exposed over the pipe � desktop manages them locally.
            await Task.CompletedTask;
       return new();
        }

  public async Task SaveGroupAsync(VaultGroup group)
        {
       await Task.CompletedTask;
        }

    public async Task DeleteGroupAsync(Guid id)
    {
    await Task.CompletedTask;
        }

     public async Task<HashSet<Guid>> GetGroupMemberIdsAsync(Guid groupId)
        {
            await Task.CompletedTask;
   return new();
        }

   // ── Vault health ──────────────────────────────────────────────────────
        public async Task<VaultHealthResult> GetVaultHealthAsync()
        {
// Compute health locally from login data already fetched.
var logins = await GetLoginItemsAsync();
        var auths  = await GetAuthenticatorsAsync();

    // Minimal local calculation � reuse the core calculator if available,
      // otherwise return a zero-score placeholder so the UI is never null.
   return new VaultHealthResult
  {
            Score   = 0,
     Status   = VaultHealthStatus.AtRisk,
TotalCredentials  = logins.Count,
    TotalAuthenticators = auths.Count,
         CalculatedAt      = DateTime.UtcNow,
            };
        }

        // ── Event logs ────────────────────────────────────────────────────────
        public async Task<List<EventLog>> GetEventLogsAsync()
        {
  // Event logs are not yet exposed over the pipe.
          await Task.CompletedTask;
        return new();
        }

  public async Task AddEventLogAsync(EventLog entry)
     {
    await Task.CompletedTask;
        }

        // ── Notifications ─────────────────────────────────────────────────────
        public async Task<List<UserNotification>> GetNotificationsAsync()
        {
    await Task.CompletedTask;
     return new();
     }

     public async Task AddNotificationAsync(UserNotification notification)
    {
            await Task.CompletedTask;
        }

        public async Task MarkNotificationsSeenAsync()
        {
  await Task.CompletedTask;
 }

        public async Task DeleteNotificationsAsync(IEnumerable<Guid> ids)
        {
     await Task.CompletedTask;
        }
    }
}
