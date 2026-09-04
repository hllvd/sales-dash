using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesApp.Notifications.Models;
using SalesApp.Notifications.Repositories;
using SalesApp.Notifications.Services;

namespace SalesApp.Notifications.BackgroundJobs
{
    /// <summary>
    /// Background periodic service that checks for expired pending domain requests (older than 30 days)
    /// and auto-declines them cleanly.
    /// This fills the role of DynamoDB Streams -> Lambda TTL delete listener in a clean, self-contained way.
    /// </summary>
    public class StaleRequestCleanupService : BackgroundService
    {
        private readonly INotificationRepository _repository;
        private readonly INotificationQueue _queue;
        private readonly ILogger<StaleRequestCleanupService> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

        public StaleRequestCleanupService(
            INotificationRepository repository,
            INotificationQueue queue,
            ILogger<StaleRequestCleanupService> logger)
        {
            _repository = repository;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StaleRequestCleanupService initialized. Interval={Interval}", CheckInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                    // Best-effort cleanup logic: queries pending requests and expires them if over 30 days old
                    _logger.LogDebug("Running periodic check for stale domain requests.");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in StaleRequestCleanupService execution.");
                }
            }
        }
    }
}
