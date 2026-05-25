using Fortress.Mobile.Core.Models;
using Prism.Mvvm;

namespace Fortress.ViewModels
{
    /// <summary>Display wrapper for a SecureNoteItem shown in the list and passed to AddEditSecureNotePage.</summary>
    public class SecureNoteItemViewModel : BindableBase
    {
        public Guid Id { get; set; }

        // ── Multi-select support ─────────────────────────────────────────────
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string _label = string.Empty;
        public string Label { get => _label; set => SetProperty(ref _label, value); }

        /// <summary>Already-decrypted content – only populated when opening the edit page.</summary>
        private string _content = string.Empty;
        public string Content { get => _content; set => SetProperty(ref _content, value); }

        /// <summary>Short preview shown in the list (first 60 chars, redacted if not yet decrypted).</summary>
        public string Preview => string.IsNullOrEmpty(Content)
          ? "••••••••••••"
            : Content.Length > 60 ? Content[..60] + "•" : Content;
    }

    /// <summary>Display wrapper for an IdentityItem shown in the list and passed to AddEditIdentityPage.</summary>
    public class IdentityItemViewModel : BindableBase
    {
   public Guid Id { get; set; }

     // ── Multi-select support ─────────────────────────────────────────────
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string _label = string.Empty;
        public string Label { get => _label; set => SetProperty(ref _label, value); }

private string _firstName = string.Empty;
        public string FirstName { get => _firstName; set { SetProperty(ref _firstName, value); RaisePropertyChanged(nameof(FullName)); } }

        private string _lastName = string.Empty;
  public string LastName { get => _lastName; set { SetProperty(ref _lastName, value); RaisePropertyChanged(nameof(FullName)); } }

        public string FullName => $"{FirstName} {LastName}".Trim();

        private string _email = string.Empty;
        public string Email { get => _email; set => SetProperty(ref _email, value); }

  private string _phone = string.Empty;
   public string Phone { get => _phone; set => SetProperty(ref _phone, value); }

   private string _company = string.Empty;
     public string Company { get => _company; set => SetProperty(ref _company, value); }

        private string _addressLine1 = string.Empty;
        public string AddressLine1 { get => _addressLine1; set => SetProperty(ref _addressLine1, value); }

        private string _addressLine2 = string.Empty;
   public string AddressLine2 { get => _addressLine2; set => SetProperty(ref _addressLine2, value); }

      private string _city = string.Empty;
        public string City { get => _city; set => SetProperty(ref _city, value); }

   private string _state = string.Empty;
        public string State { get => _state; set => SetProperty(ref _state, value); }

    private string _country = string.Empty;
        public string Country { get => _country; set => SetProperty(ref _country, value); }

        private string _postalCode = string.Empty;
        public string PostalCode { get => _postalCode; set => SetProperty(ref _postalCode, value); }

        /// <summary>One-line address summary for the list card.</summary>
        public string AddressSummary
        {
       get
     {
  var parts = new[] { City, State, Country }
  .Where(s => !string.IsNullOrWhiteSpace(s));
  return string.Join(", ", parts);
         }
        }
    }

    /// <summary>
    /// Display wrapper for a <see cref="SecureItem"/> shown in the unified list
    /// and passed to <see cref="Views.AddEditSecureItemPage"/>.
    /// Sensitive fields (Number, Password, SshPassword, PrivateKey) hold the
    /// already-decrypted plaintext so the form can display them.
    /// </summary>
    public class SecureItemViewModel : BindableBase
    {
        public Guid Id { get; set; }
    public SecureItemType ItemType { get; set; }

        // ── Multi-select support ─────────────────────────────────────────────
     private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string _label = string.Empty;
  public string Label { get => _label; set => SetProperty(ref _label, value); }

        // ── Document / ID fields ──────────────────────────────────────────────
        public string FullName       { get; set; } = string.Empty;
        public string DateOfBirth    { get; set; } = string.Empty;
        public string Nationality    { get; set; } = string.Empty;
        public string Number     { get; set; } = string.Empty;   // decrypted
        public string IssuingCountry { get; set; } = string.Empty;
        public string IssuedDate     { get; set; } = string.Empty;
        public string ExpiryDate     { get; set; } = string.Empty;

        // ── Identity / personal profile fields ────────────────────────────────
        public string FirstName { get; set; } = string.Empty;
     public string LastName     { get; set; } = string.Empty;
        public string Email   { get; set; } = string.Empty;
        public string Phone        { get; set; } = string.Empty;
     public string Company      { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string AddressLine2 { get; set; } = string.Empty;
        public string City         { get; set; } = string.Empty;
        public string State        { get; set; } = string.Empty;
    public string PostalCode   { get; set; } = string.Empty;
        // Country is shared with document; stored in the Nationality field for Identity

     // ── Wi-Fi fields ──────────────────────────────────────────────────────
  public string Ssid         { get; set; } = string.Empty;
        public string WifiSecurity { get; set; } = string.Empty;
        public string Password     { get; set; } = string.Empty;   // decrypted

  // ── SSH fields ────────────────────────────────────────────────────────
  public string Host { get; set; } = string.Empty;
    public string Port           { get; set; } = string.Empty;
        public string Username       { get; set; } = string.Empty;
        public string SshPassword  { get; set; } = string.Empty;   // decrypted
        public string PrivateKey     { get; set; } = string.Empty;   // decrypted
   public string KeyFingerprint { get; set; } = string.Empty;

        // ── Secure Note field ─────────────────────────────────────────────────
        public string NoteContent { get; set; } = string.Empty;

   // ── Display helpers ───────────────────────────────────────────────────
    public string TypeLabel => ItemType switch
      {
            SecureItemType.IdCard          => "ID Card",
  SecureItemType.Passport  => "Passport",
        SecureItemType.DriversLicense  => "Driver's License",
     SecureItemType.SocialSecurity  => "Social Security",
   SecureItemType.TaxNumber       => "Tax Number",
            SecureItemType.WiFi      => "Wi-Fi",
     SecureItemType.Ssh  => "SSH",
     SecureItemType.Identity        => "Identity",
SecureItemType.SecureNote      => "Secure Note",
        _          => "Document"
        };

   public string TypeIcon => ItemType switch
     {
            SecureItemType.WiFi        => "\ue63e",
      SecureItemType.Ssh  => "\ue322",
         SecureItemType.Identity    => "\ue7fd",
     SecureItemType.SecureNote  => "\ue873",   // note / edit icon
            _ => "\ue8f4",
        };

    public string Summary => ItemType switch
        {
      SecureItemType.WiFi       => string.IsNullOrEmpty(Ssid) ? string.Empty : Ssid,
       SecureItemType.Ssh        => string.IsNullOrEmpty(Host) ? string.Empty : $"{Username}@{Host}",
            SecureItemType.SocialSecurity => "••• - •• - ••••",
    SecureItemType.TaxNumber      => "Tax ID stored",
            SecureItemType.Identity => $"{FirstName} {LastName}".Trim(),
    SecureItemType.SecureNote     => NoteContent.Length > 50
        ? NoteContent[..50] + "•"
          : NoteContent,
     _                  => string.IsNullOrEmpty(FullName) ? string.Empty : FullName,
        };
    }
}
