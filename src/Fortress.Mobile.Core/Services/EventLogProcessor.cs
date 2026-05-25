using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;
using Shiny.Jobs;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Fortress.Mobile.Core.Services
{
    public sealed class EventLogProcessor : IJob, IEventLogProcessor
    {
     private readonly IDataStorageService _dataStorageService;
        private readonly ILogger<EventLogProcessor> _logger;
        private readonly ISharedCredentialWriter? _sharedCredentialWriter;

    public EventLogProcessor(
      IDataStorageService dataStorageService,
            ILogger<EventLogProcessor> logger,
  ISharedCredentialWriter? sharedCredentialWriter = null)
        {
        _dataStorageService = dataStorageService;
    _logger = logger;
     _sharedCredentialWriter = sharedCredentialWriter;
        }

   // ── IJob (background housekeeping — trim old logs) ────────────────────
        public async Task Run(JobInfo jobInfo, CancellationToken cancelToken)
        {
   try
   {
    var all = (await _dataStorageService.GetEventLogsAsync()).ToList();
          // Keep only the last 500 entries; discard anything older than 90 days
 var cutoff = DateTime.UtcNow.AddDays(-90);
    var toDelete = all
      .Where(l => l.DateTime < cutoff)
           .ToList();

  if (toDelete.Count > 0)
         {
      _logger.LogInformation("[ActivityLog] Trimming {Count} old log entries", toDelete.Count);
     await _dataStorageService.DeleteEventLogsAsync();
 // Re-insert the ones we want to keep
   var keep = all.Except(toDelete).ToList();
    if (keep.Count > 0)
            await _dataStorageService.AddEventLogsAsync(keep);
  }
   }
            catch (Exception ex)
       {
  _logger.LogError(ex, "[ActivityLog] Background trim failed");
   }
     }

   // ── IEventLogProcessor ────────────────────────────────────────────────

public async Task ProcessEventLogAsync(EventLog eventLog)
        {
      try
    {
   await _dataStorageService.AddEventLogsAsync([eventLog]);

#if IOS
          if (_sharedCredentialWriter != null)
  await _sharedCredentialWriter.ProcessPendingUsageEventsAsync();
#endif
      }
      catch (Exception ex)
            {
      _logger.LogError(ex, "[ActivityLog] Failed to persist event {EventType}", eventLog.EventType);
   }
        }

 public async Task<IEnumerable<AuditLog>> GetLocalLogsAsync(
   List<int>? eventTypes,
      DateTime startDate,
      DateTime endDate,
            int recordCount = 100)
     {
   try
       {
  var all = await _dataStorageService.GetEventLogsAsync();

    var query = all.AsEnumerable();

     // Filter by type if a non-empty filter list was supplied
                if (eventTypes?.Count > 0)
         query = query.Where(l => eventTypes.Contains(l.EventType));

    // Date range
          query = query
       .Where(l => l.DateTime >= startDate && l.DateTime <= endDate);

    // Newest first, then page
   var paged = query
.OrderByDescending(l => l.DateTime)
     .Take(recordCount);

     return paged.Select(ToAuditLog).ToList();
  }
            catch (Exception ex)
       {
_logger.LogError(ex, "[ActivityLog] GetLocalLogsAsync failed");
     return [];
   }
  }

   public async Task ClearLocalLogsAsync()
        {
            try
    {
        await _dataStorageService.DeleteEventLogsAsync();
 }
            catch (Exception ex)
   {
    _logger.LogError(ex, "[ActivityLog] ClearLocalLogsAsync failed");
    }
        }

  // ── Mapping helper ────────────────────────────────────────────────────

        private static AuditLog ToAuditLog(EventLog log)
        {
   var typeId = log.EventType;
  var displayName = GetDisplayName((EventLogType)typeId);
  var localTime = log.DateTime.ToLocalTime();

    return new AuditLog
     {
  DateTimeRaw = log.DateTime,
            DateTime = localTime.ToString("dd MMM yyyy, HH:mm"),
     EventType = displayName,
    EventTypeId = typeId,
      CredentialLabel = log.CredentialLabel,
   Detail = log.Detail,
  };
  }

     private static string GetDisplayName(EventLogType type)
        {
  var member = typeof(EventLogType).GetMember(type.ToString()).FirstOrDefault();
 var attr = member?.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? type.ToString();
        }
    }
}
