using Microsoft.Extensions.Logging;
using SalesApp.Notifications.Models;
using SalesApp.Notifications.Repositories;
using SalesApp.Notifications.Utils;

namespace SalesApp.Notifications.Services
{
    public interface INotificationWriter
    {
        Task WriteNotificationAsync(NotificationItem item);
        Task WriteRequestAsync(DomainRequest request, NotificationItem linkedNotif);
    }

    public class NotificationWriter : INotificationWriter
    {
        private readonly INotificationRepository _repository;
        private readonly INotificationQueue _queue;
        private readonly ILogger<NotificationWriter> _logger;

        public NotificationWriter(
            INotificationRepository repository,
            INotificationQueue queue,
            ILogger<NotificationWriter> logger)
        {
            _repository = repository;
            _queue = queue;
            _logger = logger;
        }

        public async Task WriteNotificationAsync(NotificationItem item)
        {
            // 1. Check user preferences before persisting
            var prefs = await _repository.GetPrefsAsync(item.UserId);
            if (prefs.EnabledCategories.TryGetValue(item.Category, out var enabled) && !enabled)
            {
                _logger.LogInformation("Notification suppressed by user preference. Category={Category}, UserId={UserId}", item.Category, item.UserId);
                return;
            }

            // 2. Assign deterministic ULID key if not already set
            if (string.IsNullOrEmpty(item.SK))
            {
                item.SK = $"NOTIF#{UlidGenerator.NewUlid()}";
            }
            item.PK = $"USER#{item.UserId}";

            // 3. Informational/gamification items expire in 60 days
            if (!item.Ttl.HasValue)
            {
                item.Ttl = DateTimeOffset.UtcNow.AddDays(60).ToUnixTimeSeconds();
            }

            // 4. Persist to DynamoDB
            await _repository.CreateNotificationAsync(item);

            // 5. Fan-out real-time SSE event through queue
            var sseEvent = new SseEvent
            {
                Event = "notification",
                Data = item
            };
            await _queue.EnqueueAsync((item.UserId, sseEvent));

            // Also emit updated unread count
            var count = await _repository.GetUnreadCountAsync(item.UserId);
            await _queue.EnqueueAsync((item.UserId, new SseEvent
            {
                Event = "unread_count",
                Data = new { unreadCount = count }
            }));
        }

        public async Task WriteRequestAsync(DomainRequest request, NotificationItem linkedNotif)
        {
            var ulid = UlidGenerator.NewUlid();

            // Set request keys
            request.PK = $"USER#{request.RecipientUserId}";
            request.SK = $"REQUEST#{request.RequestType}#{ulid}";
            request.Status = RequestStatus.PENDING.ToString();
            request.CreatedAt = DateTime.UtcNow;
            request.Ttl = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();

            // Set linked notification keys
            linkedNotif.PK = $"USER#{request.RecipientUserId}";
            linkedNotif.SK = $"NOTIF#{ulid}";
            linkedNotif.UserId = request.RecipientUserId;
            linkedNotif.RelatedPK = request.PK;
            linkedNotif.RelatedSK = request.SK;
            linkedNotif.Category = NotificationCategory.SOLICITACOES.ToString();
            linkedNotif.Priority = NotificationPriority.HIGH.ToString();
            linkedNotif.Unread = true;
            linkedNotif.CreatedAt = DateTime.UtcNow;
            linkedNotif.Ttl = DateTimeOffset.UtcNow.AddDays(60).ToUnixTimeSeconds();

            request.RelatedNotifSK = linkedNotif.SK;

            // 1. Atomically persist both to DynamoDB
            await _repository.CreateRequestWithNotificationAsync(request, linkedNotif);

            // 2. Real-time fan-out for the recipient
            await _queue.EnqueueAsync((request.RecipientUserId, new SseEvent
            {
                Event = "notification",
                Data = linkedNotif
            }));

            // 3. Emit updated unread count
            var count = await _repository.GetUnreadCountAsync(request.RecipientUserId);
            await _queue.EnqueueAsync((request.RecipientUserId, new SseEvent
            {
                Event = "unread_count",
                Data = new { unreadCount = count }
            }));
        }
    }
}
