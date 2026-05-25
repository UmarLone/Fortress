using System.ComponentModel.DataAnnotations;

namespace Fortress.Mobile.Core.Models
{
    public enum NotificationType
    {

        [Display(Name = "Information")]
        Information = 0,
        [Display(Name = "Warning")]
        Warning = 1,
        [Display(Name = "Error")]
        Error = 2,
        [Display(Name = "Confirm")]
        Ask = 3,
        [Display(Name = "Success")]
        Success = 4,
        [Display(Name = "Critical")]
        Critical = 5,
    }
}
