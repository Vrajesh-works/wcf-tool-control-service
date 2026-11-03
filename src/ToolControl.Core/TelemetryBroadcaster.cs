using System;
using System.Collections.Concurrent;

namespace ToolControl.Core
{
    /// <summary>Fan-out for live readings. Publishing is non-blocking by design.</summary>
    public sealed class TelemetryBroadcaster
    {
        private readonly ConcurrentDictionary<string, SubscriberQueue<ChamberReading>> _subscribers =
            new ConcurrentDictionary<string, SubscriberQueue<ChamberReading>>();

        public SubscriberQueue<ChamberReading> Subscribe(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client id is required.", nameof(clientId));
            var queue = new SubscriberQueue<ChamberReading>();
            _subscribers.AddOrUpdate(clientId, queue, (_, existing) => existing);
            return _subscribers[clientId];
        }

        public bool Unsubscribe(string clientId)
        {
            if (_subscribers.TryRemove(clientId, out var queue))
            {
                queue.Complete();
                queue.Dispose();
                return true;
            }
            return false;
        }

        public int SubscriberCount => _subscribers.Count;

        /// <summary>Never blocks, never throws for a slow or dead client.</summary>
        public void Publish(ChamberReading reading)
        {
            foreach (var queue in _subscribers.Values)
            {
                try { queue.Enqueue(reading); }
                catch (ObjectDisposedException) { /* client left mid-publish; unsubscribe cleans it up */ }
            }
        }
    }
}
