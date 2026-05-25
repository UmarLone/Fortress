using Fortress.Core.Contracts;
using Fortress.Core.Models;
using Fortress.Core.Services;
using Microsoft.Extensions.Logging;

namespace Fortress.Service.Infrastructure;

/// <summary>
/// Handles all vault CRUD IPC methods:
///   Logins, Authenticators, Credit Cards, Secure Items, Identities, Secure Notes.
///
/// Every mutating method validates the session token before touching storage.
/// Sensitive fields are encrypted by this handler before persisting.
/// For update operations, null / empty-string sensitive fields mean "keep existing
/// encrypted value" — the field is not re-encrypted.
/// </summary>
internal sealed class VaultItemHandler
{
    private readonly IDataStorageService _storage;
    private readonly VaultSessionService _session;
    private readonly ICryptographyService _crypto;
    private readonly ILogger<VaultItemHandler> _logger;

    public VaultItemHandler(
        IDataStorageService storage,
        VaultSessionService session,
        ICryptographyService crypto,
        ILogger<VaultItemHandler> logger)
    {
        _storage = storage;
        _session = session;
        _crypto  = crypto;
        _logger  = logger;
    }

    // ── Auth guard ────────────────────────────────────────────────────────────
    private bool Authorized(string token) => _session.ValidateToken(token);

    // ── Enum helpers ──────────────────────────────────────────────────────────
    private static AuthenticatorType ParseAuthType(string? value) =>
        (value?.ToUpperInvariant()) switch
        {
            "HOTP"              => AuthenticatorType.Hotp,
            "MOTP" or "M-OTP"  => AuthenticatorType.MobileOtp,
            "STEAM" or "STEAMOTP" => AuthenticatorType.SteamOtp,
            _                   => AuthenticatorType.Totp,  // default: TOTP
        };

    private static HashAlgorithmType ParseAlgorithm(string? value) =>
        (value?.ToUpperInvariant()) switch
        {
            "SHA256" => HashAlgorithmType.Sha256,
            "SHA512" => HashAlgorithmType.Sha512,
            _        => HashAlgorithmType.Sha1,  // default: SHA1
        };

    private static (string month, string year) SplitExpiry(string expiry)
    {
        var parts = expiry.Split('/');
        if (parts.Length == 2)
            return (parts[0].Trim(), parts[1].Trim());
        return (string.Empty, string.Empty);
    }

    // ── Login items ───────────────────────────────────────────────────────────
    public async Task<object> GetLoginItemsAsync(GetLoginItemsRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var items = await _storage.GetLoginItemsAsync();
        _logger.LogDebug("[VaultItems] GetLoginItems → {Count} items", items.Count());
        return new { items };
    }

    public async Task<object> SaveLoginItemAsync(SaveLoginItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        if (req.EncryptSensitiveFields)
        {
            req.Item.Password  = (await _crypto.Encrypt(req.Item.Password)).Data  ?? req.Item.Password;
            if (req.Item.OtpSecret is not null)
                req.Item.OtpSecret = (await _crypto.Encrypt(req.Item.OtpSecret)).Data ?? req.Item.OtpSecret;
        }

        await _storage.SaveLoginItemAsync(req.Item);
        _logger.LogInformation("[VaultItems] SaveLoginItem  id={Id} label={Label}", req.Item.Id, req.Item.Label);
        return new { saved = true, id = req.Item.Id };
    }

    public async Task<object> DeleteLoginItemAsync(DeleteItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        await _storage.DeleteLoginItemAsync(req.Id);
        _logger.LogInformation("[VaultItems] DeleteLoginItem id={Id}", req.Id);
        return new { deleted = true, id = req.Id };
    }

