using Fortress.Core.Contracts;
using Fortress.Core.Intelligence;
using Fortress.Core.Models;
using Fortress.Core.Services;
using Fortress.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fortress.Service.Infrastructure
{
    /// <summary>
    /// Handles all autofill-related IPC requests:
    /// GetCredentials, FillConfirmed, SaveNewLogin.
    /// Encrypts/decrypts via CryptographyService so the raw passwords
    /// never leave this process in plain-text over the pipe.
    /// </summary>
    public sealed class AutofillHandler
    {
        private readonly IDataStorageService _storage;
        private readonly ICryptographyService _crypto;
        private readonly VaultSessionService _session;
        private readonly AutofillSuggestionEngine _engine;
        private readonly FortressPreferenceWrapper _prefs;
        private readonly ILogger<AutofillHandler> _logger;

        public AutofillHandler(
   IDataStorageService storage,
         ICryptographyService crypto,
    VaultSessionService session,
         AutofillSuggestionEngine engine,
    FortressPreferenceWrapper prefs,
       ILogger<AutofillHandler> logger)
        {
            _storage = storage;
            _crypto = crypto;
            _session = session;
            _engine = engine;
            _prefs = prefs;
            _logger = logger;
        }

        // ── GetCredentials ────────────────────────────────────────────────────
        public async Task<GetCredentialsResponse> HandleGetCredentialsAsync(GetCredentialsRequest req)
        {
            if (!_session.ValidateToken(req.SessionToken))
                return new GetCredentialsResponse { IsVaultLocked = true };

            var logins = (await _storage.GetLoginItemsAsync()).ToList();
            var cards = (await _storage.GetCreditCardItemsAsync()).ToList();
            var identities = (await _storage.GetIdentityItemsAsync()).ToList();

            var combined = _engine.GetAllSuggestions(logins, cards, identities, req.Url);

            var dtos = new List<AutofillSuggestionDto>();

            foreach (var s in combined.LoginSuggestions)
            {
                var login = logins.FirstOrDefault(l => l.Id == s.CredentialId);
                if (login is null) continue;
                dtos.Add(new AutofillSuggestionDto
                {
                    Id = login.Id.ToString(),
                    Type = "Login",
                    Label = string.IsNullOrWhiteSpace(login.Label) ? login.Url : login.Label,
                    Username = login.Username ?? string.Empty,
                    IconUri = $"https://hubfunctions-us.azurewebsites.net/api/LogoService/Icon?domain={CoreHelpers.GetDomain(login.Url)}&size=64",
                    MatchScore = s.MatchScore,
                    MatchReason = s.MatchReason,
                    RequireAuthBeforeFill = login.RequireAuthBeforeFill,
                });
            }

            foreach (var s in combined.CreditCardSuggestions)
            {
                var card = cards.FirstOrDefault(c => c.Id == s.CreditCardId);
                if (card is null) continue;
                dtos.Add(new AutofillSuggestionDto
                {
                    Id = card.Id.ToString(),
                    Type = "CreditCard",
                    Label = s.CardLabel ?? card.Label,
                    Username = card.CardholderName,
                    IconUri = "creditcardlogo.png",
                    MatchScore = s.MatchScore,
                    MatchReason = s.MatchReason,
                    RequireAuthBeforeFill = card.RequireAuthBeforeFill,
                });
            }

            foreach (var s in combined.IdentitySuggestions)
            {
                var identity = identities.FirstOrDefault(i => i.Id == s.IdentityId);
                if (identity is null) continue;
                dtos.Add(new AutofillSuggestionDto
                {
                    Id = identity.Id.ToString(),
                    Type = "Identity",
                    Label = s.IdentityLabel ?? identity.Label,
                    Username = identity.Email,
                    IconUri = "addresslogo.png",
                    MatchScore = s.MatchScore,
                    MatchReason = s.MatchReason,
                });
            }

            return new GetCredentialsResponse
            {
                IsVaultLocked = false,
                Suggestions = dtos.OrderByDescending(d => d.MatchScore).ToList().AsReadOnly(),
            };
        }

        // ── FillConfirmed ─────────────────────────────────────────────────────
        public async Task<FillConfirmedResponse> HandleFillConfirmedAsync(FillConfirmedRequest req)
        {
            if (!_session.ValidateToken(req.SessionToken))
                return Fail("Vault is locked.");

            if (!Guid.TryParse(req.CredentialId, out var id))
                return Fail("Invalid credential ID.");

            // Try Login first
            var login = await _storage.GetLoginItemAsync(l => l.Id == id);
            if (login is not null)
            {
                var password = await DecryptAsync(login.Password);
                var totp = login.OtpSecret is not null
               ? OtpHelper.GenerateOtp((await DecryptAsync(login.OtpSecret)).Data ?? "").Code
                 : null;
                return new FillConfirmedResponse
                {
                    Success = true,
                    Username = login.Username,
                    Password = password.Data,
                    Totp = totp,
                };
            }

            // Try CreditCard
            var card = await _storage.GetCreditCardItemAsync(id);
            if (card is not null)
            {
                return new FillConfirmedResponse
                {
                    Success = true,
                    CardNumber = (await DecryptAsync(card.Number)).Data,
                    CardExpiry = $"{card.ExpiryMonth}/{card.ExpiryYear}",
                    CardCvv = (await DecryptAsync(card.Cvv)).Data,
                    CardholderName = card.CardholderName,
                };
            }

            return Fail("Credential not found.");
        }

        // ── SaveNewLogin ──────────────────────────────────────────────────────
        public async Task SaveNewLoginAsync(SaveNewLoginRequest req)
        {
            if (!_session.ValidateToken(req.SessionToken)) return;

            var encPass = await _crypto.Encrypt(req.Password);
            if (!encPass.Succeeded) return;

            var item = new LoginItem
            {
                Id = Guid.NewGuid(),
                Label = CoreHelpers.GetDomain(req.Url) ?? req.Url,
                Url = req.Url,
                Username = req.Username,
                Password = encPass.Data!,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await _storage.SaveLoginItemAsync(item);
            _logger.LogInformation("[AutofillHandler] Saved new login for {Url}", req.Url);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private async Task<CommandResult<string>> DecryptAsync(string? cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return new CommandResult<string>(string.Empty) { Succeeded = true };
            return await _crypto.Decrypt(cipher);
        }

        private static FillConfirmedResponse Fail(string msg) =>
       new() { Success = false, ErrorMessage = msg };
    }
}
