using System;
using System.Runtime.InteropServices;
using System.Text;
using MemoryPack;

namespace NitroNetwork.Core
{
    /// <summary>
    /// A buffer class for efficient network serialization and deserialization.
    /// Provides methods for reading and writing various data types including primitive types, 
    /// strings, structs, and complex objects.
    /// </summary>
    public class NitroBuffer : IDisposable
    {
        /// <summary>
        /// The internal buffer for writing data.
        /// </summary>
        [NonSerialized]
        internal byte[] buffer;

        /// <summary>
        /// Gets a span representing the valid data in the buffer.
        /// </summary>
        public Span<byte> Buffer => buffer.AsSpan(0, tam);

        /// <summary>
        /// Current position in the buffer. Starts at 3 to reserve space for
        /// command ID and identity ID in the first bytes.
        /// </summary>
        internal int tam = 3;
        public NitroBuffer(int capacity)
        {
            buffer = new byte[capacity];
        }
        public NitroBuffer()
        {
            buffer = new byte[1024];
        }
        /// <summary>
        /// Writes a string to the buffer using UTF-8 encoding and null termination.
        /// </summary>
        /// <param name="txt">The string to write.</param>
        private void WriteString(string txt)
        {
            if (string.IsNullOrEmpty(txt))
            {

                buffer[tam++] = 0;
                buffer[tam++] = 0;
                return;
            }

            Encoding utf8 = Encoding.UTF8;

            ushort byteCount = (ushort)utf8.GetByteCount(txt);
            buffer[tam++] = (byte)(byteCount & 0xFF);
            buffer[tam++] = (byte)((byteCount >> 8) & 0xFF);

            Span<byte> targetSpan = buffer.AsSpan(tam, byteCount);

            int bytesWritten = utf8.GetBytes(txt.AsSpan(), targetSpan);

            tam += bytesWritten;
        }

        /// <summary>
        /// Writes a complex object to the buffer using MemoryPack serialization.
        /// Uses 0x1E as a record separator to mark the end of the serialized data.
        /// </summary>
        /// <typeparam name="T">The type of object to write.</typeparam>
        /// <param name="type">The object to write.</param>
        public void WriteClass<T>(T type)
        {
            if (type == null)
            {
                // Handle null case - write a special marker
                buffer[tam++] = 0;
                buffer[tam++] = 0;
                return;
            }

            Span<byte> nBuffer = MemoryPackSerializer.Serialize(type);
            buffer[tam++] = (byte)(nBuffer.Length & 0xFF);
            buffer[tam++] = (byte)((nBuffer.Length >> 8) & 0xFF);
            // Copy the serialized data to the buffer
            nBuffer.CopyTo(buffer.AsSpan(tam));
            tam += nBuffer.Length;
        }

        /// <summary>
        /// Writes data to the buffer based on the type. Handles strings, complex objects,
        /// and value types (structs) differently.
        /// </summary>
        /// <typeparam name="P">The type of data to write.</typeparam>
        /// <param name="p1">The data to write.</param>
        public unsafe void Write<P>(P p1)
        {
            if (typeof(P) == typeof(string))
            {
                WriteString(Convert.ChangeType(p1, typeof(string)) as string);
                return;
            }
            if (!typeof(P).IsValueType || !typeof(P).IsLayoutSequential && !typeof(P).IsExplicitLayout)
            {
                WriteClass(p1);
                return;
            }
            int size_t = Marshal.SizeOf<P>();

            // Check if there's enough space in the buffer
            if (3 + size_t > buffer.Length)
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, 3 + size_t));

            Span<byte> bSpan = buffer.AsSpan(tam);

            fixed (byte* byteArrayPtr = bSpan)
                Marshal.StructureToPtr(p1, (IntPtr)byteArrayPtr, true);

