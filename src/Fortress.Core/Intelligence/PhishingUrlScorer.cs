using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fortress.Core.Intelligence
{
    /// <summary>
    /// Lightweight on-device phishing URL scorer. Uses ML.NET logistic
    /// regression trained on synthetic features. No network calls, no model file.
    /// </summary>
    public sealed class PhishingUrlScorer
  {
   private static readonly MLContext _ml = new(seed: 0);
   private readonly ILogger<PhishingUrlScorer>? _logger;

     private static readonly HashSet<string> _phishKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
   "login","signin","sign-in","secure","security","verify","verification",
    "update","confirm","account","banking","wallet","payment","invoice",
     "webscr","ebayisapi","paypal","support","helpdesk","password","reset"
        };

  private static readonly HashSet<string> _suspiciousTlds = new(StringComparer.OrdinalIgnoreCase)
        {
   ".tk",".ml",".ga",".cf",".gq",".xyz",".top",".click",
  ".link",".pw",".work",".party",".download",".racing",".review"
    };

    private static readonly string[] _brands =
        [
    "google","paypal","apple","microsoft","amazon","facebook",
     "instagram","twitter","netflix","spotify","github","linkedin",
     "bankofamerica","chase","wellsfargo","barclays","hsbc"
     ];

        private PredictionEngine<UrlFeatureRow, PhishingPrediction>? _engine;
        private readonly object _lock = new();

     public PhishingUrlScorer(ILogger<PhishingUrlScorer>? logger = null) => _logger = logger;

        public PhishingScore Score(string url)
 {
     if (string.IsNullOrWhiteSpace(url))
         return new PhishingScore { Probability = 0f, Explanation = "Empty URL" };

     var features = ExtractFeatures(url);
  var engine = GetOrBuildEngine();
   var prediction = engine.Predict(features);
     float prob = Math.Clamp(prediction.Probability, 0f, 1f);
  var explanation = BuildExplanation(features, prob);
    _logger?.LogDebug("PhishingUrlScorer: {Url} ? {P:P0} ({E})", url, prob, explanation);
   return new PhishingScore { Probability = prob, IsSuspicious = prob >= 0.6f, Explanation = explanation };
        }

        internal UrlFeatureRow ExtractFeatures(string rawUrl)
        {
  string url = rawUrl.Trim();
  string host = string.Empty, path = string.Empty, scheme = string.Empty;
     try
    {
  if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
  url = "http://" + url;
        var uri = new Uri(url);
            host   = uri.Host.ToLowerInvariant();
           path   = uri.AbsolutePath.ToLowerInvariant();
    scheme = uri.Scheme.ToLowerInvariant();
     }
    catch { }

   bool isIp = System.Net.IPAddress.TryParse(host, out _);
 int subDepth = Math.Max(0, host.Split('.').Length - 2);
        int urlLen = url.Length;
double digitRatio   = urlLen == 0 ? 0 : url.Count(char.IsDigit) / (double)urlLen;
   double specialRatio = urlLen == 0 ? 0 : url.Count(c => c is '-' or '_' or '@' or '%' or '~') / (double)urlLen;
    bool hasKeyword = _phishKeywords.Any(k => url.Contains(k, StringComparison.OrdinalIgnoreCase));
   bool suspTld = _suspiciousTlds.Any(t => host.EndsWith(t, StringComparison.OrdinalIgnoreCase));
    string hostRoot = host.Split('.').Length >= 2 ? host.Split('.')[^2] : host;
   bool nearMiss = _brands.Any(b => LevenshteinDistance(hostRoot, b) is > 0 and <= 2);
  int pathDepth = string.IsNullOrEmpty(path) ? 0 : path.Split('/').Count(s => !string.IsNullOrEmpty(s));

    return new UrlFeatureRow
   {
   UrlLength = urlLen, SubdomainDepth = subDepth,
            DigitRatio = (float)digitRatio, SpecialCharRatio = (float)specialRatio,
             IsIpAddress = isIp ? 1f : 0f, HasPhishKeyword = hasKeyword ? 1f : 0f,
  SuspiciousTld = suspTld ? 1f : 0f, NearMissBrand = nearMiss ? 1f : 0f,
PathDepth = pathDepth, IsHttps = scheme == "https" ? 1f : 0f
       };
        }

        private PredictionEngine<UrlFeatureRow, PhishingPrediction> GetOrBuildEngine()
{
        if (_engine != null) return _engine;
 lock (_lock) { return _engine ??= BuildEngine(); }
        }

       private PredictionEngine<UrlFeatureRow, PhishingPrediction> BuildEngine()
       {
  var data = _ml.Data.LoadFromEnumerable(BuildTrainingData());
         var pipeline = _ml.Transforms
   .Concatenate("Features",
       nameof(UrlFeatureRow.UrlLength), nameof(UrlFeatureRow.SubdomainDepth),
    nameof(UrlFeatureRow.DigitRatio), nameof(UrlFeatureRow.SpecialCharRatio),
    nameof(UrlFeatureRow.IsIpAddress), nameof(UrlFeatureRow.HasPhishKeyword),
    nameof(UrlFeatureRow.SuspiciousTld), nameof(UrlFeatureRow.NearMissBrand),
   nameof(UrlFeatureRow.PathDepth), nameof(UrlFeatureRow.IsHttps))
  .Append(_ml.Transforms.NormalizeMinMax("Features"))
     .Append(_ml.BinaryClassification.Trainers.LbfgsLogisticRegression(
    labelColumnName: "Label", featureColumnName: "Features"));
     return _ml.Model.CreatePredictionEngine<UrlFeatureRow, PhishingPrediction>(pipeline.Fit(data));
        }

  private static IEnumerable<LabelledUrlFeatureRow> BuildTrainingData() =>
   new[]
  {
    // Phishing
    Row(true,  120, 0, 0.05f, 0.02f, 1f, 1f, 0f, 0f, 3, 0f),
   Row(true,  250, 2, 0.10f, 0.08f, 0f, 1f, 0f, 1f, 5, 0f),
   Row(true,   90, 1, 0.03f, 0.04f, 0f, 0f, 1f, 0f, 2, 0f),
  Row(true,   80, 1, 0.02f, 0.03f, 0f, 1f, 0f, 1f, 2, 0f),
   Row(true,  200, 3, 0.30f, 0.10f, 0f, 1f, 0f, 0f, 6, 0f),
  Row(true,  150, 5, 0.05f, 0.05f, 0f, 1f, 0f, 0f, 3, 1f),
    Row(true,  300, 2, 0.15f, 0.12f, 0f, 1f, 1f, 1f, 8, 0f),
 Row(true,  180, 4, 0.20f, 0.08f, 1f, 0f, 0f, 0f, 4, 0f),
    // Legitimate
   Row(false,  30, 0, 0.0f,  0.0f,  0f, 0f, 0f, 0f, 1, 1f),
   Row(false,  45, 0, 0.0f,  0.01f, 0f, 0f, 0f, 0f, 2, 1f),
  Row(false,  55, 1, 0.02f, 0.01f, 0f, 0f, 0f, 0f, 2, 1f),
   Row(false,  40, 0, 0.01f, 0.0f,  0f, 0f, 0f, 0f, 1, 1f),
  Row(false,  60, 0, 0.03f, 0.02f, 0f, 0f, 0f, 0f, 3, 1f),
   Row(false,  38, 1, 0.0f,  0.0f,  0f, 0f, 0f, 0f, 1, 1f),
  Row(false,  70, 1, 0.05f, 0.02f, 0f, 0f, 0f, 0f, 3, 1f),
  Row(false,  80, 1, 0.02f, 0.03f, 0f, 0f, 0f, 0f, 4, 1f),
        };

  private static LabelledUrlFeatureRow Row(bool label, int len, int sub,
 float digit, float special, float ip, float keyword,
  float tld, float brand, int path, float https) => new()
  {
        Label = label, UrlLength = len, SubdomainDepth = sub,
  DigitRatio = digit, SpecialCharRatio = special, IsIpAddress = ip,
        HasPhishKeyword = keyword, SuspiciousTld = tld, NearMissBrand = brand,
  PathDepth = path, IsHttps = https
     };

        private static string BuildExplanation(UrlFeatureRow f, float prob)
        {
   if (prob < 0.3f) return "URL appears safe";
    if (prob < 0.6f) return "Some suspicious signals detected";
       var reasons = new List<string>();
            if (f.IsIpAddress > 0f)   reasons.Add("IP-address host");
     if (f.HasPhishKeyword > 0f) reasons.Add("phishing keywords");
     if (f.SuspiciousTld > 0f)   reasons.Add("suspicious TLD");
   if (f.NearMissBrand > 0f)   reasons.Add("brand impersonation");
   if (f.SubdomainDepth > 3)   reasons.Add("excessive subdomains");
     if (f.DigitRatio > 0.2f)     reasons.Add("high digit ratio");
   if (f.IsHttps == 0f)         reasons.Add("no HTTPS");
  return reasons.Count > 0 ? "Suspicious: " + string.Join(", ", reasons) : "Multiple suspicious signals";
   }

        private static int LevenshteinDistance(string a, string b)
        {
    if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
  if (string.IsNullOrEmpty(b)) return a.Length;
  if (a.Length > b.Length) (a, b) = (b, a);
   int[] prev = Enumerable.Range(0, a.Length + 1).ToArray();
  int[] curr = new int[a.Length + 1];
  for (int j = 1; j <= b.Length; j++)
   {
     curr[0] = j;
    for (int i = 1; i <= a.Length; i++)
     {
    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
    curr[i] = Math.Min(Math.Min(prev[i] + 1, curr[i - 1] + 1), prev[i - 1] + cost);
     }
     (prev, curr) = (curr, prev);
   }
   return prev[a.Length];
    }

  // ── Data types ────────────────────────────────────────────────────────────
      public sealed class UrlFeatureRow
   {
    public float UrlLength { get; set; }
  public float SubdomainDepth { get; set; }
     public float DigitRatio { get; set; }
    public float SpecialCharRatio { get; set; }
       public float IsIpAddress { get; set; }
  public float HasPhishKeyword { get; set; }
    public float SuspiciousTld { get; set; }
   public float NearMissBrand { get; set; }
    public float PathDepth { get; set; }
     public float IsHttps { get; set; }
   }

   private sealed class LabelledUrlFeatureRow
    {
   public bool Label { get; set; }
      public float UrlLength { get; set; }
        public float SubdomainDepth { get; set; }
   public float DigitRatio { get; set; }
  public float SpecialCharRatio { get; set; }
     public float IsIpAddress { get; set; }
  public float HasPhishKeyword { get; set; }
   public float SuspiciousTld { get; set; }
      public float NearMissBrand { get; set; }
  public float PathDepth { get; set; }
  public float IsHttps { get; set; }
    }

   private sealed class PhishingPrediction
    {
   [ColumnName("PredictedLabel")] public bool PredictedLabel { get; set; }
[ColumnName("Probability")]    public float Probability { get; set; }
 }
    }

public sealed class PhishingScore
    {
        public float Probability { get; init; }
       public bool IsSuspicious { get; init; }
   public string Explanation { get; init; } = string.Empty;
   public override string ToString() => $"{Probability:P0} � {Explanation}";
    }
}
