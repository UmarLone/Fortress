using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// Uses ML.NET SSA (Singular Spectrum Analysis) spike detection to identify
    /// credentials whose password strength score is anomalously low compared to
    /// the rest of the vault – catches outliers that a fixed threshold misses.
    ///
    /// Example: vault of 20 strong passwords (score 80+) with one score of 12
    /// will be flagged even if 12 is technically above "VeryWeak" threshold.
    ///
    /// On-device, no network, no API key. Pure managed, ARM-safe.
    /// </summary>
    public sealed class PasswordAnomalyDetector
    {
    private static readonly MLContext _ml = new(seed: 0);
      private readonly ILogger<PasswordAnomalyDetector>? _logger;

        // Minimum vault size before anomaly detection is meaningful
      private const int MinCredentials = 5;

        public PasswordAnomalyDetector(ILogger<PasswordAnomalyDetector>? logger = null)
        {
     _logger = logger;
        }

        /// <summary>
        /// Returns the IDs of credentials whose password strength is anomalously
        /// low relative to the rest of the vault.
     /// Returns an empty set when the vault is too small to analyse.
        /// </summary>
     public HashSet<Guid> DetectAnomalies(IList<LoginItem> credentials)
  {
         var result = new HashSet<Guid>();

            // Filter to credentials that have a stored strength score
       var scored = credentials
        .Where(c => c.PasswordStrengthScore > 0)
         .OrderBy(c => c.Id)   // deterministic order
     .ToList();

if (scored.Count < MinCredentials)
            {
   _logger?.LogDebug("PasswordAnomalyDetector: only {N} scored credentials – skipping", scored.Count);
                return result;
  }

   try
      {
      var rows = scored
       .Select(c => new ScoreRow { Score = c.PasswordStrengthScore })
          .ToList();

var data = _ml.Data.LoadFromEnumerable(rows);

      // SSA spike detection on the score series
       // pvalueHistoryLength = window, confidence = 95 %
    var pipeline = _ml.Transforms.DetectSpikeBySsa(
               outputColumnName: "Prediction",
       inputColumnName:  nameof(ScoreRow.Score),
    confidence:       95,
        pvalueHistoryLength: Math.Max(4, scored.Count / 3),
       trainingWindowSize:scored.Count,
           seasonalityWindowSize: Math.Max(2, scored.Count / 4));

  var transformed = pipeline.Fit(data).Transform(data);

       var predictions = _ml.Data
      .CreateEnumerable<SpikePrediction>(transformed, reuseRowObject: false)
    .ToList();

       for (int i = 0; i < predictions.Count; i++)
       {
       var pred = predictions[i];
      // Prediction[0] = alert (1 = spike), [1] = raw score, [2] = p-value
         if (pred.Prediction is { Length: >= 1 } && pred.Prediction[0] == 1)
       {
     // Only flag downward spikes (anomalously LOW scores)
             var vaultMean = scored.Average(c => (double)c.PasswordStrengthScore);
         if (scored[i].PasswordStrengthScore < vaultMean)
         {
        result.Add(scored[i].Id);
           _logger?.LogDebug(
        "PasswordAnomalyDetector: anomaly on {Id} score={S} mean={M:F1}",
    scored[i].Id, scored[i].PasswordStrengthScore, vaultMean);
       }
    }
        }

   _logger?.LogInformation(
        "PasswordAnomalyDetector: {Total} credentials ? {Anomalies} anomalies",
          scored.Count, result.Count);
      }
      catch (Exception ex)
        {
    _logger?.LogWarning(ex, "PasswordAnomalyDetector: analysis failed – returning empty set");
 }

            return result;
      }

        // ── ML.NET data types ─────────────────────────────────────────────────────
        private sealed class ScoreRow
        {
       public float Score { get; set; }
        }

        private sealed class SpikePrediction
        {
            [ColumnName("Prediction")]
     public double[] Prediction { get; set; } = [];
        }
    }
}
