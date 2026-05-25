using Fortress.Mobile.Core.Services;
using Fortress.ViewModels;
using MauiIcons.Material;
using System.Text.RegularExpressions;

namespace Fortress.Views
{
    public partial class SetupPage : ContentPage
    {
        private bool _isPasswordVisible = false;
        private bool _isConfirmPasswordVisible = false;
        private SetupPageViewModel? _viewModel => BindingContext as SetupPageViewModel;

        // Step configuration
        private readonly (string Title, string Description, MaterialIcons Icon)[] _steps =
        {
            ("Create Your Master Password", "This is the one password that unlocks everything",         MaterialIcons.LockOpen),
      ("Choose How to Unlock",     "Quick access, strong protection",        MaterialIcons.Shield),
            ("Enable AutoFill",          "Fill passwords,cards and identities in apps automatically — no typing needed",      MaterialIcons.AutoAwesome),
  ("Back Up Your Vault",      "Keep your data safe even if you lose your phone",         MaterialIcons.CloudQueue),
        };

        public SetupPage()
   {
     InitializeComponent();
        }

   protected override void OnDisappearing()
        {
      PreferenceWrapper.Instance.PreventLocking = false;
            base.OnDisappearing();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
   PreferenceWrapper.Instance.PreventLocking = true;
          UpdateStepIndicator(_viewModel?.CurrentStep ?? 0);
      _viewModel?.RefreshAutofillStatus();
     }

        protected override void OnBindingContextChanged()
        {
        base.OnBindingContextChanged();
    if (_viewModel != null)
            {
             _viewModel.PropertyChanged += (s, e) =>
          {
             if (e.PropertyName == nameof(_viewModel.CurrentStep))
           UpdateStepIndicator(_viewModel.CurrentStep);
    };
 }
        }

        private void UpdateStepIndicator(int step)
        {
   try
       {
        var indicators = new Border[]
        {
        FindByName("step0Indicator") as Border,
    FindByName("step1Indicator") as Border,
          FindByName("step2Indicator") as Border,
      FindByName("step3Indicator") as Border,
  };

     for (int i = 0; i < indicators.Length; i++)
          {
          if (indicators[i] == null) continue;
      if (i < step)      // completed
     {
    indicators[i].BackgroundColor = Colors.White;
      indicators[i].WidthRequest    = 40;
         indicators[i].Opacity         = 1.0;
          }
    else if (i == step)  // active
     {
   indicators[i].BackgroundColor = Colors.White;
              indicators[i].WidthRequest    = 50;
          indicators[i].Opacity         = 1.0;
        }
            else       // upcoming
            {
        indicators[i].BackgroundColor = Color.FromArgb("#60FFFFFF");
     indicators[i].WidthRequest = 40;
          indicators[i].Opacity       = 0.4;
              }
                }

      var title       = FindByName("stepTitle")as Label;
    var description = FindByName("stepDescription") as Label;
        var icon        = FindByName("stepIcon")    as MauiIcons.Core.MauiIcon;

      if (title    != null) title.Text       = _steps[step].Title;
      if (description != null) description.Text = _steps[step].Description;
    if (icon        != null) icon.Icon        = _steps[step].Icon;
  }
    catch (Exception ex)
      {
                System.Diagnostics.Debug.WriteLine($"UpdateStepIndicator: {ex.Message}");
      }
        }

        // ── Password validation ───────────────────────────────────────────────
        private void Password_TextChanged(object sender, TextChangedEventArgs e)        => ValidatePassword();
        private void ConfirmPassword_TextChanged(object sender, TextChangedEventArgs e) => ValidatePassword();

