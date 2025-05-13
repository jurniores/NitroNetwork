using System;
using System.Collections.Generic;
using NitroNetwork.Core;
using UnityEngine;
namespace NitroNetwork.Core
{
    public class NitroBehaviour : MonoBehaviour
    {
        protected NitroIdentity Identity;
        protected bool IsStatic = false;
        protected bool IsServer = false, IsClient = false, IsMine;
        protected NitroBuffer __buffer = new();
        protected int __tamRpcS = 0, __tamRpcC = 0;

        internal void SetConfigs(NitroIdentity identity, bool isServer, bool isClient, bool isMine)
        {
            this.Identity = identity;
            IsStatic = identity.IsStatic;
            IsServer = isServer;
            IsClient = isClient;
            IsMine = isMine;
        }
        protected void __SendForServer(Span<byte> message, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
        {
            NitroManager.SendForServer(message, deliveryMode, channel);
        }
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
            if(Identity.callConn == null) Identity.callConn = Identity.conn;
            NitroManager.SendForClient(message, Identity.callConn, room: Identity.room, target: target, deliveryMode: deliveryMode, channel: channel);
            Identity.callConn = null;
        }
        protected internal virtual void OnInstantiated(){}
        protected internal virtual void __RegisterMyRpcServer(Dictionary<int, Action<NitroBuffer>> RpcServer)
        {
            __tamRpcS = RpcServer.Count;
        }
        protected internal virtual void __RegisterMyRpcClient(Dictionary<int, Action<NitroBuffer>> RpcClient)
        {
            __tamRpcC = RpcClient.Count;
        }
    }
}