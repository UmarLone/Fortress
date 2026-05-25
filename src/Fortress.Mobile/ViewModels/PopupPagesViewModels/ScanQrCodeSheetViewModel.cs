using Controls.UserDialogs.Maui;
using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;

namespace Fortress.ViewModels.PopupPagesViewModels
{
    // ── Args ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Optional args passed to <see cref="ScanQrCodeSheetViewModel.InitializeAsync"/>.
    /// When <see cref="ScanOnlyMode"/> is <c>true</c> the "Enter Manually" tab
    /// is hidden — used by the Import page where manual entry makes no sense.
    /// <para>
    /// <see cref="SheetTitle"/> and <see cref="SheetSubtitle"/> let each caller
    /// provide context-specific header text instead of the generic defaults.
    /// </para>
    /// </summary>
    public sealed record ScanQrArgs(
        bool ScanOnlyMode = false,
        string SheetTitle = null,
        string SheetSubtitle = null);

    // ── Threat-analysis result ────────────────────────────────────────────────

    /// <summary>Severity returned by <see cref="QrThreatAnalyser"/>.</summary>
    public enum ThreatLevel { Safe, Warning, Blocked }

    /// <summary>Result of the offline QR code threat check.</summary>
    public sealed class ThreatAnalysisResult
    {
        public ThreatLevel Level { get; init; }
        public string Summary { get; init; } = string.Empty;
        /// <summary>Raw decoded value that was analysed.</summary>
        public string Value { get; init; } = string.Empty;
    }

    // ── Pure-logic threat analyser (no external services, no network) ─────────

    /// <summary>
    /// Offline heuristic checks applied to every scanned QR payload before it
    /// is returned to the caller.
    ///
    /// Checks (in priority order):
    ///   1. Empty / whitespace payload     → Blocked
    ///   2. Excessively long payload (&gt; 2 048 chars)        → Blocked
    ///   3. Dangerous URI schemes (javascript:, data:, …)→ Blocked
    ///   4. Valid otpauth:// URI         → Safe
    ///   5. Valid otpauth-migration:// URI (Google/Microsoft)   → Safe
    ///   6. Bare Base32 secret (only A-Z 2-7 =)       → Safe
    ///   7. URL with a suspicious TLD or IP host      → Warning
    ///   8. URL containing known phishing keywords    → Warning
    ///   9. Unexpected content            → Warning
    /// </summary>
    public static class QrThreatAnalyser
    {
        private static readonly string[] DangerousSchemes =
               ["javascript:", "data:", "vbscript:", "file:", "ftp:"];

        private static readonly string[] PhishingKeywords =
       ["login", "signin", "verify", "secure", "account", "update",
             "confirm", "banking", "paypal", "password", "credential"];

        private static readonly string[] SuspiciousTlds =
                 [".xyz", ".top", ".icu", ".tk", ".ml", ".ga", ".cf", ".gq",
       ".pw", ".click", ".link", ".work", ".online"];

        public static ThreatAnalysisResult Analyse(string raw)
        {
            // 1 — Empty
            if (string.IsNullOrWhiteSpace(raw))
                return Block(raw, "QR code contains no readable data.");

            var trimmed = raw.Trim();

            // 2 — Suspiciously long
            if (trimmed.Length > 2048)
                return Block(raw, "Payload is unusually long and has been blocked.");

            var lower = trimmed.ToLowerInvariant();

            // 3 — Dangerous scheme
            foreach (var scheme in DangerousSchemes)
            {
                if (lower.StartsWith(scheme, StringComparison.Ordinal))
                    return Block(raw, $"Dangerous URI scheme '{scheme.TrimEnd(':')}' detected and blocked.");
            }

            // 4 — Valid otpauth URI (single account — the standard happy path)
            if (lower.StartsWith("otpauth://", StringComparison.Ordinal))
            {
                if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
                    return Safe(raw, "Valid authenticator QR code.");
                return Warn(raw, "Looks like an OTP URI but the format is malformed — proceed with caution.");
            }

            // 5 — Google / Microsoft Authenticator bulk-export migration QR
            //     These encode one or many accounts as a protobuf payload in
            //the "data" query parameter, e.g.:
            //       otpauth-migration://offline?data=<base64-protobuf>
            if (lower.StartsWith("otpauth-migration://", StringComparison.Ordinal))
            {
                if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
                    return Safe(raw, "Valid Google / Microsoft Authenticator export QR code.");
                return Warn(raw, "Looks like an Authenticator migration URI but the format is malformed — proceed with caution.");
            }

            // 6 — Bare Base32 secret
            if (IsBase32(trimmed))
                return Safe(raw, "Looks like a bare OTP secret key.");

            // 7 / 8 — HTTP/HTTPS URL checks
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
       && (uri.Scheme == "https" || uri.Scheme == "http"))
            {
                var host = uri.Host.ToLowerInvariant();

                if (System.Net.IPAddress.TryParse(host, out _))
                    return Warn(raw, "QR code points to a raw IP address — this is unusual for authenticator codes.");

                foreach (var tld in SuspiciousTlds)
                {
                    if (host.EndsWith(tld, StringComparison.Ordinal))
                        return Warn(raw, $"Domain ends with a high-risk TLD '{tld}'.");
                }

                var urlLower = lower;
                foreach (var kw in PhishingKeywords)
                {
                    if (urlLower.Contains(kw, StringComparison.Ordinal))
                        return Warn(raw, $"URL contains a suspicious keyword '{kw}' — verify the source before proceeding.");
                }

                return Warn(raw, "QR code contains a URL rather than an authenticator secret — verify the source.");
            }

