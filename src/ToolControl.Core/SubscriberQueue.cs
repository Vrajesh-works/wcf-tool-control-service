using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ToolControl.Core
{
    /// <summary>
    /// A bounded, thread-safe, per-client queue.
    /// Producers never block: when the queue is full the oldest item is dropped
    /// and <see cref="DroppedCount"/> is incremented, so one slow client can
    /// never stall the control loop or other clients.
    /// </summary>
    public sealed class SubscriberQueue<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly int _capacity;
        private long _droppedCount;
        private int _completed;

        public SubscriberQueue(int capacity = 1000)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int Count => _queue.Count;
        public long DroppedCount => Interlocked.Read(ref _droppedCount);
        public bool IsCompleted => Volatile.Read(ref _completed) == 1;

        public void Enqueue(T item)
        {
            if (IsCompleted) return;
            _queue.Enqueue(item);
            // Bound the queue without blocking the producer.
            while (_queue.Count > _capacity && _queue.TryDequeue(out _))
            {
                Interlocked.Increment(ref _droppedCount);
            }
            _signal.Release();
        }

        public async Task<T> TakeAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (_queue.TryDequeue(out var item)) return item;
                if (IsCompleted) throw new OperationCanceledException("Queue completed.");
                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public void Complete()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0) _signal.Release();
        }

        public void Dispose()
        {
            Complete();
            _signal.Dispose();
        }
    }
}
