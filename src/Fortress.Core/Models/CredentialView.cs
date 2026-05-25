namespace Fortress.Core.Models
{
    /// <summary>
    /// Flat view model for a vault credential — used by autofill and list UIs.
    /// Pure POCO, no Prism / MAUI dependency. UI layers bind to their own
    /// observable wrappers on top of this.
    /// </summary>
    public class CredentialView
    {
        public Guid Id { get; set; }
        public string IconUri { get; set; } = string.Empty;
        public string FallbackIcon { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public bool HasOtp { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string CredentialType { get; set; } = string.Empty;
        public string? Data { get; set; }
        public string? Meta { get; set; }
        public double Progress { get; set; }
        public int Duration { get; set; } = 30;
        public string? Code { get; set; }
        public Guid? GroupId { get; set; }
        public string? GroupName { get; set; }
        public bool RequireAuthBeforeFill { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int PasswordStrengthScore { get; set; }
        public int PasswordStrengthLevel { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }
}
