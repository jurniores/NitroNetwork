using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NitroNetwork.Core;
using NUnit.Framework;
using UnityEngine;
namespace NitroNetwork.Core
{
    [DefaultExecutionOrder(-99)] // Sets the execution order of this script in Unity
    public class NitroIdentity : MonoBehaviour
    {
        // Indicates whether the identity belongs to the server, client, or the local player
        public bool IsServer = false, IsClient = false, IsMine = false;

        [HideInInspector]
        public bool IsStatic = false; // Indicates if the identity is static (does not change during execution)

        private string namePrefab; // Name of the prefab associated with this identity
        public ushort Id; // Unique identifier for this identity
        [SerializeField]
        private bool SpawnInParent = true; // Indicates if the object should spawn as a child of another

        // Connections associated with this identity
        public NitroConn conn, callConn;

        public NitroRoom room; // Room associated with this identity

        [SerializeField]
        private List<string> listClien = new(), listServer = new(); // Lists for storing RPC information

        // Dictionaries for storing server and client RPCs
        internal Dictionary<int, Action<NitroBuffer>> RpcServer = new(), RpcClient = new();

        /// <summary>
        /// Called when the object is initialized.
        /// Registers RPCs and configures the identity.
        /// </summary>
        void Awake()
        {
            // Registers RPCs for child behaviors
            foreach (var nb in GetComponentsInChildren<NitroBehaviour>(true))
            {
                if (IsServer || IsStatic) nb.__RegisterMyRpcServer(RpcServer);
                if (IsClient || IsStatic) nb.__RegisterMyRpcClient(RpcClient);
            }

            // Adds registered RPCs to lists for debugging
            foreach (var rpc in RpcServer)
            {
                listServer.Add($"Key {rpc.Key} value {rpc.Value.Method.Name}");
            }
            foreach (var rpc in RpcClient)
            {
                listClien.Add($"Key {rpc.Key} value {rpc.Value.Method.Name}");
            }

            // If the identity is static, register it with the NitroManager
            if (IsStatic)
            {
                NitroManager.RegisterIdentity(this, IsStatic: IsStatic);
                SetConfig();
            }
        }

        /// <summary>
        /// Configures the identity based on its current state.
        /// </summary>
        internal void SetConfig()
        {
            if (IsStatic) NitroManager.IsServerAndClient(ref IsServer, ref IsClient, this);

            // Configures child behaviors with the identity's states
            foreach (var nb in GetComponentsInChildren<NitroBehaviour>(true))
            {
                nb.SetConfigs(this, IsServer, IsClient, IsMine);
            }
        }

        /// <summary>
        /// Configures the identity with specific information.
        /// </summary>
        /// <param name="conn">The connection associated with this identity.</param>
        /// <param name="id">The unique identifier for this identity.</param>
        /// <param name="isServer">Indicates if this identity belongs to the server.</param>
        /// <param name="isClient">Indicates if this identity belongs to the client.</param>
        /// <param name="isMine">Indicates if this identity belongs to the local player.</param>
        internal void SetConfig(NitroConn conn, ushort id, bool isServer, bool isClient, bool isMine)
        {
            Id = id;
            this.conn = conn;
            IsServer = isServer;
            IsClient = isClient;
            IsMine = isMine;
            SetConfig();
        }

        /// <summary>
        /// Spawns a new identity on the server and registers it.
        /// </summary>
        /// <param name="conn">The connection associated with the new identity.</param>
        /// <param name="room">The room where the identity will be spawned.</param>
        /// <param name="target">The target audience for the spawn.</param>
        /// <returns>The newly spawned NitroIdentity.</returns>
        public NitroIdentity Spawn(NitroConn conn, NitroRoom room = null)
        {
            var newRoom = room ?? NitroManager.GetFirstRoom(); // Gets the room if not specified

            // Checks if the connection is in the room
            if (!conn.rooms.ContainsKey(newRoom.Name))
            {
                Debug.LogError($"Conn {conn.Id} is not in the room {room.Name}");
                return null;
            }

            gameObject.SetActive(false); // Deactivates the current object
            var newIdentity = Instantiate(this); // Instantiates a new identity
            NitroManager.RegisterIdentity(newIdentity); // Registers the new identity with the NitroManager
            using var buffer = NitroManager.Rent();
            // Configures the buffer for the spawn RPC
            buffer.SetInfo((byte)NitroCommands.SpawnRPC, (ushort)NitroCommands.SpawnIdentity);
            Id = newIdentity.Id;
            this.conn = conn;
            newIdentity.conn = conn;
            newIdentity.IsServer = true;
            newIdentity.namePrefab = name;
            namePrefab = name;
            newIdentity.name = $"{name}(Server)";

            newIdentity.SetConfig();
            newRoom.SetIdentity(newIdentity);
            SendSpawnForClient(conn, Target.All, room: newRoom);
            newIdentity.gameObject.SetActive(true);
            return newIdentity;
        }

        /// <summary>
        /// Sends a spawn RPC to the client.
        /// </summary>
        /// <param name="conn">The connection to send the spawn RPC to.</param>
        /// <param name="target">The target audience for the spawn RPC.</param>
        /// <param name="room">The room associated with the spawn RPC.</param>
        /// <returns>The current NitroIdentity.</returns>
        internal NitroIdentity SendSpawnForClient(NitroConn conn = null, Target target = Target.All, NitroRoom room = null)
        {
            using var buffer = NitroManager.Rent();
            buffer.SetInfo((byte)NitroCommands.SpawnRPC, (ushort)NitroCommands.SpawnIdentity);
            buffer.Write(this.Id);
            buffer.Write(this.conn.Id);
            buffer.Write(SpawnInParent);
            buffer.Write(namePrefab);
            buffer.Write(transform.position);
            buffer.Write(transform.rotation.eulerAngles);
            buffer.Write(transform.parent != null ? transform.parent.name : "");
            NitroManager.SendForClient(buffer.Buffer.ToArray(), conn, room, target);
            buffer.Dispose();
            return this;
        }

        /// <summary>
        /// Sends a destroy RPC to the client.
        /// </summary>
        /// <param name="conn">The connection to send the destroy RPC to.</param>
        /// <param name="target">The target audience for the destroy RPC.</param>
        /// <param name="room">The room associated with the destroy RPC.</param>
        internal void SendDestroyForClient(NitroConn conn = null, Target target = Target.All, NitroRoom room = null)
        {
            var buffer = new NitroBuffer();
            buffer.SetInfo((byte)NitroCommands.DespawnIdentity, (ushort)NitroCommands.SpawnIdentity);
            buffer.Write(Id);
            room.peersRoom.TryGetValue(conn.Id, out var peerRoom);
            NitroManager.SendForClient(buffer.Buffer.ToArray(), conn, target: target, room: room);
        }

        /// <summary>
        /// Destroys the current identity and sends a destroy RPC to the client.
        /// </summary>
        public void Destroy()
        {
            SendDestroyForClient(conn, room: room);
            Destroy(gameObject);
        }

        /// <summary>
        /// Called when the object is destroyed.
        /// Unregisters the identity and clears RPCs.
        /// </summary>
        private void OnDestroy()
        {
            if (IsClient)
            {
                NitroManager.UnRegisterIdentity(this, false);
                RpcClient.Clear();
            }
            else
            {
                NitroManager.UnRegisterIdentity(this);
                room?.identities.Remove(Id);
                conn?.identitiesOnDestroy.Remove(Id);
                RpcServer.Clear();
            }
        }

    }
}