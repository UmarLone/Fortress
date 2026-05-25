using Fortress.Core.Models;

namespace Fortress.Windows.Desktop.Services
{
    /// <summary>
    /// Provides rich mock data for UI development and testing.
    /// Replace with real IPC calls to Fortress.Service when ready.
    /// </summary>
    public interface IDesktopDataService
    {
        // ── Login items ───────────────────────────────────────────────────────
        Task<List<LoginItem>> GetLoginItemsAsync();
        Task SaveLoginItemAsync(LoginItem item);
        Task DeleteLoginItemAsync(Guid id);

        // ── Credit cards ──────────────────────────────────────────────────────
        Task<List<CreditCardItem>> GetCreditCardsAsync();
        Task SaveCreditCardAsync(CreditCardItem item);
        Task DeleteCreditCardAsync(Guid id);

        // ── Identities ────────────────────────────────────────────────────────
        Task<List<IdentityItem>> GetIdentitiesAsync();
        Task SaveIdentityAsync(IdentityItem item);
        Task DeleteIdentityAsync(Guid id);

        // ── Secure items ──────────────────────────────────────────────────────
        Task<List<SecureItem>> GetSecureItemsAsync();
        Task SaveSecureItemAsync(SecureItem item);
        Task DeleteSecureItemAsync(Guid id);

        // ── Authenticators ────────────────────────────────────────────────────
        Task<List<Authenticator>> GetAuthenticatorsAsync();
        Task SaveAuthenticatorAsync(Authenticator item);
        Task DeleteAuthenticatorAsync(Guid id);

        // ── Passkeys ──────────────────────────────────────────────────────────
        Task<List<PasskeyItem>> GetPasskeysAsync();

        // ── Groups ────────────────────────────────────────────────────────────
        Task<List<VaultGroup>> GetGroupsAsync();
        Task SaveGroupAsync(VaultGroup group);
        Task DeleteGroupAsync(Guid id);
        Task<HashSet<Guid>> GetGroupMemberIdsAsync(Guid groupId);

        // ── Vault health ──────────────────────────────────────────────────────
        Task<VaultHealthResult> GetVaultHealthAsync();

        // ── Event logs ────────────────────────────────────────────────────────
        Task<List<EventLog>> GetEventLogsAsync();
        Task AddEventLogAsync(EventLog entry);

        // ── Notifications ─────────────────────────────────────────────────────
        Task<List<UserNotification>> GetNotificationsAsync();
        Task AddNotificationAsync(UserNotification notification);
        Task MarkNotificationsSeenAsync();
        Task DeleteNotificationsAsync(IEnumerable<Guid> ids);
    }

    public sealed class MockDataService : IDesktopDataService
    {
        // ── Stable IDs ───────────────────────────────────────────────────────
        private static readonly Guid WorkGroupId     = new("aaaaaaaa-0001-0000-0000-000000000000");
        private static readonly Guid PersonalGroupId = new("aaaaaaaa-0002-0000-0000-000000000000");
        private static readonly Guid FinanceGroupId  = new("aaaaaaaa-0003-0000-0000-000000000000");
        private static readonly Guid SocialGroupId   = new("aaaaaaaa-0004-0000-0000-000000000000");
        private static readonly Guid InfraGroupId    = new("aaaaaaaa-0005-0000-0000-000000000000");

        private static readonly Guid GitHubId   = new("bbbbbbbb-0001-0000-0000-000000000000");
        private static readonly Guid GoogleId   = new("bbbbbbbb-0002-0000-0000-000000000000");
        private static readonly Guid M365Id     = new("bbbbbbbb-0003-0000-0000-000000000000");
        private static readonly Guid AmazonId   = new("bbbbbbbb-0004-0000-0000-000000000000");
        private static readonly Guid NetflixId  = new("bbbbbbbb-0005-0000-0000-000000000000");
        private static readonly Guid SpotifyId  = new("bbbbbbbb-0006-0000-0000-000000000000");
        private static readonly Guid TwitterId  = new("bbbbbbbb-0007-0000-0000-000000000000");
  private static readonly Guid LinkedInId = new("bbbbbbbb-0008-0000-0000-000000000000");
        private static readonly Guid SlackId    = new("bbbbbbbb-0009-0000-0000-000000000000");
        private static readonly Guid AzureId    = new("bbbbbbbb-0010-0000-0000-000000000000");
 private static readonly Guid RouterId   = new("bbbbbbbb-0011-0000-0000-000000000000");
        private static readonly Guid DropboxId  = new("bbbbbbbb-0012-0000-0000-000000000000");