    public async Task<object> UpdateLoginAsync(UpdateLoginRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = await _storage.GetLoginItemAsync(x => x.Id == req.Id);
        if (item is null) return Error($"Login {req.Id} not found.");

        if (req.Label    is not null) item.Label    = req.Label;
        if (req.Url      is not null) item.Url      = req.Url;
        if (req.Username is not null) item.Username = req.Username;
        if (req.Notes    is not null) item.Notes    = req.Notes;
        if (req.IsFavorite.HasValue)  item.IsFavorite = req.IsFavorite.Value;

        if (!string.IsNullOrEmpty(req.Password))
            item.Password = (await _crypto.Encrypt(req.Password)).Data ?? item.Password;

        if (!string.IsNullOrEmpty(req.TotpSecret))
            item.OtpSecret = (await _crypto.Encrypt(req.TotpSecret)).Data ?? item.OtpSecret;
        else if (req.TotpSecret == string.Empty)
            item.OtpSecret = null;  // explicit clear

        item.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveLoginItemAsync(item);
        _logger.LogInformation("[VaultItems] UpdateLogin    id={Id} label={Label}", item.Id, item.Label);
        return new { saved = true, id = item.Id };
    }

    // ── Authenticators ────────────────────────────────────────────────────────
    public async Task<object> GetAuthenticatorsAsync(GetAuthenticatorsRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var items = await _storage.GetAuthenticatorsAsync();
        _logger.LogDebug("[VaultItems] GetAuthenticators → {Count} items", items.Count());
        return new { items };
    }

    public async Task<object> SaveAuthenticatorAsync(SaveAuthenticatorRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = new Authenticator
        {
            Id         = Guid.NewGuid(),
            Issuer     = req.Label,
            Username   = req.Username,
            Secret     = req.Secret,
            Type       = ParseAuthType(req.AuthType),
            Digits     = req.Digits > 0 ? req.Digits : 6,
            Period     = req.Period > 0 ? req.Period : 30,
            Algorithm  = ParseAlgorithm(req.Algorithm),
            IsFavorite = req.IsFavorite,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };

        await _storage.AddOrUpdateAuthenticatorsAsync(new[] { item });
        _logger.LogInformation("[VaultItems] SaveAuthenticator id={Id} issuer={Issuer}", item.Id, item.Issuer);
        return new { saved = true, id = item.Id };
    }

    public async Task<object> DeleteAuthenticatorAsync(DeleteItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        await _storage.DeleteAuthenticatorAsync(req.Id);
        _logger.LogInformation("[VaultItems] DeleteAuthenticator id={Id}", req.Id);
        return new { deleted = true, id = req.Id };
    }

    public async Task<object> UpdateAuthenticatorAsync(UpdateAuthenticatorRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = await _storage.GetAuthenticatorAsync(x => x.Id == req.Id);
        if (item is null) return Error($"Authenticator {req.Id} not found.");

        if (req.Label    is not null) item.Issuer    = req.Label;
        if (req.Username is not null) item.Username  = req.Username;
        if (req.Secret   is not null) item.Secret    = req.Secret;
        if (req.AuthType is not null) item.Type      = ParseAuthType(req.AuthType);
        if (req.Digits.HasValue)      item.Digits    = req.Digits.Value;
        if (req.Period.HasValue)      item.Period    = req.Period.Value;
        if (req.Algorithm is not null) item.Algorithm = ParseAlgorithm(req.Algorithm);
        if (req.IsFavorite.HasValue)  item.IsFavorite = req.IsFavorite.Value;

        item.UpdatedAt = DateTime.UtcNow;
        await _storage.AddOrUpdateAuthenticatorsAsync(new[] { item });
        _logger.LogInformation("[VaultItems] UpdateAuthenticator id={Id} issuer={Issuer}", item.Id, item.Issuer);
        return new { saved = true, id = item.Id };
    }

    // ── Credit cards ──────────────────────────────────────────────────────────
    public async Task<object> GetCreditCardsAsync(GetCreditCardsRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var raw = (await _storage.GetCreditCardItemsAsync()).ToList();
        _logger.LogDebug("[VaultItems] GetCreditCards → {Count} items", raw.Count);

        // Decrypt sensitive fields before sending to the client
        var items = await Task.WhenAll(raw.Select(async i =>
        {
            var number = (await _crypto.Decrypt(i.Number)).Data ?? i.Number;
            var cvv    = string.IsNullOrEmpty(i.Cvv) ? i.Cvv
                       : (await _crypto.Decrypt(i.Cvv)).Data ?? i.Cvv;
            return new
            {
                i.Id, i.Label, i.CardholderName,
                number,
                cvv,
                i.ExpiryMonth, i.ExpiryYear,
                i.IsFavorite, i.Notes,
                i.CreatedAt, i.UpdatedAt,
            };
        }));

        return new { items };
    }

