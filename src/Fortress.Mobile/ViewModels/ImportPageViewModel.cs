using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Services;
using Fortress.ViewModels.PopupPagesViewModels;
using Fortress.Views.PopupPages;
using MauiIcons.Core;
using MauiIcons.Material;
using Microsoft.Extensions.Logging;

namespace Fortress.ViewModels
{
    public sealed class ImportPageViewModel : ViewModelBase
    {
        // ── Step ─────────────────────────────────────────────────────────────
        private ImportPageStep _step = ImportPageStep.PickFile;
        public ImportPageStep Step
        {
    get => _step;
         set
         {
   SetProperty(ref _step, value);
           RaisePropertyChanged(nameof(IsPickStep));
            RaisePropertyChanged(nameof(IsPreviewStep));
      RaisePropertyChanged(nameof(IsResultStep));
            RaisePropertyChanged(nameof(CurrentStepNumber));
        }
        }

        public bool IsPickStep    => Step == ImportPageStep.PickFile;
   public bool IsPreviewStep => Step == ImportPageStep.Preview;
  public bool IsResultStep  => Step == ImportPageStep.Result;
    public int  CurrentStepNumber => Step switch
    {
  ImportPageStep.Preview => 2,
 ImportPageStep.Result  => 3,
        _              => 1,
    };

        // ── Tab ──────────────────────────────────────────────────────────────
        private ImportTab _selectedTab = ImportTab.Passwords;
        public ImportTab SelectedTab
        {
        get => _selectedTab;
            set
         {
     SetProperty(ref _selectedTab, value);
    RaisePropertyChanged(nameof(IsPasswordsTab));
          RaisePropertyChanged(nameof(IsAuthenticatorsTab));
            }
        }

        public bool IsPasswordsTab      => SelectedTab == ImportTab.Passwords;
        public bool IsAuthenticatorsTab => SelectedTab == ImportTab.Authenticators;

        private DelegateCommand? _selectPasswordsTabCommand;
     public DelegateCommand SelectPasswordsTabCommand =>
_selectPasswordsTabCommand ??= new DelegateCommand(
      () => SelectedTab = ImportTab.Passwords);

        private DelegateCommand? _selectAuthenticatorsTabCommand;
        public DelegateCommand SelectAuthenticatorsTabCommand =>
      _selectAuthenticatorsTabCommand ??= new DelegateCommand(
     () => SelectedTab = ImportTab.Authenticators);

        // ── File pick ────────────────────────────────────────────────────────
        private string _selectedFileName = string.Empty;
        public string SelectedFileName
  {
        get => _selectedFileName;
set { SetProperty(ref _selectedFileName, value); RaisePropertyChanged(nameof(HasFile)); }
        }
        public bool HasFile => !string.IsNullOrWhiteSpace(_selectedFileName);

        // ── Password preview ─────────────────────────────────────────────────
   private ImportPreview? _preview;
        public ImportPreview? Preview
        {
          get => _preview;
    set
   {
       SetProperty(ref _preview, value);
   RaisePropertyChanged(nameof(FormatLabel));
     RaisePropertyChanged(nameof(HasWarnings));
      RaisePropertyChanged(nameof(Warnings));
         RaisePropertyChanged(nameof(HasValidRows));
    }
        }

        public string FormatLabel => _preview?.DetectedFormat switch
        {
            ImportSourceFormat.ChromeCSV     => "Chrome / Edge (CSV)",
  ImportSourceFormat.FirefoxCSV    => "Firefox (CSV)",
       ImportSourceFormat.SafariCSV     => "Safari (CSV)",
       ImportSourceFormat.BitwardenCSV  => "Bitwarden (CSV)",
            ImportSourceFormat.BitwardenJSON => "Bitwarden (JSON)",
  ImportSourceFormat.OnePasswordCSV  => "1Password (CSV)",
  ImportSourceFormat.OnePasswordJSON => "1Password (JSON)",
 ImportSourceFormat.DashlaneCSV   => "Dashlane (CSV)",
 ImportSourceFormat.DashlaneJSON  => "Dashlane (JSON)",
   ImportSourceFormat.LastPassCSV   => "LastPass (CSV)",
    ImportSourceFormat.GenericCSV    => "Generic CSV",
         _ => "Unknown format"
        };

