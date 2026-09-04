using System.Collections.Concurrent;
using System.Threading.Channels;
using SalesApp.Notifications.Models;

namespace SalesApp.Notifications.Services
{
    public interface ISseConnectionManager
    {
        ChannelReader<SseEvent> Subscribe(string userId, CancellationToken cancellationToken);
        void Unsubscribe(string userId, ChannelWriter<SseEvent> writer);
        Task BroadcastToUserAsync(string userId, SseEvent sseEvent);
        int ActiveConnectionCount { get; }
    }

    public class SseConnectionManager : ISseConnectionManager
    {
        private readonly ConcurrentDictionary<string, List<Channel<SseEvent>>> _connections = new();

        public int ActiveConnectionCount => _connections.Values.Sum(list => list.Count);

        public ChannelReader<SseEvent> Subscribe(string userId, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _connections.AddOrUpdate(userId,
                _ => new List<Channel<SseEvent>> { channel },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(channel);
                    }
                    return list;
                });

            cancellationToken.Register(() => Unsubscribe(userId, channel.Writer));

            return channel.Reader;
        }

        public void Unsubscribe(string userId, ChannelWriter<SseEvent> writer)
        {
            if (_connections.TryGetValue(userId, out var list))
            {
                lock (list)
                {
                    list.RemoveAll(c => c.Writer == writer);
                }

                if (list.Count == 0)
                {
                    _connections.TryRemove(userId, out _);
                }
            }
        }

        public async Task BroadcastToUserAsync(string userId, SseEvent sseEvent)
        {
            if (_connections.TryGetValue(userId, out var list))
            {
                List<Channel<SseEvent>> activeChannels;
                lock (list)
                {
                    activeChannels = list.ToList();
                }

                foreach (var channel in activeChannels)
                {
                    await channel.Writer.WriteAsync(sseEvent);
                }
            }
        }
    }
}
