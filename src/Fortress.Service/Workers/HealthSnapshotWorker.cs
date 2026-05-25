using Fortress.Core.Contracts;
using Fortress.Core.Models;
using Fortress.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fortress.Service.Workers
{
    /// <summary>
    /// Runs once per day at 03:00 local time.
    /// Calculates the vault health score and persists a <see cref="VaultHealthSnapshot"/>
    /// to LiteDB so the trend sparkline has daily data points.
    /// Only runs while the vault is unlocked (skip silently if locked).
    /// </summary>
    public sealed class HealthSnapshotWorker : BackgroundService
  {
   private readonly IServiceProvider _services;
  private readonly ILogger<HealthSnapshotWorker> _logger;

     public HealthSnapshotWorker(IServiceProvider services, ILogger<HealthSnapshotWorker> logger)
        {
 _services = services;
     _logger = logger;
  }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
     {
     _logger.LogInformation("[HealthSnapshot] Worker started.");

    while (!stoppingToken.IsCancellationRequested)
     {
   var now   = DateTime.Now;
   var nextRun = DateTime.Today.AddHours(3);          // 03:00 today
   if (now >= nextRun) nextRun = nextRun.AddDays(1);  // already past — tomorrow

  var delay = nextRun - now;
  _logger.LogDebug("[HealthSnapshot] Next run in {H:0.0}h", delay.TotalHours);

   await Task.Delay(delay, stoppingToken);
     if (stoppingToken.IsCancellationRequested) break;

   await RunSnapshotAsync(stoppingToken);
   }
     }

    private async Task RunSnapshotAsync(CancellationToken ct)
  {
   try
  {
   using var scope = _services.CreateScope();
 var session = scope.ServiceProvider.GetRequiredService<Infrastructure.VaultSessionService>();

    if (!session.IsUnlocked)
    {
       _logger.LogDebug("[HealthSnapshot] Vault locked — skipping.");
       return;
      }

   var storage    = scope.ServiceProvider.GetRequiredService<IDataStorageService>();
    var calculator = scope.ServiceProvider.GetRequiredService<VaultHealthCalculator>();

     var logins = (await storage.GetLoginItemsAsync()).ToList();
    var auths  = (await storage.GetAuthenticatorsAsync()).ToList();

    var result = calculator.Calculate(logins, auths);
     var snapshot = new VaultHealthSnapshot
   {
   Id        = Guid.NewGuid(),
     RecordedDate  = DateTime.UtcNow.Date,
 Score          = result.Score,
     Status         = result.Status,
    WeakCount      = result.WeakPasswordsCount,
    ReusedCount    = result.ReusedPasswordsCount,
    BreachedCount  = result.BreachedCount,
    Missing2FACount = result.Missing2FACount,
    TotalCredentials = result.TotalCredentials,
     AttackSurfaceScore = result.AttackSurfaceScore,
   };

    await storage.SaveHealthSnapshotAsync(snapshot);
    _logger.LogInformation("[HealthSnapshot] Snapshot saved — Score={S} Status={T}", result.Score, result.Status);
        }
   catch (Exception ex)
   {
  _logger.LogError(ex, "[HealthSnapshot] Failed to save health snapshot.");
 }
    }
  }
}
