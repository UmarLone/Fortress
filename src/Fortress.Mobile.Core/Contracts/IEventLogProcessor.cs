using Fortress.Mobile.Core.Models;
 

namespace Fortress.Mobile.Core.Contracts
{
    public interface IEventLogProcessor
    {
        /// <summary>Records a single vault activity event to local storage.</summary>
        Task ProcessEventLogAsync(EventLog eventLog);

        /// <summary>
     /// Returns activity log entries filtered by event types, date range and page size.
        /// </summary>
        Task<IEnumerable<AuditLog>> GetLocalLogsAsync(
      List<int>? eventTypes,
   DateTime startDate,
            DateTime endDate,
    int recordCount = 100);

        /// <summary>Permanently deletes all locally stored activity logs.</summary>
        Task ClearLocalLogsAsync();
    }
}
