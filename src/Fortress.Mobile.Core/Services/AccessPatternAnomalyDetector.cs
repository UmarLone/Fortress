using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Detects unusual vault access patterns using ML.NET IID (Independent and
    /// Identically Distributed) spike + change-point detection.
    ///
    /// Two signals are monitored:
    ///   1. Access frequency – sudden spike in how often a credential is accessed
    ///   2. Access time-of-day – credential accessed at an unusual hour
///
    /// Complements <see cref="PasswordAnomalyDetector"/> (which looks at password
    /// strength outliers) by looking at *behaviour* rather than *content*.
    ///
    /// 100% on-device, no network, deterministic given the same event log.
    /// </summary>
    public sealed class AccessPatternAnomalyDetector
    {
  private static readonly MLContext _ml = new(seed: 0);
  private readonly ILogger<AccessPatternAnomalyDetector>? _logger;

   private const int MinEvents       = 10;   // minimum log entries to analyse
  private const double ConfidenceIID = 95.0; // IID spike confidence %
  private const double ConfidenceSSA = 95.0; // SSA change-point confidence %

        public AccessPatternAnomalyDetector(ILogger<AccessPatternAnomalyDetector>? logger = null)
   {
       _logger = logger;
        }

     // ── Public API ────────────────────────────────────────────────────────────
        /// <summary>
        /// Analyses the vault access log and returns IDs of credentials that show
     /// suspicious access patterns (sudden spikes or behaviour changes).
        /// </summary>
        public AccessAnomalyReport Analyse(IList<VaultAccessEvent> events)
        {
    var report = new AccessAnomalyReport();

     if (events.Count < MinEvents)
  {
    _logger?.LogDebug(
          "AccessPatternAnomalyDetector: only {N} events – skipping (min={M})",
    events.Count, MinEvents);
    return report;
        }

  try
   {
     report.FrequencySpikes = DetectFrequencySpikes(events);
       report.TimeOfDayAnomalies = DetectTimeOfDayAnomalies(events);
       report.ChangePoints = DetectAccessChangePoints(events);

  _logger?.LogInformation(
      "AccessPatternAnomalyDetector: spikes={S} tod={T} changes={C}",
     report.FrequencySpikes.Count,
     report.TimeOfDayAnomalies.Count,
      report.ChangePoints.Count);
      }
            catch (Exception ex)
   {
  _logger?.LogWarning(ex, "AccessPatternAnomalyDetector: analysis failed");
        }

  return report;
     }

   // ── Spike detection ───────────────────────────────────────────────────────
        /// <summary>
/// IID Spike: finds credentials whose daily access count suddenly surged.
        /// Useful for detecting a compromised account being harvested.
        /// </summary>
  private List<Guid> DetectFrequencySpikes(IList<VaultAccessEvent> events)
  {
  var anomalies = new HashSet<Guid>();

  // Build a per-credential daily access time-series
  var byCredential = events
   .GroupBy(e => e.CredentialId)
   .Where(g => g.Count() >= 5); // need at least 5 data points

  foreach (var group in byCredential)
   {
     // Bucket by day ? count per day
     var dailyCounts = group
     .GroupBy(e => e.AccessedAt.Date)
       .OrderBy(d => d.Key)
  .Select(d => new CountRow { Count = d.Count() })
       .ToList();

if (dailyCounts.Count < 4) continue;

   var data = _ml.Data.LoadFromEnumerable(dailyCounts);

     var pipeline = _ml.Transforms.DetectIidSpike(
       outputColumnName: "Prediction",
   inputColumnName: nameof(CountRow.Count),
 confidence: ConfidenceIID,
  pvalueHistoryLength: Math.Max(2, dailyCounts.Count / 2));

       var transformed = pipeline.Fit(data).Transform(data);
       var predictions = _ml.Data
 .CreateEnumerable<SpikePrediction>(transformed, reuseRowObject: false)
     .ToList();

            // A spike at the *last* data point is the most actionable
 var last = predictions.LastOrDefault();
  if (last?.Prediction is { Length: >= 1 } && last.Prediction[0] == 1)
    {
      anomalies.Add(group.Key);
        _logger?.LogDebug(
  "AccessPatternAnomalyDetector: frequency spike on {Id}", group.Key);
     }
        }

    return anomalies.ToList();
  }

  /// <summary>
 /// Time-of-day anomaly: credential accessed outside its normal window.
     /// E.g. a work login accessed at 3 AM is suspicious.
        /// </summary>
        private List<Guid> DetectTimeOfDayAnomalies(IList<VaultAccessEvent> events)
  {
   var anomalies = new List<Guid>();

     var byCredential = events
 .GroupBy(e => e.CredentialId)
 .Where(g => g.Count() >= 8);

  foreach (var group in byCredential)
 {
      var hours = group.Select(e => (double)e.AccessedAt.Hour).ToList();
      if (hours.Count < 4) continue;

  double mean   = hours.Average();
  double stdDev = Math.Sqrt(hours.Average(h => (h - mean) * (h - mean)));
  if (stdDev < 1.0) continue; // no variance – can't judge

        // Flag the most recent access if it is 2.5 ? away from mean
  var latest = group.OrderByDescending(e => e.AccessedAt).First();
  double z = Math.Abs(latest.AccessedAt.Hour - mean) / stdDev;

     if (z >= 2.5)
   {
     anomalies.Add(group.Key);
    _logger?.LogDebug(
       "AccessPatternAnomalyDetector: time-of-day anomaly on {Id} z={Z:F1}",
            group.Key, z);
       }
       }

   return anomalies;
  }

        /// <summary>
   /// SSA change-point: detects a sustained shift in access frequency
        /// (i.e. the pattern changed permanently, not just a one-off spike).
        /// </summary>
     private List<Guid> DetectAccessChangePoints(IList<VaultAccessEvent> events)
  {
   var changePoints = new List<Guid>();

  // Overall daily vault access volume (all credentials combined)
   var dailyTotals = events
       .GroupBy(e => e.AccessedAt.Date)
        .OrderBy(d => d.Key)
     .Select(d => new CountRow { Count = d.Count() })
   .ToList();

   if (dailyTotals.Count < 8) return changePoints;

    var data = _ml.Data.LoadFromEnumerable(dailyTotals);

    int window   = Math.Max(4, dailyTotals.Count / 4);
    int seasonal = Math.Max(2, window / 2);

   var pipeline = _ml.Transforms.DetectChangePointBySsa(
  outputColumnName: "Prediction",
       inputColumnName: nameof(CountRow.Count),
         confidence:  ConfidenceSSA,
 changeHistoryLength: window,
      trainingWindowSize: dailyTotals.Count,
   seasonalityWindowSize: seasonal);

   var transformed = pipeline.Fit(data).Transform(data);

   var predictions = _ml.Data
  .CreateEnumerable<SpikePrediction>(transformed, reuseRowObject: false)
  .ToList();

   // Collect credentials with events after the last detected change point
  int lastChangeIdx = -1;
  for (int i = predictions.Count - 1; i >= 0; i--)
  {
      if (predictions[i].Prediction is { Length: >= 1 } && predictions[i].Prediction[0] == 1)
     {
      lastChangeIdx = i;
       break;
      }
        }

   if (lastChangeIdx >= 0)
  {
      var changeDate = events
        .GroupBy(e => e.AccessedAt.Date)
      .OrderBy(d => d.Key)
             .ElementAt(lastChangeIdx).Key;

       // Flag credentials that were first accessed after the change point
      changePoints = events
      .Where(e => e.AccessedAt.Date >= changeDate)
      .Select(e => e.CredentialId)
     .Distinct()
      .ToList();

         _logger?.LogDebug(
         "AccessPatternAnomalyDetector: change point at {Date}, {N} credentials",
    changeDate.ToShortDateString(), changePoints.Count);
        }

   return changePoints;
  }

      // ── ML.NET data types ─────────────────────────────────────────────────────
      private sealed class CountRow
        {
     [ColumnName("Count")]
   public float Count { get; set; }
   }

   private sealed class SpikePrediction
   {
    [ColumnName("Prediction")]
  public double[] Prediction { get; set; } = [];
   }
    }

    // ── Supporting types ──────────────────────────────────────────────────────────
    /// <summary>A single vault access event used as input to the anomaly detector.</summary>
  public sealed class VaultAccessEvent
    {
   public Guid CredentialId { get; init; }
     public DateTime AccessedAt { get; init; }
   public string Action { get; init; } = string.Empty; // "view" | "copy" | "autofill"
    }

    /// <summary>Combined output of the access pattern analysis.</summary>
    public sealed class AccessAnomalyReport
    {
   /// <summary>Credential IDs with a sudden access frequency spike.</summary>
  public List<Guid> FrequencySpikes { get; set; } = new();
  /// <summary>Credential IDs accessed at an unusual time of day.</summary>
    public List<Guid> TimeOfDayAnomalies { get; set; } = new();
  /// <summary>Credential IDs first accessed after a detected change point.</summary>
    public List<Guid> ChangePoints { get; set; } = new();

    public bool HasAnomalies =>
    FrequencySpikes.Count > 0 || TimeOfDayAnomalies.Count > 0 || ChangePoints.Count > 0;

        /// <summary>Union of all flagged credential IDs.</summary>
   public HashSet<Guid> AllFlagged => new(
   FrequencySpikes.Concat(TimeOfDayAnomalies).Concat(ChangePoints));
    }
}
