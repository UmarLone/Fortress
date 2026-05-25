using System;

namespace Fortress.iPhone.Autofill
{
    public class AutofillCredential
    {
        public Guid Id { get; set; } // Credential ID for logging usage
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool HasOtp { get; set; }
      
        public string Code { get; set; } = string.Empty;
        public int Progress { get; set; } = 30; // seconds remaining
        public string Data { get; set; } = string.Empty; // encrypted data for OTP generation
        public string? IconUri { get; set; } // URL to fetch website logo
        public string? FallbackIcon { get; set; } // Local fallback icon name
        public string CredentialType { get; set; } = string.Empty; // For logging (Web, PhoneApplication, etc.)
        
        public AutofillCredential(string domain, string username, string password)
        {
            Domain = domain;
            Username = username;
            Password = password;
            HasOtp = false;
        }
    }
}
