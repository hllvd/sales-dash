using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SalesApp.Notifications.Services
{
    /// <summary>
    /// Background service that consumes messages from the NotificationQueue and broadcasts
    /// them to active SSE connections for real-time delivery.
    /// Acts as the in-process replacement for DynamoDB Streams -> Lambda fan-out.
    /// </summary>
    public class NotificationFanOutWorker : BackgroundService
    {
        private readonly INotificationQueue _queue;
        private readonly ISseConnectionManager _sseManager;
        private readonly ILogger<NotificationFanOutWorker> _logger;

        public NotificationFanOutWorker(
            INotificationQueue queue,
            ISseConnectionManager sseManager,
            ILogger<NotificationFanOutWorker> logger)
        {
            _queue = queue;
            _sseManager = sseManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationFanOutWorker started.");

            try
            {
                await foreach (var (userId, sseEvent) in _queue.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await _sseManager.BroadcastToUserAsync(userId, sseEvent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to broadcast SSE event to user {UserId}", userId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("NotificationFanOutWorker stopping due to cancellation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in NotificationFanOutWorker.");
            }
        }
    }
}