            // 9 — Unexpected content
            return Warn(raw, "Unexpected QR code content — this does not look like an authenticator code.");
        }

        private static bool IsBase32(string s)
        {
            if (s.Length < 16) return false;
            var upper = s.ToUpperInvariant().TrimEnd('=');
            return upper.All(c => (c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7'));
        }

        private static ThreatAnalysisResult Safe(string raw, string msg) =>
         new() { Level = ThreatLevel.Safe, Summary = msg, Value = raw };

        private static ThreatAnalysisResult Warn(string raw, string msg) =>
            new() { Level = ThreatLevel.Warning, Summary = msg, Value = raw };

        private static ThreatAnalysisResult Block(string raw, string msg) =>
            new() { Level = ThreatLevel.Blocked, Summary = msg, Value = raw };
    }

    // ── ViewModel ─────────────────────────────────────────────────────────────

    public class ScanQrCodeSheetViewModel : BottomSheetViewModelBase, IPostShowInitialize
    {
        // ── Mode ─────────────────────────────────────────────────────────────/// <summary>
        /// When true (set by the Import page) the "Enter Manually" tab is
        /// hidden because manual entry makes no sense for a bulk-export scan.
        /// </summary>
        private bool _isScanOnlyMode;
        public bool IsScanOnlyMode
        {
            get => _isScanOnlyMode;
            set { SetProperty(ref _isScanOnlyMode, value); RaisePropertyChanged(nameof(ShowManualTab)); }
        }
        /// <summary>True when the manual tab should be visible (i.e. not import mode).</summary>
        public bool ShowManualTab => !_isScanOnlyMode;

        // ── Header text (caller-supplied or default) ──────────────────────────
        private string _sheetTitle = "Add Authenticator";
        public string SheetTitle
        {
            get => _sheetTitle;
            set => SetProperty(ref _sheetTitle, value);
        }

        private string _sheetSubtitle = "Scan a QR code or enter the secret manually";
        public string SheetSubtitle
        {
            get => _sheetSubtitle;
            set => SetProperty(ref _sheetSubtitle, value);
        }

        // ── Tab state ────────────────────────────────────────────────────────
        private bool _isScanTab = true;
        public bool IsScanTab
        {
            get => _isScanTab;
            set
            {
                SetProperty(ref _isScanTab, value);
                RaisePropertyChanged(nameof(IsManualTab));
            }
        }
        public bool IsManualTab => !_isScanTab;

        // ── Camera state ──────────────────────────────────────────────────────
        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set
 {
  SetProperty(ref _isScanning, value);
       RaisePropertyChanged(nameof(ScanButtonIconGlyph));
      }
        }

        private bool _isAnalyzing;
        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set => SetProperty(ref _isAnalyzing, value);
        }

        // Guard flag — prevents the barcode handler firing more than once
        // for the same scan (large / dense QR codes can trigger multiple events
        // before IsDetecting is set back to false).
        private int _scanGuard; // 0 = idle, 1 = processing

        // ── Threat-analysis state ─────────────────────────────────────────────

        private bool _isAnalysingThreat;
        public bool IsAnalysingThreat
        {
            get => _isAnalysingThreat;
            set
            {
                SetProperty(ref _isAnalysingThreat, value);
                RaisePropertyChanged(nameof(IsScanActive));
            }
        }

