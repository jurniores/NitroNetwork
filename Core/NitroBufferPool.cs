using System;
using System.Collections.Generic;
using UnityEngine;

namespace NitroNetwork.Core
{
    /// <summary>
    /// NitroBufferPool provides a pooling mechanism for NitroBuffer instances.
    /// This helps minimize memory allocations and reduce garbage collection overhead
    /// by reusing buffer objects instead of creating and destroying them frequently.
    /// </summary>
    /// <remarks>
    /// The pool manages the lifecycle of NitroBuffer objects, offering methods to rent (obtain) and return (release) buffers.
    /// When a buffer is returned, it should be reset to its initial state before being reused.
    /// If the pool is empty when a buffer is requested, a new NitroBuffer instance is created.
    /// </remarks>
    /// <threadsafety>
    /// This class is not thread-safe. If used across multiple threads, external synchronization is required.
    /// </threadsafety>
    internal sealed class NitroBufferPool
    {
        /// <summary>
        /// The maximum time in milliseconds that a buffer is tracked before being considered as not returned.
        /// Used only in debug mode for tracking buffer leaks or long-lived buffers.
        /// </summary>
        private const int MAX_TRACKING_TIME = 500;

        /// <summary>
        /// The default capacity for each NitroBuffer instance.
        /// </summary>
        private int DefaultCapacity { get; } = 32000;

        /// <summary>
        /// Internal queue holding available NitroBuffer instances.
        /// </summary>
        private readonly Queue<NitroBuffer> _pool;
        private readonly Dictionary<int, Queue<NitroBuffer>> _poolDelta;
        List<int> keys = new(){
            1,5,9,13,17,21,25,29,33
        };

        /// <summary>
        /// Initializes a new instance of the NitroBufferPool class with the specified buffer capacity and pool size.
        /// </summary>
        /// <param name="capacity">The capacity of each NitroBuffer.</param>
        /// <param name="poolSize">The initial number of buffers to create in the pool.</param>
        internal NitroBufferPool(int capacity = 32000, int poolSize = 32)
        {
            DefaultCapacity = capacity;
            _pool = new Queue<NitroBuffer>();
            _poolDelta = new Dictionary<int, Queue<NitroBuffer>>();
            for (int i = 0; i < poolSize; i++)
            {
                _pool.Enqueue(new NitroBuffer(capacity, i));
            }

            foreach (var key in keys)
            {
                _poolDelta.Add(key, new Queue<NitroBuffer>());
                for (int i = 0; i < 100; i++)
                {
                    _poolDelta[key].Enqueue(new NitroBuffer(key, i));
                }
            }
        }

        /// <summary>
        /// Rents (retrieves) a NitroBuffer from the pool. If the pool is empty, a new buffer is created.
        /// </summary>
        /// <param name="enableTracking">Indicates if tracking should be enabled (for debugging).</param>
        /// <returns>A NitroBuffer instance ready for use.</returns>
        public NitroBuffer Rent(bool enableTracking = true)
        {
            if (_pool.Count > 0)
            {
                var buffer = _pool.Dequeue();
                return buffer;
            }
            else
            {
                return new NitroBuffer(DefaultCapacity, _pool.Count + 1);
            }
        }

        public NitroBuffer RentDelta(int key)
        {
            if (_poolDelta[key].Count > 0)
            {
                var buffer = _poolDelta[key].Dequeue();
                return buffer;
            }
            else
            {
                Debug.Log("criando um novo buffer");
                return new NitroBuffer(DefaultCapacity, _pool.Count + 1);
            }
        }

        // Let's track the object and check if it's back in the pool.
        // Very slow operation, but useful for debugging. Debug mode only.

        /// <summary>
        /// Returns a NitroBuffer to the pool, making it available for reuse.
        /// </summary>
        /// <param name="_buffer">The NitroBuffer instance to return.</param>
        public void Return(NitroBuffer _buffer)
        {
            if (_poolDelta.ContainsKey(_buffer.buffer.Length))
            {
                _poolDelta[_buffer.buffer.Length].Enqueue(_buffer);
                return;
            }
            _pool.Enqueue(_buffer);
        }
    }
}