using System;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.Events;

namespace NitroNetwork.Core
{
    [Serializable]
    public class NitroVar<T> : INitroVar
    {

        /// <summary>
        /// Represents a variable in the NitroNetwork framework, allowing for dynamic data storage
        /// </summary>
        internal byte Id;

        /// <summary>
        /// The variable's value, which can be of any type.
        /// </summary>
        public T value;
        public T Value
        {
            get => value;
            set
            {
                Set(value);
            }
        }

        /// <summary>
        /// The NitroIdentity associated with this variable, used for network communication.
        /// </summary>
        internal NitroIdentity Identity;

        /// <summary>
        /// Client authority indicates whether the client has authority over this NitroVar.
        /// </summary>
        public bool ClientAuthority = false;
        /// <summary>
        /// Indicates that the NetVar should reply to all clients in the room.
        /// </summary>
        public bool Reply = false;

        /// <summary>
        /// Indicates whether the RPC requires the caller to be the owner of the object.
        /// Default is <c>true</c>.
        /// </summary>
        public bool RequiresOwner = true;

        /// <summary>
        /// Indicates whether the RPC should be encrypted.
        /// Default is <c>false</c>.
        /// </summary>
        public bool Encrypt = false;

        /// <summary>
        /// Specifies the Target audience for the RPC, for Client RPC.
        /// Default is <see cref="Target.All"/>.
        /// </summary>
        public Target Target = Target.All;

        /// <summary>
        /// Defines the delivery mode for the RPC, such as reliable or unreliable.
        /// Default is <see cref="DeliveryMode.ReliableOrdered"/>.
        /// </summary>
        public DeliveryMode DeliveryMode = DeliveryMode.ReliableOrdered;

        /// <summary>
        /// Specifies the communication channel for the RPC.
        /// Default is <c>0</c>.
        /// </summary>
        public byte Channel = 0;
        /// <summary>
        /// An action that is invoked when the value of the NitroVar changes.
        /// </summary>
        public UnityAction<T, T, NitroConn> OnChange = null;
        /// <summary>
        /// Initializes a new instance of the NitroVar class with the specified parameters.
        /// </summary>
        public NitroVar(T initialValue = default, bool clientAuthority = false, bool requiresOwner = true, bool reply = false, Target target = Target.All, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0, bool encrypt = false, UnityAction<T, T, NitroConn> onChange = null)
        {
            value = initialValue;
            ClientAuthority = clientAuthority;
            Reply = reply;
            RequiresOwner = requiresOwner;
            Target = target;
            DeliveryMode = deliveryMode;
            Channel = channel;
            Encrypt = encrypt;
            OnChange = onChange;
        }
        /// <summary>
        /// Sets the value of the NitroVar and sends it to the server or client based on
        /// the current connection context.
        /// </summary>
        private void Set(T value)
        {
            if (Identity.IsStatic)
            {
                Debug.LogError("For static identities, use the Send method");
                return;
            }
            SendClient(value);
            SendServer(value);
        }
        /// <summary>
        /// Sends the value of the NitroVar to the server or client based on the specified parameters for Static Identities.
        /// </summary>
        public void Send(bool IsServer, T Value)
        {
            if (IsServer)
            {
                if (!__ReturnValidateServerCall(Identity.name, Identity)) return;
                SendServer(Value);
            }
            else
            {
                if (!__ReturnValidateClientCall(Identity.name, Identity)) return;
                SendClient(Value);
            }
        }
        /// <summary>
        /// Sends the value of the NitroVar to the client or server based on the current connection context.
        /// </summary>
        private void SendClient(T value)
        {
            if (Identity.IsClient)
            {
                var __buffer = NitroManager.Rent();
                __buffer.SetInfo((byte)NitroCommands.NetVar, Identity.Id);
                __buffer.Write(Id);
                __buffer.Write(value);
                if (Encrypt)
                {
                    __buffer.EncriptAes(NitroManager.ClientConn.keyAes, 5);
                }
                NitroManager.SendForServer(__buffer.Buffer, DeliveryMode, Channel);
                __buffer.Dispose();
            }
        }
        /// <summary>
        /// Sends the value of the NitroVar to the server based on the current connection context.
        /// </summary>
        private void SendServer(T value)
        {
            if (Identity.IsServer)
            {
                var __buffer = NitroManager.Rent();
                __buffer.SetInfo((byte)NitroCommands.NetVar, Identity.Id);
                __buffer.Write(Id);
                __buffer.Write(value);
                if (Encrypt)
                {
                    __buffer.EncriptAes(NitroManager.ServerConn.keyAes, 5);
                }
                NitroManager.SendForClient(__buffer.Buffer, Identity.callConn, room: Identity.room, Target: Target, DeliveryMode: DeliveryMode, channel: Channel);
                Identity.callConn = Identity.Owner;
                __buffer.Dispose();
            }
        }
        /// <summary>
        /// Sets the configuration for the NitroVar, including its ID and associated NitroIdentity.
        /// </summary>
        void INitroVar.SetConfig(byte Id, NitroIdentity identity)
        {
            this.Id = Id;
            this.Identity = identity;
        }
        /// <summary>
        /// Reads the value of the NitroVar from the provided NitroBuffer.
        /// </summary>
        void INitroVar.ReadVar(NitroBuffer _buffer)
        {
            if (!__ReturnValidateServerReceived(ClientAuthority, RequiresOwner, "NitroVar", Identity)) return;

            if (Encrypt)
            {
                _buffer.DecryptAes(Identity.callConn.keyAes, 5);
            }
            var newValue = _buffer.Read<T>();
            OnChange?.Invoke(value, newValue, Identity.callConn);
            value = newValue;

            if (ClientAuthority && Reply && Identity.callConn != null && Identity.callConn.Id != -1)
            {
                if (Encrypt)
                {
                    using var buffer = NitroManager.Rent();
                    buffer.SetInfo((byte)NitroCommands.NetVar, Identity.Id);
                    buffer.Write(Id);
                    buffer.Write(value);
                    buffer.EncriptAes(NitroManager.ServerConn.keyAes, 5);
                    NitroManager.SendForClient(buffer.Buffer, Identity.callConn, room: Identity.room, Target: Target.ExceptSelf, DeliveryMode: DeliveryMode, channel: Channel);
                    buffer.Dispose();
                }
                else
                {
                    NitroManager.SendForClient(_buffer.buffer.AsSpan(0, _buffer.Length), Identity.callConn, room: Identity.room, Target: Target.ExceptSelf, DeliveryMode: DeliveryMode, channel: Channel);
                }

            }


        }

