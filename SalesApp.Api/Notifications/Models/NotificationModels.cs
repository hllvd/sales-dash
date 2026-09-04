using System.Text.Json.Serialization;

namespace SalesApp.Notifications.Models
{
    public enum NotificationCategory
    {
        CONQUISTAS,     // Achievements (Normal)
        PROGRESSO,      // Progress / Coaching (Normal)
        URGENCIA,       // Urgency (High)
        SOLICITACOES,   // Requests requiring accept/decline (High)
        TAREFAS,        // Tasks / Reminders (Normal)
        OPORTUNIDADES   // Sales opportunities / churn risk (High/Critical)
    }

    public enum NotificationPriority
    {
        LOW,        // Notification center only
        NORMAL,     // Center + toast
        HIGH,       // Center + toast + push
        CRITICAL    // Prominent / blocking alert
    }

    public enum RequestStatus
    {
        PENDING,
        ACCEPTED,
        DECLINED,
        EXPIRED
    }

    public static class AnimationKeys
    {
        public const string NONE = "NONE";
        public const string LEVEL_UP = "LEVEL_UP";
        public const string BADGE_UNLOCKED = "BADGE_UNLOCKED";
        public const string TROPHY = "TROPHY";
        public const string TARGET_REACHED = "TARGET_REACHED";
        public const string URGENT_ALERT = "URGENT_ALERT";
    }

    public static class ActionTypes
    {
        public const string ACCEPT_CHILD_USER_REQUEST = "ACCEPT_CHILD_USER_REQUEST";
        public const string DECLINE_CHILD_USER_REQUEST = "DECLINE_CHILD_USER_REQUEST";
        public const string ACCEPT_MATRICULA_REQUEST = "ACCEPT_MATRICULA_REQUEST";
        public const string DECLINE_MATRICULA_REQUEST = "DECLINE_MATRICULA_REQUEST";
    }

    public class NotificationAction
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Informational, disposable notification item stored in DynamoDB.
    /// PK: USER#<userId>
    /// SK: NOTIF#<ulid>
    /// GSI1PK: UNREAD#<userId> (sparse - only present when Unread == true)
    /// GSI1SK: NOTIF#<ulid>
    /// </summary>
    public class NotificationItem
    {
        [JsonPropertyName("PK")]
        public string PK { get; set; } = string.Empty;

        [JsonPropertyName("SK")]
        public string SK { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id => SK.StartsWith("NOTIF#") ? SK.Substring(6) : SK;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = NotificationCategory.CONQUISTAS.ToString();

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = NotificationPriority.NORMAL.ToString();

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
        public bool Unread { get; set; } = true;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("ttl")]
        public long? Ttl { get; set; }
    }

    /// <summary>
    /// Domain request item representing an actionable business entity.
    /// PK: USER#<recipientUserId>
    /// SK: REQUEST#<REQUEST_TYPE>#<ulid>
    /// GSI2PK: REQUESTER#<requesterUserId> (reverse lookup)
    /// GSI2SK: REQUEST#<REQUEST_TYPE>#<ulid>
    /// </summary>
    public class DomainRequest
    {
        [JsonPropertyName("PK")]
        public string PK { get; set; } = string.Empty;

        [JsonPropertyName("SK")]
        public string SK { get; set; } = string.Empty;

        [JsonPropertyName("recipientUserId")]
        public string RecipientUserId { get; set; } = string.Empty;

        [JsonPropertyName("requesterUserId")]
        public string RequesterUserId { get; set; } = string.Empty;

        [JsonPropertyName("requestType")]
        public string RequestType { get; set; } = string.Empty; // e.g. "CHILD_USER_REQUEST", "MATRICULA_REQUEST"

        [JsonPropertyName("status")]
        public string Status { get; set; } = RequestStatus.PENDING.ToString();

        [JsonPropertyName("payloadJson")]
        public string PayloadJson { get; set; } = "{}";

        [JsonPropertyName("relatedNotifSK")]
        public string? RelatedNotifSK { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("resolvedAt")]
        public DateTime? ResolvedAt { get; set; }

        [JsonPropertyName("resolvedBy")]
        public string? ResolvedBy { get; set; }

        [JsonPropertyName("ttl")]
        public long? Ttl { get; set; }
    }

    /// <summary>
    /// User preferences entity for notifications.
    /// PK: USER#<userId>
    /// SK: PREFS
    /// </summary>
    public class NotificationPrefs
    {
        [JsonPropertyName("PK")]
        public string PK { get; set; } = string.Empty;

        [JsonPropertyName("SK")]
        public string SK { get; set; } = "PREFS";

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("enabledCategories")]
        public Dictionary<string, bool> EnabledCategories { get; set; } = new()
        {
            { NotificationCategory.CONQUISTAS.ToString(), true },
            { NotificationCategory.PROGRESSO.ToString(), true },
            { NotificationCategory.URGENCIA.ToString(), true },
            { NotificationCategory.SOLICITACOES.ToString(), true },
            { NotificationCategory.TAREFAS.ToString(), true },
            { NotificationCategory.OPORTUNIDADES.ToString(), true }
        };

        [JsonPropertyName("allowPush")]
        public bool AllowPush { get; set; } = true;

        [JsonPropertyName("allowToast")]
        public bool AllowToast { get; set; } = true;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Device token registration entity for future FCM/APNs push.
    /// PK: USER#<userId>
    /// SK: DEVICE#<platform>#<token>
    /// </summary>
    public class DeviceTokenItem
    {
        [JsonPropertyName("PK")]
        public string PK { get; set; } = string.Empty;

        [JsonPropertyName("SK")]
        public string SK { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty; // "fcm_android", "fcm_web", "apns_ios"

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