        public bool IsScanActive => !_isAnalysingThreat && !_hasThreatResult;

        private string _threatAnalysisText = "Analysing for threats…";
        public string ThreatAnalysisText
        {
            get => _threatAnalysisText;
            set => SetProperty(ref _threatAnalysisText, value);
        }

        private bool _hasThreatResult;
        public bool HasThreatResult
        {
            get => _hasThreatResult;
            set
            {
                SetProperty(ref _hasThreatResult, value);
                RaisePropertyChanged(nameof(IsScanActive));
            }
        }

        private bool _threatIsBlocked;
        public bool ThreatIsBlocked
        {
       get => _threatIsBlocked;
    set
            {
   SetProperty(ref _threatIsBlocked, value);
  RaisePropertyChanged(nameof(ThreatIconGlyph));
 }
    }

        private bool _threatIsWarning;
        public bool ThreatIsWarning
        {
            get => _threatIsWarning;
            set => SetProperty(ref _threatIsWarning, value);
        }

        private bool _threatIsSafe;
        public bool ThreatIsSafe
        {
            get => _threatIsSafe;
            set => SetProperty(ref _threatIsSafe, value);
        }

        private string _threatResultTitle = string.Empty;
        public string ThreatResultTitle
        {
            get => _threatResultTitle;
            set => SetProperty(ref _threatResultTitle, value);
        }

        private string _threatResultMessage = string.Empty;
        public string ThreatResultMessage
        {
            get => _threatResultMessage;
            set => SetProperty(ref _threatResultMessage, value);
        }

        // Holds the scanned value while the user decides what to do
        private string? _pendingScannedValue;

        // ── Manual entry fields ───────────────────────────────────────────────
        private string _manualIssuer = string.Empty;
        public string ManualIssuer
        {
            get => _manualIssuer;
            set { SetProperty(ref _manualIssuer, value); Validate(); }
        }

        private string _manualAccount = string.Empty;
        public string ManualAccount
        {
            get => _manualAccount;
            set { SetProperty(ref _manualAccount, value); Validate(); }
        }

        private string _manualSecret = string.Empty;
        public string ManualSecret
        {
            get => _manualSecret;
            set { SetProperty(ref _manualSecret, value); Validate(); }
        }

        private string _manualPeriod = "30";
        public string ManualPeriod
        {
            get => _manualPeriod;
            set { SetProperty(ref _manualPeriod, value); Validate(); }
        }

        // ── Validation ────────────────────────────────────────────────────────
        private string _issuerError = string.Empty;
        public string IssuerError
        {
            get => _issuerError;
            set { SetProperty(ref _issuerError, value); RaisePropertyChanged(nameof(HasIssuerError)); }
        }
        public bool HasIssuerError => !string.IsNullOrEmpty(_issuerError);

        private string _accountError = string.Empty;
        public string AccountError
        {
            get => _accountError;
            set { SetProperty(ref _accountError, value); RaisePropertyChanged(nameof(HasAccountError)); }
        }
        public bool HasAccountError => !string.IsNullOrEmpty(_accountError);

        private string _secretError = string.Empty;
        public string SecretError
        {
            get => _secretError;
            set { SetProperty(ref _secretError, value); RaisePropertyChanged(nameof(HasSecretError)); }
        }
        public bool HasSecretError => !string.IsNullOrEmpty(_secretError);

        private string _periodError = string.Empty;
        public string PeriodError
        {
            get => _periodError;
            set { SetProperty(ref _periodError, value); RaisePropertyChanged(nameof(HasPeriodError)); }
        }
        public bool HasPeriodError => !string.IsNullOrEmpty(_periodError);

