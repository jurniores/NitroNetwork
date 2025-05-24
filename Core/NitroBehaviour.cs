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
        /// The network identity associated with this behaviour.
        /// </summary>
        protected NitroIdentity Identity;
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
        /// <param name="deliveryMode">The delivery mode (default: ReliableOrdered).</param>
        /// <param name="channel">The channel to use (default: 0).</param>
        protected void __SendForServer(Span<byte> message, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
        {
            NitroManager.SendForServer(message, deliveryMode, channel);
        }
        /// <summary>
        /// Sends a message to clients, with options for target, delivery mode, and channel.
        /// </summary>
        /// <param name="message">The message data to send.</param>
        /// <param name="target">The target clients (default: All).</param>
        /// <param name="deliveryMode">The delivery mode (default: ReliableOrdered).</param>
        /// <param name="channel">The channel to use (default: 0).</param>
        protected void __SendForClient(Span<byte> message, Target target = Target.All, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
        {
            if (Identity.IsStatic)
            {
                if (Identity.callConn == null)
                {
                    Debug.LogError("This is a client RPC, call it inside a method on the server for static identities.");
                    return;
                }
            }

            NitroManager.SendForClient(message, Identity.callConn, room: Identity.room, target: target, deliveryMode: deliveryMode, channel: channel);
            Identity.callConn = Identity.conn;
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
            if (IsClient && !IsStatic) { throw new($"RPC {nameMethod} cannot be called on the client."); }
            if (Identity == null) throw new($"RPC {nameMethod}: Identity does not exist, insert the NetworkIdentity, or check if the identity exists");
            return true;
        }
        /// <summary>
        /// Validates the client call. Throws an exception if the call is invalid.
        /// </summary>
        protected bool __ReturnValidateClientCall(string nameMethod)
        {
            if (IsServer && !IsStatic) throw new($"RPC {nameMethod} cannot be called on the server.");
            if (Identity == null) throw new($"RPC {nameMethod}: Identity does not exist, insert the NetworkIdentity, or check if the identity exists");
            if (!IsClient) { throw new($"RPC {nameMethod}You cannot send a message before the client connects. Use NitroManager.OnClientConnected"); }
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
            if (requiresOwner && !Identity.IsStatic && Identity.conn.Id != Identity.callConn.Id)
            {
                throw new($"Access denied RPC {nameMethod}: requiresOwner is true, and the connection does not match the object's spawn connection. Identity: " + Identity.Id + " Rpc: {method.Identifier.Text} Class: {classe.Identifier.Text}");
            }
            return true;
        }

    }
}