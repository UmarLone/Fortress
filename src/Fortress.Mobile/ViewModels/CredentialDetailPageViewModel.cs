using Fortress.Mobile.Adapters;
using Fortress.Mobile.Core.Contracts;
using Fortress.Mobile.Core.Mappers;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using Fortress.Mobile.Core.Utilities;
using Fortress.Extensions;
using Fortress.Helpers;
using Fortress.Services;
using Fortress.Views;
using Microsoft.Extensions.Logging;
using System.Timers;
using System.Windows.Input;
using Timer = System.Timers.Timer;

namespace Fortress.ViewModels
{
    /// <summary>
    /// Read-only detail view for a single password/login entry.
    /// Shows all metadata including timestamps, password strength, and live OTP.
    /// The Edit button navigates to AddEditCredentialPage.
    /// </summary>
    public sealed class CredentialDetailPageViewModel : ViewModelBase
    {
        private readonly ICryptographyService _crypto;
        private readonly IDeviceServices _deviceServices;
        private readonly ILogger<CredentialDetailPageViewModel> _logger;
        private readonly IBottomSheetService _bottomSheetService;
        private readonly IDataStorageService _dataStorageService;
        private readonly VaultHealthCalculator _healthCalculator;

        private CredentialView? _item;
        private LoginItem? _loginItem;
        private Timer? _otpTimer;

        // ── Bound properties ─────────────────────────────────────────────────

        private string _domain = string.Empty;
        public string Domain { get => _domain; set => SetProperty(ref _domain, value); }

        private string _username = string.Empty;
        public string Username { get => _username; set => SetProperty(ref _username, value); }

        private string _passwordDisplay = "••••••••••••";
        public string PasswordDisplay { get => _passwordDisplay; set => SetProperty(ref _passwordDisplay, value); }

