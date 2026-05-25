using Fortress.Core.Models;
using System.Collections.ObjectModel;

namespace Fortress.Windows.Desktop.ViewModels
{
    /// <summary>Observable wrapper for a LoginItem � used in list views.</summary>
    public partial class LoginItemViewModel : ObservableObject
    {
        public Guid Id { get; }
        public string Label { get; }
        public string Url { get; }
        public string Username { get; }
        public string Password { get; }
        public string? OtpSecret { get; }
        public LoginType LoginType { get; }
        public int PasswordStrengthScore { get; }
        public int PasswordStrengthLevel { get; }
        public bool IsFavorite { get; }
        public bool HasOtp { get; }
        public DateTime CreatedAt { get; }
        public DateTime UpdatedAt { get; }
        public string Notes { get; }

        [ObservableProperty] private bool _isPasswordVisible;
        [ObservableProperty] private string _displayPassword = string.Empty;
        [ObservableProperty] private bool _isSelected;

        public LoginItemViewModel(LoginItem item)
        {
            Id = item.Id;
            Label = item.Label;
            Url = item.Url;
            Username = item.Username;
            Password = item.Password;
            OtpSecret = item.OtpSecret;
            LoginType = item.LoginType;
            PasswordStrengthScore = item.PasswordStrengthScore;
            PasswordStrengthLevel = item.PasswordStrengthLevel;
            IsFavorite = item.IsFavorite;
            HasOtp = !string.IsNullOrEmpty(item.OtpSecret);
            CreatedAt = item.CreatedAt;
            UpdatedAt = item.UpdatedAt;
            Notes = item.Notes;
            _displayPassword = new string('\u2022', Math.Min(Password.Length, 12));
        }

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
            DisplayPassword = IsPasswordVisible
                ? Password
                : new string('\u2022', Math.Min(Password.Length, 12));
        }