        private static readonly Dictionary<Guid, HashSet<Guid>> GroupMembers = new()
        {
            [WorkGroupId] = new() { GitHubId, M365Id, SlackId, AzureId },
            [PersonalGroupId] = new() { GoogleId, SpotifyId, NetflixId, DropboxId, TwitterId },
            [FinanceGroupId] = new() { AmazonId },
            [SocialGroupId] = new() { TwitterId, LinkedInId, SpotifyId },
            [InfraGroupId] = new() { AzureId, RouterId, GitHubId },
        };

        // ── Login Items ──────────────────────────────────────────────────────

        public Task<List<LoginItem>> GetLoginItemsAsync() => Task.FromResult(new List<LoginItem>
        {
            new() { Id = GitHubId,   Label = "GitHub",            Url = "https://github.com",   Username = "john.doe@email.com",  Password = "Tr0ub4dor&3",      LoginType = LoginType.Web, IsFavorite = true,  PasswordStrengthScore = 88, PasswordStrengthLevel = 4, OtpSecret = "JBSWY3DPEHPK3PXP",  CreatedAt = DateTime.UtcNow.AddDays(-120), UpdatedAt = DateTime.UtcNow.AddDays(-5)    },
 new() { Id = GoogleId,   Label = "Google",  Url = "https://google.com",     Username = "john.doe@gmail.com",  Password = "Passw0rd!",    LoginType = LoginType.Web,         IsFavorite = true,  PasswordStrengthScore = 62, PasswordStrengthLevel = 2,      CreatedAt = DateTime.UtcNow.AddDays(-300), UpdatedAt = DateTime.UtcNow.AddDays(-90)   },
 new() { Id = M365Id,     Label = "Microsoft 365",       Url = "https://office.com",          Username = "john@contoso.com",    Password = "Str0ngP@ss#2024",  LoginType = LoginType.Web,         IsFavorite = false, PasswordStrengthScore = 95, PasswordStrengthLevel = 4, OtpSecret = "BASE32SECRET",      CreatedAt = DateTime.UtcNow.AddDays(-60),  UpdatedAt = DateTime.UtcNow.AddDays(-2)    },
   new() { Id = AmazonId,   Label = "Amazon",     Url = "https://amazon.com",          Username = "john.doe@email.com",Password = "password123",      LoginType = LoginType.Web,      PasswordStrengthScore = 15, PasswordStrengthLevel = 0, CreatedAt = DateTime.UtcNow.AddDays(-500), UpdatedAt = DateTime.UtcNow.AddDays(-400)},
         new() { Id = NetflixId,  Label = "Netflix",           Url = "https://netflix.com",         Username = "john.doe@email.com",  Password = "password123",    LoginType = LoginType.Web,          PasswordStrengthScore = 15, PasswordStrengthLevel = 0,       CreatedAt = DateTime.UtcNow.AddDays(-400), UpdatedAt = DateTime.UtcNow.AddDays(-380)  },
        new() { Id = SpotifyId,  Label = "Spotify",     Url = "https://spotify.com",       Username = "john@email.com",      Password = "Sp0t1fy#Rox!", LoginType = LoginType.Web,              PasswordStrengthScore = 76, PasswordStrengthLevel = 3,       CreatedAt = DateTime.UtcNow.AddDays(-200), UpdatedAt = DateTime.UtcNow.AddDays(-30)   },
  new() { Id = TwitterId,  Label = "Twitter / X",        Url = "https://x.com",               Username = "@johndoe",        Password = "Tw1tt3rPass!",     LoginType = LoginType.Web,   PasswordStrengthScore = 70, PasswordStrengthLevel = 3, CreatedAt = DateTime.UtcNow.AddDays(-700), UpdatedAt = DateTime.UtcNow.AddDays(-1)    },
            new() { Id = LinkedInId, Label = "LinkedIn",     Url = "https://linkedin.com",        Username = "john.doe@email.com",  Password = "L1nk3dIn$ecure",  LoginType = LoginType.Web,            PasswordStrengthScore = 72, PasswordStrengthLevel = 3,          CreatedAt = DateTime.UtcNow.AddDays(-600), UpdatedAt = DateTime.UtcNow.AddDays(-50)   },
     new() { Id = SlackId,    Label = "Slack \u2014 Contoso",      Url = "https://contoso.slack.com",   Username = "john@contoso.com",    Password = "Sl@ck2024Secure!", LoginType = LoginType.Web,      IsFavorite = true,  PasswordStrengthScore = 90, PasswordStrengthLevel = 4,     CreatedAt = DateTime.UtcNow.AddDays(-80),  UpdatedAt = DateTime.UtcNow.AddDays(-3)    },
         new() { Id = AzureId,    Label = "Azure Portal",    Url = "https://portal.azure.com",    Username = "john@contoso.com",    Password = "Az@r3Portal#99",   LoginType = LoginType.Web,     IsFavorite = true,  PasswordStrengthScore = 88, PasswordStrengthLevel = 4, OtpSecret = "AZUREOTPSECRET32",  CreatedAt = DateTime.UtcNow.AddDays(-45),  UpdatedAt = DateTime.UtcNow.AddDays(-1)    },
            new() { Id = RouterId,   Label = "Home Router",       Url = "http://192.168.1.1",      Username = "admin",  Password = "admin",    LoginType = LoginType.Web,      PasswordStrengthScore = 2,  PasswordStrengthLevel = 0,           CreatedAt = DateTime.UtcNow.AddDays(-1000),UpdatedAt = DateTime.UtcNow.AddDays(-1000) },
  new() { Id = Guid.NewGuid(), Label = "VS Code",     Url = string.Empty,    Username = "john@contoso.com",    Password = "C0dePa$$2024",     LoginType = LoginType.DesktopApp,             PasswordStrengthScore = 85, PasswordStrengthLevel = 4,    CreatedAt = DateTime.UtcNow.AddDays(-30),  UpdatedAt = DateTime.UtcNow.AddDays(-7)    },
    new() { Id = Guid.NewGuid(), Label = "Windows \u2014 DESKTOP-JD01", Url = string.Empty,      Username = "john",     Password = "W1nd0ws#Local!",   LoginType = LoginType.WindowsLocal, PasswordStrengthScore = 80, PasswordStrengthLevel = 3,          CreatedAt = DateTime.UtcNow.AddDays(-200), UpdatedAt = DateTime.UtcNow.AddDays(-60)   },
      new() { Id = DropboxId,  Label = "Dropbox",   Url = "https://dropbox.com",      Username = "john.doe@email.com",  Password = "Dr0pB0x@Safe!",   LoginType = LoginType.Web,     PasswordStrengthScore = 78, PasswordStrengthLevel = 3,     CreatedAt = DateTime.UtcNow.AddDays(-450), UpdatedAt = DateTime.UtcNow.AddDays(-20)   },
    });

