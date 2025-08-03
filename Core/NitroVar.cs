using System;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.Events;

namespace NitroNetwork.Core
{

    public class NitroVar<T> : INitroVar
    {

        /// <summary>
        /// Represents a variable in the NitroNetwork framework, allowing for dynamic data storage
        /// </summary>
        public byte Id;

        /// <summary>
        /// The variable's value, which can be of any type.
        /// </summary>
        private T value;
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
        public NitroIdentity Identity;

        /// <summary>
        /// The type of the variable, which can be specified during initialization.
        /// /// </summary>
        private NitroBuffer buffer = new();

        /// <summary>
        /// Client authority indicates whether the client has authority over this NitroVar.
        /// </summary>
        public bool ClientAuthority = false;

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

        public UnityAction<T> OnChange = null;

        public NitroVar(bool clientAuthority = false, bool requiresOwner = true, Target target = Target.All, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0, bool encrypt = false, UnityAction<T> OnChange = null)
        {

            ClientAuthority = clientAuthority;
            RequiresOwner = requiresOwner;
            Target = target;
            DeliveryMode = deliveryMode;
            Channel = channel;
            Encrypt = encrypt;
            this.OnChange = OnChange;
        }


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
                NitroManager.SendForServer(__buffer.Buffer, DeliveryMode.ReliableOrdered, Channel);
                __buffer.Dispose();
            }
        }
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
                    if (Target == Target.Self)
                    {
                        __buffer.EncriptAes(Identity.callConn.keyAes);
                    }
                    else
                    {
                        __buffer.EncriptAes(NitroManager.ServerConn.keyAes);
                    }
                }
                NitroManager.SendForClient(__buffer.Buffer, Identity.callConn, room: Identity.room, Target: Target.All, DeliveryMode: DeliveryMode.ReliableOrdered, channel: Channel);
                Identity.callConn = Identity.Owner;
                __buffer.Dispose();

            }
        }

        void INitroVar.SetConfig(byte Id, NitroIdentity identity)
        {
            this.Id = Id;
            this.Identity = identity;
        }

        void INitroVar.ReadVar(NitroBuffer _buffer)
        {
            if (!__ReturnValidateServerReceived(ClientAuthority, RequiresOwner, "NitroVar", Identity)) return;

            if (_buffer.Length > buffer.buffer.Length)
            {
                buffer = new NitroBuffer(_buffer.Length, 0);

            }
            buffer.WriteForRead(_buffer.buffer.AsSpan(0, _buffer.Length));
            buffer.Length = _buffer.Length;
            buffer.tam = _buffer.tam;

            if (Encrypt)
            {
                buffer.DecryptAes(Identity.callConn.keyAes, 5);
            }
            value = buffer.Read<T>();

            OnChange?.Invoke(value);
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
        internal static bool __ReturnValidateClientReceived(string nameMethod)
        {
            // No Error in moment
            return true;
        }

        internal static bool __ReturnValidateServerReceived(bool ClientAuthority, bool requiresOwner, string nameMethod, NitroIdentity Identity)
        {
            if (!ClientAuthority && Identity.IsServer && !Identity.IsClient)
            {
                NitroManager.DisconnectConn(Identity.callConn);
                Debug.LogError($"NitroVar cannot be called on the server without ClientAuthority. Identity: " + Identity.Id + " Conn " + Identity.Owner.Id);
                return false;
            }
            if (requiresOwner && Identity.IsServer && !Identity.IsStatic && Identity.Owner.Id != Identity.callConn.Id)
            {
                NitroManager.DisconnectConn(Identity.callConn);
                Debug.LogError($"Access denied RPC {nameMethod}: requiresOwner is true, and the connection does not match the object's spawn connection. Identity: " + Identity.Id+ " Conn " + Identity.Owner.Id);
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
                if (Target == Target.Self)
                {
                    __buffer.EncriptAes(Identity.callConn.keyAes);
                }
                else
                {
                    __buffer.EncriptAes(NitroManager.ServerConn.keyAes);
                }
            }
            NitroManager.SendForClient(__buffer.Buffer, conn, Target: target, channel: 2);
            __buffer.Dispose();
        }
    }


}