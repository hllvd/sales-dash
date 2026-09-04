using System.Text.Json.Serialization;
using SalesApp.Notifications.Models;

namespace SalesApp.Notifications.DTOs
{
    public class NotificationResponseDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("sk")]
        public string Sk { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("animation")]
        public string Animation { get; set; } = AnimationKeys.NONE;

        [JsonPropertyName("actions")]
        public List<NotificationAction> Actions { get; set; } = new();

        [JsonPropertyName("relatedPK")]
        public string? RelatedPK { get; set; }

        [JsonPropertyName("relatedSK")]
        public string? RelatedSK { get; set; }

        [JsonPropertyName("unread")]
        public bool Unread { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    public class PagedNotificationsResponseDto
    {
        [JsonPropertyName("items")]
        public List<NotificationResponseDto> Items { get; set; } = new();

        [JsonPropertyName("nextCursor")]
        public string? NextCursor { get; set; }

        [JsonPropertyName("unreadCount")]
        public int UnreadCount { get; set; }
    }

    public class UnreadCountResponseDto
    {
        [JsonPropertyName("unreadCount")]
        public int UnreadCount { get; set; }
    }

    public class DomainRequestResponseDto
    {
        [JsonPropertyName("sk")]
        public string Sk { get; set; } = string.Empty;

        [JsonPropertyName("recipientUserId")]
        public string RecipientUserId { get; set; } = string.Empty;

        [JsonPropertyName("requesterUserId")]
        public string RequesterUserId { get; set; } = string.Empty;

        [JsonPropertyName("requestType")]
        public string RequestType { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("payloadJson")]
        public string PayloadJson { get; set; } = "{}";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("resolvedAt")]
        public DateTime? ResolvedAt { get; set; }
    }

    public class AcceptDeclineRequestDto
    {
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }

    public class RegisterDeviceTokenDto
    {
        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
