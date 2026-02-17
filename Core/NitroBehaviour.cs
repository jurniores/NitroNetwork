using System;
using System.Collections.Generic;
using NitroNetwork.Core;
using UnityEngine;
namespace NitroNetwork.Core
{
    /// <summary>
    /// Base class for networked behaviours in NitroNetwork.
    /// Provides identity, role flags, and utility methods for sending messages and registering RPCs.
    /// </summary>
    public class NitroBehaviour : MonoBehaviour
    {
        /// <summary>
        /// The type of NitroBehaviour, indicating whether it is a server or client behaviour.
        /// /// </summary>
        public const NitroType Server = NitroType.Server;
        /// <summary>
        /// The type of NitroBehaviour, indicating whether it is a client behaviour.
        /// /// </summary>
        public const NitroType Client = NitroType.Client;
        /// <summary>
        /// The network identity associated with this behaviour.
        /// </summary>
        [HideInInspector]
        public NitroIdentity Identity;
        /// <summary>
        /// Gets the NitroIdentity associated with this behaviour.
        /// /// </summary>
        protected NitroConn Conn => Identity != null ? Identity.Owner : null;
        /// <summary>
        /// Gets the NitroConn used for calling RPCs on this behaviour.
        /// /// </summary>
        protected NitroConn CallConn => Identity != null ? Identity.callConn : null;
        /// <summary>
        /// Indicates if this object is static in the network context.
        /// </summary>
        protected bool IsStatic = false;
        /// <summary>
        /// Indicates if this instance is running on the server.
        /// </summary>
        protected bool IsServer = false;
        /// <summary>
        /// Indicates if this instance is running on the client.
        /// </summary>
        protected bool IsClient = false;
        /// <summary>
        /// Indicates if this object is owned by the local peer.
        /// </summary>
        protected bool IsMine;
        /// <summary>
        /// Internal buffer for network serialization.
        /// </summary>
        protected NitroBuffer __buffer = new();
        /// <summary>
        /// Internal count of registered server RPCs.
        /// </summary>
        protected int __tamRpcS = 0;
        /// <summary>
        /// Internal count of registered client RPCs.
        /// </summary>
        protected int __tamRpcC = 0;

