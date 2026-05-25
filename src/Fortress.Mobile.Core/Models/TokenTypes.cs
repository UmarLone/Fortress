using System.ComponentModel.DataAnnotations;

namespace Fortress.Mobile.Core.Models
{
    public enum TokenTypes
    {
        [Display(Name = "Halberd")]
        Halberd = 1,
        [Display(Name = "Android")]
        Android = 2,
        [Display(Name = "IPhone")]
        IPhone = 3,
        [Display(Name = "GKChain")]
        GKChain = 4,       
        [Display(Name = "Fingerprint")]
        Fingerprint = 6,
        [Display(Name = "SmartCard")]
        SmartCard = 7
    }
}