        private bool _isPasswordVisible;
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                SetProperty(ref _isPasswordVisible, value);
                RaisePropertyChanged(nameof(VisibilityIcon));
            }
        }
        public string VisibilityIcon => _isPasswordVisible ? "\uE8F5" : "\uE8F4"; // visibility / visibility_off

        private string _iconUri = string.Empty;
        public string IconUri { get => _iconUri; set => SetProperty(ref _iconUri, value); }

        private bool _hasOtp;
        public bool HasOtp { get => _hasOtp; set => SetProperty(ref _hasOtp, value); }

        private string _otpCode = string.Empty;
        public string OtpCode { get => _otpCode; set => SetProperty(ref _otpCode, value); }

        private double _otpProgress;
        public double OtpProgress { get => _otpProgress; set => SetProperty(ref _otpProgress, value); }

        private int _otpDuration = 30;
        public int OtpDuration { get => _otpDuration; set => SetProperty(ref _otpDuration, value); }

        private bool _hasFavicon;
        public bool HasFavicon { get => _hasFavicon; set => SetProperty(ref _hasFavicon, value); }

        // ── Metadata ─────────────────────────────────────────────────────────

        private string _credentialType = string.Empty;
        public string CredentialType { get => _credentialType; set => SetProperty(ref _credentialType, value); }

        private string _createdDisplay = string.Empty;
        public string CreatedDisplay { get => _createdDisplay; set => SetProperty(ref _createdDisplay, value); }

        private string _updatedDisplay = string.Empty;
        public string UpdatedDisplay { get => _updatedDisplay; set => SetProperty(ref _updatedDisplay, value); }

        private string _lastUsedDisplay = "Not tracked";
        public string LastUsedDisplay { get => _lastUsedDisplay; set => SetProperty(ref _lastUsedDisplay, value); }

        // ── Password strength ─────────────────────────────────────────────────

        private int _strengthScore;
        public int StrengthScore { get => _strengthScore; set => SetProperty(ref _strengthScore, value); }

        private string _strengthLabel = string.Empty;
        public string StrengthLabel { get => _strengthLabel; set => SetProperty(ref _strengthLabel, value); }

        private string _strengthColor = "#64748B";
        public string StrengthColor { get => _strengthColor; set { SetProperty(ref _strengthColor, value); RaisePropertyChanged(nameof(StrengthColorValue)); } }

        /// <summary>Typed Color for use with Syncfusion ProgressFill (Brush binding).</summary>
        public Color StrengthColorValue => Color.FromArgb(_strengthColor);

        // ── AI Risk Badge ─────────────────────────────────────────────────────

        private string _riskBadgeText = string.Empty;
        public string RiskBadgeText { get => _riskBadgeText; set { SetProperty(ref _riskBadgeText, value); RaisePropertyChanged(nameof(HasRiskBadge)); } }

        private string _riskBadgeColor = "#22C55E";
        public string RiskBadgeColor { get => _riskBadgeColor; set => SetProperty(ref _riskBadgeColor, value); }

        private string _riskBadgeIcon = "\ue86c"; // check_circle
        public string RiskBadgeIcon { get => _riskBadgeIcon; set => SetProperty(ref _riskBadgeIcon, value); }

        public bool HasRiskBadge => !string.IsNullOrEmpty(_riskBadgeText);

        // ── Notes ────────────────────────────────────────────────────────────

        private string _notes = string.Empty;
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

        private bool _hasNotes;
        public bool HasNotes { get => _hasNotes; set => SetProperty(ref _hasNotes, value); }

        // ── 2FA ──────────────────────────────────────────────────────────────

        private bool _requireAuth;
        public bool RequireAuth { get => _requireAuth; set => SetProperty(ref _requireAuth, value); }

        // ── Constructor ───────────────────────────────────────────────────────

        public CredentialDetailPageViewModel(
            INavigationService navigationService,
            ICryptographyService crypto,
            IDeviceServices deviceServices,
            ILogger<CredentialDetailPageViewModel> logger,
            IBottomSheetService bottomSheetService,
            IDataStorageService dataStorageService,
            VaultHealthCalculator healthCalculator)
            : base(navigationService)
        {
            _crypto = crypto;
            _deviceServices = deviceServices;
            _logger = logger;
            _bottomSheetService = bottomSheetService;
            _dataStorageService = dataStorageService;
            _healthCalculator = healthCalculator;
        }

        // ── Navigation ────────────────────────────────────────────────────────

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);
            if (parameters.TryGetValue("credential", out CredentialView cred) && cred != null)
            {
                _item = cred;
                _loginItem = LoginItemMapper.Map(cred);
                await LoadAsync(cred);
            }
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            StopOtpTimer();
            base.OnNavigatedFrom(parameters);
        }

        // ── Data loading ──────────────────────────────────────────────────────

        private async Task LoadAsync(CredentialView cred)
        {
            Domain = cred.Domain;
            Username = cred.Username;
            IconUri = cred.IconUri ?? string.Empty;
            HasFavicon = !string.IsNullOrEmpty(cred.IconUri);
            HasOtp = cred.HasOtp;
            OtpDuration = cred.Duration;
            CredentialType = FormatType(cred.CredentialType);
            RequireAuth = cred.RequireAuthBeforeFill;
            Notes = cred.Notes ?? string.Empty;
            HasNotes = !string.IsNullOrWhiteSpace(cred.Notes);

            // Timestamps
            CreatedDisplay = FormatDate(cred.CreatedAt);
            UpdatedDisplay = FormatDate(cred.UpdatedAt);

            // Password strength
            StrengthScore = cred.PasswordStrengthScore;
            (StrengthLabel, StrengthColor) = ScoreToLabel(cred.PasswordStrengthLevel);

            // Password — kept masked until user taps eye icon
            PasswordDisplay = string.IsNullOrEmpty(cred.Password)
                ? "— (no password saved)"
                : "••••••••••••";
            IsPasswordVisible = false;

            // Start OTP timer if needed
            if (cred.HasOtp && !string.IsNullOrEmpty(cred.Data))
                StartOtpTimer(cred.Data);

            // Compute AI risk badge from vault health data (background thread)
            _ = ComputeRiskBadgeAsync(cred);
        }

        /// <summary>
        /// Computes a per-credential risk badge by running the vault health calculator
        /// and looking up this credential's detail. Runs on a background thread.
        /// </summary>
        private async Task ComputeRiskBadgeAsync(CredentialView cred)
        {
            try
            {
                var credentials = (await _dataStorageService.GetLoginItemsAsync()).ToList();
                var authenticators = (await _dataStorageService.GetAuthenticatorsAsync()).ToList();

                var health = await Task.Run(() => _healthCalculator.Calculate(credentials, authenticators));

                var detail = health.Details.FirstOrDefault(d => d.Id == cred.Id);
                if (detail is null)
                {
                    // Credential not found in health data — show neutral badge
                    RiskBadgeText = string.Empty;
                    return;
                }

                // Build the badge based on the worst finding
                var issues = new List<string>();

                if (health.BreachedCount > 0 && credentials.Any(c =>
                     c.Id == cred.Id && health.Details.Any(d => d.Id == c.Id && d.IsWeak)))
                {
                    // Check if THIS credential is specifically breached
                    // We approximate: if it's weak and there are breaches, flag it
                }

                if (detail.IsReused)
                    issues.Add("Reused");
                if (detail.IsWeak)
                    issues.Add("Weak");
                if (detail.IsOld)
                    issues.Add("Old");
                if (!detail.HasTwoFactor)
                    issues.Add("No 2FA");

                if (issues.Count == 0)
                {
                    RiskBadgeIcon = "\ue86c";  // check_circle
                    RiskBadgeColor = "#22C55E"; // green
                    RiskBadgeText = "Strong & Unique";
                }
                else if (issues.Count == 1)
                {
                    RiskBadgeIcon = "\ue002";// warning
                    RiskBadgeColor = "#F59E0B"; // amber
                    RiskBadgeText = issues[0];
                }
                else
                {
                    RiskBadgeIcon = "\ue002";  // warning
                    RiskBadgeColor = detail.IsReused || detail.IsWeak ? "#EF4444" : "#F59E0B";
                    RiskBadgeText = string.Join(" · ", issues);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compute risk badge");
                RiskBadgeText = string.Empty;
            }
        }

        // ── Commands ─────────────────────────────────────────────────────────

        private AsyncCommand? _editCommand;
        public ICommand EditCommand =>
            _editCommand ??= new AsyncCommand(async () =>
            {
                if (_item == null) return;
                await NavigationService.NavigateAsync(
                    nameof(AddEditCredentialPage),
                    new NavigationParameters { { "credential", _item } });
            });

        private AsyncCommand? _togglePasswordCommand;
        public ICommand TogglePasswordCommand =>
            _togglePasswordCommand ??= new AsyncCommand(TogglePasswordAsync);

        private AsyncCommand? _copyUsernameCommand;
        public ICommand CopyUsernameCommand =>
            _copyUsernameCommand ??= new AsyncCommand(async () =>
            {
                if (!string.IsNullOrEmpty(Username))
                    await _deviceServices.CopyToClipboard(Username, "Username copied",
                        PreferenceWrapper.Instance.ClearClipboardTimeout);
            });

        private AsyncCommand? _copyPasswordCommand;
        public ICommand CopyPasswordCommand =>
            _copyPasswordCommand ??= new AsyncCommand(async () =>
            {
                if (_item == null || string.IsNullOrEmpty(_item.Password)) return;
                try
                {
                    var dec = await _crypto.Decrypt(_item.Password);
                    var pwd = dec.Succeeded && !string.IsNullOrEmpty(dec.Data) ? dec.Data : _item.Password;
                    if (!string.IsNullOrEmpty(pwd))
                        await _deviceServices.CopyToClipboard(pwd, "Password copied",
                            PreferenceWrapper.Instance.ClearClipboardTimeout);
                }
                catch (Exception ex) { _logger.LogError(ex, "Copy password failed"); }
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
                if (_loginItem == null) return;
                await NavigationService.NavigateAsync(
                    nameof(Views.ShareItemPage),
                    new NavigationParameters { { "loginItem", _loginItem } });
            });

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task TogglePasswordAsync()
        {
            if (_item == null) return;
            if (!_isPasswordVisible)
            {
                // No password stored at all
                if (string.IsNullOrEmpty(_item.Password))
                {
                    PasswordDisplay = "— (no password saved)";
                    IsPasswordVisible = true;
                    return;
                }
                try
                {
                    var dec = await _crypto.Decrypt(_item.Password);
                    PasswordDisplay = dec.Succeeded && !string.IsNullOrEmpty(dec.Data)
                        ? dec.Data
                        : "— (decrypt failed)";
                }
                catch { PasswordDisplay = "— (decrypt failed)"; }
                IsPasswordVisible = true;
            }
            else
            {
                PasswordDisplay = "••••••••••••";
                IsPasswordVisible = false;
            }
        }

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

        private static string FormatType(string type) => type switch
        {
            "Web" or "Otp" => "Website Login",
            "PhoneApplication" => "Mobile App",
            "Application" => "Desktop App",
            "WindowsLocal" or "Domain" => "Windows Account",
            "MacLocal" => "macOS Account",
            _ => "Login"
        };

        private static (string label, string color) ScoreToLabel(int level) => level switch
        {
            1 => ("Very Weak", "#EF4444"),
            2 => ("Weak", "#F97316"),
            3 => ("Fair", "#EAB308"),
            4 => ("Strong", "#22C55E"),
            5 => ("Very Strong", "#16A34A"),
            _ => ("Unknown", "#64748B")
        };
    }
}
