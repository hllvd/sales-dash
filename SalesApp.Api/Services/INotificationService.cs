using System;
using System.Threading.Tasks;

namespace SalesApp.Services
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class NotificationPayload
    {
        public int? TargetUserInternalId { get; set; }
        public string? TargetRole { get; set; }
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public interface INotificationService
    {
        Task NotifyUserAsync(int userInternalId, string message, NotificationType type, string? entityType = null, int? entityId = null);
        Task NotifyRoleAsync(string role, string message, NotificationType type, string? entityType = null, int? entityId = null);
        Task NotifyAsync(NotificationPayload payload);
    }
}
