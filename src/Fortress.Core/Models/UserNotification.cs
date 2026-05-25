namespace Fortress.Core.Models
{
    public class UserNotification
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public string Message { get; set; } = string.Empty;
    public bool IsSeen { get; set; }
        public bool IsExpired { get; set; }
        public DateTime CreationDateTime { get; set; }
    }

    public enum NotificationType
    {
   Info    = 0,
        Warning = 1,
        Alert   = 2,
      BreachDetected = 3,
        SaveLoginPrompt = 4,
    }
}