        public bool HasWarnings => _preview?.Warnings.Any(w => !string.IsNullOrWhiteSpace(w)) == true;
        public IReadOnlyList<string> Warnings =>
     _preview?.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)).ToList()
      ?? (IReadOnlyList<string>)Array.Empty<string>();

        public bool HasValidRows => (_preview?.ValidRows ?? 0) > 0;

        // ── Merge option ─────────────────────────────────────────────────────
        private DuplicateMergeOption _mergeOption = DuplicateMergeOption.ImportAsDuplicate;
        public DuplicateMergeOption MergeOption
  {
  get => _mergeOption;
            set
          {
       SetProperty(ref _mergeOption, value);
     RaisePropertyChanged(nameof(MergeKeepExisting));
       RaisePropertyChanged(nameof(MergeReplace));
            RaisePropertyChanged(nameof(MergeImportAsDuplicate));
            }
        }

        public bool MergeKeepExisting
     {
         get => MergeOption == DuplicateMergeOption.KeepExisting;
            set { if (value) MergeOption = DuplicateMergeOption.KeepExisting; }
 }
     public bool MergeReplace
        {
 get => MergeOption == DuplicateMergeOption.ReplaceWithImported;
            set { if (value) MergeOption = DuplicateMergeOption.ReplaceWithImported; }
   }
        public bool MergeImportAsDuplicate
        {
            get => MergeOption == DuplicateMergeOption.ImportAsDuplicate;
     set { if (value) MergeOption = DuplicateMergeOption.ImportAsDuplicate; }
        }

        private DelegateCommand<string>? _selectMergeCommand;
        public DelegateCommand<string> SelectMergeCommand =>
         _selectMergeCommand ??= new DelegateCommand<string>(option =>
        {
      MergeOption = option switch
     {
         "KeepExisting"       => DuplicateMergeOption.KeepExisting,
           "ReplaceWithImported"=> DuplicateMergeOption.ReplaceWithImported,
           _         => DuplicateMergeOption.ImportAsDuplicate,
              };
       });

 // ── Import result ────────────────────────────────────────────────────
        private ImportResult? _result;
  public ImportResult? Result
   {
     get => _result;
      set
      {
          SetProperty(ref _result, value);
          RaisePropertyChanged(nameof(ResultSummary));
          RaisePropertyChanged(nameof(IsPasswordResult));
          RaisePropertyChanged(nameof(FinalResultSummary));
      }
        }

    public string ResultSummary => _result == null
      ? string.Empty
        : $"Imported {_result.Imported} – Skipped {_result.Skipped} – Replaced {_result.Replaced} – Failed {_result.Failed}";

        // ── Busy / status ────────────────────────────────────────────────────
   private bool _isBusy;
        public bool IsBusy
      {
            get => _isBusy;
     set => SetProperty(ref _isBusy, value);
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
 get => _statusText;
   set => SetProperty(ref _statusText, value);
    }

        // ── Authenticator import state ───────────────────────────────────────
        private AuthenticatorImportPreview? _authPreview;
        public AuthenticatorImportPreview? AuthPreview
 {
get => _authPreview;
          set
            {
            SetProperty(ref _authPreview, value);
       RaisePropertyChanged(nameof(HasAuthPreview));
         RaisePropertyChanged(nameof(AuthFormatLabel));
            RaisePropertyChanged(nameof(HasAuthValidRows));
            }
        }

        public bool HasAuthPreview => _authPreview != null;

    public string AuthFormatLabel => _authPreview?.DetectedFormat switch
        {
  AuthenticatorImportFormat.AegisJson       => "Aegis JSON",
            AuthenticatorImportFormat.RaivoJson       => "Raivo OTP JSON",
       AuthenticatorImportFormat.TwoFasJson      => "2FAS JSON",
AuthenticatorImportFormat.OtpAuthUriList  => "otpauth:// URI list",
            AuthenticatorImportFormat.GoogleMigration => "Google Authenticator",
            _ => "Authenticators",
        };

        private AuthenticatorImportResult? _authResult;
  public AuthenticatorImportResult? AuthResult
        {
  get => _authResult;
            set
            {
                SetProperty(ref _authResult, value);
                RaisePropertyChanged(nameof(AuthResultSummary));
                RaisePropertyChanged(nameof(IsAuthResult));
                RaisePropertyChanged(nameof(FinalResultSummary));
            }
        }

 public string AuthResultSummary => _authResult == null
     ? string.Empty
        : $"Imported {_authResult.Imported} · Skipped {_authResult.Skipped} · Failed {_authResult.Failed}";

        public bool IsPasswordResult => _result != null;
        public bool IsAuthResult     => _authResult != null;
        public string FinalResultSummary => _authResult != null ? AuthResultSummary : ResultSummary;
        public bool HasAuthValidRows => (_authPreview?.ValidCount ?? 0) > 0;

        // ── Fields ───────────────────────────────────────────────────────────
        private string? _pendingFilePath;

        private readonly IVaultImportService _importer;
        private readonly IBottomSheetService _sheets;
        private readonly ILogger<ImportPageViewModel> _logger;
        private readonly IEventLogProcessor _eventLogProcessor;

        // ── Constructor ──────────────────────────────────────────────────────
        public ImportPageViewModel(
            INavigationService navigationService,
            IVaultImportService importer,
         IBottomSheetService sheets,
   ILogger<ImportPageViewModel> logger,
  IEventLogProcessor eventLogProcessor)
   : base(navigationService)
        {
            _importer = importer;
            _sheets= sheets;
            _logger   = logger;
         _eventLogProcessor = eventLogProcessor;
        }

    // ── Commands ─────────────────────────────────────────────────────────
        private DelegateCommand? _pickFileCommand;
        public DelegateCommand PickFileCommand =>
            _pickFileCommand ??= new DelegateCommand(async () => await ExecutePickFileAsync());

 private DelegateCommand? _analyseCommand;
        public DelegateCommand AnalyseCommand =>
            _analyseCommand ??= new DelegateCommand(async () => await ExecuteAnalyseAsync(),
        () => HasFile && !IsBusy)
            .ObservesProperty(() => HasFile)
      .ObservesProperty(() => IsBusy);

    private DelegateCommand? _commitCommand;
        public DelegateCommand CommitCommand =>
   _commitCommand ??= new DelegateCommand(async () => await ExecuteCommitAsync(),
  () => Preview != null && HasValidRows && !IsBusy)
 .ObservesProperty(() => Preview)
    .ObservesProperty(() => HasValidRows)
    .ObservesProperty(() => IsBusy);

 private DelegateCommand? _startOverCommand;
        public DelegateCommand StartOverCommand =>
            _startOverCommand ??= new DelegateCommand(ExecuteStartOver);

      private DelegateCommand? _goBackCommand;
      public DelegateCommand GoBackCommand =>
    _goBackCommand ??= new DelegateCommand(async () =>
     await NavigationService.NavigateAsync(
         $"/{nameof(NavigationPage)}/{nameof(Views.HomePage)}/{nameof(Views.MenuPage)}"));

        // ── Authenticator commands ────────────────────────────────────────────
        private DelegateCommand? _pickAuthFileCommand;
        public DelegateCommand PickAuthFileCommand =>
