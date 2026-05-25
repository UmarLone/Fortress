using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Core.Utilities;
using Fortress.Extensions;
using Fortress.Helpers;
using Fortress.Models;
using Fortress.Services;
using Fortress.Views;
using Microsoft.Extensions.Logging;
using System.Timers;
using System.Windows.Input;
using Timer = System.Timers.Timer;

namespace Fortress.ViewModels
{
    /// <summary>
    /// Read-only detail view for a single authenticator (TOTP) entry.
    /// Shows live OTP code, metadata (added/updated dates), and an Edit button.
    /// </summary>
    public sealed class AuthenticatorDetailPageViewModel : ViewModelBase
    {
        private readonly ICryptographyService _crypto;
        private readonly IDeviceServices _deviceServices;
        private readonly ILogger<AuthenticatorDetailPageViewModel> _logger;

        private Authenticator? _item;
        private Timer? _otpTimer;

        // ── Bound properties ─────────────────────────────────────────────────
        private string _issuer = string.Empty;
        public string Issuer { get => _issuer; set => SetProperty(ref _issuer, value); }

        private string _account = string.Empty;
        public string Account { get => _account; set => SetProperty(ref _account, value); }

        private string _iconUri = string.Empty;
        public string IconUri { get => _iconUri; set => SetProperty(ref _iconUri, value); }

        private string _otpCode = "• • • •";
        public string OtpCode { get => _otpCode; set => SetProperty(ref _otpCode, value); }

        private double _otpProgress;
        public double OtpProgress { get => _otpProgress; set => SetProperty(ref _otpProgress, value); }

        private int _otpDuration = 30;
        public int OtpDuration { get => _otpDuration; set => SetProperty(ref _otpDuration, value); }

        private string _algorithm = "SHA1";
        public string Algorithm { get => _algorithm; set => SetProperty(ref _algorithm, value); }

        private int _digits = 6;
        public int Digits { get => _digits; set => SetProperty(ref _digits, value); }

        private int _period = 30;
        public int Period { get => _period; set => SetProperty(ref _period, value); }

        // ── Metadata ─────────────────────────────────────────────────────────
        private string _createdDisplay = string.Empty;
        public string CreatedDisplay { get => _createdDisplay; set => SetProperty(ref _createdDisplay, value); }

        private string _updatedDisplay = string.Empty;
        public string UpdatedDisplay { get => _updatedDisplay; set => SetProperty(ref _updatedDisplay, value); }

        private string _notes = string.Empty;
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

        private bool _hasNotes;
        public bool HasNotes { get => _hasNotes; set => SetProperty(ref _hasNotes, value); }

        // ── Constructor ───────────────────────────────────────────────────────
        public AuthenticatorDetailPageViewModel(
         INavigationService navigationService,
       ICryptographyService crypto,
        IDeviceServices deviceServices,
                ILogger<AuthenticatorDetailPageViewModel> logger)
         : base(navigationService)
        {
            _crypto = crypto;
            _deviceServices = deviceServices;
            _logger = logger;
        }

        // ── Navigation ────────────────────────────────────────────────────────
        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            if (parameters.TryGetValue("authenticator", out Authenticator auth) && auth != null)
            {
                _item = auth;
                await LoadAsync(auth);
            }
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            StopOtpTimer();
            base.OnNavigatedFrom(parameters);
        }

        // ── Data loading ──────────────────────────────────────────────────────
        private async Task LoadAsync(Authenticator auth)
        {
            Issuer = auth.Issuer ?? string.Empty;
            Account = auth.Username ?? string.Empty;
            IconUri = auth.IconUri ?? string.Empty;
            OtpDuration = auth.Period > 0 ? auth.Period : 30;
            Period = auth.Period > 0 ? auth.Period : 30;
            Digits = auth.Digits > 0 ? auth.Digits : 6;
            Algorithm = FormatAlgorithm(auth.Algorithm);
            Notes = auth.Notes ?? string.Empty;
            HasNotes = !string.IsNullOrWhiteSpace(auth.Notes);

            CreatedDisplay = FormatDate(auth.CreatedAt);
            UpdatedDisplay = FormatDate(auth.UpdatedAt);

            // Decrypt secret and start OTP timer
            if (!string.IsNullOrEmpty(auth.Secret))
            {
                try
                {
                    var dec = await _crypto.Decrypt(auth.Secret);
                    var secret = dec.Succeeded ? dec.Data : auth.Secret;
                    if (!string.IsNullOrEmpty(secret))
                        StartOtpTimer(secret);
                }
                catch (Exception ex) { _logger.LogError(ex, "AuthenticatorDetail: decrypt failed"); }
            }
        }

        // ── Commands ─────────────────────────────────────────────────────────
        private AsyncCommand? _editCommand;
        public ICommand EditCommand =>
        _editCommand ??= new AsyncCommand(async () =>
                  {
                      if (_item == null) return;
                      await NavigationService.NavigateAsync(
                nameof(AddEditAuthenticatorPage),
               new NavigationParameters { { "authenticator", _item } });
                  });

        private AsyncCommand? _copyOtpCommand;
        public ICommand CopyOtpCommand =>
   _copyOtpCommand ??= new AsyncCommand(async () =>
          {
              if (!string.IsNullOrEmpty(OtpCode))
                  await _deviceServices.CopyToClipboard(OtpCode, "OTP code copied",
             PreferenceWrapper.Instance.ClearClipboardTimeout);
          });

        private AsyncCommand? _shareCommand;
        public ICommand ShareCommand =>
           _shareCommand ??= new AsyncCommand(async () =>
        {
            if (_item == null) return;
            await NavigationService.NavigateAsync(
               nameof(Views.ShareItemPage),
              new NavigationParameters { { "authenticator", _item } });
        });

        // ── OTP timer ────────────────────────────────────────────────────────
        private void StartOtpTimer(string secret)
        {
            StopOtpTimer();
            TickOtp(secret);
            _otpTimer = new Timer(1000) { AutoReset = true };
            _otpTimer.Elapsed += (_, _) => TickOtp(secret);
            _otpTimer.Start();
        }

        private void TickOtp(string secret)
        {
            try
            {
                var totp = OtpHelper.GenerateOtp(secret);
                MainThread.BeginInvokeOnMainThread(() =>
                    {
                        OtpCode = totp.Code;
                        OtpProgress = totp.RemainingSeconds;
                    });
            }
            catch { }
        }

        private void StopOtpTimer()
        {
            _otpTimer?.Stop();
            _otpTimer?.Dispose();
            _otpTimer = null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string FormatDate(DateTime dt)
        {
            if (dt == default) return "Unknown";
            var local = dt.ToLocalTime();
            var age = DateTime.Now - local;
            return age.TotalDays < 1
           ? $"Today at {local:h:mm tt}"
         : age.TotalDays < 2
         ? $"Yesterday at {local:h:mm tt}"
              : local.ToString("MMM d, yyyy");
        }

        private static string FormatAlgorithm(HashAlgorithm algo) => algo switch
        {
            HashAlgorithm.Sha256 => "SHA-256",
            HashAlgorithm.Sha512 => "SHA-512",
            _ => "SHA-1"
        };
    }
}
