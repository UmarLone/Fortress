using Fortress.Core.Contracts;
using Fortress.Core.Models;
using Fortress.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fortress.Service.Workers
{
    /// <summary>
    /// Trims the event log to a rolling window (default 90 days / 5 000 rows).
    /// Runs once per day at 04:00 local time.
    /// </summary>
    public sealed class EventLogTrimWorker : BackgroundService
  {
     private readonly IServiceProvider _services;
   private readonly ILogger<EventLogTrimWorker> _logger;
  private const int MaxDays = 90;

   public EventLogTrimWorker(IServiceProvider services, ILogger<EventLogTrimWorker> logger)
     { _services = services; _logger = logger; }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
    while (!stoppingToken.IsCancellationRequested)
       {
     var now    = DateTime.Now;
   var nextRun = DateTime.Today.AddHours(4);
   if (now >= nextRun) nextRun = nextRun.AddDays(1);
     await Task.Delay(nextRun - now, stoppingToken);
      if (stoppingToken.IsCancellationRequested) break;

   try
    {
  using var scope = _services.CreateScope();
  var storage = scope.ServiceProvider.GetRequiredService<IDataStorageService>();
     var logs    = (await storage.GetEventLogsAsync()).ToList();
  var cutoff  = DateTime.UtcNow.AddDays(-MaxDays);
    var stale   = logs.Where(l => l.DateTime < cutoff).ToList();
   if (stale.Count > 0)
{
    // Batch-delete by clearing and re-inserting the remaining rows
    var keep = logs.Except(stale).ToList();
 await storage.DeleteEventLogsAsync();
      if (keep.Count > 0)
   await storage.AddEventLogsAsync(keep);
    _logger.LogInformation("[EventLogTrim] Removed {N} stale log entries.", stale.Count);
 }
     }
    catch (Exception ex) { _logger.LogError(ex, "[EventLogTrim] Trim failed."); }
    }
  }
}
}
