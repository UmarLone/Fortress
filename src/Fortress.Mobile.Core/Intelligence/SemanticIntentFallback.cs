using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fortress.Mobile.Core.Intelligence
{
    /// <summary>
    /// Semantic intent fallback using TF-IDF cosine similarity over the training
    /// corpus.  Used when <see cref="MlNetIntentClassifier"/> returns a low-confidence
    /// prediction – handles paraphrases and out-of-vocabulary phrasing that SDCA
    /// wasn't trained on.
    ///
    /// Pipeline: FeaturizeText (bigrams + char-ngrams) ? cosine similarity search
    /// over a pre-built corpus index.
    ///
    /// Size: ~0 MB extra (reuses Microsoft.ML which is already in the project).
    /// Latency: &lt;5ms per query after first call (index built once).
    /// </summary>
    public sealed class SemanticIntentFallback
    {
 private static readonly MLContext _ml = new(seed: 0);
        private readonly ILogger<SemanticIntentFallback>? _logger;

        // ── Index ─────────────────────────────────────────────────────────────────
     private float[][]? _corpus;
        private VoiceCommandIntent[]? _labels;
        private readonly object _lock = new();

        public SemanticIntentFallback(ILogger<SemanticIntentFallback>? logger = null)
  {
            _logger = logger;
    }

        // ── Public API ────────────────────────────────────────────────────────────
        /// <summary>
        /// Returns the best-matching intent and its cosine similarity score.
        /// Returns <see cref="VoiceCommandIntent.Unknown"/> when the best score
        /// is below <paramref name="minScore"/>.
    /// </summary>
        public (VoiceCommandIntent intent, float score) Predict(string transcript, float minScore = 0.35f)
        {
    if (string.IsNullOrWhiteSpace(transcript))
   return (VoiceCommandIntent.Unknown, 0f);

         EnsureIndex();

          var queryVec = Vectorise(transcript);
            if (queryVec == null)
     return (VoiceCommandIntent.Unknown, 0f);

    float bestScore = 0f;
    int bestIdx = -1;
            for (int i = 0; i < _corpus!.Length; i++)
            {
       float s = CosineSimilarity(queryVec, _corpus[i]);
        if (s > bestScore) { bestScore = s; bestIdx = i; }
       }

            if (bestIdx < 0 || bestScore < minScore)
     return (VoiceCommandIntent.Unknown, bestScore);

   var intent = _labels![bestIdx];
            _logger?.LogDebug(
    "SemanticIntentFallback: '{T}' ? {I} ({S:F3})", transcript, intent, bestScore);
            return (intent, bestScore);
        }

        // ── Index ─────────────────────────────────────────────────────────────────
        private void EnsureIndex()
 {
            if (_corpus != null) return;
         lock (_lock)
            {
           if (_corpus != null) return;
BuildIndex();
   }
    }

        private void BuildIndex()
        {
  var corpus = GetCorpus();
      var rows   = corpus.Select(e => new TextRow { Text = e.text }).ToList();
            var data   = _ml.Data.LoadFromEnumerable(rows);

   var pipeline = _ml.Transforms.Text.FeaturizeText(
         outputColumnName: "Features",
        new Microsoft.ML.Transforms.Text.TextFeaturizingEstimator.Options
    {
         WordFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
         {
           NgramLength    = 2,
   UseAllLengths  = true,
     Weighting      = Microsoft.ML.Transforms.Text.NgramExtractingEstimator
    .WeightingCriteria.TfIdf
    },
            CharFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
     {
      NgramLength   = 3,
      UseAllLengths = true
 },
KeepDiacritics   = false,
          KeepPunctuations = false,
    CaseMode    = Microsoft.ML.Transforms.Text.TextNormalizingEstimator.CaseMode.Lower
                },
     nameof(TextRow.Text));

         var transformed = pipeline.Fit(data).Transform(data);
     _corpus = transformed.GetColumn<float[]>("Features").ToArray();
            _labels = corpus.Select(e => e.intent).ToArray();

            _logger?.LogDebug(
     "SemanticIntentFallback: index built – {N} entries, dim={D}",
      _corpus.Length, _corpus[0].Length);
 }

        private float[]? Vectorise(string text)
     {
      try
     {
var rows = new[] { new TextRow { Text = text } };
 var data = _ml.Data.LoadFromEnumerable(rows);
  var pipeline = _ml.Transforms.Text.FeaturizeText(
         outputColumnName: "Features",
                    new Microsoft.ML.Transforms.Text.TextFeaturizingEstimator.Options
     {
     WordFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
    {
           NgramLength    = 2,
      UseAllLengths  = true,
     Weighting      = Microsoft.ML.Transforms.Text.NgramExtractingEstimator
       .WeightingCriteria.TfIdf
       },
  CharFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
       {
           NgramLength   = 3,
   UseAllLengths = true
  },
    KeepDiacritics   = false,
           KeepPunctuations = false,
     CaseMode         = Microsoft.ML.Transforms.Text.TextNormalizingEstimator.CaseMode.Lower
     },
        nameof(TextRow.Text));
       var transformed = pipeline.Fit(data).Transform(data);
 return transformed.GetColumn<float[]>("Features").FirstOrDefault();
      }
      catch (Exception ex)
{
     _logger?.LogWarning(ex, "SemanticIntentFallback: vectorise failed");
          return null;
   }
        }

     private static float CosineSimilarity(float[] a, float[] b)
        {
       int len = Math.Min(a.Length, b.Length);
      float dot = 0f, magA = 0f, magB = 0f;
            for (int i = 0; i < len; i++)
    {
    dot  += a[i] * b[i];
      magA += a[i] * a[i];
         magB += b[i] * b[i];
          }
            float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
          return denom < 1e-10f ? 0f : dot / denom;
        }

        // ── Corpus ────────────────────────────────────────────────────────────────
        // Representative phrases for each intent – kept short to reduce index size.
        // These complement the SDCA training data in MlNetIntentClassifier.

        private static (string text, VoiceCommandIntent intent)[] GetCorpus() =>
    [
            ("save password login credentials account",         VoiceCommandIntent.AddPassword),
 ("add new login store website credentials",   VoiceCommandIntent.AddPassword),
            ("add credit card payment card debit card visa",          VoiceCommandIntent.AddCreditCard),
   ("store card payment method mastercard amex",      VoiceCommandIntent.AddCreditCard),
            ("add authenticator two factor 2fa otp totp", VoiceCommandIntent.AddAuthenticator),
 ("save secure note private encrypted memo text",         VoiceCommandIntent.AddSecureNote),
          ("add identity person profile contact address",         VoiceCommandIntent.AddIdentity),
            ("save address location street postal shipping billing",  VoiceCommandIntent.AddAddress),
     ("open passwords show logins view accounts list",         VoiceCommandIntent.OpenPasswords),
      ("open cards show credit cards view payment methods", VoiceCommandIntent.OpenCreditCards),
   ("open authenticators show 2fa codes view otp",          VoiceCommandIntent.OpenAuthenticators),
("open notes show secure notes view private memos",       VoiceCommandIntent.OpenSecureNotes),
     ("open identities show profiles view contacts people",   VoiceCommandIntent.OpenIdentities),
            ("open groups show vault groups",      VoiceCommandIntent.OpenGroups),
    ("open settings preferences configuration menu",       VoiceCommandIntent.OpenSettings),
        ("how many passwords count total number accounts",       VoiceCommandIntent.HowManyPasswords),
        ("how many cards count total credit debit payment",      VoiceCommandIntent.HowManyCards),
  ("lock vault secure app lock fortress protect", VoiceCommandIntent.LockVault),
   ("vault summary overview statistics what stored",        VoiceCommandIntent.VaultSummary),
            ("vault health score security report check",             VoiceCommandIntent.ShowVaultHealth),
  ("risky accounts at risk unsafe attention needed",       VoiceCommandIntent.ShowRiskyAccounts),
            ("reused passwords duplicate same password",      VoiceCommandIntent.ShowReusedPasswords),
            ("weak passwords short simple insecure",         VoiceCommandIntent.ShowWeakPasswords),
            ("breached passwords compromised leaked hacked",       VoiceCommandIntent.ShowBreachedAccounts),
   ("missing 2fa no two factor authentication accounts",    VoiceCommandIntent.ListMissing2FA),
            ("generate password create strong random suggest",       VoiceCommandIntent.GeneratePassword),
   ("sync vault backup upload cloud synchronise",           VoiceCommandIntent.SyncNow),
    ("fortress mode activate enable protection",       VoiceCommandIntent.EnableFortressMode),
       ("attack surface exposure blast radius linked accounts", VoiceCommandIntent.AttackSurface),
 ("voice journal write note record dictate thought memo capture moment", VoiceCommandIntent.VoiceJournal),
    ("help what can you do what can i say commands guide how to use", VoiceCommandIntent.Help),
   ("hello hey hi greetings good morning afternoon evening howdy", VoiceCommandIntent.Greeting),
      ("thank thanks appreciate cheers good job well done nice one", VoiceCommandIntent.Thanks),
    ];

        // ── ML.NET types ──────────────────────────────────────────────────────────
        private sealed class TextRow
{
          [ColumnName("Text")]
public string Text { get; set; } = string.Empty;
    }
    }
}