        private bool _canAddManual;
        public bool CanAddManual
        {
            get => _canAddManual;
            set => SetProperty(ref _canAddManual, value);
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        // ── Computed icon glyphs ──────────────────────────────────────────────
        /// <summary>QrCodeScanner glyph while idle; Stop glyph while scanning.</summary>
        public string ScanButtonIconGlyph =>
   _isScanning ? "\ue047" : "\uf206";  // stop : qr_code_scanner

        /// <summary>Block glyph when threat is blocked; Warning glyph otherwise.</summary>
        public string ThreatIconGlyph =>
        _threatIsBlocked ? "\ue14b" : "\ue002";  // block : warning

        // ── Services ──────────────────────────────────────────────────────────
        private readonly IUserDialogs _dialogService;
        private readonly IDeviceServices _deviceService;

        public ScanQrCodeSheetViewModel(IUserDialogs dialogService, IDeviceServices deviceService)
        {
            _dialogService = dialogService;
            _deviceService = deviceService;
        }

        public override async Task InitializeAsync(object args, string title)
        {
            // Read mode flag and optional header text from caller
            if (args is ScanQrArgs scanArgs)
            {
                IsScanOnlyMode = scanArgs.ScanOnlyMode;
                if (!string.IsNullOrWhiteSpace(scanArgs.SheetTitle))
                    SheetTitle = scanArgs.SheetTitle;
                if (!string.IsNullOrWhiteSpace(scanArgs.SheetSubtitle))
                    SheetSubtitle = scanArgs.SheetSubtitle;
            }

            if (!await _deviceService.VerifyCameraPermissions())
            {
                IsScanTab = false;
                return;
            }
            StartScanner();
        }

        // ── Camera logic ──────────────────────────────────────────────────────
        public void StartScanner()
        {
            Interlocked.Exchange(ref _scanGuard, 0); // reset guard on every (re)start
            IsScanning = false;
            IsAnalyzing = false;
            ExecuteStartReaderCommand();
        }

        private void ExecuteStartReaderCommand()
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
              using var dlg = _dialogService.Loading("Starting scanner...", maskType: MaskType.Gradient);
              await Task.Delay(400);
              IsScanning = true;
              IsAnalyzing = true;
          });
        }

        private void ExecuteStopReaderCommand()
        {
            IsScanning = false;
            IsAnalyzing = false;
        }

        // ── Barcode scanned ───────────────────────────────────────────────────

        private void ExecuteOnBarcodeScannedCommand(object obj)
        {
            // Atomically claim the guard — drop duplicate frames from the same scan.
   if (Interlocked.CompareExchange(ref _scanGuard, 1, 0) != 0)
  return;

     // Use InvokeOnMainThreadAsync (returns Task) instead of
    // BeginInvokeOnMainThread (takes Action) so the async body is
            // properly awaited and exceptions are not silently swallowed.
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
         {
   try
       {
    if (obj is not ZXing.Net.Maui.BarcodeResult result
          || string.IsNullOrEmpty(result.Value))
    {
    // Release guard so the scanner can try again
 Interlocked.Exchange(ref _scanGuard, 0);
      return;
      }

  // Stop the camera before processing
      IsScanning = false;
          IsAnalyzing = false;

     await RunThreatAnalysisAsync(result.Value);
      }
        catch (Exception ex)
       {
    // Without the debugger, unhandled exceptions in async-in-Action
    // lambdas are completely silent. Log and reset so the user can retry.
             System.Diagnostics.Debug.WriteLine($"[QR] Scan handler failed: {ex}");
       Interlocked.Exchange(ref _scanGuard, 0);
      IsScanning = false;
   IsAnalyzing = false;
   }
  });
        }

        /// <summary>
        /// Runs the threat-analysis pipeline and either returns the value
        /// (Safe), shows a warning with Proceed/Cancel (Warning), or blocks
        /// with an informational message (Blocked).
        /// </summary>
        private async Task RunThreatAnalysisAsync(string scannedValue)
        {
            _pendingScannedValue = scannedValue;
            ThreatAnalysisText = "Analysing for threats…";
            IsAnalysingThreat = true;
            HasThreatResult = false;

            // Small artificial delay so the animation is visible even for fast checks
            await Task.Delay(600);

            var analysis = QrThreatAnalyser.Analyse(scannedValue);

            IsAnalysingThreat = false;

            switch (analysis.Level)
            {
                case ThreatLevel.Safe:
                    // No result card needed — return immediately
                    ReturnResult?.Invoke(scannedValue);
                    DismissAction?.Invoke();
                    break;

                case ThreatLevel.Warning:
                    ThreatResultTitle = "Potential Risk Detected";
                    ThreatResultMessage = analysis.Summary;
                    ThreatIsBlocked = false;
                    ThreatIsWarning = true;
                    ThreatIsSafe = false;
                    HasThreatResult = true;
                    break;

                case ThreatLevel.Blocked:
                    ThreatResultTitle = "Threat Blocked";
                    ThreatResultMessage = analysis.Summary;
                    ThreatIsBlocked = true;
                    ThreatIsWarning = false;
                    ThreatIsSafe = false;
                    HasThreatResult = true;
                    _pendingScannedValue = null; // can't proceed
                    break;
            }
        }

        // ── Threat result actions ─────────────────────────────────────────────

        private void ExecuteProceedAnywayCommand()
        {
            if (_pendingScannedValue is null) return;
            HasThreatResult = false;
            ReturnResult?.Invoke(_pendingScannedValue);
            DismissAction?.Invoke();
        }

        private void ExecuteDismissThreatCommand()
        {
            _pendingScannedValue = null;
            HasThreatResult = false;
            IsScanTab = true;
            StartScanner();
        }

        // ── Tab switching ─────────────────────────────────────────────────────
        private void SwitchToScan()
        {
            IsScanTab = true;
            if (!IsScanning) StartScanner();
        }

        private void SwitchToManual()
        {
            ExecuteStopReaderCommand();
            IsScanTab = false;
        }

        // ── Manual validation ─────────────────────────────────────────────────
        private bool _validated;

        private void Validate()
        {
            if (!_validated) { CanAddManual = IsFormValid(); return; }
            ShowErrors();
            CanAddManual = IsFormValid();
        }

        private bool IsFormValid()
        {
            if (string.IsNullOrWhiteSpace(ManualIssuer)) return false;
            if (string.IsNullOrWhiteSpace(ManualSecret)) return false;
            if (!IsValidBase32(ManualSecret.Trim())) return false;
            if (!int.TryParse(ManualPeriod, out int p) || p < 15 || p > 120) return false;
            return true;
        }

        private void ShowErrors()
        {
            IssuerError = string.IsNullOrWhiteSpace(ManualIssuer) ? "Issuer / service name is required" : string.Empty;

            if (string.IsNullOrWhiteSpace(ManualSecret))
                SecretError = "Secret key is required";
            else if (!IsValidBase32(ManualSecret.Trim()))
                SecretError = "Must be a valid Base32 secret (A–Z, 2–7, no spaces)";
            else
                SecretError = string.Empty;

            if (!int.TryParse(ManualPeriod, out int p) || p < 15 || p > 120)
                PeriodError = "Period must be a number between 15 and 120";
            else
                PeriodError = string.Empty;

            AccountError = string.Empty;
        }

        private static bool IsValidBase32(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var clean = value.ToUpperInvariant().Replace(" ", "").TrimEnd('=');
            return clean.All(c => (c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7'));
        }

        // ── Manual add ────────────────────────────────────────────────────────
        private void ExecuteAddManualCommand()
        {
            _validated = true;
            ShowErrors();
            if (!IsFormValid()) { CanAddManual = false; return; }

            var account = string.IsNullOrWhiteSpace(ManualAccount)
                 ? ManualIssuer
                  : $"{ManualIssuer}:{ManualAccount.Trim()}";

            var secret = ManualSecret.Trim().ToUpperInvariant();
            var period = ManualPeriod.Trim();

            var uri = $"otpauth://totp/{Uri.EscapeDataString(account)}" +
                    $"?secret={secret}" +
                   $"&issuer={Uri.EscapeDataString(ManualIssuer.Trim())}" +
         $"&algorithm=SHA1&digits=6&period={period}";

            ReturnResult?.Invoke(uri);
            DismissAction?.Invoke();
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public DelegateCommand<object> QrCodeResultCommand =>
                 new DelegateCommand<object>(ExecuteOnBarcodeScannedCommand);

        public DelegateCommand StartReaderCommand =>
    new DelegateCommand(ExecuteStartReaderCommand);

        public DelegateCommand StopReaderCommand =>
                  new DelegateCommand(ExecuteStopReaderCommand);

        public DelegateCommand ToggleScanCommand =>
               new DelegateCommand(() =>
      {
          if (IsScanning)
              ExecuteStopReaderCommand();
          else
              ExecuteStartReaderCommand();
      });

        public DelegateCommand SwitchToScanCommand =>
  new DelegateCommand(SwitchToScan);

        public DelegateCommand SwitchToManualCommand =>
  new DelegateCommand(SwitchToManual);

        public DelegateCommand AddManualCommand =>
 new DelegateCommand(ExecuteAddManualCommand);

        public DelegateCommand ProceedAnywayCommand =>
          new DelegateCommand(ExecuteProceedAnywayCommand);

        public DelegateCommand DismissThreatCommand =>
         new DelegateCommand(ExecuteDismissThreatCommand);

        public DialogCloseListener RequestClose { get; private set; }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }
    }
}
