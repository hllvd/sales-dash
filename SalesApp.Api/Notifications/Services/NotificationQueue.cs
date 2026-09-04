using System.Threading.Channels;
using SalesApp.Notifications.Models;

namespace SalesApp.Notifications.Services
{
    public interface INotificationQueue
    {
        ValueTask EnqueueAsync((string userId, SseEvent sseEvent) message, CancellationToken cancellationToken = default);
        IAsyncEnumerable<(string userId, SseEvent sseEvent)> ReadAllAsync(CancellationToken cancellationToken = default);
    }

    public class NotificationQueue : INotificationQueue
    {
        private readonly Channel<(string userId, SseEvent sseEvent)> _channel;

        public NotificationQueue()
        {
            _channel = Channel.CreateBounded<(string userId, SseEvent sseEvent)>(new BoundedChannelOptions(5000)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public ValueTask EnqueueAsync((string userId, SseEvent sseEvent) message, CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(message, cancellationToken);
        }

        public IAsyncEnumerable<(string userId, SseEvent sseEvent)> ReadAllAsync(CancellationToken cancellationToken = default)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