        public string Domain
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Url)) return Label;
                try { return new Uri(Url).Host.Replace("www.", ""); }
                catch { return Label; }
            }
        }

        public string FaviconUrl => string.IsNullOrWhiteSpace(Domain)
            ? string.Empty
            : $"https://www.google.com/s2/favicons?domain={Domain}&sz=64";
    }

    /// <summary>Observable wrapper for a CreditCardItem.</summary>
    public partial class CreditCardViewModel : ObservableObject
    {
        public Guid Id { get; }
        public string Label { get; }
        public string CardholderName { get; }
        public string Number { get; }
        public string ExpiryMonth { get; }
        public string ExpiryYear { get; }
        public string Cvv { get; }
        public string CardNetwork { get; }
        public string BillingAddress { get; }
        public DateTime CreatedAt { get; }
        public DateTime UpdatedAt { get; }

        [ObservableProperty] private bool _isNumberVisible;
        [ObservableProperty] private string _displayNumber = string.Empty;

        public CreditCardViewModel(CreditCardItem item)
        {
            Id = item.Id;
            Label = item.Label;
            CardholderName = item.CardholderName;
            Number = item.Number;
            ExpiryMonth = item.ExpiryMonth;
            ExpiryYear = item.ExpiryYear;
            Cvv = item.Cvv;
            CardNetwork = item.CardNetwork;
            BillingAddress = item.BillingAddress;
            CreatedAt = item.CreatedAt;
            UpdatedAt = item.UpdatedAt;
            _displayNumber = MaskNumber(item.Number);
        }

        [RelayCommand]
        private void ToggleNumberVisibility()
        {
            IsNumberVisible = !IsNumberVisible;
            DisplayNumber = IsNumberVisible ? FormatNumber(Number) : MaskNumber(Number);
        }

        public string ExpiryDisplay => $"{ExpiryMonth}/{ExpiryYear}";

        public string NetworkColor => CardNetwork?.ToLower() switch
        {
            "visa"       => "#1A1F71",
            "mastercard" => "#EB001B",
            "amex"       => "#007BC1",
            _      => "#4B5563"
        };

  private static string MaskNumber(string n)
        {
        if (string.IsNullOrWhiteSpace(n) || n.Length < 4)
        return "\u2022\u2022\u2022\u2022 \u2022\u2022\u2022\u2022 \u2022\u2022\u2022\u2022 \u2022\u2022\u2022\u2022";
    return $"\u2022\u2022\u2022\u2022 \u2022\u2022\u2022\u2022 \u2022\u2022\u2022\u2022 {n[^4..]}";
      }

        private static string FormatNumber(string n)
        {
      n = n.Replace(" ", "");
    return n.Length switch
            {
      15 => $"{n[..4]} {n[4..10]} {n[10..]}",
 16 => $"{n[..4]} {n[4..8]} {n[8..12]} {n[12..]}",
      _  => n
     };
     }
    }

    /// <summary>Observable wrapper for an Authenticator with live TOTP.</summary>
  public partial class AuthenticatorViewModel : ObservableObject
    {
      public Guid Id { get; }
        public string Issuer { get; }
        public string Username { get; }
      public AuthenticatorType Type { get; }
        public int Period { get; }
        public int Digits { get; }
  public bool IsFavorite { get; }
public DateTime CreatedAt { get; }

        private readonly string _secret;

        [ObservableProperty] private string _currentCode = "------";
        [ObservableProperty] private double _progress = 1.0;
   [ObservableProperty] private int _secondsRemaining = 30;

        public string IconUrl =>
          $"https://www.google.com/s2/favicons?domain={Issuer?.ToLower()}.com&sz=64";

        public AuthenticatorViewModel(Authenticator item)
        {
   Id = item.Id;
            Issuer = item.Issuer;
   Username = item.Username;
            Type = item.Type;
            Period = item.Period > 0 ? item.Period : 30;
    Digits = item.Digits > 0 ? item.Digits : 6;
 IsFavorite = item.IsFavorite;
      CreatedAt = item.CreatedAt;
            _secret = item.Secret ?? string.Empty;
     RefreshCode();
        }

        public void Tick()
        {
     var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      var remaining = (int)(Period - (epoch % Period));
          SecondsRemaining = remaining;
            Progress = remaining / (double)Period;
            // Refresh when the counter rolls over
            if (remaining == Period || remaining == Period - 1)
     RefreshCode();
   }

        private void RefreshCode()
        {
            if (string.IsNullOrWhiteSpace(_secret))
            {
      CurrentCode = "------";
 return;
            }
 try
{
         var result = Fortress.Core.Utilities.OtpHelper.GenerateOtp(_secret);
      CurrentCode = string.IsNullOrEmpty(result.Code) ? "------" : result.Code;
     }
catch
{
     CurrentCode = "------";
            }
     }
    }

    /// <summary>Observable wrapper for a VaultGroup.</summary>
    public class VaultGroupViewModel
    {
        public Guid Id { get; }
  public string Name { get; }
        public string IconKey { get; }
        public string Color { get; }

    public VaultGroupViewModel(VaultGroup g)
        {
          Id = g.Id;
            Name = g.Name;
   IconKey = g.IconKey;
    Color = g.Color;
        }
    }

 /// <summary>Flat event log row used in the Activity Log page.</summary>
  public class EventLogViewModel
    {
        public Guid Id { get; }
      public DateTime DateTime { get; }
        public int EventType { get; }
    public string CredentialLabel { get; }
        public string Detail { get; }

        public string TimeAgo
   {
        get
   {
       var ago = System.DateTime.UtcNow - DateTime;
   if (ago.TotalMinutes < 1)  return "Just now";
        if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes}m ago";
     if (ago.TotalHours   < 24) return $"{(int)ago.TotalHours}h ago";
      if (ago.TotalDays    < 7)  return $"{(int)ago.TotalDays}d ago";
              return DateTime.ToLocalTime().ToString("MMM d");
  }
        }

     public EventLogViewModel(EventLog e)
        {
     Id = e.Id;
   DateTime = e.DateTime;
            EventType = e.EventType;
            CredentialLabel = e.CredentialLabel ?? string.Empty;
   Detail = e.Detail ?? string.Empty;
        }
    }

    /// <summary>Flat notification row.</summary>
    public class NotificationViewModel
    {
        public Guid Id { get; }
        public string Title { get; }
    public string Message { get; }
        public Fortress.Core.Models.NotificationType Type { get; }
 public bool IsSeen { get; }
        public DateTime CreationDateTime { get; }

        public string TimeAgo
        {
            get
 {
          var ago = DateTime.UtcNow - CreationDateTime;
          if (ago.TotalMinutes < 1)  return "Just now";
            if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes}m ago";
   if (ago.TotalHours   < 24) return $"{(int)ago.TotalHours}h ago";
                return CreationDateTime.ToLocalTime().ToString("MMM d");
            }
        }

   public NotificationViewModel(UserNotification n)
     {
   Id = n.Id;
        Title = n.Title;
  Message = n.Message;
            Type = n.Type;
      IsSeen = n.IsSeen;
            CreationDateTime = n.CreationDateTime;
        }
    }
}