        // ── Credit Cards ─────────────────────────────────────────────────────

        public Task<List<CreditCardItem>> GetCreditCardsAsync() => Task.FromResult(new List<CreditCardItem>
        {
            new() { Id = Guid.NewGuid(), Label = "Chase Sapphire",      CardholderName = "JOHN DOE", Number = "4111111111114242", ExpiryMonth = "12", ExpiryYear = "2027", Cvv = "123",  CardNetwork = "Visa",       BillingAddress = "123 Main St, New York, NY 10001", CreatedAt = DateTime.UtcNow.AddDays(-365), UpdatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Id = Guid.NewGuid(), Label = "Amex Gold",       CardholderName = "JOHN DOE", Number = "378282246310005",  ExpiryMonth = "08", ExpiryYear = "2026", Cvv = "2010", CardNetwork = "Amex",       BillingAddress = "123 Main St, New York, NY 10001", CreatedAt = DateTime.UtcNow.AddDays(-500), UpdatedAt = DateTime.UtcNow.AddDays(-5)  },
   new() { Id = Guid.NewGuid(), Label = "Mastercard Business", CardholderName = "JOHN DOE", Number = "5500005555555559", ExpiryMonth = "03", ExpiryYear = "2025", Cvv = "456",  CardNetwork = "Mastercard", BillingAddress = "456 Corp Ave, NYC, NY",        CreatedAt = DateTime.UtcNow.AddDays(-200), UpdatedAt = DateTime.UtcNow.AddDays(-100)},
        });

