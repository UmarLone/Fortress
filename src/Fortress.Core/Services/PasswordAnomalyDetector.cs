using Fortress.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fortress.Core.Services
{
    /// <summary>
    /// Uses ML.NET SSA spike detection to find credentials with anomalously
    /// low password strength relative to the rest of the vault.
 /// On-device, no network, pure managed.
    /// </summary>
    public sealed class PasswordAnomalyDetector
    {
  private static readonly MLContext _ml = new(seed: 0);
    private readonly ILogger<PasswordAnomalyDetector>? _logger;
     private const int MinCredentials = 5;

  public PasswordAnomalyDetector(ILogger<PasswordAnomalyDetector>? logger = null) => _logger = logger;

  public HashSet<Guid> DetectAnomalies(IList<LoginItem> credentials)
  {
      var result = new HashSet<Guid>();
     var scored = credentials.Where(c => c.PasswordStrengthScore > 0).OrderBy(c => c.Id).ToList();
     if (scored.Count < MinCredentials) return result;

  try
  {
    var rows = scored.Select(c => new ScoreRow { Score = c.PasswordStrengthScore }).ToList();
     var data = _ml.Data.LoadFromEnumerable(rows);

    var pipeline = _ml.Transforms.DetectSpikeBySsa(
   outputColumnName: "Prediction",
  inputColumnName: nameof(ScoreRow.Score),
     confidence: 95,
      pvalueHistoryLength: Math.Max(4, scored.Count / 3),
   trainingWindowSize: scored.Count,
        seasonalityWindowSize: Math.Max(2, scored.Count / 4));

   var predictions = _ml.Data
     .CreateEnumerable<SpikePrediction>(pipeline.Fit(data).Transform(data), false)
            .ToList();

 var mean = scored.Average(c => (double)c.PasswordStrengthScore);
     for (int i = 0; i < predictions.Count; i++)
   if (predictions[i].Prediction is { Length: >= 1 } && predictions[i].Prediction[0] == 1
       && scored[i].PasswordStrengthScore < mean)
 result.Add(scored[i].Id);
     }
   catch (Exception ex) { _logger?.LogWarning(ex, "PasswordAnomalyDetector: analysis failed"); }
    return result;
    }

  private sealed class ScoreRow { public float Score { get; set; } }

 private sealed class SpikePrediction
   {
     [ColumnName("Prediction")]
    public double[] Prediction { get; set; } = [];
 }
    }
}
