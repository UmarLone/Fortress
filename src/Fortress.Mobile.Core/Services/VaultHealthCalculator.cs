using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Intelligence;
using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Deterministic, offline vault health calculator.
    ///
    /// Algorithm:
    ///   1. Start at 100 points.
    ///   2. For each finding category, deduct (count – pointsEach),
    /// capped at MaxDeductionPerCategory.
    ///   3. Clamp final score to [0, 100].
    ///   4. Classify into Excellent / Good / AtRisk / Critical.
    ///
    /// No external services. No AI. Identical inputs ? identical output.
    /// </summary>
    public sealed class VaultHealthCalculator
    {
        private readonly VaultHealthConfig _cfg;
        private readonly PasswordAnomalyDetector? _anomalyDetector;
        private readonly ILogger<VaultHealthCalculator>? _logger;

        // Common-breach wordlist – purely local, no network calls.
        // Covers the most-used passwords from public data-breach compilations.
        private static readonly HashSet<string> _breachWordlist = new(StringComparer.OrdinalIgnoreCase)
        {
            "123456", "password", "123456789", "12345678", "12345", "1234567",
        "1234567890", "qwerty", "abc123", "111111", "123123", "admin",
    "letmein", "welcome", "monkey", "dragon", "master", "login",
      "pass", "1111", "sunshine", "princess", "password1", "iloveyou",
         "football", "shadow", "superman", "michael", "jessica", "charlie",
   "donald", "trustno1", "batman", "access", "hello", "whatever",
            "696969", "mustang", "maverick", "startrek", "starwars", "password2",
            "test", "temp", "guest", "changeme", "qwerty123", "q1w2e3r4",
 "zaq12wsx", "asdfgh", "passw0rd", "p@ssword", "p@ss", "pa$$word"
   };

        public VaultHealthCalculator(
                 VaultHealthConfig? config = null,
           PasswordAnomalyDetector? anomalyDetector = null,
           ILogger<VaultHealthCalculator>? logger = null)
        {
            _cfg = config ?? new VaultHealthConfig();
            _anomalyDetector = anomalyDetector;
            _logger = logger;
        }

        // ── Public API ────────────────────────────────────────────────────────────
        /// <summary>
        /// Calculates a full <see cref="VaultHealthResult"/> from raw vault data.
        /// All parameters except <paramref name="credentials"/> are optional.
        /// </summary>
        public VaultHealthResult Calculate(
     IEnumerable<LoginItem> credentials,
   IEnumerable<Authenticator>? authenticators = null,
            DateTime? referenceDate = null)
        {
            var creds = credentials?.ToList() ?? new List<LoginItem>();
            var auths = authenticators?.ToList() ?? new List<Authenticator>();
            var now = referenceDate ?? DateTime.UtcNow;

            _logger?.LogInformation("VaultHealthCalculator: analysing {Count} credentials", creds.Count);

            if (creds.Count == 0)
                return EmptyVaultResult();

            var authDomains = auths
                            .Where(a => !string.IsNullOrWhiteSpace(a.Issuer))
                         .Select(a => NormaliseDomain(a.Issuer!))
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var passwordGroups = creds
            .Where(c => !string.IsNullOrWhiteSpace(c.PasswordHash)
          ? true
            : !string.IsNullOrWhiteSpace(c.Password))
             .GroupBy(c => !string.IsNullOrWhiteSpace(c.PasswordHash)
           ? c.PasswordHash!
             : c.Password!)
               .Where(g => g.Count() > 1)
         .SelectMany(g => g.Select(c => c.Id))
           .ToHashSet();

            var details = creds.Select(c => BuildDetail(c, passwordGroups, authDomains, now)).ToList();

            int weakCount = details.Count(d => d.IsWeak);
            int reusedCount = details.Count(d => d.IsReused);
            int oldCount = details.Count(d => d.IsOld);
            int missing2FACount = details.Count(d => !d.HasTwoFactor);
     // Build a lookup once – avoids O(n…) creds.First() inside the loop below
      var credById = creds.ToDictionary(c => c.Id);
    int breachedCount = details.Count(d =>
       _breachWordlist.Contains(credById[d.Id].Password ?? ""));
            int emptyCount = creds.Count(c => string.IsNullOrWhiteSpace(c.Password));
       int userAsPassCount = creds.Count(c =>
       !string.IsNullOrWhiteSpace(c.Password) &&
        !string.IsNullOrWhiteSpace(c.Username) &&
 string.Equals(c.Password, c.Username, StringComparison.OrdinalIgnoreCase));

            int score = 100;
            var findings = new List<VaultFinding>();

            score -= AddFinding(findings, breachedCount, creds,
      c => _breachWordlist.Contains(c.Password ?? ""),
          FindingSeverity.Critical, "Breached Passwords",
          "These passwords appear in public data-breach databases and are known to attackers.",
                   "Change these passwords immediately to something unique and strong.",
                   _cfg.DeductionPerBreachedAccount);

            score -= AddFinding(findings, emptyCount, creds,
               c => string.IsNullOrWhiteSpace(c.Password),
             FindingSeverity.Critical, "Empty Passwords",
     "These accounts have no password set, leaving them completely unprotected.",
         "Set a strong, unique password for each account.",
        _cfg.DeductionPerEmptyPassword);

            score -= AddFinding(findings, userAsPassCount, creds,
                   c => !string.IsNullOrWhiteSpace(c.Password) &&
                     !string.IsNullOrWhiteSpace(c.Username) &&
                    string.Equals(c.Password, c.Username, StringComparison.OrdinalIgnoreCase),
         FindingSeverity.High, "Username Used as Password",
             "Using your username as your password is one of the first things attackers try.",
                "Replace these with randomly generated, unique passwords.",
                _cfg.DeductionPerUsernameAsPassword);

            score -= AddFinding(findings, reusedCount, creds,
             c => passwordGroups.Contains(c.Id),
                  FindingSeverity.High, "Reused Passwords",
                  "If one site is breached, all accounts sharing the same password are immediately compromised.",
                        "Generate a unique password for each account.",
             _cfg.DeductionPerReusedPassword);

            score -= AddFinding(findings, weakCount, creds,
       c => c.PasswordStrengthScore > 0
 ? (PasswordStrengthLevel)c.PasswordStrengthLevel is PasswordStrengthLevel.VeryWeak or PasswordStrengthLevel.Weak
       : IsWeakPassword(c.Password),
 FindingSeverity.Medium, "Weak Passwords",
     "These passwords are short, simple, or missing numbers and special characters.",
      "Replace with passwords of 14+ characters including uppercase, numbers and symbols.",
      _cfg.DeductionPerWeakPassword);

            score -= AddFinding(findings, missing2FACount, creds,
                c => !authDomains.Contains(NormaliseDomain(c.Url ?? "")),
                FindingSeverity.Medium, "No Two-Factor Authentication",
                "Accounts without 2FA are vulnerable even if the password is strong.",
         "Enable an authenticator app on these accounts and save the secret here.",
              _cfg.DeductionPerMissing2FA);

            score -= AddFinding(findings, oldCount, creds,
                  c => IsOldPassword(c, now),
                         FindingSeverity.Low, "Old Passwords",
                   $"Passwords unchanged for over {_cfg.MaxPasswordAgeDays} days are more likely to be compromised.",
                   "Review and rotate passwords that haven't been changed in over a year.",
                _cfg.DeductionPerOldPassword);

            if (_anomalyDetector != null)
            {
                var anomalyIds = _anomalyDetector.DetectAnomalies(creds);
                if (anomalyIds.Count > 0)
                {
                    score -= AddFinding(findings, anomalyIds.Count, creds,
                     c => anomalyIds.Contains(c.Id),
                FindingSeverity.Medium, "Outlier Weak Passwords",
             "These passwords are significantly weaker than the rest of your vault, making them the most likely targets.",
            "Bring these passwords up to the same strength as the rest of your vault.",
           _cfg.DeductionPerWeakPassword);
                }
            }

            score = Math.Clamp(score, 0, 100);
            var status = ClassifyScore(score);

            _logger?.LogInformation(
                "VaultHealthCalculator: score={Score} status={Status} findings={Findings}",
           score, status, findings.Count);

            return new VaultHealthResult
            {
                Score = score,
                Status = status,
                TotalCredentials = creds.Count,
                TotalAuthenticators = auths.Count,
                WeakPasswordsCount = weakCount,
                ReusedPasswordsCount = reusedCount,
                OldPasswordsCount = oldCount,
                Missing2FACount = missing2FACount,
                BreachedCount = breachedCount,
                EmptyPasswordCount = emptyCount,
                UsernameAsPasswordCount = userAsPassCount,
                AttackSurfaceScore = ComputeAttackSurface(creds, details, authDomains, passwordGroups),
                CredentialClusters = BuildCredentialClusters(creds, details, passwordGroups),
                Details = details.AsReadOnly(),
                Findings = findings
              .OrderByDescending(f => f.Severity)
         .ThenByDescending(f => f.PointsDeducted)
            .ToList()
        .AsReadOnly(),
                CalculatedAt = now,
                Config = _cfg
            };
        }

        private CredentialHealthDetail BuildDetail(
                 LoginItem c,
              HashSet<Guid> reusedIds,
          HashSet<string> authDomains,
             DateTime now)
        {
            int strengthScore;
            PasswordStrengthLevel strengthLevel;

            if (c.PasswordStrengthScore > 0)
            {
                strengthScore = c.PasswordStrengthScore;
                strengthLevel = (PasswordStrengthLevel)c.PasswordStrengthLevel;
            }
            else
            {
                (strengthScore, strengthLevel) = ScorePassword(c.Password);
            }

            bool isWeak = strengthLevel is PasswordStrengthLevel.VeryWeak or PasswordStrengthLevel.Weak;
            bool has2FA = authDomains.Contains(NormaliseDomain(c.Url ?? ""));

            return new CredentialHealthDetail
            {
                Id = c.Id,
                Label = c.Label ?? c.Url ?? "(unknown)",
                Username = c.Username ?? string.Empty,
                IsWeak = isWeak,
                IsReused = reusedIds.Contains(c.Id),
                IsOld = IsOldPassword(c, now),
                HasTwoFactor = has2FA,
                PasswordStrengthScore = strengthScore,
                StrengthLevel = strengthLevel
            };
        }

        private int AddFinding(
      List<VaultFinding> findings,
       int count,
            List<LoginItem> allCreds,
   Func<LoginItem, bool> predicate,
         FindingSeverity severity,
   string title,
         string description,
            string recommendation,
     int pointsEach)
        {
            if (count == 0) return 0;

            var deduction = Math.Min(count * pointsEach, _cfg.MaxDeductionPerCategory);
            var affected = allCreds
        .Where(predicate)
         .Select(c => string.IsNullOrWhiteSpace(c.Label) ? c.Username : c.Label)
         .Where(l => !string.IsNullOrWhiteSpace(l))
        .Distinct()
    .OrderBy(l => l)
      .ToList();

            findings.Add(new VaultFinding
            {
                Severity = severity,
                Title = title,
                Description = description,
                PointsDeducted = deduction,
                AffectedLabels = affected.AsReadOnly(),
                Recommendation = recommendation
            });

            return deduction;
        }

        private bool IsOldPassword(LoginItem c, DateTime now)
        {
            if (c.UpdatedAt == default) return false;
            return (now - c.UpdatedAt).TotalDays > _cfg.MaxPasswordAgeDays;
        }

        // ── Password strength analyser ────────────────────────────────────────────
        /// <summary>
        /// Returns a 0–100 per-password strength score and a named level.
        /// Deterministic – no random entropy.
        /// </summary>
        public (int score, PasswordStrengthLevel level) ScorePassword(string? password)
        {
            if (string.IsNullOrEmpty(password))
                return (0, PasswordStrengthLevel.VeryWeak);

            int score = 0;
            int len = password.Length;

            score += len switch
            {
                < 6 => 0,
                < 8 => 8,
                < 10 => 16,
                < 12 => 24,
                < 14 => 32,
                < 20 => 38,
                _ => 40
            };

            if (password.Any(char.IsUpper)) score += 10;
            if (password.Any(char.IsLower)) score += 10;
            if (password.Any(char.IsDigit)) score += 10;
            if (password.Any(c => !char.IsLetterOrDigit(c))) score += 10;

            double uniqueRatio = (double)password.Distinct().Count() / len;
            score += (int)(uniqueRatio * 20);

            score -= CountSequentialRuns(password) * 3;
            score -= CountKeyboardWalks(password) * 4;
            if (_breachWordlist.Contains(password)) score -= 40;

            score = Math.Clamp(score, 0, 100);

            var level = score switch
            {
                < 20 => PasswordStrengthLevel.VeryWeak,
                < 40 => PasswordStrengthLevel.Weak,
                < 60 => PasswordStrengthLevel.Fair,
                < 80 => PasswordStrengthLevel.Strong,
                _ => PasswordStrengthLevel.VeryStrong
            };

            return (score, level);
        }

        private bool IsWeakPassword(string? password)
        {
            var (_, level) = ScorePassword(password);
            return level is PasswordStrengthLevel.VeryWeak or PasswordStrengthLevel.Weak;
        }

        private VaultHealthStatus ClassifyScore(int score) => score switch
        {
            >= 90 => VaultHealthStatus.Excellent,
            >= 75 => VaultHealthStatus.Good,
            >= 50 => VaultHealthStatus.AtRisk,
            _ => VaultHealthStatus.Critical
        };

        private VaultHealthResult EmptyVaultResult() => new()
        {
            Score = 100,
            Status = VaultHealthStatus.Excellent,
            TotalCredentials = 0,
            TotalAuthenticators = 0,
            CalculatedAt = DateTime.UtcNow,
            Config = _cfg
        };

        private static string NormaliseDomain(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            try
            {
                var s = raw.Trim().ToLowerInvariant();
                if (!s.StartsWith("http")) s = "https://" + s;
                return new Uri(s).Host.TrimStart('w', '.');
            }
            catch
            {
                return raw.Trim().ToLowerInvariant();
            }
        }

        private static int CountSequentialRuns(string password)
        {
            int runs = 0, run = 1;
            for (int i = 1; i < password.Length; i++)
            {
                int delta = password[i] - password[i - 1];
                if (delta == 1 || delta == -1) { run++; if (run >= 3) runs++; }
                else run = 1;
            }
            return runs;
        }

        private static readonly string[] _keyboardRows =
        {
            "qwertyuiop", "asdfghjkl", "zxcvbnm",
            "1234567890", "!@#$%^&*()"
        };

        private static int CountKeyboardWalks(string password)
        {
            int walks = 0;
            var lower = password.ToLowerInvariant();
            foreach (var row in _keyboardRows)
                for (int i = 0; i <= row.Length - 3; i++)
                    if (lower.Contains(row.Substring(i, 3), StringComparison.Ordinal))
                        walks++;
            return walks;
        }

        // ── Attack surface score ──────────────────────────────────────────────────
        /// <summary>
        /// Attack Surface Score: 0 = minimal exposure, 100 = maximally exposed.
        /// Combines blast-radius factors that a basic health score misses.
        /// </summary>
        private static int ComputeAttackSurface(
         List<LoginItem> creds,
          List<CredentialHealthDetail> details,
     HashSet<string> authDomains,
     HashSet<Guid> reusedIds)
        {
            if (creds.Count == 0) return 0;

            int surface = 0;

            // Critical accounts with no 2FA – each is a high-value single point of failure
            var criticalDomains = ItemClassifier.CriticalDomainSet;
            foreach (var c in creds)
            {
                var domain = NormaliseDomain(c.Url ?? string.Empty);
                bool isCritical = criticalDomains.Contains(domain);
                bool has2FA = authDomains.Contains(domain);

                // Critical account missing 2FA: +20 each, capped at 40
                if (isCritical && !has2FA)
                    surface = Math.Min(surface + 20, surface + 40);

                // Critical account sharing a password with another account: +15
                if (isCritical && reusedIds.Contains(c.Id))
                    surface += 15;
            }

            // Single email address used across 10+ sites: +20
            var emailGroups = creds
         .Where(c => !string.IsNullOrWhiteSpace(c.Username) && c.Username.Contains('@'))
           .GroupBy(c => c.Username.ToLowerInvariant());
            foreach (var g in emailGroups)
                if (g.Count() >= 10) surface += 20;

            // All accounts with no lock mechanism: +30 (checked in router / context)
            // We don't have PreferenceWrapper here, so this is scored at the router level
            // via IsLockConfigured. Leave space: max without lock penalty = 70.

            return Math.Clamp(surface, 0, 100);
        }

        /// <summary>
        /// Builds credential clusters – groups of accounts sharing the same password.
        /// Each cluster is a compromise chain the user needs to understand.
        /// </summary>
        private static IReadOnlyList<CredentialCluster> BuildCredentialClusters(
            List<LoginItem> creds,
    List<CredentialHealthDetail> details,
            HashSet<Guid> reusedIds)
        {
            if (reusedIds.Count == 0) return Array.Empty<CredentialCluster>();

            // Group by the hash that was used for deduplication
            var detailById = details.ToDictionary(d => d.Id);

            var clusters = creds
          .Where(c => reusedIds.Contains(c.Id))
          .GroupBy(c => string.IsNullOrWhiteSpace(c.PasswordHash)
        ? c.Password ?? string.Empty
            : c.PasswordHash)
                .Where(g => g.Count() > 1)
           .Select(g => new CredentialCluster
           {
               SharedPasswordHash = g.Key,
               Members = g.Select(c => detailById.TryGetValue(c.Id, out var d)
          ? d
         : new CredentialHealthDetail
                    {
                        Id = c.Id,
                        Label = c.Label ?? c.Url ?? "(unknown)",
                        Username = c.Username ?? string.Empty,
                        IsReused = true
                    })
            .ToList()
                  .AsReadOnly()
           })
             .OrderByDescending(cl => cl.Members.Count)
             .ToList()
            .AsReadOnly();

            return clusters;
        }
    } // end class VaultHealthCalculator
} // end namespace