        // ── Identities ───────────────────────────────────────────────────────

        public Task<List<IdentityItem>> GetIdentitiesAsync() => Task.FromResult(new List<IdentityItem>
        {
       new() { Id = Guid.NewGuid(), Label = "Personal",       FirstName = "John", LastName = "Doe", Email = "john.doe@email.com", Phone = "+1 555 123 4567", Company = string.Empty,  AddressLine1 = "123 Main Street",   City = "New York", State = "NY", Country = "United States", PostalCode = "10001", CreatedAt = DateTime.UtcNow.AddDays(-400) },
       new() { Id = Guid.NewGuid(), Label = "Work \u2014 Contoso",   FirstName = "John", LastName = "Doe", Email = "john@contoso.com",   Phone = "+1 555 987 6543", Company = "Contoso Ltd", AddressLine1 = "456 Corporate Ave", City = "New York", State = "NY", Country = "United States", PostalCode = "10018", CreatedAt = DateTime.UtcNow.AddDays(-200) },
        });

        // ── Secure Items ─────────────────────────────────────────────────────

        public Task<List<SecureItem>> GetSecureItemsAsync() => Task.FromResult(new List<SecureItem>
        {
  new() { Id = Guid.NewGuid(), Label = "US Passport",  ItemType = SecureItemType.Passport,  FullName = "John Doe", Number = "C12345678",  IssuingCountry = "United States", IssuedDate = "2019-03-15", ExpiryDate = "2029-03-14", Nationality = "American", DateOfBirth = "1990-01-15", CreatedAt = DateTime.UtcNow.AddDays(-600) },
            new() { Id = Guid.NewGuid(), Label = "Driver's License \u2014 NY", ItemType = SecureItemType.DriversLicense, FullName = "John Doe", Number = "123456789", IssuingCountry = "United States", IssuedDate = "2020-06-01", ExpiryDate = "2026-06-01",    CreatedAt = DateTime.UtcNow.AddDays(-500) },
         new() { Id = Guid.NewGuid(), Label = "Home Wi-Fi",               ItemType = SecureItemType.WiFi,            Ssid = "HomeNetwork_5G", Password = "WiFiP@ssword2024!", WifiSecurity = "WPA3",            CreatedAt = DateTime.UtcNow.AddDays(-300) },
   new() { Id = Guid.NewGuid(), Label = "Dev Server SSH",     ItemType = SecureItemType.Ssh,     Host = "dev.contoso.com", Port = "22", Username = "john", SshPassword = "SshP@ss!", KeyFingerprint = "SHA256:abc123xyz...",       CreatedAt = DateTime.UtcNow.AddDays(-90)  },
       new() { Id = Guid.NewGuid(), Label = "SSN",          ItemType = SecureItemType.SocialSecurity,  FullName = "John Doe", Number = "123-45-6789",         CreatedAt = DateTime.UtcNow.AddDays(-700) },
        });

        // ── Authenticators ───────────────────────────────────────────────────

