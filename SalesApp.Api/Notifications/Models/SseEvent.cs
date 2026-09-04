using System.Text.Json.Serialization;

namespace SalesApp.Notifications.Models
{
    public class SseEvent
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = "message"; // "notification", "unread_count", "ping"

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        public string ToSseFormat()
        {
            var json = Data is string str ? str : System.Text.Json.JsonSerializer.Serialize(Data);
            return $"event: {Event}\ndata: {json}\n\n";
        }
    }
}
