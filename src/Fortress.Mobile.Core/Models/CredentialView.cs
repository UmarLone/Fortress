using Prism.Mvvm;
using System;

namespace Fortress.Mobile.Core.Models
{
    public class CredentialView : BindableBase
    {
       
        private Guid id;
        public Guid Id
        {
            get { return id; }
            set { SetProperty(ref id, value); }
        }
        private string iconUri;
        public string IconUri
        {
            get { return iconUri; }
            set { SetProperty(ref iconUri, value); }
        }
        private string fallbackIcon;
        public string FallbackIcon
        {
            get { return fallbackIcon; }
            set { SetProperty(ref fallbackIcon, value); }
        }
        private string domain;
        public string Domain
        {
            get { return domain; }
            set { SetProperty(ref domain, value); }
        }

        private bool hasOtp;
        public bool HasOtp
        {
            get { return hasOtp; }
            set { SetProperty(ref hasOtp, value); }
        }
        
        private string username;
        public string Username
        {
            get { return username; }
            set { SetProperty(ref username, value); }
        }
        private string password;
        public string Password
        {
            get { return password; }
            set { SetProperty(ref password, value); }
        }
        private string credentialType;
        public string CredentialType
        {
            get { return credentialType; }
            set { SetProperty(ref credentialType, value); }
        }
        public string Data { get; set; }
        public string Meta { get; set; }

        private double progress;
        public double Progress
        {
            get { return progress; }
            set { SetProperty(ref progress, value); }
        }
        private int duration = 30;
        public int Duration
        {
            get { return duration; }
            set { SetProperty(ref duration, value); }
        }
        private string code;
        public string Code
        {
            get { return code; }
            set { SetProperty(ref code, value); }
        }
        
        private Guid? _groupId;
        /// <summary>The VaultGroup this credential belongs to, or null if ungrouped.</summary>
        public Guid? GroupId
        {
            get => _groupId;
            set => SetProperty(ref _groupId, value);
        }

        private string _groupName;
        /// <summary>Cached group name for display — populated by the ViewModel after load.</summary>
        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        private bool _requireAuthBeforeFill;
        /// <summary>
        /// Mirror of <see cref="LoginItem.RequireAuthBeforeFill"/>
        /// When true this credential requires biometric/PIN before autofill.
        /// </summary>
        public bool RequireAuthBeforeFill
        {
            get => _requireAuthBeforeFill;
            set => SetProperty(ref _requireAuthBeforeFill, value);
        }

        // ── Metadata ─────────────────────────────────────────────────────────

        private DateTime _createdAt;
        /// <summary>When the item was first saved to the vault.</summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        private DateTime _updatedAt;
        /// <summary>When the item was last modified.</summary>
        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        private int _passwordStrengthScore;
        /// <summary>0–100 strength score computed at save time.</summary>
        public int PasswordStrengthScore
        {
            get => _passwordStrengthScore;
            set => SetProperty(ref _passwordStrengthScore, value);
        }

        private int _passwordStrengthLevel;
        /// <summary>Numeric PasswordStrengthLevel enum value.</summary>
        public int PasswordStrengthLevel
        {
            get => _passwordStrengthLevel;
            set => SetProperty(ref _passwordStrengthLevel, value);
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private string _isFavorite_unused;
        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set => SetProperty(ref _isFavorite, value);
        }

        private bool _isSelected;
     /// <summary>Tracks whether this item is checked in multi-select mode.</summary>
    public bool IsSelected
      {
            get => _isSelected;
       set => SetProperty(ref _isSelected, value);
        }
    }
}