        public Task<List<Authenticator>> GetAuthenticatorsAsync() => Task.FromResult(new List<Authenticator>
        {
            new() { Id = Guid.NewGuid(), Issuer = "GitHub",     Username = "john.doe@email.com",  Type = AuthenticatorType.Totp, Period = 30, Digits = 6, Secret = "JBSWY3DPEHPK3PXP",  IsFavorite = true,  CreatedAt = DateTime.UtcNow.AddDays(-120) },
            new() { Id = Guid.NewGuid(), Issuer = "Google",Username = "john.doe@gmail.com",  Type = AuthenticatorType.Totp, Period = 30, Digits = 6, Secret = "HXDMVJECJJWSRB3HWIZR4IFUGFTMXBOZ", IsFavorite = true,  CreatedAt = DateTime.UtcNow.AddDays(-300) },
        new() { Id = Guid.NewGuid(), Issuer = "Microsoft",  Username = "john@contoso.com",    Type = AuthenticatorType.Totp, Period = 30, Digits = 6, Secret = "JBSWY3DPEHPK3PXP",      IsFavorite = false, CreatedAt = DateTime.UtcNow.AddDays(-180) },
     new() { Id = Guid.NewGuid(), Issuer = "Slack",      Username = "john@contoso.com",    Type = AuthenticatorType.Totp, Period = 30, Digits = 6, Secret = "BASE32SLACKSECRET",        IsFavorite = false, CreatedAt = DateTime.UtcNow.AddDays(-80)  },
  new() { Id = Guid.NewGuid(), Issuer = "Amazon AWS", Username = "john.doe@email.com",  Type = AuthenticatorType.Totp, Period = 30, Digits = 6, Secret = "AWSSECRETBASE32KEY",   IsFavorite = false, CreatedAt = DateTime.UtcNow.AddDays(-45)  },
            new() { Id = Guid.NewGuid(), Issuer = "Binance",    Username = "john@email.com",      Type = AuthenticatorType.Totp, Period = 30, Digits = 6, Secret = "BINANCESECRETKEY32",          IsFavorite = false, CreatedAt = DateTime.UtcNow.AddDays(-200) },
        });

        // ── Passkeys ─────────────────────────────────────────────────────────

        public Task<List<PasskeyItem>> GetPasskeysAsync() => Task.FromResult(new List<PasskeyItem>
    {
          new() { Id = Guid.NewGuid(), RpId = "github.com", RpName = "GitHub", UserName = "john.doe",          UserDisplayName = "John Doe", Algorithm = -7, SignCount = 12, CreatedAt = DateTime.UtcNow.AddDays(-30) },
  new() { Id = Guid.NewGuid(), RpId = "google.com", RpName = "Google", UserName = "john.doe@gmail.com", UserDisplayName = "John Doe", Algorithm = -7, SignCount = 5,  CreatedAt = DateTime.UtcNow.AddDays(-10) },
        });

        // ── Groups ───────────────────────────────────────────────────────────

        public Task<List<VaultGroup>> GetGroupsAsync() => Task.FromResult(new List<VaultGroup>
     {
            new() { Id = WorkGroupId,     Name = "Work",       IconKey = "Briefcase24", Color = "#3B82F6" },
     new() { Id = PersonalGroupId, Name = "Personal",       IconKey = "Person24",    Color = "#10B981" },
 new() { Id = FinanceGroupId,  Name = "Finance",        IconKey = "Money24",     Color = "#F59E0B" },
    new() { Id = SocialGroupId,   Name = "Social Media",   IconKey = "Globe24",     Color = "#8B5CF6" },
            new() { Id = InfraGroupId,    Name = "Infrastructure", IconKey = "Server24",    Color = "#EF4444" },
});

        public Task<HashSet<Guid>> GetGroupMemberIdsAsync(Guid groupId) =>
  Task.FromResult(GroupMembers.TryGetValue(groupId, out var ids) ? ids : new HashSet<Guid>());

        // ── Vault Health ─────────────────────────────────────────────────────