        /// <summary>
        /// Sets the network configuration for this behaviour.
        /// </summary>
        /// <param name="identity">The NitroIdentity to associate.</param>
        /// <param name="isServer">True if running on server.</param>
        /// <param name="isClient">True if running on client.</param>
        /// <param name="isMine">True if owned by local peer.</param>
        internal void SetConfigs(NitroIdentity identity, bool isServer, bool isClient, bool isMine)
        {
            this.Identity = identity;
            IsStatic = identity.IsStatic;
            IsServer = isServer;
            IsClient = isClient;
            IsMine = isMine;
        }
        /// <summary>
        /// Sends a message to the server using the specified delivery mode and channel.
        /// </summary>
        /// <param name="message">The message data to send.</param>
        /// <param name="DeliveryMode">The delivery mode (default: ReliableOrdered).</param>
        /// <param name="channel">The channel to use (default: 0).</param>
        protected void __SendForServer(Span<byte> message, DeliveryMode DeliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
        {
            NitroManager.SendForServer(message, DeliveryMode, channel);
        }
        /// <summary>
        /// Sends a message to clients, with options for Target, delivery mode, and channel.
        /// </summary>
        /// <param name="message">The message data to send.</param>
        /// <param name="Target">The Target clients (default: All).</param>
        /// <param name="DeliveryMode">The delivery mode (default: ReliableOrdered).</param>
        /// <param name="channel">The channel to use (default: 0).</param>
        protected void __SendForClient(Span<byte> message, Target Target = Target.All, DeliveryMode DeliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
        {
            if (Identity.IsStatic)
            {
                if (Identity.callConn == null)
                {
                    Debug.LogError("This is a client RPC, call it inside a method on the server for static identities.");
                    return;
                }
            }
            NitroManager.SendForClient(message, Identity.Owner, room: Identity.room, Target: Target, DeliveryMode: DeliveryMode, channel: channel);
            Identity.callConn = Identity.Owner;
        }
        /// <summary>
        /// Delta function for Vector3. It compares the delta with a reference vector and returns a byte array.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static byte[] Delta(Vector3 delta, ref Vector3 compare)
        {
            using var buffer = NitroManager.Rent();
            byte mask = 0;
            buffer.Write(mask);
            if (compare.x != delta.x)
            {
                mask |= 0b00000001; // Set the first bit to true
                compare.x = delta.x;
                buffer.Write(delta.x);
            }
            if (compare.y != delta.y)
            {
                mask |= 0b00000010; // Set the second bit to true
                compare.y = delta.y;
                buffer.Write(delta.y);
            }
            if (compare.z != delta.z)
            {
                mask |= 0b00000100; // Set the third bit to true
                compare.z = delta.z;
                buffer.Write(delta.z);
            }
            buffer.buffer[4] = mask;
            using var newArray = NitroManager.RentDelta(buffer.tam - 4);
            newArray.WriteForRead(buffer.Buffer[4..]);
            return newArray.buffer;
        }
        /// <summary>
        /// Delta function for Quaternion. It compares the delta with a reference quaternion and returns a byte array.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static byte[] Delta(Quaternion delta, ref Quaternion compare)
        {
            using var buffer = NitroManager.Rent();
            byte mask = 0;
            buffer.Write(mask);
            if (compare.x != delta.x)
            {
                mask |= 0b00000001; // Set the first bit to true
                compare.x = delta.x;
                buffer.Write(delta.x);
            }
            if (compare.y != delta.y)
            {
                mask |= 0b00000010; // Set the second bit to true
                compare.y = delta.y;
                buffer.Write(delta.y);
            }
            if (compare.z != delta.z)
            {
                mask |= 0b00000100; // Set the third bit to true
                compare.z = delta.z;
                buffer.Write(delta.z);
            }
            if (compare.w != delta.w)
            {
                mask |= 0b00001000; // Set the fourth bit to true
                compare.w = delta.w;
                buffer.Write(delta.w);
            }
            buffer.buffer[4] = mask;
            using var newArray = NitroManager.RentDelta(buffer.tam - 4);
            newArray.WriteForRead(buffer.Buffer[4..]);
            return newArray.buffer;
        }
        /// <summary>
        /// Delta function for Vector3. It compares the delta with a reference vector and returns a byte array.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static byte[] Delta(Vector3 delta, Vector3 delta2, ref Vector3 compare, ref Vector3 compare2)
        {
            using var buffer = NitroManager.Rent();
            byte mask = 0;
            buffer.Write(mask);
            if (compare.x != delta.x)
            {
                mask |= 0b00000001; // Set the first bit to true
                compare.x = delta.x;
                buffer.Write(delta.x);
            }
            if (compare.y != delta.y)
            {
                mask |= 0b00000010; // Set the second bit to true
                compare.y = delta.y;
                buffer.Write(delta.y);
            }
            if (compare.z != delta.z)
            {
                mask |= 0b00000100; // Set the third bit to true
                compare.z = delta.z;
                buffer.Write(delta.z);
            }
            if (compare2.x != delta2.x)
            {
                mask |= 0b00001000; // Set the fourth bit to true
                compare2.x = delta2.x;
                buffer.Write(delta2.x);
            }
            if (compare2.y != delta2.y)
            {
                mask |= 0b00010000; // Set the fifth bit to true
                compare2.y = delta2.y;
                buffer.Write(delta2.y);
            }
            if (compare2.z != delta2.z)
            {
                mask |= 0b00100000; // Set the sixth bit to true
                compare2.z = delta2.z;
                buffer.Write(delta2.z);
            }
            buffer.buffer[4] = mask;
            using var newArray = NitroManager.RentDelta(buffer.tam - 4);
            newArray.WriteForRead(buffer.Buffer[4..]);
            return newArray.buffer;
        }
        /// <summary>
        /// Delta function for Quaternion. It compares the delta with a reference quaternion and returns a byte array.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static byte[] Delta(Quaternion delta, Quaternion delta2, ref Quaternion compare, ref Quaternion compare2)
        {
            using var buffer = NitroManager.Rent();
            byte mask = 0;
            buffer.Write(mask);
            if (compare.x != delta.x)
            {
                mask |= 0b00000001; // Set the first bit to true
                compare.x = delta.x;
                buffer.Write(delta.x);
            }
            if (compare.y != delta.y)
            {
                mask |= 0b00000010; // Set the second bit to true
                compare.y = delta.y;
                buffer.Write(delta.y);
            }
            if (compare.z != delta.z)
            {
                mask |= 0b00000100; // Set the third bit to true
                compare.z = delta.z;
                buffer.Write(delta.z);
            }
            if (compare.w != delta.w)
            {
                mask |= 0b00001000; // Set the fourth bit to true
                compare.w = delta.w;
                buffer.Write(delta.w);
            }
            if (compare2.x != delta2.x)
            {
                mask |= 0b00001000; // Set the fourth bit to true
                compare2.x = delta2.x;
                buffer.Write(delta2.x);
            }
            if (compare2.y != delta2.y)
            {
                mask |= 0b00010000; // Set the fifth bit to true
                compare2.y = delta2.y;
                buffer.Write(delta2.y);
            }
            if (compare2.z != delta2.z)
            {
                mask |= 0b00100000; // Set the sixth bit to true
                compare2.z = delta2.z;
                buffer.Write(delta2.z);
            }
            if (compare2.w != delta2.w)
            {
                mask |= 0b01000000; // Set the seventh bit to true
                compare2.w = delta2.w;
                buffer.Write(delta2.w);
            }
            buffer.buffer[4] = mask;
            using var newArray = NitroManager.RentDelta(buffer.tam - 4);
            newArray.WriteForRead(buffer.Buffer[4..]);
            return newArray.buffer;
        }
        /// <summary>
        /// Reads a delta from the buffer and updates the newVector3 reference.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static byte[] Delta(Vector3 delta, Quaternion quaternion, ref Vector3 v3Compare, ref Quaternion qCompare)
        {
            using var buffer = NitroManager.Rent();
            byte mask = 0;
            buffer.Write(mask);
            if (v3Compare.x != delta.x)
            {
                mask |= 0b00000001; // Set the first bit to true
                v3Compare.x = delta.x;
                buffer.Write(delta.x);
            }
            if (v3Compare.y != delta.y)
            {
                mask |= 0b00000010; // Set the second bit to true
                v3Compare.y = delta.y;
                buffer.Write(delta.y);
            }
            if (v3Compare.z != delta.z)
            {
                mask |= 0b00000100; // Set the third bit to true
                v3Compare.z = delta.z;
                buffer.Write(delta.z);
            }
            if (qCompare.x != quaternion.x)
            {
                mask |= 0b00001000; // Set the fourth bit to true
                qCompare.x = quaternion.x;
                buffer.Write(quaternion.x);
            }
            if (qCompare.y != quaternion.y)
            {
                mask |= 0b00010000; // Set the fifth bit to true
                qCompare.y = quaternion.y;
                buffer.Write(quaternion.y);
            }
            if (qCompare.z != quaternion.z)
            {
                mask |= 0b00100000; // Set the sixth bit to true
                qCompare.z = quaternion.z;
                buffer.Write(quaternion.z);
            }
            if (qCompare.w != quaternion.w)
            {
                mask |= 0b01000000; // Set the seventh bit to true
                qCompare.w = quaternion.w;
                buffer.Write(quaternion.w);
            }
            buffer.buffer[4] = mask;
            using var newArray = NitroManager.RentDelta(buffer.tam - 4);
            newArray.WriteForRead(buffer.Buffer[4..]);
            return newArray.buffer;
        }

        /// <summary>
        /// Reads a delta from the buffer and updates the newVector3 reference.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static void ReadDelta(byte[] buffer, ref Vector3 newV)
        {
            // Read the mask from the buffer
            using var bufferRead = NitroManager.Rent();
            bufferRead.WriteForRead(buffer, 4);
            bufferRead.Length = buffer.Length + 4;

            byte mask = bufferRead.Read<byte>();

            if ((mask & 0b00000001) != 0)
            {
                newV.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00000010) != 0)
            {
                newV.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00000100) != 0)
            {
                newV.z = bufferRead.Read<float>();
            }
        }
        public static void ReadDelta(byte[] buffer, ref Quaternion newQ)
        {
            // Read the mask from the buffer
            using var bufferRead = NitroManager.Rent();
            bufferRead.WriteForRead(buffer, 4);
            bufferRead.Length = buffer.Length + 4;

            byte mask = bufferRead.Read<byte>();

            if ((mask & 0b00000001) != 0)
            {
                newQ.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00000010) != 0)
            {
                newQ.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00000100) != 0)
            {
                newQ.z = bufferRead.Read<float>();
            }
            if ((mask & 0b00001000) != 0)
            {
                newQ.w = bufferRead.Read<float>();
            }
        }
        /// <summary>
        /// Reads a delta from the buffer and updates the newVector3 and _newV3 references.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static void ReadDelta(byte[] buffer, ref Vector3 newV, ref Vector3 _newV)
        {
            // Read the mask from the buffer
            using var bufferRead = NitroManager.Rent();
            bufferRead.WriteForRead(buffer, 4);
            bufferRead.Length = buffer.Length + 4;
            byte mask = bufferRead.Read<byte>();

            if ((mask & 0b00000001) != 0)
            {
                newV.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00000010) != 0)
            {
                newV.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00000100) != 0)
            {
                newV.z = bufferRead.Read<float>();
            }
            if ((mask & 0b00001000) != 0)
            {
                _newV.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00010000) != 0)
            {
                _newV.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00100000) != 0)
            {
                _newV.z = bufferRead.Read<float>();
            }
        }
        /// <summary>
        /// Reads a delta from the buffer and updates the newVector3 and _newV3 references.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static void ReadDelta(byte[] buffer, ref Quaternion newQ, ref Quaternion _newQ)
        {
            // Read the mask from the buffer
            using var bufferRead = NitroManager.Rent();
            bufferRead.WriteForRead(buffer, 4);
            bufferRead.Length = buffer.Length + 4;
            byte mask = bufferRead.Read<byte>();

            if ((mask & 0b00000001) != 0)
            {
                newQ.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00000010) != 0)
            {
                newQ.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00000100) != 0)
            {
                newQ.z = bufferRead.Read<float>();
            }
            if ((mask & 0b00001000) != 0)
            {
                newQ.w = bufferRead.Read<float>();
            }

            if ((mask & 0b00000001) != 0)
            {
                _newQ.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00000010) != 0)
            {
                _newQ.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00000100) != 0)
            {
                _newQ.z = bufferRead.Read<float>();
            }
            if ((mask & 0b00001000) != 0)
            {
                _newQ.w = bufferRead.Read<float>();
            }
        }
        /// <summary>
        /// Reads a delta from the buffer and updates the newVector3 and newQuaternion references.
        /// The first byte is a mask indicating which components have changed.
        /// </summary>
        public static void ReadDelta(byte[] buffer, ref Vector3 newVector3, ref Quaternion newQ)
        {
            // Read the mask from the buffer
            using var bufferRead = NitroManager.Rent();
            bufferRead.WriteForRead(buffer, 4);
            bufferRead.Length = buffer.Length + 4;
            byte mask = bufferRead.Read<byte>();

            if ((mask & 0b00000001) != 0)
            {
                newVector3.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00000010) != 0)
            {
                newVector3.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00000100) != 0)
            {
                newVector3.z = bufferRead.Read<float>();
            }

            if ((mask & 0b00001000) != 0)
            {
                newQ.x = bufferRead.Read<float>();
            }
            if ((mask & 0b00010000) != 0)
            {
                newQ.y = bufferRead.Read<float>();
            }
            if ((mask & 0b00100000) != 0)
            {
                newQ.z = bufferRead.Read<float>();
            }
            if ((mask & 0b01000000) != 0)
            {
                newQ.w = bufferRead.Read<float>();
            }
        }
        /// <summary>
        /// Called when the object is instantiated over the network. Can be overridden.
        /// </summary>
        protected internal virtual void OnInstantiated() { }
        /// <summary>
        /// Registers this object's server RPCs. Can be overridden.
        /// </summary>
        /// <param name="RpcServer">Dictionary of server RPCs.</param>
        protected internal virtual void __RegisterMyRpcServer(Dictionary<int, Action<NitroBuffer>> RpcServer)
        {
            __tamRpcS = RpcServer.Count;
        }
        /// <summary>
        /// Registers this object's client RPCs. Can be overridden.
        /// </summary>
        /// <param name="RpcClient">Dictionary of client RPCs.</param>
        protected internal virtual void __RegisterMyRpcClient(Dictionary<int, Action<NitroBuffer>> RpcClient)
        {
            __tamRpcC = RpcClient.Count;
        }
        /// <summary>
        /// Logs a debug message with the NitroBehaviour prefix.
        /// </summary>
        /// <param name="message">The message to log.</param>
        protected bool __ReturnValidateServerCall(string nameMethod)
        {
            if (IsClient && !IsServer) { Debug.LogError($"RPC {nameMethod} cannot be called on the client."); return false; }
            if (Identity == null) { Debug.LogError($"RPC {nameMethod}: Identity does not exist, insert the NetworkIdentity, or check if the identity exists"); return false; }
            return true;
        }
        /// <summary>
        /// Validates the client call. Throws an exception if the call is invalid.
        /// </summary>
        protected bool __ReturnValidateClientCall(string nameMethod)
        {
            if (IsServer && !IsClient) {Debug.LogError($"RPC {nameMethod} cannot be called on the server."); return false; }
            if (Identity == null) { Debug.LogError($"RPC {nameMethod}: Identity does not exist, insert the NetworkIdentity, or check if the identity exists"); return false; }
            if (!IsClient) { Debug.LogError($"RPC {nameMethod}You cannot send a message before the client connects. Use NitroManager.OnClientConnected"); return false;
            }
            return true;
        }
        /// <summary>
        /// Validates the server received call. Throws an exception if the call is invalid.
        /// </summary>
        protected bool __ReturnValidateClientReceived(string nameMethod)
        {
            // No Error in moment
            return true;
        }
        /// <summary>
        /// Validates the server received call. Throws an exception if the call is invalid.
        /// </summary>
        protected bool __ReturnValidateServerReceived(bool requiresOwner, string nameMethod)
        {
            if (requiresOwner && !Identity.IsStatic && Identity.Owner.Id != Identity.callConn.Id)
            {
                NitroManager.DisconnectConn(Identity.callConn);
                Debug.LogError($"Access denied RPC {nameMethod}: requiresOwner is true, and the connection does not match the object's spawn connection. Identity: " + Identity.Id+ " Conn " + Identity.Owner.Id);
                return false;
            }
            return true;
        }

    }
}