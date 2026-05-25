using Fortress.Core.Intelligence;
using Fortress.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fortress.Core.Services
{
    public sealed class VaultHealthCalculator
    {
  private readonly VaultHealthConfig _cfg;
     private readonly PasswordAnomalyDetector? _anomaly;
   private readonly ILogger<VaultHealthCalculator>? _logger;

   private static readonly HashSet<string> _breachWordlist = new(StringComparer.OrdinalIgnoreCase)
 {
   "123456","password","123456789","12345678","12345","1234567","1234567890",
   "qwerty","abc123","111111","123123","admin","letmein","welcome","monkey",
   "dragon","master","login","pass","1111","sunshine","princess","password1",
    "iloveyou","football","shadow","superman","michael","jessica","charlie",
      "donald","trustno1","batman","access","hello","whatever","696969",
    "mustang","test","temp","guest","changeme","qwerty123","q1w2e3r4",
    "zaq12wsx","asdfgh","passw0rd","p@ssword","p@ss","pa$$word",
  };

       public VaultHealthCalculator(
    VaultHealthConfig? config = null,
  PasswordAnomalyDetector? anomalyDetector = null,
     ILogger<VaultHealthCalculator>? logger = null)
  {
   _cfg = config ?? new VaultHealthConfig();
_anomaly = anomalyDetector;
   _logger = logger;
      }

  public VaultHealthResult Calculate(
    IEnumerable<LoginItem> credentials,
   IEnumerable<Authenticator>? authenticators = null,
     DateTime? referenceDate = null)
        {
     var creds = credentials?.ToList() ?? [];
  var auths  = authenticators?.ToList() ?? [];
  var now    = referenceDate ?? DateTime.UtcNow;

 _logger?.LogInformation("VaultHealthCalculator: analysing {N} credentials", creds.Count);
  if (creds.Count == 0) return EmptyResult();

   var authDomains = auths
 .Where(a => !string.IsNullOrWhiteSpace(a.Issuer))
    .Select(a => NormaliseDomain(a.Issuer!))
  .ToHashSet(StringComparer.OrdinalIgnoreCase);

  var reusedIds = creds
 .Where(c => !string.IsNullOrWhiteSpace(c.PasswordHash)
    ? true : !string.IsNullOrWhiteSpace(c.Password))
  .GroupBy(c => !string.IsNullOrWhiteSpace(c.PasswordHash) ? c.PasswordHash! : c.Password!)
   .Where(g => g.Count() > 1)
     .SelectMany(g => g.Select(c => c.Id))
   .ToHashSet();

   var details = creds.Select(c => BuildDetail(c, reusedIds, authDomains, now)).ToList();

  var credById = creds.ToDictionary(c => c.Id);
  int weakCount    = details.Count(d => d.IsWeak);
  int reusedCount  = details.Count(d => d.IsReused);
  int oldCount     = details.Count(d => d.IsOld);
  int miss2fa      = details.Count(d => !d.HasTwoFactor);
  int breached     = details.Count(d => _breachWordlist.Contains(credById[d.Id].Password ?? ""));
  int empty      = creds.Count(c => string.IsNullOrWhiteSpace(c.Password));
  int userAsPass   = creds.Count(c =>
   !string.IsNullOrWhiteSpace(c.Password) && !string.IsNullOrWhiteSpace(c.Username) &&
    string.Equals(c.Password, c.Username, StringComparison.OrdinalIgnoreCase));

   int score = 100;
   var findings = new List<VaultFinding>();

  score -= Deduct(findings, creds, c => _breachWordlist.Contains(c.Password ?? ""),
 FindingSeverity.Critical, "Breached Passwords",
   "These passwords appear in public data-breach databases.",
   "Change immediately to unique strong passwords.",
    _cfg.DeductionPerBreachedAccount);

  score -= Deduct(findings, creds, c => string.IsNullOrWhiteSpace(c.Password),
   FindingSeverity.Critical, "Empty Passwords",
  "Accounts with no password are completely unprotected.",
      "Set a strong unique password.",
   _cfg.DeductionPerEmptyPassword);

  score -= Deduct(findings, creds,
    c => !string.IsNullOrWhiteSpace(c.Password) && !string.IsNullOrWhiteSpace(c.Username) &&
string.Equals(c.Password, c.Username, StringComparison.OrdinalIgnoreCase),
    FindingSeverity.High, "Username Used as Password",
 "Using username as password is one of the first things attackers try.",
  "Replace with randomly generated unique passwords.",
    _cfg.DeductionPerUsernameAsPassword);

     score -= Deduct(findings, creds, c => reusedIds.Contains(c.Id),
      FindingSeverity.High, "Reused Passwords",
  "If one site is breached, all accounts sharing the same password are compromised.",
   "Generate a unique password for each account.",
   _cfg.DeductionPerReusedPassword);

    score -= Deduct(findings, creds,
  c => c.PasswordStrengthScore > 0
   ? (PasswordStrengthLevel)c.PasswordStrengthLevel is PasswordStrengthLevel.VeryWeak or PasswordStrengthLevel.Weak
    : IsWeakPassword(c.Password),
  FindingSeverity.Medium, "Weak Passwords",
"Short or simple passwords are easily cracked.",
   "Use 14+ characters with uppercase, numbers and symbols.",
   _cfg.DeductionPerWeakPassword);

  score -= Deduct(findings, creds,
    c => !authDomains.Contains(NormaliseDomain(c.Url ?? "")),
   FindingSeverity.Medium, "No Two-Factor Authentication",
  "Accounts without 2FA are vulnerable even with strong passwords.",
  "Enable an authenticator app on these accounts.",
  _cfg.DeductionPerMissing2FA);

   score -= Deduct(findings, creds, c => IsOldPassword(c, now),
     FindingSeverity.Low, "Old Passwords",
  $"Passwords unchanged for over {_cfg.MaxPasswordAgeDays} days.",
  "Review and rotate old passwords annually.",
  _cfg.DeductionPerOldPassword);

  // ML anomaly detection
      if (_anomaly != null)
   {
    var anomalyIds = _anomaly.DetectAnomalies(creds);
  if (anomalyIds.Count > 0)
         score -= Deduct(findings, creds, c => anomalyIds.Contains(c.Id),
       FindingSeverity.Medium, "Outlier Weak Passwords",
   "These passwords are significantly weaker than the rest of your vault.",
       "Bring these up to the same strength as the rest of your vault.",
     _cfg.DeductionPerWeakPassword);
    }

  score = Math.Clamp(score, 0, 100);
  var status = ClassifyScore(score);

  return new VaultHealthResult
   {
    Score = score, Status = status,
  TotalCredentials = creds.Count, TotalAuthenticators = auths.Count,
     WeakPasswordsCount = weakCount, ReusedPasswordsCount = reusedCount,
    OldPasswordsCount = oldCount, Missing2FACount = miss2fa,
    BreachedCount = breached, EmptyPasswordCount = empty,
   UsernameAsPasswordCount = userAsPass,
 AttackSurfaceScore = ComputeAttackSurface(creds, details, authDomains, reusedIds),
     CredentialClusters = BuildClusters(creds, details, reusedIds),
   Details = details.AsReadOnly(),
 Findings = findings.OrderByDescending(f => f.Severity).ThenByDescending(f => f.PointsDeducted).ToList().AsReadOnly(),
   CalculatedAt = now, Config = _cfg,
 };
   }

  // ── Strength scoring ──────────────────────────────────────────────────────
  public (int score, PasswordStrengthLevel level) ScorePassword(string? password)
  {
    if (string.IsNullOrEmpty(password)) return (0, PasswordStrengthLevel.VeryWeak);
  int s = 0; int len = password.Length;
    s += len switch { < 6 => 0, < 8 => 8, < 10 => 16, < 12 => 24, < 14 => 32, < 20 => 38, _ => 40 };
  if (password.Any(char.IsUpper)) s += 10;
   if (password.Any(char.IsLower)) s += 10;
  if (password.Any(char.IsDigit)) s += 10;
     if (password.Any(c => !char.IsLetterOrDigit(c))) s += 10;
    s += (int)((double)password.Distinct().Count() / len * 20);
   s -= CountSequentialRuns(password) * 3;
   s -= CountKeyboardWalks(password) * 4;
    if (_breachWordlist.Contains(password)) s -= 40;
    s = Math.Clamp(s, 0, 100);
     var level = s switch { < 20 => PasswordStrengthLevel.VeryWeak, < 40 => PasswordStrengthLevel.Weak, < 60 => PasswordStrengthLevel.Fair, < 80 => PasswordStrengthLevel.Strong, _ => PasswordStrengthLevel.VeryStrong };
   return (s, level);
  }

  private bool IsWeakPassword(string? pw)
   { var (_, l) = ScorePassword(pw); return l is PasswordStrengthLevel.VeryWeak or PasswordStrengthLevel.Weak; }

  // ── Helpers ───────────────────────────────────────────────────────────────
  private CredentialHealthDetail BuildDetail(LoginItem c, HashSet<Guid> reusedIds, HashSet<string> authDomains, DateTime now)
  {
    int ss; PasswordStrengthLevel sl;
 if (c.PasswordStrengthScore > 0) { ss = c.PasswordStrengthScore; sl = (PasswordStrengthLevel)c.PasswordStrengthLevel; }
   else (ss, sl) = ScorePassword(c.Password);
   return new CredentialHealthDetail
    {
  Id = c.Id, Label = c.Label ?? c.Url ?? "(unknown)", Username = c.Username ?? string.Empty,
   IsWeak = sl is PasswordStrengthLevel.VeryWeak or PasswordStrengthLevel.Weak,
  IsReused = reusedIds.Contains(c.Id), IsOld = IsOldPassword(c, now),
   HasTwoFactor = authDomains.Contains(NormaliseDomain(c.Url ?? "")),
 PasswordStrengthScore = ss, StrengthLevel = sl,
     };
  }

  private int Deduct(List<VaultFinding> findings, List<LoginItem> creds,
  Func<LoginItem, bool> predicate, FindingSeverity severity,
    string title, string desc, string rec, int pointsEach)
  {
   int count = creds.Count(predicate);
   if (count == 0) return 0;
  var deduction = Math.Min(count * pointsEach, _cfg.MaxDeductionPerCategory);
  var affected = creds.Where(predicate)
  .Select(c => string.IsNullOrWhiteSpace(c.Label) ? c.Username : c.Label)
   .Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().OrderBy(l => l).ToList();
  findings.Add(new VaultFinding { Severity = severity, Title = title, Description = desc,
    PointsDeducted = deduction, AffectedLabels = affected.AsReadOnly(), Recommendation = rec });
   return deduction;
 }

  private bool IsOldPassword(LoginItem c, DateTime now) =>
   c.UpdatedAt != default && (now - c.UpdatedAt).TotalDays > _cfg.MaxPasswordAgeDays;

   private static VaultHealthStatus ClassifyScore(int s) =>
  s >= 90 ? VaultHealthStatus.Excellent : s >= 75 ? VaultHealthStatus.Good :
   s >= 50 ? VaultHealthStatus.AtRisk : VaultHealthStatus.Critical;

  private VaultHealthResult EmptyResult() => new() { Score = 100, Status = VaultHealthStatus.Excellent, CalculatedAt = DateTime.UtcNow, Config = _cfg };

  private static string NormaliseDomain(string raw)
   {
   if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
     try { var s = raw.Trim().ToLowerInvariant(); if (!s.StartsWith("http")) s = "https://" + s; return new Uri(s).Host.TrimStart('w', '.'); }
  catch { return raw.Trim().ToLowerInvariant(); }
    }

   private static int ComputeAttackSurface(List<LoginItem> creds, List<CredentialHealthDetail> details, HashSet<string> authDomains, HashSet<Guid> reusedIds)
    {
   if (creds.Count == 0) return 0;
  int surface = 0;
   foreach (var c in creds)
  {
  var domain = NormaliseDomain(c.Url ?? "");
   bool isCritical = ItemClassifier.CriticalDomainSet.Contains(domain);
   if (isCritical && !authDomains.Contains(domain)) surface = Math.Min(surface + 20, surface + 40);
   if (isCritical && reusedIds.Contains(c.Id)) surface += 15;
    }
    foreach (var g in creds.Where(c => !string.IsNullOrWhiteSpace(c.Username) && c.Username.Contains('@'))
    .GroupBy(c => c.Username!.ToLowerInvariant()))
  if (g.Count() >= 10) surface += 20;
   return Math.Clamp(surface, 0, 100);
     }

   private static IReadOnlyList<CredentialCluster> BuildClusters(List<LoginItem> creds, List<CredentialHealthDetail> details, HashSet<Guid> reusedIds)
{
    if (reusedIds.Count == 0) return Array.Empty<CredentialCluster>();
  var detailById = details.ToDictionary(d => d.Id);
   return creds.Where(c => reusedIds.Contains(c.Id))
    .GroupBy(c => string.IsNullOrWhiteSpace(c.PasswordHash) ? c.Password ?? "" : c.PasswordHash)
  .Where(g => g.Count() > 1)
    .Select(g => new CredentialCluster
      {
   SharedPasswordHash = g.Key,
  Members = g.Select(c => detailById.TryGetValue(c.Id, out var d) ? d : new CredentialHealthDetail { Id = c.Id, Label = c.Label ?? c.Url ?? "(unknown)", IsReused = true }).ToList().AsReadOnly(),
   }).OrderByDescending(cl => cl.Members.Count).ToList().AsReadOnly();
    }

   private static int CountSequentialRuns(string pw)
    {
    int runs = 0, run = 1;
    for (int i = 1; i < pw.Length; i++) { int d = pw[i] - pw[i - 1]; if (d == 1 || d == -1) { run++; if (run >= 3) runs++; } else run = 1; }
   return runs;
  }

   private static readonly string[] _kbRows = ["qwertyuiop","asdfghjkl","zxcvbnm","1234567890"];
   private static int CountKeyboardWalks(string pw)
  {
    int w = 0; var l = pw.ToLowerInvariant();
  foreach (var row in _kbRows) for (int i = 0; i <= row.Length - 3; i++) if (l.Contains(row.Substring(i, 3))) w++;
   return w;
   }
    }
}