        public Task<VaultHealthResult> GetVaultHealthAsync() => Task.FromResult(new VaultHealthResult
        {
            Score = 72,
            Status = VaultHealthStatus.AtRisk,
            TotalCredentials = 15,
            TotalAuthenticators = 6,
            WeakPasswordsCount = 3,
            ReusedPasswordsCount = 2,
            OldPasswordsCount = 4,
            Missing2FACount = 8,
            BreachedCount = 1,
            EmptyPasswordCount = 0,
            AttackSurfaceScore = 45,
            CalculatedAt = DateTime.UtcNow,
            Details = new List<CredentialHealthDetail>
    {
           new() { Id = Guid.NewGuid(), Label = "Amazon",     IsWeak = true,  IsReused = true, PasswordStrengthScore = 15, StrengthLevel = Fortress.Core.Models.PasswordStrengthLevel.VeryWeak },
             new() { Id = Guid.NewGuid(), Label = "Netflix",        IsWeak = true,  IsReused = true, PasswordStrengthScore = 15, StrengthLevel = Fortress.Core.Models.PasswordStrengthLevel.VeryWeak },
    new() { Id = Guid.NewGuid(), Label = "Home Router",    IsWeak = true,      PasswordStrengthScore = 2,  StrengthLevel = Fortress.Core.Models.PasswordStrengthLevel.VeryWeak },
      new() { Id = Guid.NewGuid(), Label = "1Password (old)",     IsOld = true,    PasswordStrengthScore = 55, StrengthLevel = Fortress.Core.Models.PasswordStrengthLevel.Fair     },
            },
            Findings = new List<VaultFinding>
          {
      new() { Severity = FindingSeverity.Critical, Title = "Breached Password Detected",      Description = "1 of your accounts uses a password found in a known data breach.", PointsDeducted = 10, AffectedLabels = new[] { "Amazon" },          Recommendation = "Change your Amazon password immediately."      },
           new() { Severity = FindingSeverity.High,     Title = "Reused Passwords",           Description = "2 accounts share the same password. A single breach exposes all of them.", PointsDeducted = 10, AffectedLabels = new[] { "Amazon", "Netflix" },                Recommendation = "Use a unique password for each account."       },
        new() { Severity = FindingSeverity.High, Title = "Weak Passwords",        Description = "3 accounts have passwords that are easy to guess or crack.",       PointsDeducted = 8,  AffectedLabels = new[] { "Amazon", "Netflix", "Home Router" },         Recommendation = "Use the Password Generator to create strong passwords." },
           new() { Severity = FindingSeverity.Medium,   Title = "Missing Two-Factor Authentication",Description = "8 accounts have no 2FA configured.",   PointsDeducted = 6,  AffectedLabels = new[] { "Amazon", "Netflix", "Spotify", "Dropbox", "LinkedIn", "Twitter / X", "1Password (old)", "Home Router" },    Recommendation = "Enable 2FA on every account that supports it."        },
          new() { Severity = FindingSeverity.Low,      Title = "Old Passwords",         Description = "4 passwords have not been changed in over a year.", PointsDeducted = 4,  AffectedLabels = new[] { "Amazon", "Netflix", "Home Router", "1Password (old)" },         Recommendation = "Rotate passwords that are older than 12 months."      },
            },
            CredentialClusters = new List<CredentialCluster>
   {
      new() { SharedPasswordHash = "abc123", Members = new List<CredentialHealthDetail>
     {
   new() { Label = "Amazon",  Username = "john.doe@email.com" },
         new() { Label = "Netflix", Username = "john.doe@email.com" },
     }}
            }
        });

        // ── Event Logs ───────────────────────────────────────────────────────

        public Task<List<EventLog>> GetEventLogsAsync() => Task.FromResult(new List<EventLog>
     {
            new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddMinutes(-5),  EventType = 1, CredentialLabel = "GitHub",       Detail = "Password copied" },
     new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddMinutes(-12), EventType = 1, CredentialLabel = "Azure",        Detail = "Password copied"             },
         new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddHours(-1), EventType = 3, CredentialLabel = "Amazon",       Detail = "Autofill blocked \u2014 risk score 87"    },
         new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddHours(-2),  EventType = 1, CredentialLabel = "Slack",        Detail = "OTP code copied"   },
        new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddHours(-5),    EventType = 2, CredentialLabel = "Netflix",   Detail = "Credential added"         },
            new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddHours(-8),    EventType = 4, CredentialLabel = "LinkedIn",     Detail = "Password changed"    },
            new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-1),     EventType = 3, CredentialLabel = "phishing.ru",  Detail = "Autofill blocked \u2014 unknown domain"   },
  new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-1),     EventType = 1, CredentialLabel = "Microsoft",    Detail = "Password copied" },
            new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-2),EventType = 2, CredentialLabel = "GitHub",       Detail = "Credential updated"      },
