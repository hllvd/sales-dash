using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SalesApp.Services
{
    public class LoggingNotificationService : INotificationService
    {
        private readonly ILogger<LoggingNotificationService> _logger;

        public LoggingNotificationService(ILogger<LoggingNotificationService> logger)
        {
            _logger = logger;
        }

        public Task NotifyUserAsync(int userInternalId, string message, NotificationType type, string? entityType = null, int? entityId = null)
        {
            _logger.LogInformation("Notification created for User {UserInternalId} [{Type}]: {Message} (Entity: {EntityType} #{EntityId})",
                userInternalId, type, message, entityType ?? "None", entityId ?? 0);
            return Task.CompletedTask;
        }

        public Task NotifyRoleAsync(string role, string message, NotificationType type, string? entityType = null, int? entityId = null)
        {
            _logger.LogInformation("Notification created for Role {Role} [{Type}]: {Message} (Entity: {EntityType} #{EntityId})",
                role, type, message, entityType ?? "None", entityId ?? 0);
            return Task.CompletedTask;
        }

        public Task NotifyAsync(NotificationPayload payload)
        {
            _logger.LogInformation("Notification dispatched: {PayloadJson}", JsonSerializer.Serialize(payload));
            return Task.CompletedTask;
        }
    }
}
