using Fortress.ViewModels;

namespace Fortress.Views
{
    public partial class AddEditCreditCardPage : ContentPage
    {
    private AddEditCreditCardPageViewModel Vm =>
            BindingContext as AddEditCreditCardPageViewModel;

        private Controls.VaultFormField _fieldCvv;
        private Controls.VaultFormField _fieldCardName;
 private Controls.VaultFormField _fieldCardHolder;

      // ── Card number single-entry state ────────────────────────────────────
        private Entry _cardNumberEntry;
        private bool _isFormattingCardNumber = false;

        // ── Expiry single-entry state ─────────────────────────────────────────
private Entry _expiryEntry;
   private bool _isFormattingExpiry = false;

     // Track the VM we're currently subscribed to so we can unsubscribe cleanly
        private AddEditCreditCardPageViewModel _subscribedVm;

public AddEditCreditCardPage()
        {
     InitializeComponent();

      _fieldCvv        = this.FindByName<Controls.VaultFormField>("FieldCvv");
     _fieldCardName   = this.FindByName<Controls.VaultFormField>("FieldCardName");
            _fieldCardHolder = this.FindByName<Controls.VaultFormField>("FieldCardHolder");

   if (_fieldCvv        != null) _fieldCvv.TextChanged        += OnAnyFieldChanged;
       if (_fieldCardName   != null) _fieldCardName.TextChanged   += OnAnyFieldChanged;
            if (_fieldCardHolder != null) _fieldCardHolder.TextChanged += OnAnyFieldChanged;
        }

   // ── Subscribe to SeedRequested when Prism sets the VM ─────────────────

        protected override void OnBindingContextChanged()
 {
            base.OnBindingContextChanged();

     // Unsubscribe from old VM
            if (_subscribedVm != null)
 {
              _subscribedVm.SeedRequested -= OnSeedRequested;
     _subscribedVm = null;
       }

   // Subscribe to new VM
         if (Vm != null)
            {
      _subscribedVm = Vm;
                Vm.SeedRequested += OnSeedRequested;
    }
        }

      // ── Seed entries — called by VM after OnNavigatedTo finishes ──────────

     private void OnSeedRequested(object sender, EventArgs e)
  {
    // Dispatch to the UI thread — Prism's OnNavigatedTo can be called
            // from a non-UI context on some versions.
     Dispatcher.Dispatch(() =>
      {
 // Card number
           if (_cardNumberEntry != null)
{
   var cardSeed = FormatCardNumberDisplay(Vm?.CardNumber ?? string.Empty);
            if (_cardNumberEntry.Text != cardSeed)
     {
         _isFormattingCardNumber = true;
       _cardNumberEntry.Text = cardSeed;
           _isFormattingCardNumber = false;
                }
      }

              // Expiry
    if (_expiryEntry != null)
      {
            var expirySeed = BuildExpiryString(Vm?.ExpiryMonth, Vm?.ExpiryYear);
    if (_expiryEntry.Text != expirySeed)
      {
             _isFormattingExpiry = true;
            _expiryEntry.Text = expirySeed;
           _isFormattingExpiry = false;
         }
 }
 });
        }

      // ── Field change handlers ─────────────────────────────────────────────

        private void OnAnyFieldChanged(object sender, TextChangedEventArgs e) => Vm?.Validate();

        // ── Card number single-entry ──────────────────────────────────────────

        private void CardNumberEntry_Loaded(object sender, EventArgs e)
        {
   if (sender is not Entry entry) return;
          _cardNumberEntry = entry;
        }

        private void OnCardNumberEntryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingCardNumber) return;
  if (sender is not Entry entry) return;

       _cardNumberEntry = entry;

   var newText = e.NewTextValue ?? string.Empty;
         var digits  = new string(newText.Where(char.IsDigit).ToArray());

   var network = AddEditCreditCardPageViewModel.DetectNetwork(digits);
    var maxLen  = network == "Amex" ? 15 : 16;
   if (digits.Length > maxLen) digits = digits[..maxLen];

         var formatted = FormatCardNumberDisplay(digits);

_isFormattingCardNumber = true;
 if (Vm != null)
            {
         Vm.CardNumber  = digits;
      Vm.CardNetwork = network;
     }
            _isFormattingCardNumber = false;

      if (entry.Text == formatted)
          {
     Vm?.Validate();
        return;
     }

 _isFormattingCardNumber = true;
            entry.Dispatcher.Dispatch(() =>
      {
          try
       {
         entry.Text = formatted;
   entry.CursorPosition = formatted.Length;
          }
                finally
    {
    _isFormattingCardNumber = false;
     }
     Vm?.Validate();
            });
        }

      /// <summary>Formats raw digits as "XXXX XXXX XXXX XXXX".</summary>
        private static string FormatCardNumberDisplay(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return string.Empty;
            var sb = new System.Text.StringBuilder(19);
    for (int i = 0; i < digits.Length; i++)
   {
    if (i > 0 && i % 4 == 0) sb.Append(' ');
   sb.Append(digits[i]);
            }
          return sb.ToString();
        }

        // ── Expiry single-entry ───────────────────────────────────────────────

    private void ExpiryEntry_Loaded(object sender, EventArgs e)
   {
     if (sender is not Entry entry) return;
       _expiryEntry = entry;
        }

        private void OnExpiryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingExpiry) return;
       if (sender is not Entry entry) return;

            _expiryEntry = entry;

  var newText = e.NewTextValue ?? string.Empty;
 var digits  = new string(newText.Where(char.IsDigit).ToArray());
          if (digits.Length > 4) digits = digits[..4];

      var formatted = digits.Length > 2
          ? digits[..2] + "/" + digits[2..]
: digits;

            _isFormattingExpiry = true;
            PushExpiryToVm(formatted);
            _isFormattingExpiry = false;

          if (entry.Text == formatted)
     {
  Vm?.Validate();
        return;
    }

       _isFormattingExpiry = true;
            entry.Dispatcher.Dispatch(() =>
    {
       try
                {
                    entry.Text = formatted;
        entry.CursorPosition = formatted.Length;
       }
        finally
       {
        _isFormattingExpiry = false;
        }
                Vm?.Validate();
    });
     }

   /// <summary>Splits "MM/YY" → ExpiryMonth + ExpiryYear on the ViewModel.</summary>
        private void PushExpiryToVm(string formatted)
        {
   if (Vm == null) return;
            var digits = new string(formatted.Where(char.IsDigit).ToArray());
            Vm.ExpiryMonth = digits.Length >= 2 ? digits[..2] : digits;
     Vm.ExpiryYear  = digits.Length == 4 ? digits[2..] : string.Empty;
  }

        /// <summary>Builds the "MM/YY" display string from stored month/year parts.</summary>
      private static string BuildExpiryString(string? month, string? year)
     {
            if (string.IsNullOrEmpty(month)) return string.Empty;
 return string.IsNullOrEmpty(year) ? month : $"{month}/{year}";
        }

// ── Scan button ───────────────────────────────────────────────────────

        private async void OnScanCardTapped(object sender, EventArgs e)
        {
    // Scanner integration placeholder — uncomment when SDK is added.
        }
    }
}