        /// <summary>
        /// Logs a debug message with the NitroBehaviour prefix.
        /// </summary>
        /// <param name="message">The message to log.</param>
        internal static bool __ReturnValidateServerCall(string nameMethod, NitroIdentity identity)
        {
            if (!identity.IsServer) { throw new($"NitroVar {nameMethod} cannot be called on the client."); }
            return true;
        }
        /// <summary>
        /// Validates the client call. Throws an exception if the call is invalid.
        /// </summary>
        internal static bool __ReturnValidateClientCall(string nameMethod, NitroIdentity identity)
        {
            if (!identity.IsClient) throw new($"NitroVar {nameMethod} cannot be called on the server.");
            return true;
        }
        /// <summary>
        /// Validates the client received call for NitroVar.
        /// </summary>
        internal static bool __ReturnValidateClientReceived(string nameMethod)
        {
            // No Error in moment
            return true;
        }
        /// <summary>
        /// Validates the server received call for NitroVar.
        /// </summary>
        /// <param name="ClientAuthority"></param>
        /// <param name="requiresOwner"></param>
        /// <param name="nameMethod"></param>
        /// <param name="Identity"></param>
        /// <returns></returns>
        internal static bool __ReturnValidateServerReceived(bool ClientAuthority, bool requiresOwner, string nameMethod, NitroIdentity Identity)
        {
            if (!ClientAuthority && Identity.callConn.Id != -1)
            {
                NitroManager.DisconnectConn(Identity.callConn);
                Debug.LogError($"NitroVar cannot be called on the server without ClientAuthority. Identity: " + Identity.Id + " Conn " + Identity.Owner?.Id);
                return false;
            }
            if (requiresOwner && Identity.IsServer && !Identity.IsStatic && Identity.Owner.Id != Identity.callConn.Id)
            {
                NitroManager.DisconnectConn(Identity.callConn);
                Debug.LogError($"Access denied RPC {nameMethod}: requiresOwner is true, and the connection does not match the object's spawn connection. Identity: " + Identity.Id + " Conn " + Identity.Owner.Id);
                return false;
            }

            return true;
        }


        void INitroVar.Send(Target target, NitroConn conn)
        {
            var __buffer = NitroManager.Rent();
            __buffer.SetInfo((byte)NitroCommands.NetVar, Identity.Id);
            __buffer.Write(Id);
            __buffer.Write(value);
            if (Encrypt)
            {
                __buffer.EncriptAes(NitroManager.ServerConn.keyAes, 5);
            }
            NitroManager.SendForClient(__buffer.Buffer, conn, Target: target, channel: 2);
            __buffer.Dispose();
        }
    }


}