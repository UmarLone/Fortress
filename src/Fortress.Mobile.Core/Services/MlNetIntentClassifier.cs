using Fortress.Mobile.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fortress.Mobile.Core.Services
{
    /// <summary>
    /// On-device ML.NET multiclass text classifier for voice command intent recognition.
    /// Replaces the hand-rolled TF-IDF cosine classifier with a proper trained SDCA model.
    ///
    /// Pipeline:
    ///   FeaturizeText (unigrams + bigrams, char n-grams) ? SdcaMaximumEntropy
    ///
    /// Model size: ~80 KB in memory. Trains in &lt;50 ms on first call, then cached.
    /// No native dependencies — pure managed, ARM-safe, works on Android &amp; iOS.
    /// </summary>
    public sealed class MlNetIntentClassifier
    {
        private static readonly MLContext _mlContext = new(seed: 42);

        private ITransformer? _model;
        private PredictionEngine<IntentInput, IntentPrediction>? _engine;
        private readonly object _lock = new();
        private readonly ILogger<MlNetIntentClassifier>? _logger;

        private const string AssetName = "intent_classifier_v1.zip";

        private static readonly string FallbackCachePath = Path.Combine(
       FileSystem.CacheDirectory, AssetName);

        public float ConfidenceThreshold { get; set; } = 0.45f;

        public MlNetIntentClassifier(ILogger<MlNetIntentClassifier>? logger = null)
        {
            _logger = logger;
        }

        // ── Public API ───────────────────────────────────────────────────────

        public (VoiceCommandIntent intent, float confidence) Predict(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
                return (VoiceCommandIntent.Unknown, 0f);

            // 1. Normalise — strip punctuation, lowercase, expand contractions
            var normalised = Normalise(transcript);

            // 2. ML.NET classifier
            var engine = GetOrBuildEngine();
            var prediction = engine.Predict(new IntentInput { Text = normalised });

            if (!Enum.TryParse<VoiceCommandIntent>(prediction.PredictedLabel, out var mlIntent))
                mlIntent = VoiceCommandIntent.Unknown;

            float mlConfidence = 0f;
            try
            {
                var labelBuffer = new VBuffer<ReadOnlyMemory<char>>();
                engine.OutputSchema["Score"].Annotations.GetValue("KeyValues", ref labelBuffer);
                var labels = labelBuffer.GetValues();
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i].ToString() == prediction.PredictedLabel && i < prediction.Scores.Length)
                    {
                        mlConfidence = prediction.Scores[i];
                        break;
                    }
                }
            }
            catch
            {
                if (prediction.Scores?.Length > 0)
                    mlConfidence = prediction.Scores.Max();
            }

            _logger?.LogDebug("MlNet: \"{T}\" → {I} ({C:P0})", normalised, mlIntent, mlConfidence);

            // 3. If ML is confident enough, trust it
            if (mlConfidence >= ConfidenceThreshold && mlIntent != VoiceCommandIntent.Unknown)
                return (mlIntent, mlConfidence);

            // 4. Fallback: keyword/rule-based matcher for robustness
            var ruleIntent = RuleBasedFallback(normalised);
            if (ruleIntent != VoiceCommandIntent.Unknown)
            {
                _logger?.LogDebug("MlNet: rule fallback → {I} for \"{T}\"", ruleIntent, normalised);
                return (ruleIntent, 0.70f);
            }

            // 5. If ML had a low-confidence guess, still use it rather than Unknown
            if (mlIntent != VoiceCommandIntent.Unknown && mlConfidence >= 0.30f)
                return (mlIntent, mlConfidence);

            return (VoiceCommandIntent.Unknown, mlConfidence);
        }

        // ── Text normalisation ───────────────────────────────────────────────

        private static string Normalise(string text)
        {
            text = text.ToLowerInvariant().Trim();
            // Expand common contractions so training data and live speech match
            text = text
           .Replace("what's", "what is")
              .Replace("whats", "what is")
           .Replace("what're", "what are")
             .Replace("i've", "i have")
               .Replace("i'm", "i am")
                   .Replace("i'd", "i would")
               .Replace("i'll", "i will")
            .Replace("don't", "do not")
                      .Replace("doesn't", "does not")
              .Replace("haven't", "have not")
            .Replace("can't", "cannot")
               .Replace("let's", "let us")
                 .Replace("there's", "there is")
           .Replace("how's", "how is")
                .Replace("'s", "")
             .Replace("'", "");
            // Strip punctuation except spaces
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
                if (char.IsLetterOrDigit(c) || c == ' ')
                    sb.Append(c);
            text = sb.ToString().Trim();

            // Strip politeness / filler prefixes so classifier sees core intent
            ReadOnlySpan<string> fillers =
            [
                "can you please ", "can you ", "could you please ", "could you ",
                "please ", "i would like to ", "i want to ", "i need to ",
                "help me ", "let me ", "just ", "quickly ",
                "is it possible to ", "how do i ", "how can i ",
            ];
            foreach (var f in fillers)
                if (text.StartsWith(f, StringComparison.Ordinal))
                { text = text[f.Length..].TrimStart(); break; }

            // Strip trailing fillers
            ReadOnlySpan<string> trailers = [" please", " for me", " right now", " now", " thanks"];
            foreach (var t in trailers)
                if (text.EndsWith(t, StringComparison.Ordinal))
                { text = text[..^t.Length].TrimEnd(); break; }

            return text;
        }

        // ── Rule-based fallback matcher ──────────────────────────────────────

        private static VoiceCommandIntent RuleBasedFallback(string t)
        {
            // Ordered from most-specific to least-specific to avoid false positives.
            // 't' is already lowercased and contraction-expanded.

            // ── Strip wake word prefix for rule matching too ─────────────────
      var stripped = t;
       ReadOnlySpan<string> wakeWords = ["hey fortress ", "hello fortress ", "hi fortress ",
                "ok fortress ", "okay fortress ", "yo fortress ", "fortress "];
            foreach (var w in wakeWords)
    {
  if (stripped.StartsWith(w, StringComparison.Ordinal))
    { stripped = stripped[w.Length..].TrimStart(); break; }
            }
  // Also handle comma after wake word
    foreach (var w in wakeWords)
            {
        var trimmed = w.TrimEnd();
       if (stripped.StartsWith(trimmed + ", ", StringComparison.Ordinal))
         { stripped = stripped[(trimmed.Length + 2)..].TrimStart(); break; }
}
            // Use stripped text for all rule matching below
     t = stripped;

     // ── Smart queries (answer without navigating away) ───────────────
            if (ContainsAny(t, "what is in my vault", "whats in my vault", "what do i have in my vault",
            "tell me what is in my vault", "vault summary", "summarise my vault",
         "overview of my vault", "vault overview", "what do i have stored",
       "vault statistics", "give me a summary"))
                return VoiceCommandIntent.VaultSummary;

            if (ContainsAny(t, "how many passwords", "count my passwords", "number of passwords",
           "how many logins", "password count", "total passwords"))
                return VoiceCommandIntent.HowManyPasswords;

            if (ContainsAny(t, "how many cards", "count my cards", "number of cards",
        "card count", "total cards", "how many credit cards", "how many debit cards"))
                return VoiceCommandIntent.HowManyCards;

            if (ContainsAny(t, "attack surface", "blast radius", "how exposed", "how at risk",
        "show my exposure", "compromise chains", "accounts share passwords",
  "accounts are linked", "accounts linked together"))
                return VoiceCommandIntent.AttackSurface;

            // ── Vault health ─────────────────────────────────────────────────
            if (ContainsAny(t, "vault health", "security score", "health score", "health report",
                        "how secure is my vault", "how is my vault health"))
                return VoiceCommandIntent.ShowVaultHealth;

            if (ContainsAny(t, "risky accounts", "accounts at risk", "unsafe accounts",
         "accounts need attention", "what accounts are risky"))
                return VoiceCommandIntent.ShowRiskyAccounts;

            if (ContainsAny(t, "reused passwords", "passwords are reused", "any passwords reused",
          "check for reused", "find reused"))
                return VoiceCommandIntent.ShowReusedPasswords;

            if (ContainsAny(t, "weak passwords", "passwords are weak", "any passwords weak",
                "check for weak", "find weak passwords"))
                return VoiceCommandIntent.ShowWeakPasswords;

            if (ContainsAny(t, "breached", "compromised passwords", "passwords leaked",
                   "data breaches", "have i been breached", "passwords in breach"))
                return VoiceCommandIntent.ShowBreachedAccounts;

            if (ContainsAny(t, "missing two factor", "missing 2fa", "without 2fa",
                "no authenticator", "accounts missing 2fa", "need 2fa",
           "accounts without two factor"))
                return VoiceCommandIntent.ListMissing2FA;

            // ── Vault operations ─────────────────────────────────────────────
            if (ContainsAny(t, "lock my vault", "lock the vault", "lock the app",
           "lock fortress", "lock everything", "secure the vault",
                         "lock fortress", "lock up my vault", "secure my vault now"))
                return VoiceCommandIntent.LockVault;

            if (ContainsAny(t, "sync my vault", "sync now", "synchronise", "synchronize",
                  "back up my vault", "backup my vault", "upload my vault",
              "start a sync", "do a sync", "sync to cloud", "update cloud"))
                return VoiceCommandIntent.SyncNow;

            if (ContainsAny(t, "generate a password", "generate password", "create a strong password",
  "make a new password", "suggest a password", "new password",
      "random password", "password generator", "open generator",
        "i need a new password", "create secure password"))
                return VoiceCommandIntent.GeneratePassword;

            if (ContainsAny(t, "fortress mode", "enable fortress", "activate fortress", "turn on fortress"))
                return VoiceCommandIntent.EnableFortressMode;

            // ── Navigation ───────────────────────────────────────────────────
            if (ContainsAny(t, "open settings", "go to settings", "show settings",
         "app settings", "open preferences", "navigate to settings",
               "open the settings", "settings menu"))
                return VoiceCommandIntent.OpenSettings;

            if (ContainsAny(t, "open groups", "show groups", "go to groups", "my groups",
            "list my groups", "view my groups"))
                return VoiceCommandIntent.OpenGroups;

            if (ContainsAny(t, "open identities", "show identities", "my identities",
             "view identities", "identity documents", "personal profiles",
    "open contacts", "show contacts"))
                return VoiceCommandIntent.OpenIdentities;

            if (ContainsAny(t, "secure notes", "private notes", "secret notes",
                   "open notes", "show notes", "go to notes", "my notes",
      "view notes", "encrypted notes"))
                return VoiceCommandIntent.OpenSecureNotes;

            if (ContainsAny(t, "open authenticators", "show authenticators", "my authenticators",
             "two factor codes", "2fa codes", "otp codes", "totp codes",
           "one time passwords", "go to authenticators", "view authenticators"))
                return VoiceCommandIntent.OpenAuthenticators;

            if (ContainsAny(t, "open cards", "show cards", "my cards", "credit cards",
               "debit cards", "payment cards", "go to cards", "view cards",
"open credit cards", "show credit cards"))
                return VoiceCommandIntent.OpenCreditCards;

            if (ContainsAny(t, "open passwords", "show passwords", "my passwords", "my logins",
            "view passwords", "go to passwords", "list passwords",
                     "show logins", "view logins", "see my passwords",
              "open logins", "go to logins", "my accounts", "show accounts"))
                return VoiceCommandIntent.OpenPasswords;

            // ── Add intents (most ambiguous — last) ──────────────────────────
            // ── Voice journal (before general "note" keywords) ───────────────
            if (ContainsAny(t, "write a note", "record a note", "voice journal",
     "journal entry", "record my thoughts", "dictate a note",
        "write a journal", "record a journal", "take a note",
           "capture a thought", "start journaling", "voice note",
   "log a thought", "record a moment", "capture a moment",
            "quick note", "memo to self", "hey fortress write",
    "hey fortress record", "hey fortress journal",
  "record what i say", "write down what i say"))
   return VoiceCommandIntent.VoiceJournal;

        if (ContainsAny(t, "add two factor", "add 2fa", "save 2fa", "new authenticator",
       "add authenticator", "setup 2fa", "set up two factor",
       "add otp", "store totp", "two factor authentication"))
                return VoiceCommandIntent.AddAuthenticator;

            if (ContainsAny(t, "add credit card", "save credit card", "save my visa",
                "add my mastercard", "save debit card", "add payment card",
            "store card number", "add card to wallet", "add my bank card",
         "add my card", "new payment method", "save card"))
                return VoiceCommandIntent.AddCreditCard;

            if (ContainsAny(t, "add secure note", "save note", "save private note", "new note",
                       "add a note", "store note", "create note", "add note to vault",
               "keep a note", "secret memo", "encrypted note", "confidential note"))
                return VoiceCommandIntent.AddSecureNote;

            if (ContainsAny(t, "add identity", "save identity", "add contact", "new identity",
           "add person", "passport information", "drivers licence",
  "national id", "personal profile", "add personal details"))
                return VoiceCommandIntent.AddIdentity;

            if (ContainsAny(t, "save address", "add address", "home address", "work address",
      "billing address", "shipping address", "postal address",
          "mailing address", "new address", "store address"))
                return VoiceCommandIntent.AddAddress;

            if (ContainsAny(t, "add password", "save password", "new login", "store login",
                     "save login", "add login", "new password", "store password",
             "save credentials", "add credentials", "remember password",
        "add website account", "new website login", "save account",
            "add account", "add my email", "store my account"))
                return VoiceCommandIntent.AddPassword;

            return VoiceCommandIntent.Unknown;
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            foreach (var term in terms)
                if (text.Contains(term, StringComparison.Ordinal))
                    return true;
            return false;
        }

        // ── Training ─────────────────────────────────────────────────────────

        private PredictionEngine<IntentInput, IntentPrediction> GetOrBuildEngine()
        {
            if (_engine != null) return _engine;

            lock (_lock)
            {
                if (_engine != null) return _engine;

                // Bump this version string whenever BuildTrainingData() changes.
                const string ModelVersion = "v9";
                var versionPath = Path.Combine(FileSystem.CacheDirectory, "intent_classifier.version");
                var cachePath = FallbackCachePath;

                if (File.Exists(versionPath))
                {
                    var storedVersion = File.ReadAllText(versionPath).Trim();
                    if (storedVersion != ModelVersion && File.Exists(cachePath))
                    {
                        File.Delete(cachePath);
                        _logger?.LogInformation("MlNetIntentClassifier: stale model cache deleted (was {Old}, now {New})", storedVersion, ModelVersion);
                    }
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    using var assetStream = FileSystem.OpenAppPackageFileAsync(AssetName).GetAwaiter().GetResult();
                    _model = _mlContext.Model.Load(assetStream, out _);
                    _engine = _mlContext.Model.CreatePredictionEngine<IntentInput, IntentPrediction>(_model);
                    sw.Stop();
                    _logger?.LogInformation("MlNetIntentClassifier: loaded bundled asset in {Ms}ms", sw.ElapsedMilliseconds);
                    return _engine;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "MlNetIntentClassifier: bundled asset load failed, trying cache");
                }

                if (File.Exists(FallbackCachePath))
                {
                    try
                    {
                        _model = _mlContext.Model.Load(FallbackCachePath, out _);
                        _engine = _mlContext.Model.CreatePredictionEngine<IntentInput, IntentPrediction>(_model);
                        sw.Stop();
                        _logger?.LogInformation("MlNetIntentClassifier: loaded from cache in {Ms}ms", sw.ElapsedMilliseconds);
                        return _engine;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "MlNetIntentClassifier: cache load failed, retraining");
                        File.Delete(FallbackCachePath);
                    }
                }

                _logger?.LogWarning("MlNetIntentClassifier: bundled model missing — training on-device");
                var data = _mlContext.Data.LoadFromEnumerable(BuildTrainingData());

                var pipeline = _mlContext.Transforms.Conversion
                  .MapValueToKey("Label", nameof(IntentInput.Label))
          .Append(_mlContext.Transforms.Text.FeaturizeText(
                        "Features",
           new Microsoft.ML.Transforms.Text.TextFeaturizingEstimator.Options
           {
               WordFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
               {
                   NgramLength = 2,
                   UseAllLengths = true,
                   Weighting = Microsoft.ML.Transforms.Text.NgramExtractingEstimator.WeightingCriteria.TfIdf
               },
               CharFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
               {
                   NgramLength = 3,
                   UseAllLengths = true
               },
               KeepDiacritics = false,
               KeepPunctuations = false,
               CaseMode = Microsoft.ML.Transforms.Text.TextNormalizingEstimator.CaseMode.Lower
           },
                 nameof(IntentInput.Text)))
            .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                  labelColumnName: "Label",
           featureColumnName: "Features",
            maximumNumberOfIterations: 200))
           .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                _model = pipeline.Fit(data);
                _engine = _mlContext.Model.CreatePredictionEngine<IntentInput, IntentPrediction>(_model);

                try
                {
                    _mlContext.Model.Save(_model, data.Schema, FallbackCachePath);
                    File.WriteAllText(versionPath, ModelVersion);
                    _logger?.LogInformation("MlNetIntentClassifier: saved model to {Path}", FallbackCachePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "MlNetIntentClassifier: could not save model to disk");
                }

                sw.Stop();
                _logger?.LogInformation("MlNetIntentClassifier: trained in {Ms}ms", sw.ElapsedMilliseconds);
                return _engine;
            }
        }

        // ── Training corpus ──────────────────────────────────────────────────
        // All entries are pre-normalised (lowercase, contractions expanded, no punctuation)
        // to exactly match what Normalise() produces at prediction time.

        private static IEnumerable<IntentInput> BuildTrainingData() =>
         [
           // ── AddPassword ──────────────────────────────────────────────────
           T("save my github password",         VoiceCommandIntent.AddPassword),
            T("add a new login",    VoiceCommandIntent.AddPassword),
 T("store my netflix credentials",         VoiceCommandIntent.AddPassword),
    T("create a new password entry",            VoiceCommandIntent.AddPassword),
  T("add my email account details",      VoiceCommandIntent.AddPassword),
   T("new website login",        VoiceCommandIntent.AddPassword),
            T("i need to save a login",           VoiceCommandIntent.AddPassword),
       T("save login for twitter",      VoiceCommandIntent.AddPassword),
            T("record my amazon username and password", VoiceCommandIntent.AddPassword),
            T("add app password",  VoiceCommandIntent.AddPassword),
       T("store my instagram account",  VoiceCommandIntent.AddPassword),
   T("keep my facebook login",       VoiceCommandIntent.AddPassword),
         T("new password for spotify",        VoiceCommandIntent.AddPassword),
   T("add my google account password",         VoiceCommandIntent.AddPassword),
            T("save credentials for work",        VoiceCommandIntent.AddPassword),
            T("new entry for my bank",        VoiceCommandIntent.AddPassword),
    T("add website account",   VoiceCommandIntent.AddPassword),
            T("remember my password",        VoiceCommandIntent.AddPassword),
     T("store a new login",        VoiceCommandIntent.AddPassword),
            T("save my linkedin password", VoiceCommandIntent.AddPassword),
    T("create password entry",      VoiceCommandIntent.AddPassword),
 T("add a password entry",    VoiceCommandIntent.AddPassword),
      T("save a new account",     VoiceCommandIntent.AddPassword),
      T("add new website credentials",    VoiceCommandIntent.AddPassword),
            T("i want to add a password",  VoiceCommandIntent.AddPassword),
  T("add a new password",           VoiceCommandIntent.AddPassword),
        T("save a password",        VoiceCommandIntent.AddPassword),
            T("i need to add a login",         VoiceCommandIntent.AddPassword),
        T("store my account password",        VoiceCommandIntent.AddPassword),
    T("add my work account",           VoiceCommandIntent.AddPassword),
T("save my bank password",     VoiceCommandIntent.AddPassword),
            T("new login entry", VoiceCommandIntent.AddPassword),
            T("add a site login", VoiceCommandIntent.AddPassword),
      T("keep my email password",      VoiceCommandIntent.AddPassword),
     T("store a login",        VoiceCommandIntent.AddPassword),
     T("put my password in the vault",    VoiceCommandIntent.AddPassword),
            T("add my reddit login",       VoiceCommandIntent.AddPassword),
          T("save my microsoft account",     VoiceCommandIntent.AddPassword),
         T("store my apple id password",        VoiceCommandIntent.AddPassword),
  T("add my paypal account",        VoiceCommandIntent.AddPassword),

            // ── AddCreditCard ────────────────────────────────────────────────
      T("save my visa card",       VoiceCommandIntent.AddCreditCard),
        T("add my mastercard",        VoiceCommandIntent.AddCreditCard),
     T("store a payment card",          VoiceCommandIntent.AddCreditCard),
   T("add my bank card",            VoiceCommandIntent.AddCreditCard),
        T("save my debit card details",      VoiceCommandIntent.AddCreditCard),
            T("i want to add a credit card",   VoiceCommandIntent.AddCreditCard),
       T("new payment method",   VoiceCommandIntent.AddCreditCard),
     T("store card number",        VoiceCommandIntent.AddCreditCard),
        T("add a card to my wallet",VoiceCommandIntent.AddCreditCard),
          T("keep my credit card info",           VoiceCommandIntent.AddCreditCard),
     T("save my american express card", VoiceCommandIntent.AddCreditCard),
 T("add new debit card",       VoiceCommandIntent.AddCreditCard),
  T("store my payment details",          VoiceCommandIntent.AddCreditCard),
  T("add my contactless card",  VoiceCommandIntent.AddCreditCard),
        T("save card for online shopping",VoiceCommandIntent.AddCreditCard),
  T("create a new card entry",      VoiceCommandIntent.AddCreditCard),
 T("save new payment card",    VoiceCommandIntent.AddCreditCard),
        T("add a new credit card",       VoiceCommandIntent.AddCreditCard),
  T("i want to save my card",          VoiceCommandIntent.AddCreditCard),
            T("store my credit card number",         VoiceCommandIntent.AddCreditCard),
            T("add my debit card",          VoiceCommandIntent.AddCreditCard),
            T("save payment card",      VoiceCommandIntent.AddCreditCard),
            T("new credit card entry",     VoiceCommandIntent.AddCreditCard),

        // ── AddAuthenticator ─────────────────────────────────────────────
         T("add two factor authentication",  VoiceCommandIntent.AddAuthenticator),
   T("save my 2fa code",       VoiceCommandIntent.AddAuthenticator),
T("add an authenticator",     VoiceCommandIntent.AddAuthenticator),
            T("store totp secret",   VoiceCommandIntent.AddAuthenticator),
            T("new one time password",           VoiceCommandIntent.AddAuthenticator),
  T("add otp for google",          VoiceCommandIntent.AddAuthenticator),
            T("set up two factor",   VoiceCommandIntent.AddAuthenticator),
            T("save verification code generator",       VoiceCommandIntent.AddAuthenticator),
            T("add authenticator app entry",            VoiceCommandIntent.AddAuthenticator),
       T("add 2fa",            VoiceCommandIntent.AddAuthenticator),
    T("save an authenticator",  VoiceCommandIntent.AddAuthenticator),
 T("new 2fa entry",     VoiceCommandIntent.AddAuthenticator),
      T("add two step verification",  VoiceCommandIntent.AddAuthenticator),
      T("store my authenticator",    VoiceCommandIntent.AddAuthenticator),
            T("add a new authenticator code",       VoiceCommandIntent.AddAuthenticator),
  T("set up 2fa for my account",           VoiceCommandIntent.AddAuthenticator),
    T("add my google authenticator",        VoiceCommandIntent.AddAuthenticator),

     // ── AddSecureNote ────────────────────────────────────────────────
         T("add a secure note",              VoiceCommandIntent.AddSecureNote),
    T("save a private note",       VoiceCommandIntent.AddSecureNote),
     T("create a secret memo",  VoiceCommandIntent.AddSecureNote),
     T("store an encrypted note",       VoiceCommandIntent.AddSecureNote),
            T("i need to keep a note",            VoiceCommandIntent.AddSecureNote),
 T("save text note securely",VoiceCommandIntent.AddSecureNote),
        T("new private text",          VoiceCommandIntent.AddSecureNote),
            T("add a note to my vault",    VoiceCommandIntent.AddSecureNote),
    T("store a confidential note",              VoiceCommandIntent.AddSecureNote),
    T("save my secret information",        VoiceCommandIntent.AddSecureNote),
            T("add encrypted memo",       VoiceCommandIntent.AddSecureNote),
            T("keep a private record",      VoiceCommandIntent.AddSecureNote),
  T("create a new note",      VoiceCommandIntent.AddSecureNote),
            T("add a new note",       VoiceCommandIntent.AddSecureNote),
            T("i want to add a note",       VoiceCommandIntent.AddSecureNote),
         T("save a secure note",           VoiceCommandIntent.AddSecureNote),
       T("new note in my vault",           VoiceCommandIntent.AddSecureNote),
  T("add private memo",   VoiceCommandIntent.AddSecureNote),

  // ── AddIdentity ──────────────────────────────────────────────────
            T("add a person",   VoiceCommandIntent.AddIdentity),
     T("save my identity",       VoiceCommandIntent.AddIdentity),
            T("add contact details",           VoiceCommandIntent.AddIdentity),
            T("store my personal profile",              VoiceCommandIntent.AddIdentity),
      T("create a new identity",      VoiceCommandIntent.AddIdentity),
     T("add passport information",  VoiceCommandIntent.AddIdentity),
      T("keep my personal info",         VoiceCommandIntent.AddIdentity),
        T("save a contact",    VoiceCommandIntent.AddIdentity),
            T("new personal details entry",             VoiceCommandIntent.AddIdentity),
     T("add my drivers licence",          VoiceCommandIntent.AddIdentity),
      T("save my national id",    VoiceCommandIntent.AddIdentity),
  T("store personal identification",          VoiceCommandIntent.AddIdentity),
    T("create a new identity profile",    VoiceCommandIntent.AddIdentity),
     T("add a new person",              VoiceCommandIntent.AddIdentity),
  T("add a new identity", VoiceCommandIntent.AddIdentity),
            T("save personal profile",VoiceCommandIntent.AddIdentity),
     T("new identity entry",          VoiceCommandIntent.AddIdentity),
            T("add my personal details",     VoiceCommandIntent.AddIdentity),

   // ── AddAddress ───────────────────────────────────────────────────
       T("save my home address",              VoiceCommandIntent.AddAddress),
    T("add my work address",          VoiceCommandIntent.AddAddress),
            T("store a shipping address",   VoiceCommandIntent.AddAddress),
            T("add billing address",          VoiceCommandIntent.AddAddress),
            T("keep my postal address",      VoiceCommandIntent.AddAddress),
      T("i want to add an address",  VoiceCommandIntent.AddAddress),
 T("save delivery address",  VoiceCommandIntent.AddAddress),
         T("record my street address",         VoiceCommandIntent.AddAddress),
            T("add my mailing address",         VoiceCommandIntent.AddAddress),
          T("store my residential address",   VoiceCommandIntent.AddAddress),
            T("new address entry",   VoiceCommandIntent.AddAddress),
            T("add a location",              VoiceCommandIntent.AddAddress),
            T("save an address",             VoiceCommandIntent.AddAddress),
T("create a new address",      VoiceCommandIntent.AddAddress),
        T("add my address",       VoiceCommandIntent.AddAddress),
     T("save my address",       VoiceCommandIntent.AddAddress),
     T("store a new address",            VoiceCommandIntent.AddAddress),
       T("add a new address",        VoiceCommandIntent.AddAddress),

            // ── OpenPasswords ────────────────────────────────────────────────
        T("open my passwords",      VoiceCommandIntent.OpenPasswords),
        T("show me my passwords",       VoiceCommandIntent.OpenPasswords),
       T("go to passwords",             VoiceCommandIntent.OpenPasswords),
  T("view my logins", VoiceCommandIntent.OpenPasswords),
   T("take me to my passwords",       VoiceCommandIntent.OpenPasswords),
            T("open the passwords section",       VoiceCommandIntent.OpenPasswords),
 T("show my login list",            VoiceCommandIntent.OpenPasswords),
            T("navigate to passwords",           VoiceCommandIntent.OpenPasswords),
            T("show passwords",    VoiceCommandIntent.OpenPasswords),
     T("list my passwords",       VoiceCommandIntent.OpenPasswords),
            T("display my passwords",     VoiceCommandIntent.OpenPasswords),
T("show all my passwords",    VoiceCommandIntent.OpenPasswords),
     T("let me see my passwords",                VoiceCommandIntent.OpenPasswords),
       T("view all passwords", VoiceCommandIntent.OpenPasswords),
  T("show my saved passwords",          VoiceCommandIntent.OpenPasswords),
  T("find my passwords",           VoiceCommandIntent.OpenPasswords),
     T("show my logins",           VoiceCommandIntent.OpenPasswords),
T("list all logins",    VoiceCommandIntent.OpenPasswords),
            T("view my accounts",         VoiceCommandIntent.OpenPasswords),
    T("show my accounts", VoiceCommandIntent.OpenPasswords),
            T("show me logins",               VoiceCommandIntent.OpenPasswords),
    T("display my logins",          VoiceCommandIntent.OpenPasswords),
      T("open logins",           VoiceCommandIntent.OpenPasswords),
            T("go to my logins",        VoiceCommandIntent.OpenPasswords),
       T("see my passwords",    VoiceCommandIntent.OpenPasswords),
      T("browse my passwords",        VoiceCommandIntent.OpenPasswords),
   T("show me all my logins",VoiceCommandIntent.OpenPasswords),

 // ── OpenCreditCards ──────────────────────────────────────────────
 T("open my credit cards",    VoiceCommandIntent.OpenCreditCards),
      T("show me my cards",  VoiceCommandIntent.OpenCreditCards),
  T("go to credit cards",             VoiceCommandIntent.OpenCreditCards),
         T("view my payment cards", VoiceCommandIntent.OpenCreditCards),
        T("take me to my cards",            VoiceCommandIntent.OpenCreditCards),
  T("open the cards section",   VoiceCommandIntent.OpenCreditCards),
    T("show my credit card list",            VoiceCommandIntent.OpenCreditCards),
            T("show my cards",                VoiceCommandIntent.OpenCreditCards),
        T("list my cards",  VoiceCommandIntent.OpenCreditCards),
  T("display my credit cards", VoiceCommandIntent.OpenCreditCards),
            T("show all my cards",       VoiceCommandIntent.OpenCreditCards),
            T("let me see my cards",     VoiceCommandIntent.OpenCreditCards),
      T("view all credit cards",   VoiceCommandIntent.OpenCreditCards),
     T("show my saved cards",       VoiceCommandIntent.OpenCreditCards),
            T("find my credit cards",          VoiceCommandIntent.OpenCreditCards),
            T("show me credit cards",       VoiceCommandIntent.OpenCreditCards),
       T("open cards",            VoiceCommandIntent.OpenCreditCards),
     T("go to my cards",             VoiceCommandIntent.OpenCreditCards),
      T("see my cards",VoiceCommandIntent.OpenCreditCards),
            T("show my debit cards",   VoiceCommandIntent.OpenCreditCards),
         T("view my cards",        VoiceCommandIntent.OpenCreditCards),

            // ── OpenAuthenticators ───────────────────────────────────────────
            T("open my authenticators",           VoiceCommandIntent.OpenAuthenticators),
      T("show my 2fa codes",        VoiceCommandIntent.OpenAuthenticators),
   T("go to authenticators",                VoiceCommandIntent.OpenAuthenticators),
            T("view my one time passwords",   VoiceCommandIntent.OpenAuthenticators),
            T("open the authenticator section",         VoiceCommandIntent.OpenAuthenticators),
 T("show my two factor codes",   VoiceCommandIntent.OpenAuthenticators),
            T("show authenticators",         VoiceCommandIntent.OpenAuthenticators),
   T("list my 2fa codes",     VoiceCommandIntent.OpenAuthenticators),
         T("display my authenticators",        VoiceCommandIntent.OpenAuthenticators),
    T("show all my 2fa",       VoiceCommandIntent.OpenAuthenticators),
 T("let me see my authenticators",         VoiceCommandIntent.OpenAuthenticators),
 T("view all authenticators",        VoiceCommandIntent.OpenAuthenticators),
 T("show my otp codes",        VoiceCommandIntent.OpenAuthenticators),
 T("find my authenticators",      VoiceCommandIntent.OpenAuthenticators),
      T("show me authenticators",           VoiceCommandIntent.OpenAuthenticators),
          T("open two factor codes",   VoiceCommandIntent.OpenAuthenticators),
    T("go to my 2fa",   VoiceCommandIntent.OpenAuthenticators),
            T("see my 2fa codes",     VoiceCommandIntent.OpenAuthenticators),
 T("show my totp codes",   VoiceCommandIntent.OpenAuthenticators),
       T("open my 2fa codes",       VoiceCommandIntent.OpenAuthenticators),
            T("show two factor authentication",     VoiceCommandIntent.OpenAuthenticators),

            // ── OpenSecureNotes ──────────────────────────────────────────────
     T("open my secure notes", VoiceCommandIntent.OpenSecureNotes),
            T("show me my notes",           VoiceCommandIntent.OpenSecureNotes),
            T("go to notes",          VoiceCommandIntent.OpenSecureNotes),
      T("view my private notes",          VoiceCommandIntent.OpenSecureNotes),
   T("open the notes section",     VoiceCommandIntent.OpenSecureNotes),
          T("show my secret notes",    VoiceCommandIntent.OpenSecureNotes),
     T("show notes",                 VoiceCommandIntent.OpenSecureNotes),
       T("list my secure notes",VoiceCommandIntent.OpenSecureNotes),
 T("display my notes",          VoiceCommandIntent.OpenSecureNotes),
        T("show all my notes",              VoiceCommandIntent.OpenSecureNotes),
            T("let me see my notes",               VoiceCommandIntent.OpenSecureNotes),
   T("view all secure notes",        VoiceCommandIntent.OpenSecureNotes),
         T("show my saved notes",           VoiceCommandIntent.OpenSecureNotes),
   T("find my notes",    VoiceCommandIntent.OpenSecureNotes),
            T("see my notes",            VoiceCommandIntent.OpenSecureNotes),
            T("open notes",                 VoiceCommandIntent.OpenSecureNotes),
        T("show my encrypted notes",  VoiceCommandIntent.OpenSecureNotes),

         // ── OpenIdentities ───────────────────────────────────────────────
        T("open my identities",      VoiceCommandIntent.OpenIdentities),
      T("show my identity documents",             VoiceCommandIntent.OpenIdentities),
            T("go to identities",   VoiceCommandIntent.OpenIdentities),
   T("view my personal profiles",           VoiceCommandIntent.OpenIdentities),
         T("open the identities section",   VoiceCommandIntent.OpenIdentities),
            T("show identities",   VoiceCommandIntent.OpenIdentities),
        T("list my identities",       VoiceCommandIntent.OpenIdentities),
T("display my identities",       VoiceCommandIntent.OpenIdentities),
            T("show all my identities",                 VoiceCommandIntent.OpenIdentities),
            T("let me see my identities",    VoiceCommandIntent.OpenIdentities),
         T("view all identities",             VoiceCommandIntent.OpenIdentities),
            T("show my contacts",        VoiceCommandIntent.OpenIdentities),
            T("find my identities",     VoiceCommandIntent.OpenIdentities),
   T("see my identities",  VoiceCommandIntent.OpenIdentities),
            T("show me my identities",     VoiceCommandIntent.OpenIdentities),
        T("open contacts",    VoiceCommandIntent.OpenIdentities),

 // ── OpenGroups ───────────────────────────────────────────────────
            T("open my groups",          VoiceCommandIntent.OpenGroups),
      T("show me my groups",          VoiceCommandIntent.OpenGroups),
    T("go to groups",    VoiceCommandIntent.OpenGroups),
       T("view my vault groups",  VoiceCommandIntent.OpenGroups),
      T("open the groups section",   VoiceCommandIntent.OpenGroups),
            T("show groups",      VoiceCommandIntent.OpenGroups),
            T("list my groups",      VoiceCommandIntent.OpenGroups),
T("display my groups",             VoiceCommandIntent.OpenGroups),
            T("show all my groups",      VoiceCommandIntent.OpenGroups),
            T("let me see my groups",            VoiceCommandIntent.OpenGroups),
   T("see my groups",          VoiceCommandIntent.OpenGroups),

         // ── OpenSettings ─────────────────────────────────────────────────
      T("open settings",             VoiceCommandIntent.OpenSettings),
    T("go to settings",            VoiceCommandIntent.OpenSettings),
            T("show app settings",     VoiceCommandIntent.OpenSettings),
            T("open the settings menu",         VoiceCommandIntent.OpenSettings),
       T("navigate to settings",  VoiceCommandIntent.OpenSettings),
        T("show settings",          VoiceCommandIntent.OpenSettings),
       T("open preferences",     VoiceCommandIntent.OpenSettings),
            T("go to preferences",            VoiceCommandIntent.OpenSettings),
            T("show me settings",          VoiceCommandIntent.OpenSettings),
    T("open app settings",        VoiceCommandIntent.OpenSettings),
   T("view settings",         VoiceCommandIntent.OpenSettings),
            T("take me to settings",              VoiceCommandIntent.OpenSettings),

  // ── HowManyPasswords ─────────────────────────────────────────────
        T("how many passwords do i have",           VoiceCommandIntent.HowManyPasswords),
            T("how many logins are saved",              VoiceCommandIntent.HowManyPasswords),
      T("count my passwords",         VoiceCommandIntent.HowManyPasswords),
      T("total number of passwords",   VoiceCommandIntent.HowManyPasswords),
        T("how many accounts are stored",VoiceCommandIntent.HowManyPasswords),
            T("tell me my password count",          VoiceCommandIntent.HowManyPasswords),
            T("how many passwords have i saved",        VoiceCommandIntent.HowManyPasswords),
          T("what is my password count",         VoiceCommandIntent.HowManyPasswords),
 T("how many logins do i have",        VoiceCommandIntent.HowManyPasswords),
            T("number of passwords stored",         VoiceCommandIntent.HowManyPasswords),
            T("how many passwords are in my vault",     VoiceCommandIntent.HowManyPasswords),
            T("tell me how many passwords i have",      VoiceCommandIntent.HowManyPasswords),
    T("how many accounts do i have",            VoiceCommandIntent.HowManyPasswords),

     // ── HowManyCards ─────────────────────────────────────────────────
       T("how many cards do i have",   VoiceCommandIntent.HowManyCards),
 T("how many credit cards saved",VoiceCommandIntent.HowManyCards),
   T("count my credit cards",        VoiceCommandIntent.HowManyCards),
   T("total number of cards",       VoiceCommandIntent.HowManyCards),
    T("how many payment cards stored",    VoiceCommandIntent.HowManyCards),
      T("how many cards have i saved",  VoiceCommandIntent.HowManyCards),
        T("what is my card count",      VoiceCommandIntent.HowManyCards),
            T("how many debit cards do i have",    VoiceCommandIntent.HowManyCards),
       T("number of cards stored",   VoiceCommandIntent.HowManyCards),
    T("how many cards are in my vault",       VoiceCommandIntent.HowManyCards),
        T("tell me how many cards i have",          VoiceCommandIntent.HowManyCards),

    // ── LockVault ────────────────────────────────────────────────────
   T("lock my vault",        VoiceCommandIntent.LockVault),
       T("lock the app",  VoiceCommandIntent.LockVault),
            T("lock fortress",          VoiceCommandIntent.LockVault),
      T("secure my vault now",          VoiceCommandIntent.LockVault),
  T("i want to lock my vault",      VoiceCommandIntent.LockVault),
        T("lock everything",        VoiceCommandIntent.LockVault),
       T("lock fortress",          VoiceCommandIntent.LockVault),
          T("lock the vault",      VoiceCommandIntent.LockVault),
            T("lock up my vault",  VoiceCommandIntent.LockVault),
  T("secure the app",         VoiceCommandIntent.LockVault),
            T("lock the app now", VoiceCommandIntent.LockVault),
            T("put the vault on lock",       VoiceCommandIntent.LockVault),
            T("secure my vault", VoiceCommandIntent.LockVault),

// ── VaultSummary ─────────────────────────────────────────────────
     T("give me a vault summary",         VoiceCommandIntent.VaultSummary),
   T("what is in my vault",     VoiceCommandIntent.VaultSummary),
    T("tell me about my vault",       VoiceCommandIntent.VaultSummary),
         T("vault overview",     VoiceCommandIntent.VaultSummary),
       T("summarise my vault",            VoiceCommandIntent.VaultSummary),
            T("what do i have stored",   VoiceCommandIntent.VaultSummary),
     T("vault statistics",       VoiceCommandIntent.VaultSummary),
            T("give me a summary",          VoiceCommandIntent.VaultSummary),
            T("what is in my fortress",      VoiceCommandIntent.VaultSummary),
      T("overview of my vault",     VoiceCommandIntent.VaultSummary),
     T("tell me what is in my vault",       VoiceCommandIntent.VaultSummary),
          T("show me a summary of my vault",          VoiceCommandIntent.VaultSummary),
    T("vault summary please",       VoiceCommandIntent.VaultSummary),
          T("what have i got in my vault",   VoiceCommandIntent.VaultSummary),
            T("show vault overview",          VoiceCommandIntent.VaultSummary),
     T("give me an overview of my vault",        VoiceCommandIntent.VaultSummary),
            T("what do i have in my vault",             VoiceCommandIntent.VaultSummary),
      T("tell me my vault contents",   VoiceCommandIntent.VaultSummary),
       T("show me my vault stats",  VoiceCommandIntent.VaultSummary),
            T("how many things are in my vault",        VoiceCommandIntent.VaultSummary),

      // ── ShowVaultHealth ──────────────────────────────────────────────
            T("show my vault health",    VoiceCommandIntent.ShowVaultHealth),
        T("what is my vault health score",  VoiceCommandIntent.ShowVaultHealth),
  T("check my vault health",           VoiceCommandIntent.ShowVaultHealth),
            T("open vault health",                VoiceCommandIntent.ShowVaultHealth),
            T("show my security score",        VoiceCommandIntent.ShowVaultHealth),
            T("what is my security score",          VoiceCommandIntent.ShowVaultHealth),
            T("how secure is my vault",    VoiceCommandIntent.ShowVaultHealth),
   T("check security score",      VoiceCommandIntent.ShowVaultHealth),
      T("view vault health report",   VoiceCommandIntent.ShowVaultHealth),
     T("show me the health report",   VoiceCommandIntent.ShowVaultHealth),
            T("how is my vault health",   VoiceCommandIntent.ShowVaultHealth),
            T("show vault health score",                VoiceCommandIntent.ShowVaultHealth),
     T("how healthy is my vault",                VoiceCommandIntent.ShowVaultHealth),
 T("is my vault secure",          VoiceCommandIntent.ShowVaultHealth),
            T("check my password security",  VoiceCommandIntent.ShowVaultHealth),
            T("how strong are my passwords",        VoiceCommandIntent.ShowVaultHealth),

       // ── ShowRiskyAccounts ────────────────────────────────────────────
          T("show me my risky accounts",  VoiceCommandIntent.ShowRiskyAccounts),
     T("which accounts are at risk", VoiceCommandIntent.ShowRiskyAccounts),
            T("show risky accounts",         VoiceCommandIntent.ShowRiskyAccounts),
         T("what accounts need attention",    VoiceCommandIntent.ShowRiskyAccounts),
            T("show accounts at risk",   VoiceCommandIntent.ShowRiskyAccounts),
            T("how many risky accounts do i have",      VoiceCommandIntent.ShowRiskyAccounts),
            T("list risky accounts",                VoiceCommandIntent.ShowRiskyAccounts),
       T("which logins are unsafe",         VoiceCommandIntent.ShowRiskyAccounts),
     T("show unsafe accounts",        VoiceCommandIntent.ShowRiskyAccounts),
     T("which accounts are unsafe",              VoiceCommandIntent.ShowRiskyAccounts),
            T("what are my risky passwords",         VoiceCommandIntent.ShowRiskyAccounts),

            // ── ShowReusedPasswords ──────────────────────────────────────────
            T("show me reused passwords",               VoiceCommandIntent.ShowReusedPasswords),
       T("which passwords are reused",             VoiceCommandIntent.ShowReusedPasswords),
            T("do i have reused passwords",             VoiceCommandIntent.ShowReusedPasswords),
 T("show reused passwords",                  VoiceCommandIntent.ShowReusedPasswords),
T("find reused passwords",           VoiceCommandIntent.ShowReusedPasswords),
            T("how many reused passwords do i have",    VoiceCommandIntent.ShowReusedPasswords),
   T("list reused passwords",          VoiceCommandIntent.ShowReusedPasswords),
    T("are any passwords reused",        VoiceCommandIntent.ShowReusedPasswords),
         T("check for reused passwords",    VoiceCommandIntent.ShowReusedPasswords),
            T("am i reusing any passwords",    VoiceCommandIntent.ShowReusedPasswords),
       T("do i reuse passwords", VoiceCommandIntent.ShowReusedPasswords),

       // ── ShowWeakPasswords ────────────────────────────────────────────
        T("show me weak passwords",        VoiceCommandIntent.ShowWeakPasswords),
            T("which passwords are weak",   VoiceCommandIntent.ShowWeakPasswords),
   T("do i have weak passwords",    VoiceCommandIntent.ShowWeakPasswords),
        T("show weak passwords",                 VoiceCommandIntent.ShowWeakPasswords),
            T("find weak passwords",          VoiceCommandIntent.ShowWeakPasswords),
  T("how many weak passwords do i have",      VoiceCommandIntent.ShowWeakPasswords),
            T("list weak passwords",   VoiceCommandIntent.ShowWeakPasswords),
  T("are any passwords weak",          VoiceCommandIntent.ShowWeakPasswords),
            T("check for weak passwords",     VoiceCommandIntent.ShowWeakPasswords),
  T("which passwords need to be stronger",    VoiceCommandIntent.ShowWeakPasswords),
   T("show me my bad passwords",  VoiceCommandIntent.ShowWeakPasswords),

         // ── ShowBreachedAccounts ─────────────────────────────────────────
         T("show me breached accounts",         VoiceCommandIntent.ShowBreachedAccounts),
  T("which passwords have been breached",     VoiceCommandIntent.ShowBreachedAccounts),
 T("do i have any breached passwords",       VoiceCommandIntent.ShowBreachedAccounts),
    T("show breached passwords",  VoiceCommandIntent.ShowBreachedAccounts),
            T("check for data breaches",      VoiceCommandIntent.ShowBreachedAccounts),
        T("have i been breached",          VoiceCommandIntent.ShowBreachedAccounts),
            T("any compromised passwords",     VoiceCommandIntent.ShowBreachedAccounts),
            T("are my passwords leaked",      VoiceCommandIntent.ShowBreachedAccounts),
            T("show compromised accounts",              VoiceCommandIntent.ShowBreachedAccounts),
            T("have any of my passwords been leaked",   VoiceCommandIntent.ShowBreachedAccounts),
   T("check if my passwords are compromised",  VoiceCommandIntent.ShowBreachedAccounts),
T("are any of my accounts hacked",     VoiceCommandIntent.ShowBreachedAccounts),

            // ── ListMissing2FA ───────────────────────────────────────────────
       T("which accounts are missing two factor",  VoiceCommandIntent.ListMissing2FA),
            T("show accounts without 2fa",   VoiceCommandIntent.ListMissing2FA),
    T("which logins have no authenticator",     VoiceCommandIntent.ListMissing2FA),
         T("show missing two factor authentication", VoiceCommandIntent.ListMissing2FA),
T("how many accounts lack 2fa",      VoiceCommandIntent.ListMissing2FA),
    T("list accounts without two factor",       VoiceCommandIntent.ListMissing2FA),
            T("which sites need 2fa",     VoiceCommandIntent.ListMissing2FA),
            T("find accounts missing 2fa",              VoiceCommandIntent.ListMissing2FA),
    T("which accounts do not have 2fa",         VoiceCommandIntent.ListMissing2FA),
          T("show me accounts that need two factor",VoiceCommandIntent.ListMissing2FA),
            T("accounts without two step verification", VoiceCommandIntent.ListMissing2FA),

            // ── GeneratePassword ─────────────────────────────────────────────
      T("generate a password",              VoiceCommandIntent.GeneratePassword),
   T("create a strong password",               VoiceCommandIntent.GeneratePassword),
            T("open password generator",              VoiceCommandIntent.GeneratePassword),
            T("make a new password",   VoiceCommandIntent.GeneratePassword),
  T("generate random password",    VoiceCommandIntent.GeneratePassword),
    T("suggest a password", VoiceCommandIntent.GeneratePassword),
      T("create a secure password",             VoiceCommandIntent.GeneratePassword),
 T("show password generator",       VoiceCommandIntent.GeneratePassword),
  T("i need a new password",     VoiceCommandIntent.GeneratePassword),
       T("make me a secure password",    VoiceCommandIntent.GeneratePassword),
       T("create a random password",            VoiceCommandIntent.GeneratePassword),
            T("generate a secure password for me",      VoiceCommandIntent.GeneratePassword),
         T("i need a strong password",   VoiceCommandIntent.GeneratePassword),
 T("help me create a password",  VoiceCommandIntent.GeneratePassword),

      // ── SyncNow ──────────────────────────────────────────────────────
            T("sync my vault",   VoiceCommandIntent.SyncNow),
            T("sync now", VoiceCommandIntent.SyncNow),
   T("synchronise my vault",       VoiceCommandIntent.SyncNow),
            T("back up my vault",     VoiceCommandIntent.SyncNow),
            T("upload my vault",            VoiceCommandIntent.SyncNow),
  T("start a sync",       VoiceCommandIntent.SyncNow),
      T("do a sync",        VoiceCommandIntent.SyncNow),
       T("refresh my vault",             VoiceCommandIntent.SyncNow),
            T("sync to cloud",        VoiceCommandIntent.SyncNow),
        T("update  backup",           VoiceCommandIntent.SyncNow),
 T("backup my vault now",           VoiceCommandIntent.SyncNow),
          T("do a backup",          VoiceCommandIntent.SyncNow),
        T("synchronize vault",         VoiceCommandIntent.SyncNow),

     // ── EnableFortressMode ───────────────────────────────────────────
            T("enable fortress mode",        VoiceCommandIntent.EnableFortressMode),
       T("activate fortress mode",           VoiceCommandIntent.EnableFortressMode),
    T("turn on fortress mode",              VoiceCommandIntent.EnableFortressMode),
      T("start fortress mode",        VoiceCommandIntent.EnableFortressMode),

            // ── AttackSurface ────────────────────────────────────────────────
            T("what is my attack surface",    VoiceCommandIntent.AttackSurface),
            T("how exposed is my vault",    VoiceCommandIntent.AttackSurface),
 T("show my attack surface score",         VoiceCommandIntent.AttackSurface),
            T("what is my blast radius",                VoiceCommandIntent.AttackSurface),
            T("how at risk am i",           VoiceCommandIntent.AttackSurface),
            T("show my exposure",    VoiceCommandIntent.AttackSurface),
            T("tell me my attack surface",    VoiceCommandIntent.AttackSurface),
            T("what accounts would fall together",      VoiceCommandIntent.AttackSurface),
            T("which accounts share passwords",         VoiceCommandIntent.AttackSurface),
          T("show compromise chains",               VoiceCommandIntent.AttackSurface),
         T("how many accounts are linked",           VoiceCommandIntent.AttackSurface),
    T("which accounts are linked together",   VoiceCommandIntent.AttackSurface),
         T("show my password clusters",              VoiceCommandIntent.AttackSurface),
     T("what is my security exposure",     VoiceCommandIntent.AttackSurface),

       // ── VoiceJournal ─────────────────────────────────────────────────
            T("write a note",          VoiceCommandIntent.VoiceJournal),
        T("record a note",               VoiceCommandIntent.VoiceJournal),
     T("voice journal",  VoiceCommandIntent.VoiceJournal),
        T("start a journal entry",         VoiceCommandIntent.VoiceJournal),
         T("record my thoughts",         VoiceCommandIntent.VoiceJournal),
            T("dictate a note",         VoiceCommandIntent.VoiceJournal),
            T("write a journal entry",        VoiceCommandIntent.VoiceJournal),
      T("record a journal",      VoiceCommandIntent.VoiceJournal),
            T("take a note",      VoiceCommandIntent.VoiceJournal),
 T("capture a thought",     VoiceCommandIntent.VoiceJournal),
            T("start journaling",   VoiceCommandIntent.VoiceJournal),
            T("voice note",            VoiceCommandIntent.VoiceJournal),
     T("log a thought",  VoiceCommandIntent.VoiceJournal),
            T("record a moment",   VoiceCommandIntent.VoiceJournal),
     T("capture a moment",   VoiceCommandIntent.VoiceJournal),
            T("dictate a journal entry",   VoiceCommandIntent.VoiceJournal),
            T("save my thoughts",         VoiceCommandIntent.VoiceJournal),
            T("record what i say",      VoiceCommandIntent.VoiceJournal),
  T("write down what i say",      VoiceCommandIntent.VoiceJournal),
  T("hey fortress write a note",      VoiceCommandIntent.VoiceJournal),
 T("hey fortress record a note",       VoiceCommandIntent.VoiceJournal),
            T("hey fortress journal",   VoiceCommandIntent.VoiceJournal),
   T("quick note",   VoiceCommandIntent.VoiceJournal),
      T("memo to self",      VoiceCommandIntent.VoiceJournal),
     T("i want to record a note",    VoiceCommandIntent.VoiceJournal),
       T("let me dictate a note",          VoiceCommandIntent.VoiceJournal),
     T("i want to journal",              VoiceCommandIntent.VoiceJournal),
            T("start voice recording note",      VoiceCommandIntent.VoiceJournal),
 T("speak a note into my vault",      VoiceCommandIntent.VoiceJournal),
  T("record a voice memo", VoiceCommandIntent.VoiceJournal),
   T("fortress take a note",    VoiceCommandIntent.VoiceJournal),
     T("fortress write this down", VoiceCommandIntent.VoiceJournal),
    T("new journal entry",          VoiceCommandIntent.VoiceJournal),
       T("capture my thoughts",        VoiceCommandIntent.VoiceJournal),
        T("speak my journal",             VoiceCommandIntent.VoiceJournal),
       T("i want to write a note",   VoiceCommandIntent.VoiceJournal),
    T("i have something to record",     VoiceCommandIntent.VoiceJournal),
     T("lets journal",        VoiceCommandIntent.VoiceJournal),
   T("log my day",          VoiceCommandIntent.VoiceJournal),
   // Wake word prefixed variants
   T("hey fortress take a note",    VoiceCommandIntent.VoiceJournal),
   T("hey fortress record",         VoiceCommandIntent.VoiceJournal),
   T("hey fortress write",          VoiceCommandIntent.VoiceJournal),
   T("hey fortress dictate a note", VoiceCommandIntent.VoiceJournal),
 T("hey fortress voice journal",  VoiceCommandIntent.VoiceJournal),
   T("fortress record a note",      VoiceCommandIntent.VoiceJournal),
   T("fortress write a note",       VoiceCommandIntent.VoiceJournal),
   T("hey fortress note",     VoiceCommandIntent.VoiceJournal),
   T("hey fortress memo",         VoiceCommandIntent.VoiceJournal),
   T("hey fortress log my day",     VoiceCommandIntent.VoiceJournal),

  // ── Help ─────────────────────────────────────────────────────────
  T("help", VoiceCommandIntent.Help),
  T("what can you do", VoiceCommandIntent.Help),
  T("what can i say", VoiceCommandIntent.Help),
  T("what commands are available", VoiceCommandIntent.Help),
  T("how do i use voice commands", VoiceCommandIntent.Help),
  T("what can i ask you", VoiceCommandIntent.Help),
  T("show me the commands", VoiceCommandIntent.Help),
  T("how does this work", VoiceCommandIntent.Help),
  T("what do you do", VoiceCommandIntent.Help),
  T("what are your capabilities", VoiceCommandIntent.Help),

  // ── Greeting ─────────────────────────────────────────────────────
  T("hello", VoiceCommandIntent.Greeting),
  T("hey there", VoiceCommandIntent.Greeting),
  T("good morning", VoiceCommandIntent.Greeting),
  T("good afternoon", VoiceCommandIntent.Greeting),
  T("good evening", VoiceCommandIntent.Greeting),
  T("hi fortress", VoiceCommandIntent.Greeting),
  T("hello fortress", VoiceCommandIntent.Greeting),
  T("hey fortress", VoiceCommandIntent.Greeting),
  T("howdy", VoiceCommandIntent.Greeting),
  T("hi there", VoiceCommandIntent.Greeting),

  // ── Thanks ───────────────────────────────────────────────────────
  T("thank you", VoiceCommandIntent.Thanks),
  T("thanks", VoiceCommandIntent.Thanks),
  T("thanks a lot", VoiceCommandIntent.Thanks),
  T("cheers", VoiceCommandIntent.Thanks),
  T("appreciate it", VoiceCommandIntent.Thanks),
  T("nice one", VoiceCommandIntent.Thanks),
  T("good job", VoiceCommandIntent.Thanks),
  T("well done", VoiceCommandIntent.Thanks),
  T("thanks for your help", VoiceCommandIntent.Thanks),

  // ── Wake-word-prefixed common commands ────────────────────────────
  // Users naturally say "Hey Fortress, [command]" — these samples teach
  // the ML model to recognise wake-word-prefixed phrases directly.
  T("hey fortress lock my vault",    VoiceCommandIntent.LockVault),
  T("hey fortress lock the app",       VoiceCommandIntent.LockVault),
  T("fortress lock my vault",        VoiceCommandIntent.LockVault),
  T("hey fortress sync now",       VoiceCommandIntent.SyncNow),
  T("hey fortress backup my vault",        VoiceCommandIntent.SyncNow),
  T("fortress sync my vault", VoiceCommandIntent.SyncNow),
  T("hey fortress show my passwords",      VoiceCommandIntent.OpenPasswords),
  T("hey fortress open passwords",         VoiceCommandIntent.OpenPasswords),
  T("fortress show my passwords",          VoiceCommandIntent.OpenPasswords),
  T("hey fortress how many passwords",     VoiceCommandIntent.HowManyPasswords),
  T("hey fortress vault health",           VoiceCommandIntent.ShowVaultHealth),
  T("hey fortress how secure is my vault", VoiceCommandIntent.ShowVaultHealth),
  T("hey fortress show weak passwords", VoiceCommandIntent.ShowWeakPasswords),
  T("fortress show weak passwords",        VoiceCommandIntent.ShowWeakPasswords),
  T("hey fortress generate a password",    VoiceCommandIntent.GeneratePassword),
  T("fortress generate a password",        VoiceCommandIntent.GeneratePassword),
  T("hey fortress whats in my vault",      VoiceCommandIntent.VaultSummary),
  T("hey fortress vault summary",   VoiceCommandIntent.VaultSummary),
  T("hey fortress show my cards",          VoiceCommandIntent.OpenCreditCards),
  T("hey fortress add a login",     VoiceCommandIntent.AddPassword),
  T("hey fortress add a password",      VoiceCommandIntent.AddPassword),
  T("hey fortress add a card",             VoiceCommandIntent.AddCreditCard),
  T("hey fortress open settings",  VoiceCommandIntent.OpenSettings),
  T("hey fortress help",        VoiceCommandIntent.Help),
  T("hey fortress what can you do",     VoiceCommandIntent.Help),
    ];

        private static IntentInput T(string text, VoiceCommandIntent intent) =>
 new() { Text = text, Label = intent.ToString() };

    // ── Data types ───────────────────────────────────────────────────────

        private sealed class IntentInput
  {
   [ColumnName("Text")]
            public string Text { get; set; } = string.Empty;

          [ColumnName("Label")]
   public string Label { get; set; } = string.Empty;
        }

        private sealed class IntentPrediction
      {
      [ColumnName("PredictedLabel")]
       public string PredictedLabel { get; set; } = string.Empty;

            [ColumnName("Score")]
            public float[] Scores { get; set; } = [];
      }
    }
}

