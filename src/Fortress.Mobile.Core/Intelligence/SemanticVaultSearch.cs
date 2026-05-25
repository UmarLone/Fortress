using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fortress.Mobile.Core.Intelligence
{
    /// <summary>
    /// Semantic vault search using ML.NET TF-IDF feature vectors + cosine similarity.
    /// Accepts <see cref="LoginItem"/> directly (the current vault model).
    /// </summary>
    public sealed class SemanticVaultSearch
    {
        private static readonly MLContext _ml = new(seed: 0);
        private readonly ILogger<SemanticVaultSearch>? _logger;

     private List<IndexedEntry>? _index;
        private float[][]? _vectors;
        private int _vaultVersion = -1;

        public SemanticVaultSearch(ILogger<SemanticVaultSearch>? logger = null)
        {
       _logger = logger;
   }

    // ── Public API ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns up to <paramref name="maxResults"/> login items ranked by
        /// semantic relevance to <paramref name="query"/>.
        /// </summary>
      public IReadOnlyList<SemanticSearchResult> Search(
  IList<LoginItem> items,
            string query,
          int maxResults = 10,
            float minScore = 0.05f)
   {
      if (string.IsNullOrWhiteSpace(query) || items.Count == 0)
     return Array.Empty<SemanticSearchResult>();

 EnsureIndex(items);

            var queryVec = Vectorise(query);
         if (queryVec == null)
         return Array.Empty<SemanticSearchResult>();

     var results = new List<SemanticSearchResult>(_index!.Count);
          for (int i = 0; i < _index.Count; i++)
  {
    float score = CosineSimilarity(queryVec, _vectors![i]);
  if (score >= minScore)
results.Add(new SemanticSearchResult
          {
             LoginItem = _index[i].Source,
            Score = score,
             MatchReason = BuildMatchReason(score)
    });
    }

          var ordered = results
      .OrderByDescending(r => r.Score)
    .Take(maxResults)
          .ToList()
       .AsReadOnly();

         _logger?.LogDebug(
   "SemanticVaultSearch: '{Q}' ? {N} results (top {S:F3})",
     query, ordered.Count, ordered.FirstOrDefault()?.Score ?? 0f);

            return ordered;
     }

        public void Invalidate() => _vaultVersion = -1;

   // ── Index building ────────────────────────────────────────────────────
        private void EnsureIndex(IList<LoginItem> items)
        {
            if (_index != null && _vaultVersion == items.Count) return;

            _logger?.LogDebug("SemanticVaultSearch: building index for {N} items", items.Count);

         _index = items.Select(item => new IndexedEntry
            {
                Source   = item,
   Document = BuildDocument(item)
  }).ToList();

 var rows = _index.Select(e => new TextRow { Text = e.Document }).ToList();
            var data = _ml.Data.LoadFromEnumerable(rows);

            var pipeline = _ml.Transforms.Text.FeaturizeText(
       outputColumnName: "Features",
        new Microsoft.ML.Transforms.Text.TextFeaturizingEstimator.Options
          {
     WordFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
              {
  NgramLength  = 2,
         UseAllLengths = true,
Weighting    = Microsoft.ML.Transforms.Text.NgramExtractingEstimator.WeightingCriteria.TfIdf
        },
         KeepDiacritics   = false,
        KeepPunctuations = false,
 CaseMode      = Microsoft.ML.Transforms.Text.TextNormalizingEstimator.CaseMode.Lower
      },
      nameof(TextRow.Text));

     var transformer = pipeline.Fit(data);
   var transformed = transformer.Transform(data);
     _vectors       = transformed.GetColumn<float[]>("Features").ToArray();
          _vaultVersion  = items.Count;

  _logger?.LogDebug("SemanticVaultSearch: index built, dim={D}", _vectors[0].Length);
        }

        private float[]? Vectorise(string text)
        {
     try
    {
      var row  = new[] { new TextRow { Text = text } };
    var data = _ml.Data.LoadFromEnumerable(row);
    var pipeline = _ml.Transforms.Text.FeaturizeText(
      outputColumnName: "Features",
      new Microsoft.ML.Transforms.Text.TextFeaturizingEstimator.Options
      {
          WordFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
     {
       NgramLength  = 2,
   UseAllLengths = true,
           Weighting    = Microsoft.ML.Transforms.Text.NgramExtractingEstimator.WeightingCriteria.TfIdf
          },
        KeepDiacritics   = false,
        KeepPunctuations = false,
            CaseMode     = Microsoft.ML.Transforms.Text.TextNormalizingEstimator.CaseMode.Lower
     },
   nameof(TextRow.Text));

         var transformed = pipeline.Fit(data).Transform(data);
    return transformed.GetColumn<float[]>("Features").FirstOrDefault();
            }
    catch (Exception ex)
            {
  _logger?.LogWarning(ex, "SemanticVaultSearch: vectorise failed");
    return null;
         }
    }

   // ── Helpers ───────────────────────────────────────────────────────────
        private static string BuildDocument(LoginItem item)
  {
            var parts = new List<string>();

   // Domain / URL – weighted ×2 as strongest signal
  var domain = item.Label ?? item.Url ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(domain))
          {
      parts.Add(domain);
           parts.Add(domain);
        var dot = domain.LastIndexOf('.');
                if (dot > 0) parts.Add(domain[..dot]);
    }

if (!string.IsNullOrWhiteSpace(item.Username))
                parts.Add(item.Username);

      if (!string.IsNullOrWhiteSpace(item.Notes))
                parts.Add(item.Notes);

            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
      if (a.Length != b.Length) return 0f;
   float dot = 0f, magA = 0f, magB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
     dot+= a[i] * b[i];
      magA += a[i] * a[i];
   magB += b[i] * b[i];
        }
            float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
      return denom < 1e-10f ? 0f : dot / denom;
   }

        private static string BuildMatchReason(float score) => score switch
        {
            >= 0.8f => "Strong match",
    >= 0.5f => "Good match",
            >= 0.2f => "Partial match",
       _       => "Weak match"
        };

        // ── Inner types ───────────────────────────────────────────────────────
        private sealed class TextRow
        {
  [ColumnName("Text")]
   public string Text { get; set; } = string.Empty;
        }

        private sealed class IndexedEntry
        {
       public LoginItem Source   { get; set; } = null!;
     public string    Document { get; set; } = string.Empty;
        }
    }

    /// <summary>A single semantic search result against the vault.</summary>
    public sealed class SemanticSearchResult
 {
        public LoginItem   LoginItem   { get; init; } = null!;
        public float       Score  { get; init; }
        public string      MatchReason { get; init; } = string.Empty;

        // Kept for back-compat with any existing callers that used .Credential
        [Obsolete("Use LoginItem instead")]
     public Credential Credential => new()
   {
            Id = LoginItem.Id,
      Domain   = LoginItem.Label,
            Username = LoginItem.Username,
 };
    }
}
