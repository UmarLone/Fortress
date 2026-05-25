using System;
using System.ComponentModel.DataAnnotations;

namespace Fortress.Mobile.Core.Models
{
    public class UserNotification
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public NotificationType Type { get; set; }
        public string Message { get; set; }
        public bool IsSeen { get; set; }
        public bool IsExpired { get; set; }
        public DateTime CreationDateTime { get; set; }
 
    }
}
