using System.ComponentModel.DataAnnotations;
namespace Fortress.Mobile.Core.Models
{
    public enum CredentialType
    {
        [Display(Name = "Web")]
        Web = 1,
        [Display(Name = "Otp")]
        Otp = 2,
        [Display(Name = "Application")]
        Application = 3,
        [Display(Name = "PhoneApplication")]
        PhoneApplication = 4,
        [Display(Name = "SecureNotes")]
        SecureNotes = 5,
        [Display(Name = "CreditCard")]
        CreditCard = 6,
        [Display(Name = "Address")]
        Address = 7,
    }
}
