using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fortress.Mobile.Core.Intelligence
{
    public sealed class AutofillRiskEngine : IDisposable
    {
  private static readonly string[] FeatureColumns =
    [
        nameof(AutofillRiskInput.DomainExactMatch),
     nameof(AutofillRiskInput.SubdomainMatch),
     nameof(AutofillRiskInput.HasPunycode),
          nameof(AutofillRiskInput.HasHyphen),
        nameof(AutofillRiskInput.DomainLength),
    nameof(AutofillRiskInput.FieldCount),
            nameof(AutofillRiskInput.HasPasswordField),
            nameof(AutofillRiskInput.HasEmailHint),
   nameof(AutofillRiskInput.HasOtpHint),
            nameof(AutofillRiskInput.FormHashKnown),
            nameof(AutofillRiskInput.SubmitTextUrgent),
    nameof(AutofillRiskInput.IsWebView),
    nameof(AutofillRiskInput.IsNewDevice),
      nameof(AutofillRiskInput.HourOfDay),
      nameof(AutofillRiskInput.PreviousSuccessfulLogins),
            nameof(AutofillRiskInput.KnownTrustedApp),
          nameof(AutofillRiskInput.KnownTrustedDevice)
        ];

        private readonly MLContext _ml;
        private readonly ILogger<AutofillRiskEngine>? _logger;
        private readonly object _lock = new();

        private ITransformer? _model;
        private PredictionEngine<AutofillRiskInput, AutofillRiskPrediction>? _predictionEngine;
        private bool _disposed;

   public AutofillRiskEngine(ILogger<AutofillRiskEngine>? logger = null)
        {
      _ml = new MLContext(seed: 0);
          _logger = logger;
        }

        public void TrainModel(IEnumerable<LabelledAutofillRiskInput>? trainingRows = null)
     {
       var rows = (trainingRows is null || !trainingRows.Any())
    ? AutofillRiskTrainingData.Build()
         : trainingRows;

         var data = _ml.Data.LoadFromEnumerable(rows);
            var split = _ml.Data.TrainTestSplit(data, testFraction: 0.2, seed: 0);

       var pipeline = BuildPipeline();
  var trained = pipeline.Fit(split.TrainSet);

    Evaluate(trained, split.TestSet);

     lock (_lock)
       {
  _model = trained;
   _predictionEngine = _ml.Model.CreatePredictionEngine<AutofillRiskInput, AutofillRiskPrediction>(trained);
            }

   _logger?.LogInformation("AutofillRiskEngine: training complete.");
     }

        public void SaveModel(string filePath)
  {
          ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

     ITransformer model;
            lock (_lock)
     {
              if (_model is null)
               throw new InvalidOperationException("No trained model. Call TrainModel first.");
       model = _model;
        }

            var emptySchema = _ml.Data
       .LoadFromEnumerable(Enumerable.Empty<LabelledAutofillRiskInput>())
    .Schema;

            _ml.Model.Save(model, emptySchema, filePath);
     _logger?.LogInformation("AutofillRiskEngine: model saved to {Path}.", filePath);
    }

     public void LoadModel(string filePath)
      {
  ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

     if (!File.Exists(filePath))
      throw new FileNotFoundException("Model file not found.", filePath);

  var loaded = _ml.Model.Load(filePath, out _);

            lock (_lock)
  {
                _model = loaded;
         _predictionEngine = _ml.Model.CreatePredictionEngine<AutofillRiskInput, AutofillRiskPrediction>(loaded);
        }

            _logger?.LogInformation("AutofillRiskEngine: model loaded from {Path}.", filePath);
   }

      public AutofillRiskPrediction Predict(AutofillRiskInput input)
   {
  ArgumentNullException.ThrowIfNull(input);

          PredictionEngine<AutofillRiskInput, AutofillRiskPrediction> engine;
            lock (_lock)
        {
      if (_predictionEngine is null)
            throw new InvalidOperationException("Engine not ready. Call TrainModel or LoadModel first.");
           engine = _predictionEngine;
      }

          return engine.Predict(input);
  }

        private IEstimator<ITransformer> BuildPipeline()
        {
    // SdcaLogisticRegression: 100% managed C#, no native .so required.
            // Ships in the base Microsoft.ML package and runs on Android/iOS.
 // LightGbm and FastTree were removed because both require native
      // shared libraries (lib_lightgbm.so / libFastTreeNative.so) that
          // are not included in Android/iOS ML.NET distributions.
     return _ml.Transforms
      .Concatenate("Features", FeatureColumns)
            .Append(_ml.Transforms.NormalizeMinMax("Features"))
         .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(
        labelColumnName:   "Label",
            featureColumnName: "Features",
          maximumNumberOfIterations: 100));
        }

  private void Evaluate(ITransformer model, IDataView testSet)
        {
   var predictions = model.Transform(testSet);
 var metrics = _ml.BinaryClassification.Evaluate(
              predictions,
       labelColumnName: "Label",
          scoreColumnName: "Score",
                probabilityColumnName: "Probability",
       predictedLabelColumnName: "PredictedLabel");

            _logger?.LogInformation(
       "AutofillRiskEngine – Accuracy: {Acc:P2} | AUC: {Auc:P2} | F1: {F1:P2} | AUPRC: {Pr:P2}",
        metrics.Accuracy,
  metrics.AreaUnderRocCurve,
            metrics.F1Score,
    metrics.AreaUnderPrecisionRecallCurve);

            Console.WriteLine($"[AutofillRiskEngine] Accuracy : {metrics.Accuracy:P2}");
 Console.WriteLine($"[AutofillRiskEngine] AUC  : {metrics.AreaUnderRocCurve:P2}");
    Console.WriteLine($"[AutofillRiskEngine] F1 Score : {metrics.F1Score:P2}");
         Console.WriteLine($"[AutofillRiskEngine] AUPRC    : {metrics.AreaUnderPrecisionRecallCurve:P2}");
        }

     public void Dispose()
  {
     if (_disposed) return;
     _disposed = true;
            lock (_lock)
            {
         _predictionEngine?.Dispose();
         _predictionEngine = null;
      }
        }
    }
}
