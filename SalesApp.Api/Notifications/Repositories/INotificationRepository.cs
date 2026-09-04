using SalesApp.Notifications.Models;

namespace SalesApp.Notifications.Repositories
{
    public interface INotificationRepository
    {
        // Notifications
        Task<(List<NotificationItem> items, string? nextCursor)> GetRecentAsync(string userId, int limit = 20, string? cursor = null);
        Task<int> GetUnreadCountAsync(string userId);
        Task<NotificationItem?> GetNotificationAsync(string userId, string notifSk);
        Task CreateNotificationAsync(NotificationItem item);
        Task MarkNotificationReadAsync(string userId, string notifSk);
        Task MarkAllReadAsync(string userId);

        // Requests
        Task CreateRequestWithNotificationAsync(DomainRequest request, NotificationItem notification);
        Task<DomainRequest?> GetRequestAsync(string userId, string requestSk);
        Task<List<DomainRequest>> GetPendingRequestsAsync(string userId);
        Task<List<DomainRequest>> GetSentRequestsAsync(string requesterUserId);
        Task<bool> ResolveRequestTransactAsync(string userId, string requestSk, string newStatus, string resolvedBy, string? relatedNotifSk = null);

        // Preferences
        Task<NotificationPrefs> GetPrefsAsync(string userId);
        Task UpsertPrefsAsync(NotificationPrefs prefs);

        // Device tokens
        Task UpsertDeviceTokenAsync(DeviceTokenItem deviceToken);
    }
}