        private void ValidatePassword()
        {
     if (_viewModel == null) return;

var pw      = _viewModel.UserPassword.Password        ?? string.Empty;
        var confirm = _viewModel.UserPassword.ConfirmPassword ?? string.Empty;

     bool has10  = pw.Length >= 10;
  bool hasNum = Regex.IsMatch(pw, @"\d");
            bool hasSym = Regex.IsMatch(pw, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");
            bool hasLow = Regex.IsMatch(pw, @"[a-z]");
  bool hasUp  = Regex.IsMatch(pw, @"[A-Z]");
      bool match  = pw.Length > 0 && pw == confirm;

UpdateReqIcon(icon10Characters, has10);
     UpdateReqIcon(icon1Number,  hasNum);
    UpdateReqIcon(icon1Symbol,      hasSym);
     UpdateReqIcon(iconHasLowercase, hasLow);
            UpdateReqIcon(iconHasUppercase, hasUp);

  // Strength: 1 point per criterion met (ignore match for strength)
          int strength = (has10 ? 1 : 0) + (hasNum ? 1 : 0) + (hasSym ? 1 : 0)
         + (hasLow ? 1 : 0) + (hasUp ? 1 : 0);
            // Map 0-5 to 0-4 so we have Weak/Fair/Good/Strong
 _viewModel.PasswordStrength = pw.Length == 0 ? 0 : Math.Max(1, strength - 1);

   bool ok = has10 && hasNum && hasSym && hasLow && hasUp && match;
  _viewModel.CanSetPassword = ok;
            if (_viewModel.CurrentStep == 0) _viewModel.CanGoNext = ok;
        }

    private static void UpdateReqIcon(MauiIcons.Core.MauiIcon icon, bool met)
            => icon.IconColor = met ? Color.FromArgb("#22C55E") : Color.FromArgb("#D1D5DB");

        // ── Visibility toggles ────────────────────────────────────────────────
        private void OnTogglePasswordVisibility(object sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            txtPassword.IsPassword     = !_isPasswordVisible;
         iconPasswordVisibility.Icon = _isPasswordVisible ? MaterialIcons.Visibility : MaterialIcons.VisibilityOff;
        }

        private void OnToggleConfirmPasswordVisibility(object sender, EventArgs e)
        {
        _isConfirmPasswordVisible = !_isConfirmPasswordVisible;
     txtConfirmPassword.IsPassword      = !_isConfirmPasswordVisible;
   iconConfirmPasswordVisibility.Icon = _isConfirmPasswordVisible ? MaterialIcons.Visibility : MaterialIcons.VisibilityOff;
        }

        // ── Lock option taps ──────────────────────────────────────────────────
        private void OnBiometricOptionTapped(object sender, EventArgs e)
        {
 if (_viewModel == null) return;
          _viewModel.SetupBiometric = !_viewModel.SetupBiometric;
        }

        private void OnPinOptionTapped(object sender, EventArgs e)
 {
            if (_viewModel == null) return;
            _viewModel.SetupPin = !_viewModel.SetupPin;
       if (_viewModel.SetupPin)
            {
                txtPinEntry.Text = string.Empty;
        txtPinEntry.Focus();
            }
  }

        // ── PIN entry (system keyboard) ───────────────────────────────────────
        private void PinEntry_TextChanged(object sender, TextChangedEventArgs e)
  {
     if (_viewModel == null) return;

     var text = e.NewTextValue ?? string.Empty;
          // Allow digits only
       var digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits != text)
      {
       txtPinEntry.Text = digits;
 return;
  }

_viewModel.HasPinError = false;
    _viewModel.PinErrorMessage = string.Empty;

        if (!_viewModel.IsConfirmingPin)
   {
        _viewModel.PinEntry = digits;
                if (digits.Length == 4)
         {
            _viewModel.IsConfirmingPin = true;
        txtPinEntry.Text = string.Empty;
        }
            }
   else
            {
           _viewModel.PinConfirm = digits;
    if (digits.Length == 4)
       {
        _viewModel.ValidatePinFromView();
       txtPinEntry.Text = string.Empty;
   if (_viewModel.HasPinError)
            txtPinEntry.Focus();
        }
   }
        }
    }
}