new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-3),     EventType = 5, CredentialLabel = "Vault",        Detail = "Vault unlocked"               },
    new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-3),     EventType = 6, CredentialLabel = "Vault",        Detail = "Vault locked"        },
          new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-4),     EventType = 1, CredentialLabel = "Spotify",      Detail = "Password copied"              },
            new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-5),     EventType = 2, CredentialLabel = "Dropbox",      Detail = "Credential added"            },
    new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-6),     EventType = 3, CredentialLabel = "evil-site.net",Detail = "Autofill blocked \u2014 phishing risk"    },
            new() { Id = Guid.NewGuid(), DateTime = DateTime.UtcNow.AddDays(-7),   EventType = 4, CredentialLabel = "Amazon",     Detail = "Password changed"},
        });

        // ── Notifications ────────────────────────────────────────────────────

        public Task<List<UserNotification>> GetNotificationsAsync() => Task.FromResult(new List<UserNotification>
        {
            new() { Id = Guid.NewGuid(), Title = "Breach Detected",     Type = NotificationType.BreachDetected,  Message = "Your password for Amazon was found in a known data breach. Change it immediately.", IsSeen = false, CreationDateTime = DateTime.UtcNow.AddHours(-2) },
     new() { Id = Guid.NewGuid(), Title = "Vault Health Alert",  Type = NotificationType.Alert,      Message = "Your vault health score dropped to 72 (At Risk). Review the Security Health page.", IsSeen = false, CreationDateTime = DateTime.UtcNow.AddHours(-6) },
            new() { Id = Guid.NewGuid(), Title = "Weak Password",       Type = NotificationType.Warning,     Message = "Home Router is using a default/weak password. Update it to secure your network.",    IsSeen = false, CreationDateTime = DateTime.UtcNow.AddDays(-1)  },
   new() { Id = Guid.NewGuid(), Title = "New Login Saved",     Type = NotificationType.SaveLoginPrompt, Message = "A new login for dropbox.com was saved to your vault.",       IsSeen = true,  CreationDateTime = DateTime.UtcNow.AddDays(-2)  },
    new() { Id = Guid.NewGuid(), Title = "Cloud Sync Complete", Type = NotificationType.Info,     Message = "Your vault was successfully synced to OneDrive.",            IsSeen = true,  CreationDateTime = DateTime.UtcNow.AddDays(-3)  },
        });
        // ── Write stubs (no-op in mock) ───────────────────────────────────────
        public Task SaveLoginItemAsync(LoginItem item)           => Task.CompletedTask;
        public Task DeleteLoginItemAsync(Guid id)      => Task.CompletedTask;
        public Task SaveCreditCardAsync(CreditCardItem item)       => Task.CompletedTask;
      public Task DeleteCreditCardAsync(Guid id)            => Task.CompletedTask;
        public Task SaveIdentityAsync(IdentityItem item)   => Task.CompletedTask;
public Task DeleteIdentityAsync(Guid id)         => Task.CompletedTask;
        public Task SaveSecureItemAsync(SecureItem item)           => Task.CompletedTask;
        public Task DeleteSecureItemAsync(Guid id)      => Task.CompletedTask;
        public Task SaveAuthenticatorAsync(Authenticator item)     => Task.CompletedTask;
        public Task DeleteAuthenticatorAsync(Guid id)              => Task.CompletedTask;
  public Task SaveGroupAsync(VaultGroup group)       => Task.CompletedTask;
     public Task DeleteGroupAsync(Guid id)    => Task.CompletedTask;
     public Task AddEventLogAsync(EventLog entry)               => Task.CompletedTask;
  public Task AddNotificationAsync(UserNotification n)       => Task.CompletedTask;
        public Task MarkNotificationsSeenAsync() => Task.CompletedTask;
        public Task DeleteNotificationsAsync(IEnumerable<Guid> ids)=> Task.CompletedTask;
    }
}







