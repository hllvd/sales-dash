using System.Text.Json;
using Microsoft.Extensions.Logging;
using SalesApp.Notifications.Models;
using SalesApp.Notifications.Services;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    /// <summary>
    /// Backward-compatible implementation of INotificationService that logs and forwards
    /// to the modern DynamoDB single-table notification writer.
    /// </summary>
    public class DynamoDbNotificationService : INotificationService
    {
        private readonly ILogger<DynamoDbNotificationService> _logger;
        private readonly INotificationWriter _notificationWriter;
        private readonly IUserRepository _userRepository;

        public DynamoDbNotificationService(
            ILogger<DynamoDbNotificationService> logger,
            INotificationWriter notificationWriter,
            IUserRepository userRepository)
        {
            _logger = logger;
            _notificationWriter = notificationWriter;
            _userRepository = userRepository;
        }

        public async Task NotifyUserAsync(int userInternalId, string message, NotificationType type, string? entityType = null, int? entityId = null)
        {
            _logger.LogInformation("NotifyUserAsync for userInternalId={Id}, type={Type}: {Msg}", userInternalId, type, message);

            var user = await _userRepository.GetByInternalIdAsync(userInternalId);
            if (user == null)
            {
                _logger.LogWarning("User with internalId {Id} not found. Skipping persistent notification.", userInternalId);
                return;
            }

            var category = type switch
            {
                NotificationType.Warning => NotificationCategory.URGENCIA.ToString(),
                NotificationType.Error => NotificationCategory.URGENCIA.ToString(),
                _ => NotificationCategory.CONQUISTAS.ToString()
            };

            var priority = type switch
            {
                NotificationType.Error => NotificationPriority.CRITICAL.ToString(),
                NotificationType.Warning => NotificationPriority.HIGH.ToString(),
                _ => NotificationPriority.NORMAL.ToString()
            };

            var item = new NotificationItem
            {
                UserId = user.Id.ToString(),
                Type = entityType ?? "SYSTEM_NOTIFICATION",
                Category = category,
                Priority = priority,
                Title = "Nova Notificação",
                Message = message,
                Animation = AnimationKeys.NONE,
                Unread = true,
                CreatedAt = DateTime.UtcNow
            };

            await _notificationWriter.WriteNotificationAsync(item);
        }

        public Task NotifyRoleAsync(string role, string message, NotificationType type, string? entityType = null, int? entityId = null)
        {
            _logger.LogInformation("NotifyRoleAsync for role={Role} [{Type}]: {Message}", role, type, message);
            return Task.CompletedTask;
        }

        public async Task NotifyAsync(NotificationPayload payload)
        {
            if (payload.TargetUserInternalId.HasValue)
            {
                await NotifyUserAsync(payload.TargetUserInternalId.Value, payload.Message, payload.Type, payload.RelatedEntityType, payload.RelatedEntityId);
            }
            else
            {
                _logger.LogInformation("Notification dispatched without specific target internalId: {PayloadJson}", JsonSerializer.Serialize(payload));
            }
        }
    }
}