    public async Task<object> SaveCreditCardAsync(SaveCreditCardRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var (month, year) = SplitExpiry(req.Expiry);
        var item = new CreditCardItem
        {
            Id             = Guid.NewGuid(),
            Label          = req.Label,
            CardholderName = req.CardholderName,
            Number         = (await _crypto.Encrypt(req.Number)).Data ?? req.Number,
            Cvv            = (await _crypto.Encrypt(req.Cvv)).Data    ?? req.Cvv,
            ExpiryMonth    = month,
            ExpiryYear     = year,
            IsFavorite     = req.IsFavorite,
            Notes          = req.Notes,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow,
        };

        await _storage.SaveCreditCardItemAsync(item);
        _logger.LogInformation("[VaultItems] SaveCreditCard  id={Id} label={Label}", item.Id, item.Label);
        return new { saved = true, id = item.Id };
    }

    public async Task<object> DeleteCreditCardAsync(DeleteItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        await _storage.DeleteCreditCardItemAsync(req.Id);
        _logger.LogInformation("[VaultItems] DeleteCreditCard id={Id}", req.Id);
        return new { deleted = true, id = req.Id };
    }

    public async Task<object> UpdateCreditCardAsync(UpdateCreditCardRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = await _storage.GetCreditCardItemAsync(req.Id);
        if (item is null) return Error($"CreditCard {req.Id} not found.");

        if (req.Label          is not null) item.Label          = req.Label;
        if (req.CardholderName is not null) item.CardholderName = req.CardholderName;
        if (req.Notes          is not null) item.Notes          = req.Notes;
        if (req.IsFavorite.HasValue)        item.IsFavorite     = req.IsFavorite.Value;

        if (!string.IsNullOrEmpty(req.Number))
            item.Number = (await _crypto.Encrypt(req.Number)).Data ?? item.Number;

        if (!string.IsNullOrEmpty(req.Cvv))
            item.Cvv = (await _crypto.Encrypt(req.Cvv)).Data ?? item.Cvv;

        if (!string.IsNullOrEmpty(req.Expiry))
        {
            var (month, year) = SplitExpiry(req.Expiry);
            item.ExpiryMonth = month;
            item.ExpiryYear  = year;
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveCreditCardItemAsync(item);
        _logger.LogInformation("[VaultItems] UpdateCreditCard id={Id} label={Label}", item.Id, item.Label);
        return new { saved = true, id = item.Id };
    }

    // ── Secure items ──────────────────────────────────────────────────────────
    public async Task<object> GetSecureItemsAsync(GetSecureItemsRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var raw = (await _storage.GetSecureItemsAsync()).ToList();
        _logger.LogDebug("[VaultItems] GetSecureItems → {Count} items", raw.Count);

        // Decrypt all sensitive fields before sending to the client so edit
        // drawers can pre-populate with plaintext (matches GetCreditCards pattern).
        async Task<string> DecOpt(string? cipher) =>
            string.IsNullOrEmpty(cipher) ? string.Empty
                                         : ((await _crypto.Decrypt(cipher)).Data ?? cipher);

        var items = await Task.WhenAll(raw.Select(async i => new
        {
            i.Id, i.ItemType, i.Label, i.IsFavorite, i.Notes,
            i.CreatedAt, i.UpdatedAt,
            // Document fields
            i.FullName, i.DateOfBirth, i.Nationality,
            number = await DecOpt(i.Number),
            i.IssuingCountry, i.IssuedDate, i.ExpiryDate,
            // Identity fields
            i.FirstName, i.LastName, i.Email, i.Phone, i.Company,
            i.AddressLine1, i.AddressLine2, i.City, i.State, i.Country, i.PostalCode,
            // Wi-Fi fields
            i.Ssid, i.WifiSecurity,
            password = await DecOpt(i.Password),
            // SSH fields
            i.Host, i.Port, i.Username,
            sshPassword = await DecOpt(i.SshPassword),
            privateKey  = await DecOpt(i.PrivateKey),
            i.KeyFingerprint,
            // Secure Note
            noteContent = await DecOpt(i.NoteContent),
        }));

        return new { items };
    }

    public async Task<object> SaveSecureItemAsync(SaveSecureItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        Enum.TryParse<SecureItemType>(req.ItemType, ignoreCase: true, out var itemType);

        var item = new SecureItem
        {
            Id             = Guid.NewGuid(),
            ItemType       = itemType,
            Label          = req.Label,
            IsFavorite     = req.IsFavorite,
            Notes          = req.Notes,

            // Document fields
            FullName       = req.FullName,
            DateOfBirth    = req.DateOfBirth,
            Nationality    = req.Nationality,
            IssuingCountry = req.IssuingCountry,
            IssuedDate     = req.IssuedDate,
            ExpiryDate     = req.ExpiryDate,

            // Identity fields
            FirstName      = req.FirstName,
            LastName       = req.LastName,
            Email          = req.Email,
            Phone          = req.Phone,
            Company        = req.Company,
            AddressLine1   = req.AddressLine1,
            AddressLine2   = req.AddressLine2,
            City           = req.City,
            State          = req.State,
            Country        = req.Country,
            PostalCode     = req.PostalCode,

            // Wi-Fi fields
            Ssid           = req.Ssid,
            WifiSecurity   = req.WifiSecurity,

            // SSH fields
            Host           = req.Host,
            Port           = req.Port,
            Username       = req.Username,
            KeyFingerprint = req.KeyFingerprint,

            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow,
        };

        // Encrypt sensitive fields
        item.Number      = string.IsNullOrEmpty(req.Number)      ? string.Empty : (await _crypto.Encrypt(req.Number)).Data      ?? string.Empty;
        item.Password    = string.IsNullOrEmpty(req.Password)    ? string.Empty : (await _crypto.Encrypt(req.Password)).Data    ?? string.Empty;
        item.SshPassword = string.IsNullOrEmpty(req.SshPassword) ? string.Empty : (await _crypto.Encrypt(req.SshPassword)).Data ?? string.Empty;
        item.PrivateKey  = string.IsNullOrEmpty(req.PrivateKey)  ? string.Empty : (await _crypto.Encrypt(req.PrivateKey)).Data  ?? string.Empty;
        item.NoteContent = string.IsNullOrEmpty(req.NoteContent) ? string.Empty : (await _crypto.Encrypt(req.NoteContent)).Data ?? string.Empty;

        await _storage.SaveSecureItemAsync(item);
        _logger.LogInformation("[VaultItems] SaveSecureItem  id={Id} label={Label} type={Type}", item.Id, item.Label, item.ItemType);
        return new { saved = true, id = item.Id };
    }

    public async Task<object> DeleteSecureItemAsync(DeleteItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        await _storage.DeleteSecureItemAsync(req.Id);
        _logger.LogInformation("[VaultItems] DeleteSecureItem id={Id}", req.Id);
        return new { deleted = true, id = req.Id };
    }

    public async Task<object> UpdateSecureItemAsync(UpdateSecureItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = await _storage.GetSecureItemAsync(req.Id);
        if (item is null) return Error($"SecureItem {req.Id} not found.");

        if (req.Label      is not null) item.Label      = req.Label;
        if (req.Notes      is not null) item.Notes      = req.Notes;
        if (req.IsFavorite.HasValue)    item.IsFavorite = req.IsFavorite.Value;

        // Document fields
        if (req.FullName       is not null) item.FullName       = req.FullName;
        if (req.DateOfBirth    is not null) item.DateOfBirth    = req.DateOfBirth;
        if (req.Nationality    is not null) item.Nationality    = req.Nationality;
        if (req.IssuingCountry is not null) item.IssuingCountry = req.IssuingCountry;
        if (req.IssuedDate     is not null) item.IssuedDate     = req.IssuedDate;
        if (req.ExpiryDate     is not null) item.ExpiryDate     = req.ExpiryDate;

        // Identity fields
        if (req.FirstName   is not null) item.FirstName   = req.FirstName;
        if (req.LastName    is not null) item.LastName    = req.LastName;
        if (req.Email       is not null) item.Email       = req.Email;
        if (req.Phone       is not null) item.Phone       = req.Phone;
        if (req.Company     is not null) item.Company     = req.Company;
        if (req.AddressLine1 is not null) item.AddressLine1 = req.AddressLine1;
        if (req.AddressLine2 is not null) item.AddressLine2 = req.AddressLine2;
        if (req.City        is not null) item.City        = req.City;
        if (req.State       is not null) item.State       = req.State;
        if (req.Country    is not null) item.Country    = req.Country;
        if (req.PostalCode is not null) item.PostalCode = req.PostalCode;

        // Wi-Fi fields
        if (req.Ssid        is not null) item.Ssid        = req.Ssid;
        if (req.WifiSecurity is not null) item.WifiSecurity = req.WifiSecurity;

        // SSH fields
        if (req.Host           is not null) item.Host           = req.Host;
        if (req.Port           is not null) item.Port           = req.Port;
        if (req.Username       is not null) item.Username       = req.Username;
        if (req.KeyFingerprint is not null) item.KeyFingerprint = req.KeyFingerprint;

        // Sensitive fields — only update if a new non-empty value is provided
        if (!string.IsNullOrEmpty(req.Number))
            item.Number = (await _crypto.Encrypt(req.Number)).Data ?? item.Number;

        if (!string.IsNullOrEmpty(req.Password))
            item.Password = (await _crypto.Encrypt(req.Password)).Data ?? item.Password;

        if (!string.IsNullOrEmpty(req.SshPassword))
            item.SshPassword = (await _crypto.Encrypt(req.SshPassword)).Data ?? item.SshPassword;

        if (!string.IsNullOrEmpty(req.PrivateKey))
            item.PrivateKey = (await _crypto.Encrypt(req.PrivateKey)).Data ?? item.PrivateKey;

        if (!string.IsNullOrEmpty(req.NoteContent))
            item.NoteContent = (await _crypto.Encrypt(req.NoteContent)).Data ?? item.NoteContent;

        item.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveSecureItemAsync(item);
        _logger.LogInformation("[VaultItems] UpdateSecureItem id={Id} label={Label} type={Type}", item.Id, item.Label, item.ItemType);
        return new { saved = true, id = item.Id };
    }

    // ── Identities ────────────────────────────────────────────────────────────
    public async Task<object> GetIdentitiesAsync(GetIdentitiesRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var items = await _storage.GetIdentityItemsAsync();
        _logger.LogDebug("[VaultItems] GetIdentities → {Count} items", items.Count());
        return new { items };
    }

    public async Task<object> SaveIdentityAsync(SaveIdentityRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = new IdentityItem
        {
            Id           = Guid.NewGuid(),
            Label        = req.Label,
            FirstName    = req.FirstName,
            LastName     = req.LastName,
            Email        = req.Email,
            Phone        = req.Phone,
            Company      = req.Company,
            AddressLine1 = req.AddressLine1,
            AddressLine2 = req.AddressLine2,
            City         = req.City,
            State        = req.State,
            Country      = req.Country,
            PostalCode   = req.PostalCode,
            IsFavorite   = req.IsFavorite,
            Notes        = req.Notes,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };

        await _storage.SaveIdentityItemAsync(item);
        _logger.LogInformation("[VaultItems] SaveIdentity    id={Id} label={Label}", item.Id, item.Label);
        return new { saved = true, id = item.Id };
    }

    public async Task<object> DeleteIdentityAsync(DeleteItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        await _storage.DeleteIdentityItemAsync(req.Id);
        _logger.LogInformation("[VaultItems] DeleteIdentity  id={Id}", req.Id);
        return new { deleted = true, id = req.Id };
    }

    public async Task<object> UpdateIdentityAsync(UpdateIdentityRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = await _storage.GetIdentityItemAsync(req.Id);
        if (item is null) return Error($"Identity {req.Id} not found.");

        if (req.Label        is not null) item.Label        = req.Label;
        if (req.FirstName    is not null) item.FirstName    = req.FirstName;
        if (req.LastName     is not null) item.LastName     = req.LastName;
        if (req.Email        is not null) item.Email        = req.Email;
        if (req.Phone        is not null) item.Phone        = req.Phone;
        if (req.Company      is not null) item.Company      = req.Company;
        if (req.AddressLine1 is not null) item.AddressLine1 = req.AddressLine1;
        if (req.AddressLine2 is not null) item.AddressLine2 = req.AddressLine2;
        if (req.City         is not null) item.City         = req.City;
        if (req.State        is not null) item.State        = req.State;
        if (req.Country      is not null) item.Country      = req.Country;
        if (req.PostalCode   is not null) item.PostalCode   = req.PostalCode;
        if (req.Notes        is not null) item.Notes        = req.Notes;
        if (req.IsFavorite.HasValue)      item.IsFavorite   = req.IsFavorite.Value;

        item.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveIdentityItemAsync(item);
        _logger.LogInformation("[VaultItems] UpdateIdentity  id={Id} label={Label}", item.Id, item.Label);
        return new { saved = true, id = item.Id };
    }

    // ── Secure Notes ──────────────────────────────────────────────────────────
    public async Task<object> GetSecureNotesAsync(GetSecureNotesRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var raw = (await _storage.GetSecureNoteItemsAsync()).ToList();
        _logger.LogDebug("[VaultItems] GetSecureNotes → {Count} items", raw.Count);

        // Decrypt encrypted Content before sending to the client
        var items = await Task.WhenAll(raw.Select(async i =>
        {
            var content = string.IsNullOrEmpty(i.Content)
                ? string.Empty
                : (await _crypto.Decrypt(i.Content)).Data ?? i.Content;
            return new
            {
                i.Id, i.Label,
                content,
                i.IsFavorite,
                i.CreatedAt, i.UpdatedAt,
            };
        }));

        return new { items };
    }

    public async Task<object> DeleteSecureNoteAsync(DeleteItemRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        await _storage.DeleteSecureNoteItemAsync(req.Id);
        _logger.LogInformation("[VaultItems] DeleteSecureNote id={Id}", req.Id);
        return new { deleted = true, id = req.Id };
    }

    public async Task<object> SaveSecureNoteAsync(SaveSecureNoteRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = new SecureNoteItem
        {
            Id         = Guid.NewGuid(),
            Label      = req.Label,
            Content    = string.IsNullOrEmpty(req.Content)
                ? string.Empty
                : (await _crypto.Encrypt(req.Content)).Data ?? string.Empty,
            IsFavorite = req.IsFavorite,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow,
        };

        await _storage.SaveSecureNoteItemAsync(item);
        _logger.LogInformation("[VaultItems] SaveSecureNote  id={Id} label={Label}", item.Id, item.Label);
        return new { saved = true, id = item.Id };
    }

    public async Task<object> UpdateSecureNoteAsync(UpdateSecureNoteRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();

        var item = await _storage.GetSecureNoteItemAsync(req.Id);
        if (item is null) return Error($"SecureNote {req.Id} not found.");

        if (req.Label      is not null) item.Label      = req.Label;
        if (req.IsFavorite.HasValue)    item.IsFavorite = req.IsFavorite.Value;

        if (!string.IsNullOrEmpty(req.Content))
            item.Content = (await _crypto.Encrypt(req.Content)).Data ?? item.Content;

        item.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveSecureNoteItemAsync(item);
        _logger.LogInformation("[VaultItems] UpdateSecureNote id={Id} label={Label}", item.Id, item.Label);
        return new { saved = true, id = item.Id };
    }

    // ── Generic operations ────────────────────────────────────────────────────
    /// <summary>
    /// Generic delete — probes Login → CreditCard → Identity → Authenticator →
    /// SecureNote collections and removes the first match.
    /// </summary>
    public async Task<object> DeleteItemAsync(GenericDeleteRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var id = req.CredentialId;

        try
        {
            var login = await _storage.GetLoginItemAsync(x => x.Id == id);
            if (login is not null)
            {
                await _storage.DeleteLoginItemAsync(id);
                _logger.LogInformation("[VaultItems] DeleteItem (Login) id={Id}", id);
                return new { deleted = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var card = await _storage.GetCreditCardItemAsync(id);
            if (card is not null)
            {
                await _storage.DeleteCreditCardItemAsync(id);
                _logger.LogInformation("[VaultItems] DeleteItem (CreditCard) id={Id}", id);
                return new { deleted = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var identity = await _storage.GetIdentityItemAsync(id);
            if (identity is not null)
            {
                await _storage.DeleteIdentityItemAsync(id);
                _logger.LogInformation("[VaultItems] DeleteItem (Identity) id={Id}", id);
                return new { deleted = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var auth = await _storage.GetAuthenticatorAsync(x => x.Id == id);
            if (auth is not null)
            {
                await _storage.DeleteAuthenticatorAsync(id);
                _logger.LogInformation("[VaultItems] DeleteItem (Authenticator) id={Id}", id);
                return new { deleted = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var note = await _storage.GetSecureNoteItemAsync(id);
            if (note is not null)
            {
                await _storage.DeleteSecureNoteItemAsync(id);
                _logger.LogInformation("[VaultItems] DeleteItem (SecureNote) id={Id}", id);
                return new { deleted = true, id };
            }
        }
        catch { /* not found */ }

        _logger.LogWarning("[VaultItems] DeleteItem — id={Id} not found in any collection.", id);
        return new { deleted = false, id };
    }

    /// <summary>
    /// Toggles the IsFavorite flag on any vault item type.
    /// Probes Login → CreditCard → Identity → Authenticator → SecureNote.
    /// </summary>
    public async Task<object> ToggleFavoriteAsync(ToggleFavoriteRequest req)
    {
        if (!Authorized(req.SessionToken)) return Unauthorized();
        var id = req.CredentialId;

        try
        {
            var login = await _storage.GetLoginItemAsync(x => x.Id == id);
            if (login is not null)
            {
                login.IsFavorite = req.Value;
                login.UpdatedAt  = DateTime.UtcNow;
                await _storage.SaveLoginItemAsync(login);
                return new { updated = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var card = await _storage.GetCreditCardItemAsync(id);
            if (card is not null)
            {
                card.IsFavorite = req.Value;
                card.UpdatedAt  = DateTime.UtcNow;
                await _storage.SaveCreditCardItemAsync(card);
                return new { updated = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var identity = await _storage.GetIdentityItemAsync(id);
            if (identity is not null)
            {
                identity.IsFavorite = req.Value;
                identity.UpdatedAt  = DateTime.UtcNow;
                await _storage.SaveIdentityItemAsync(identity);
                return new { updated = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var auth = await _storage.GetAuthenticatorAsync(x => x.Id == id);
            if (auth is not null)
            {
                auth.IsFavorite = req.Value;
                auth.UpdatedAt  = DateTime.UtcNow;
                await _storage.AddOrUpdateAuthenticatorsAsync(new[] { auth });
                return new { updated = true, id };
            }
        }
        catch { /* not found */ }

        try
        {
            var note = await _storage.GetSecureNoteItemAsync(id);
            if (note is not null)
            {
                note.IsFavorite = req.Value;
                note.UpdatedAt  = DateTime.UtcNow;
                await _storage.SaveSecureNoteItemAsync(note);
                return new { updated = true, id };
            }
        }
        catch { /* not found */ }

        _logger.LogWarning("[VaultItems] ToggleFavorite — id={Id} not found in any collection.", id);
        return new { updated = false, id };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static object Unauthorized() =>
        new { error = "Unauthorized — vault is locked or token is invalid." };

    private static object Error(string message) =>
        new { error = message };
}
