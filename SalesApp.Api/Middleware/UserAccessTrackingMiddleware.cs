using System.Collections.Concurrent;
using System.Security.Claims;
using SalesApp.Data;

namespace SalesApp.Middleware
{
    public class UserAccessTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserAccessTrackingMiddleware> _logger;
        private static readonly ConcurrentDictionary<Guid, DateTime> _lastWriteCache = new();
        private static readonly TimeSpan ThrottleInterval = TimeSpan.FromHours(24);

        public UserAccessTrackingMiddleware(RequestDelegate next, ILogger<UserAccessTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdStr, out var userId))
                {
                    var now = DateTime.UtcNow;
                    if (!_lastWriteCache.TryGetValue(userId, out var lastWrite) || (now - lastWrite) >= ThrottleInterval)
                    {
                        // Update cache immediately to prevent concurrent requests from firing duplicate DB updates
                        _lastWriteCache[userId] = now;

                        // Fire-and-forget background DB update
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var scope = serviceProvider.CreateScope();
                                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var user = await dbContext.Users.FindAsync(userId);
                                if (user != null)
                                {
                                    user.LastAccessedAt = now;
                                    await dbContext.SaveChangesAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to update LastAccessedAt for user {UserId}", userId);
                            }
                        });
                    }
                }
            }

            await _next(context);
        }

        public static void RecordAccess(Guid userId, DateTime accessTime)
        {
            _lastWriteCache[userId] = accessTime;
        }
    }

    public static class UserAccessTrackingMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserAccessTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserAccessTrackingMiddleware>();
        }
    }
}
