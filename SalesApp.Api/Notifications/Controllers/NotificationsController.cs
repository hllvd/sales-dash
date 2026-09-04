using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Notifications.DTOs;
using SalesApp.Notifications.Models;
using SalesApp.Notifications.Repositories;
using SalesApp.Notifications.Services;

namespace SalesApp.Notifications.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _repository;
        private readonly ISseConnectionManager _sseManager;
        private readonly INotificationWriter _writer;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationRepository repository,
            ISseConnectionManager sseManager,
            INotificationWriter writer,
            ILogger<NotificationsController> logger)
        {
            _repository = repository;
            _sseManager = sseManager;
            _writer = writer;
            _logger = logger;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        // GET: api/notifications
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedNotificationsResponseDto>>> GetRecent([FromQuery] int limit = 20, [FromQuery] string? cursor = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<PagedNotificationsResponseDto> { Success = false, Message = "Usuário não autenticado." });
            }

            if (limit is < 1 or > 50) limit = 20;

            var (items, nextCursor) = await _repository.GetRecentAsync(userId, limit, cursor);
            var unreadCount = await _repository.GetUnreadCountAsync(userId);

            var dtos = items.Select(MapToDto).ToList();

            return Ok(new ApiResponse<PagedNotificationsResponseDto>
            {
                Success = true,
                Data = new PagedNotificationsResponseDto
                {
                    Items = dtos,
                    NextCursor = nextCursor,
                    UnreadCount = unreadCount
                },
                Message = "Notificações obtidas com sucesso."
            });
        }

        // GET: api/notifications/unread-count
        [HttpGet("unread-count")]
        public async Task<ActionResult<ApiResponse<UnreadCountResponseDto>>> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<UnreadCountResponseDto> { Success = false, Message = "Usuário não autenticado." });
            }

            var count = await _repository.GetUnreadCountAsync(userId);

            return Ok(new ApiResponse<UnreadCountResponseDto>
            {
                Success = true,
                Data = new UnreadCountResponseDto { UnreadCount = count },
                Message = "Contagem de não lidas obtida com sucesso."
            });
        }

        // POST: api/notifications/{sk}/read
        [HttpPost("{sk}/read")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkRead(string sk)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<bool> { Success = false, Message = "Usuário não autenticado." });
            }

            // Support both raw ULID or NOTIF# prefix
            var notifSk = sk.StartsWith("NOTIF#") ? sk : $"NOTIF#{sk}";
            await _repository.MarkNotificationReadAsync(userId, notifSk);

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Data = true,
                Message = "Notificação marcada como lida."
            });
        }

        // POST: api/notifications/read-all
        [HttpPost("read-all")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkAllRead()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<bool> { Success = false, Message = "Usuário não autenticado." });
            }

            await _repository.MarkAllReadAsync(userId);

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Data = true,
                Message = "Todas as notificações foram marcadas como lidas."
            });
        }

        // GET: api/notifications/preferences
        [HttpGet("preferences")]
        public async Task<ActionResult<ApiResponse<NotificationPrefs>>> GetPreferences()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<NotificationPrefs> { Success = false, Message = "Usuário não autenticado." });
            }

            var prefs = await _repository.GetPrefsAsync(userId);
            return Ok(new ApiResponse<NotificationPrefs> { Success = true, Data = prefs });
        }

        // PUT: api/notifications/preferences
        [HttpPut("preferences")]
        public async Task<ActionResult<ApiResponse<NotificationPrefs>>> UpdatePreferences([FromBody] NotificationPrefs prefs)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<NotificationPrefs> { Success = false, Message = "Usuário não autenticado." });
            }

            prefs.UserId = userId;
            await _repository.UpsertPrefsAsync(prefs);

            return Ok(new ApiResponse<NotificationPrefs> { Success = true, Data = prefs, Message = "Preferências atualizadas." });
        }

        // POST: api/notifications/devices
        [HttpPost("devices")]
        public async Task<ActionResult<ApiResponse<bool>>> RegisterDevice([FromBody] RegisterDeviceTokenDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse<bool> { Success = false, Message = "Usuário não autenticado." });
            }

            await _repository.UpsertDeviceTokenAsync(new DeviceTokenItem
            {
                UserId = userId,
                Platform = dto.Platform,
                Token = dto.Token
            });

            return Ok(new ApiResponse<bool> { Success = true, Data = true, Message = "Token de dispositivo registrado." });
        }

        // GET: api/notifications/stream
        // SSE real-time stream endpoint
        [HttpGet("stream")]
        public async Task Stream(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                Response.StatusCode = 401;
                return;
            }

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Tell Nginx not to buffer SSE

            var reader = _sseManager.Subscribe(userId, cancellationToken);

            // Send initial connection event with current unread count
            var unreadCount = await _repository.GetUnreadCountAsync(userId);
            var initialEvent = new SseEvent
            {
                Event = "connected",
                Data = new { unreadCount, message = "SSE stream established" }
            };

            await Response.WriteAsync(initialEvent.ToSseFormat(), cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check for messages with timeout for heartbeat ping
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    try
                    {
                        if (await reader.WaitToReadAsync(linkedCts.Token))
                        {
                            while (reader.TryRead(out var evt))
                            {
                                await Response.WriteAsync(evt.ToSseFormat(), cancellationToken);
                                await Response.Body.FlushAsync(cancellationToken);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        // Send periodic heartbeat ping to prevent connection drop
                        var ping = new SseEvent { Event = "ping", Data = new { timestamp = DateTime.UtcNow } };
                        await Response.WriteAsync(ping.ToSseFormat(), cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SSE connection error for user {UserId}", userId);
            }
        }

        private static NotificationResponseDto MapToDto(NotificationItem item)
        {
            return new NotificationResponseDto
            {
                Id = item.Id,
                Sk = item.SK,
                Type = item.Type,
                Category = item.Category,
                Priority = item.Priority,
                Title = item.Title,
                Message = item.Message,
                Animation = item.Animation,
                Actions = item.Actions,
                RelatedPK = item.RelatedPK,
                RelatedSK = item.RelatedSK,
                Unread = item.Unread,
                CreatedAt = item.CreatedAt
            };
        }
    }
}