            tam += size_t;
        }
        public unsafe void WriteForRead(ReadOnlySpan<byte> span)
        {
            span.CopyTo(buffer.AsSpan());
        }
        /// <summary>
        /// Sets the command ID and identity ID in the buffer header.
        /// </summary>
        /// <param name="id">The command ID.</param>
        /// <param name="identityId">The identity ID.</param>
        public void SetInfo(byte id, ushort identityId)
        {
            buffer[0] = id;
            buffer[1] = (byte)(identityId & 0xFF);
            buffer[2] = (byte)((identityId >> 8) & 0xFF);
        }

        /// <summary>
        /// Reads data from the buffer based on the type. Handles strings, complex objects,
        /// and value types (structs) differently.
        /// </summary>
        /// <typeparam name="T">The type of data to read.</typeparam>
        /// <returns>The read data converted to the specified type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's not enough data in the buffer.</exception>
        /// <exception cref="FormatException">Thrown when the data format is invalid.</exception>
        public unsafe T Read<T>()
        {
            if (typeof(T) == typeof(byte[]))
            {
                object buffer = ReadBuffer();
                return (T)buffer;
            }
            if (typeof(T) == typeof(string))
            {
                object txt = ReadString();
                return (T)txt;
            }

            if (!typeof(T).IsValueType || !typeof(T).IsLayoutSequential && !typeof(T).IsExplicitLayout)
            {
                return ReadClass<T>();
            }

            int size = Marshal.SizeOf<T>();

            // Check if there's enough data
            if (tam + size > buffer.Length)
            {
                string errorMsg = $"Error reading {typeof(T).Name}: current position ({tam}) + size ({size}) exceeds buffer length ({buffer.Length})";
                UnityEngine.Debug.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            T result;
            try
            {
                Span<byte> bSpan = buffer.AsSpan(tam);
                fixed (byte* ptr = bSpan)
                {
                    result = Marshal.PtrToStructure<T>((IntPtr)ptr);
                }

                // Update the position
                tam += size;
                return result;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error reading {typeof(T).Name}: {ex.Message}";
                UnityEngine.Debug.LogError(errorMsg);

                // Show bytes in the region to help with debugging
                int startPos = Math.Max(0, tam - 10);
                int length = Math.Min(buffer.Length - startPos, tam + size + 10 - startPos);
                byte[] contextBytes = new byte[length];
                Array.Copy(buffer, startPos, contextBytes, 0, length);

                UnityEngine.Debug.LogError($"Context (bytes from position {startPos}): {BitConverter.ToString(contextBytes)}");
                throw;
            }
        }

        /// <summary>
        /// Reads a complex object from the buffer using MemoryPack deserialization.
        /// Looks for the 0x1E record separator to determine the end of the serialized data.
        /// </summary>
        /// <typeparam name="T">The type of object to read.</typeparam>
        /// <returns>The deserialized object.</returns>
        /// <exception cref="FormatException">Thrown when the record separator is not found.</exception>
        private T ReadClass<T>()
        {
            UnityEngine.Debug.Log($"Reading class {typeof(T).Name} from buffer at position {tam}");
            Span<byte> bSpan = buffer.AsSpan(tam);

            ushort size = (ushort)((bSpan[0] & 0xFF) | ((bSpan[1] & 0xFF) << 8));
            if (size == 0)
                return default;

            T t = MemoryPackSerializer.Deserialize<T>(bSpan.Slice(2, size));
            tam += 2 + size;
            return t;
        }


        private byte[] ReadBuffer()
        {
            Span<byte> span = buffer.AsSpan(tam);
            ushort size = (ushort)(span[0] | (span[1] << 8));
            if (size == 0)
                return Array.Empty<byte>();
            tam += 2 + size;
            return span.Slice(2, size).ToArray();
        }

        private ReadOnlySpan<byte> ReadSpan()
        {
            Span<byte> span = buffer.AsSpan(tam);
            ushort size = (ushort)(span[0] | (span[1] << 8));
            if (size == 0)
                return Array.Empty<byte>();
            tam += 2 + size;
            return span.Slice(2, size); ;
        }
        /// <summary>
        /// Reads a null-terminated string from the buffer using UTF-8 encoding.
        /// </summary>
        /// <returns>The read string.</returns>
        /// <exception cref="FormatException">Thrown when the null terminator is not found.</exception>
        private string ReadString()
        {
            //  Span<byte> bSpan = buffer.AsSpan(tam);

            // ushort size = (ushort)((bSpan[0] & 0xFF) | ((bSpan[1] & 0xFF) << 8));
            // if (size == 0)
            //     return default;

            // T t = MemoryPackSerializer.Deserialize<T>(bSpan.Slice(2, size));
            // tam += 2 + size;
            // return t;
            Span<byte> bSpan = buffer.AsSpan(tam);

            ushort size = (ushort)((bSpan[0] & 0xFF) | ((bSpan[1] & 0xFF) << 8));
            if (size == 0)
                return null;

            string result = Encoding.UTF8.GetString(bSpan.Slice(2, size));

            tam += 2 + size;

            return result;
        }

        /// <summary>
        /// Resets the buffer position to prepare for reuse.
        /// </summary>
        public void Dispose()
        {
            tam = 3;
            NitroManager.bufferPool.Return(this);
        }
    }
}

