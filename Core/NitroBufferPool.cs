using System.Collections.Generic;

namespace NitroNetwork.Core
{
    /// <summary>
    /// The DataBufferPool class provides a buffer pooling mechanism for managing instances of DataBuffer.
    /// It helps to minimize memory allocation overhead and reduce garbage collection pressure
    /// by reusing buffer instances from the pool.
    /// </summary>
    /// <remarks>
    /// This class is primarily used to manage the lifecycle of DataBuffer objects by providing rent and return operations.
    /// When a buffer is returned, it is reset to its initial state and made available for reuse.
    /// If the pool is empty when a buffer is requested, a new instance of DataBuffer will be created.
    /// </remarks>
    /// <threadsafety>
    /// This class is not thread-safe. Synchronization must be considered if accessed across multiple threads.
    /// </threadsafety>
    internal sealed class NitroBufferPool
    {
        /// The maximum time in milliseconds that a buffer is being tracked before it is considered
        /// to have not been disposed or returned to the pool. <c>Debug mode only.</c>
        /// 500ms seems good to me, if there is an expensive operation that takes more than 500ms, it is recommended to call SupressTracking.
        private const int MAX_TRACKING_TIME = 500;

        private int DefaultCapacity { get; } = 32000;
        private readonly Queue<NitroBuffer> _pool;

        internal NitroBufferPool(int capacity = 32000, int poolSize = 32)
        {
            DefaultCapacity = capacity;
            _pool = new Queue<NitroBuffer>();
            for (int i = 0; i < poolSize; i++)
            {

                _pool.Enqueue(new NitroBuffer(capacity));
            }

        }

        /// <inheritdoc />
        public NitroBuffer Rent(bool enableTracking = true)
        {
            if (_pool.Count > 0)
            {
                var buffer = _pool.Dequeue();
                return buffer;
            }
            else
            {

                return new NitroBuffer(DefaultCapacity);
            }
        }

        // Let's track the object and check if it's back in the pool.
        // Very slow operation, but useful for debugging. Debug mode only.


        /// <inheritdoc />
        public void Return(NitroBuffer _buffer)
        {
            _pool.Enqueue(_buffer);
        }
    }
}