_pickAuthFileCommand ??= new DelegateCommand(async () => await ExecutePickAuthFileAsync());

   private DelegateCommand? _commitAuthCommand;
        public DelegateCommand CommitAuthCommand =>
        _commitAuthCommand ??= new DelegateCommand(async () => await ExecuteCommitAuthAsync(),
 () => AuthPreview != null && !IsBusy)
            .ObservesProperty(() => AuthPreview)
    .ObservesProperty(() => IsBusy);

        private DelegateCommand? _cancelAuthPreviewCommand;
        public DelegateCommand CancelAuthPreviewCommand =>
            _cancelAuthPreviewCommand ??= new DelegateCommand(() =>
   {
       AuthPreview = null;
    AuthResult  = null;
            Step        = ImportPageStep.PickFile;
            });

     private DelegateCommand? _scanGoogleQrCommand;
        public DelegateCommand ScanGoogleQrCommand =>
            _scanGoogleQrCommand ??= new DelegateCommand(async () => await ExecuteScanGoogleQrAsync(),
          () => !IsBusy)
 .ObservesProperty(() => IsBusy);

 // ── QR scan logic ─────────────────────────────────────────────────────
        private async Task ExecuteScanGoogleQrAsync()
        {
            try
   {
        var uri = await _sheets.ShowAsync<ScanQrCodeSheet, ScanQrCodeSheetViewModel, string>(
  args: new ScanQrArgs(
    ScanOnlyMode:  true,
         SheetTitle:    "Import Authenticators",
         SheetSubtitle: "Scan a Google or Microsoft Authenticator export QR code"));
     if (!string.IsNullOrWhiteSpace(uri))
   await OnGoogleQrScannedAsync(uri);
         }
     catch (Exception ex)
            {
  _logger.LogError(ex, "ImportVM: failed to open QR scanner sheet");
    await _sheets.AlertAsync("Scanner Error", "Could not start the QR scanner.");
      }
        }

        public async Task OnGoogleQrScannedAsync(string? uri)
     {
     if (string.IsNullOrWhiteSpace(uri)) return;

            IsBusy     = true;
            StatusText = "Analysing QR…";
         AuthenticatorImportPreview? preview = null;
            try
     {
       preview = await _importer.ImportAuthenticatorsPreviewFromUrisAsync(
         new[] { uri }, CancellationToken.None);
      }
         catch (Exception ex)
            {
         _logger.LogError(ex, "ImportVM: QR preview failed");
       await _sheets.AlertAsync("Import Error",
   "Could not parse the QR code. Make sure it is a valid Google or Microsoft Authenticator export.");
      return;
}
         finally
     {
          IsBusy     = false;
         StatusText = string.Empty;
     }

   if (preview == null || preview.ValidCount == 0)
    {
                await _sheets.AlertAsync("Nothing to Import",
        "No valid authenticator accounts were found in the scanned QR code.");
      return;
            }

        SelectedTab = ImportTab.Authenticators;
AuthPreview = preview;
        Step        = ImportPageStep.Preview;
      }

        // ── Authenticator file import logic ───────────────────────────────────
        private async Task ExecutePickAuthFileAsync()
        {
      try
            {
           var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
     { DevicePlatform.Android, new[] { "application/json", "text/plain" } },
         { DevicePlatform.iOS,     new[] { "public.json", "public.plain-text" } },
                });

        var picked = await FilePicker.Default.PickAsync(
        new PickOptions { FileTypes = types, PickerTitle = "Pick authenticator backup" });
     if (picked == null) return;

         var dest = Path.Combine(FileSystem.CacheDirectory,
          $"authimport_{Guid.NewGuid():N}{Path.GetExtension(picked.FileName)}");

    using var src = await picked.OpenReadAsync();
 using var dst = File.OpenWrite(dest);
  await src.CopyToAsync(dst);

             IsBusy     = true;
  StatusText = "Analysing backup…";
     try
             {
           AuthPreview = await _importer.PreviewAuthenticatorsAsync(dest, CancellationToken.None);
                SelectedTab = ImportTab.Authenticators;
                Step        = ImportPageStep.Preview;
       }
 finally
              {
     IsBusy     = false;
    StatusText = string.Empty;
    }
            }
  catch (Exception ex)
            {
                _logger.LogError(ex, "ImportVM: auth file pick failed");
       await _sheets.AlertAsync("File Error", "Could not open the selected file.");
  }
        }

        private async Task ExecuteCommitAuthAsync()
        {
            if (AuthPreview == null) return;

            IsBusy     = true;
       StatusText = "Importing authenticators…";
            try
            {
                AuthResult  = await _importer.CommitAuthenticatorsAsync(AuthPreview, CancellationToken.None);
                AuthPreview = null;
                Step        = ImportPageStep.Result;
            }
       catch (Exception ex)
  {
      _logger.LogError(ex, "ImportVM: auth commit failed");
       await _sheets.AlertAsync("Import Error", "An error occurred while importing authenticators.");
        }
         finally
  {
      IsBusy     = false;
                StatusText = string.Empty;
   }
      }

        // ── Password file logic ───────────────────────────────────────────────
        private async Task ExecutePickFileAsync()
        {
  try
            {
          var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
  {
          { DevicePlatform.Android, new[] { "text/csv", "text/comma-separated-values",
         "application/json", "text/plain" } },
 { DevicePlatform.iOS,     new[] { "public.comma-separated-values",
     "public.json", "public.plain-text" } },
 });

        var picked = await FilePicker.Default.PickAsync(
         new PickOptions { FileTypes = types, PickerTitle = "Pick export file" });
       if (picked == null) return;

  var dest = Path.Combine(FileSystem.CacheDirectory,
       $"import_{Guid.NewGuid():N}{Path.GetExtension(picked.FileName)}");

          using var src  = await picked.OpenReadAsync();
            using var dst  = File.OpenWrite(dest);
     await src.CopyToAsync(dst);

          _pendingFilePath = dest;
     SelectedFileName = picked.FileName;
  Preview          = null;
          Step  = ImportPageStep.PickFile;
          }
   catch (Exception ex)
    {
       _logger.LogError(ex, "ImportVM: file pick failed");
         await _sheets.AlertAsync("File Error", "Could not open the selected file.");
 }
        }

        private async Task ExecuteAnalyseAsync()
 {
          if (string.IsNullOrEmpty(_pendingFilePath)) return;

            IsBusy     = true;
     StatusText = "Analysing file…";
    try
       {
                Preview = await _importer.PreviewAsync(_pendingFilePath, CancellationToken.None);
    Step    = ImportPageStep.Preview;
      }
    catch (Exception ex)
            {
        _logger.LogError(ex, "ImportVM: preview failed");
      await _sheets.AlertAsync("Analysis Error",
            "Could not parse the file. Make sure it is a supported export format.");
         }
   finally
 {
          IsBusy     = false;
    StatusText = string.Empty;
            }
        }

        private async Task ExecuteCommitAsync()
        {
         if (Preview == null) return;

         if (Preview.ValidRows == 0)
     {
   await _sheets.AlertAsync(
 "Nothing to Import",
      "The file contained no valid entries to import. " +
      "Please check the file format and try again.");
    return;
   }

            var hasDupes = Preview.DuplicateCount > 0;
   var message  = hasDupes
              ? $"Import {Preview.ValidRows} items into your vault?\n\n"
       + $"{Preview.DuplicateCount} duplicate(s) found – your selected merge option will apply."
      : $"Import {Preview.ValidRows} items into your vault?";

            var confirmed = await _sheets.ConfirmAsync("Confirm Import", message, "Import", "Cancel");
            if (!confirmed) return;

          IsBusy     = true;
   StatusText = "Importing…";
            try
      {
        Result = await _importer.CommitAsync(Preview, MergeOption, CancellationToken.None);
   Step   = ImportPageStep.Result;

   // Log import
 _ = _eventLogProcessor.ProcessEventLogAsync(new EventLog
    {
        EventType = (int)EventLogType.VaultImported,
      Detail    = $"{Result?.Imported ?? 0} imported, {Result?.Skipped ?? 0} skipped – {FormatLabel}",
        });

      }
   catch (Exception ex)
            {
    _logger.LogError(ex, "ImportVM: commit failed");
       await _sheets.AlertAsync("Import Error",
    "An error occurred during import. Some items may not have been saved.");
  }
            finally
   {
                IsBusy     = false;
     StatusText = string.Empty;
       }
        }

     private void ExecuteStartOver()
        {
        _pendingFilePath = null;
            SelectedFileName = string.Empty;
            Preview          = null;
     Result  = null;
         AuthPreview      = null;
     AuthResult= null;
         Step             = ImportPageStep.PickFile;
 SelectedTab      = ImportTab.Passwords;
     }

        public override void OnNavigatedTo(INavigationParameters parameters)
      {
      base.OnNavigatedTo(parameters);
            ExecuteStartOver();
      }
    }

    public enum ImportPageStep { PickFile, Preview, Result }
  public enum ImportTab      { Passwords, Authenticators }
}
