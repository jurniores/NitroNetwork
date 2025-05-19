using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
        public byte[] buffer;
        public int ID;
        /// <summary>
        /// Gets a span representing the valid data in the buffer.
        /// </summary>
        public Span<byte> Buffer => buffer.AsSpan(0, tam);

        /// <summary>
        /// Current position in the buffer. Starts at 3 to reserve space for
        /// command ID and identity ID in the first bytes.
        /// </summary>
        public int tam = 5, Length;
        public NitroBuffer(int capacity, int id)
        {
            ID = id;
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
        private void WriteBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                buffer[tam++] = 0;
                buffer[tam++] = 0;
                return;
            }

            ushort byteCount = (ushort)bytes.Length;
            buffer[tam++] = (byte)(byteCount & 0xFF);
            buffer[tam++] = (byte)((byteCount >> 8) & 0xFF);

            Span<byte> targetSpan = buffer.AsSpan(tam, byteCount);
            bytes.CopyTo(targetSpan);

            tam += byteCount;
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
            if (typeof(P) == typeof(byte[]))
            {
                WriteBytes(p1 as byte[]);
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
        public void WriteForRead(ReadOnlySpan<byte> span)
        {
            span.CopyTo(buffer.AsSpan());
        }
        /// <summary>
        /// Sets the command ID and identity ID in the buffer header.
        /// </summary>
        /// <param name="id">The command ID.</param>
        /// <param name="identityId">The identity ID.</param>
        public void SetInfo(byte id, int identityId)
        {
            buffer[0] = id;
            buffer[1] = (byte)(identityId & 0xFF);
            buffer[2] = (byte)((identityId >> 8) & 0xFF);
            buffer[3] = (byte)((identityId >> 16) & 0xFF);
            buffer[4] = (byte)((identityId >> 24) & 0xFF);
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
            if (typeof(T) == typeof(byte[]))
            {
                object bytes = ReadBytes();
                return (T)bytes;
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
                NitroLogs.LogError(errorMsg);
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
                NitroLogs.LogError(errorMsg);


                NitroLogs.LogError("If you are sending a struct, only send with primitive types, or use classes with MemoryPack");
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
            NitroLogs.Log($"Reading class {typeof(T).Name} from buffer at position {tam}");
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
            Span<byte> bSpan = buffer.AsSpan(tam);

            ushort size = (ushort)((bSpan[0] & 0xFF) | ((bSpan[1] & 0xFF) << 8));
            if (size == 0)
                return null;

            string result = Encoding.UTF8.GetString(bSpan.Slice(2, size));

            tam += 2 + size;

            return result;
        }
        private byte[] ReadBytes()
        {
            Span<byte> bSpan = buffer.AsSpan(tam);

            ushort size = (ushort)((bSpan[0] & 0xFF) | ((bSpan[1] & 0xFF) << 8));
            if (size == 0)
                return null;

            byte[] result = bSpan.Slice(2, size).ToArray();

            tam += 2 + size;

            return result;
        }
        /// <summary>
        /// Resets the buffer position to prepare for reuse.
        /// </summary>
        public void Dispose()
        {
            tam = 5;
            Length = 0;
            NitroManager.bufferPool.Return(this);
        }
        internal void EncriptRSA(string publicKey)
        {
            var bufferCripto = NitroCriptografyRSA.Encrypt(publicKey, Buffer.ToArray());
            bufferCripto.CopyTo(buffer.AsSpan(5, bufferCripto.Length));
            tam = 5 + bufferCripto.Length;
        }
        internal void DecryptRSA(string privateKey)
        {
            ReadOnlySpan<byte> bytes;
            bytes = NitroCriptografyRSA.Decrypt(privateKey, buffer.AsSpan(5, tam > 5 ? tam - 5 : Length - 5).ToArray());
            tam = 5;
            WriteForRead(bytes);
        }
        public AesResult EncriptAes(byte[] key)
        {
            var result = NitroCriptografyAES.Encrypt(buffer.AsSpan(5, tam - 5).ToArray(), key);
            result.IV.CopyTo(buffer.AsSpan(5, result.IV.Length));
            tam = 5 + result.IV.Length;
            UnityEngine.Debug.Log("ID " + ID + " Tam " + tam + " Length " + Length + " buffer" + BitConverter.ToString(buffer) + " Encripted" + BitConverter.ToString(result.EncryptedData) + " IV" + BitConverter.ToString(result.IV));

            result.EncryptedData.CopyTo(buffer.AsSpan(tam, result.EncryptedData.Length));
            tam += result.EncryptedData.Length;
            return result;
        }
        public void DecryptAes(byte[] key)
        {
            int sizeIV = 16;
            int sizeEncriptadData = Length - tam - sizeIV;
            byte[] IV = buffer.AsSpan(tam, sizeIV).ToArray();
            byte[] encryptedData = buffer.AsSpan(tam + sizeIV, sizeEncriptadData).ToArray();
            var aesResult = new AesResult
            {
                IV = IV,
                EncryptedData = encryptedData
            };

            var bufferDecript = NitroCriptografyAES.Decrypt(aesResult.EncryptedData, key, aesResult.IV);
            bufferDecript.CopyTo(buffer.AsSpan(5, bufferDecript.Length));

        }

    }
}

