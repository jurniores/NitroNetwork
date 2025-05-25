using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Diagnostics;

namespace NitroNetwork.Core
{
    /// <summary>
    /// Represents a networked identity in the NitroNetwork framework.
    /// This component is attached to GameObjects that participate in network synchronization.
    /// Handles registration of RPCs, identity configuration, spawning, destruction, and room association.
    /// </summary>
    [DefaultExecutionOrder(-99)] // Sets the execution order of this script in Unity
    public class NitroIdentity : MonoBehaviour
    {
        // Connections associated with this identity
        public NitroConn conn, callConn;
        // Room associated with this identity
        public NitroRoom room;
        // Dictionaries for storing server and client RPCs
        public Dictionary<int, Action<NitroBuffer>> RpcServer = new(), RpcClient = new();
        // Array of child behaviors associated with this identity
        private Dictionary<string, NitroBehaviour> behaviours = new();
        // Indicates if the identity is static (does not change during execution)
        [HideInInspector]
        public bool IsStatic = false;
        // Indicates whether the identity belongs to the server, client, or the local player
        public bool IsServer = false, IsClient = false, IsMine = false;
        // Unique identifier for this identity
        public int Id;
        // Indicates if the object should spawn as a child of another
        [SerializeField]
        private bool SpawnInParent = true;
        // Indicates if the object should be hidden in the hierarchy
        [Header("Hide from hierarchy")]
        [SerializeField]
        private bool Hide = true;
        // Name of the room associated with this identity
        protected string roomName;
        // Name of the prefab associated with this identity
        private string namePrefab;

        /// <summary>
        /// Called when the object is initialized.
        /// Registers RPCs for all NitroBehaviour children and configures the identity.
        /// </summary>
        void Awake()
        {

            // Registers RPCs for child behaviors
            var isServer = false;
            var isClient = false;
            NitroManager.IsServerAndClient(ref isServer, ref isClient, this);
            if (isServer && isClient && Hide)
            {
                if (IsServer) DisableAllVisualComponents();
            }
            foreach (var nb in GetComponentsInChildren<NitroBehaviour>(true))
            {
                behaviours[nb.name] = nb;
            }

            foreach (var nb in behaviours.Values)
            {
                if (IsServer || IsStatic) nb.__RegisterMyRpcServer(RpcServer);
                if (IsClient || IsStatic) nb.__RegisterMyRpcClient(RpcClient);
            }
            // If the identity is static, register it with the NitroManager
            if (IsStatic || IsClient)
            {
                NitroManager.RegisterIdentity(this, IsServer, IsStatic);
            }
            if (IsStatic)
            {
                SetConfig();
            }
        }

        /// <summary>
        /// Disables all visual components of the GameObject and its children, including Renderers and Canvas.
        /// </summary>
        public void DisableAllVisualComponents()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            // Disables all Renderers (MeshRenderer, SpriteRenderer, etc.)
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
            // Disables all Canvas
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
            }
        }

        /// <summary>
        /// Sets the room associated with this identity.
        /// </summary>
        internal void SetRoom(NitroRoom room)
        {
            roomName = room.Name;
            this.room = room;
        }

        /// <summary>
        /// Configures the identity and its child behaviours based on its current state.
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
        /// Called when the object is enabled.
        /// Registers the spawn RPC for the server and sends a spawn request if client.
        /// </summary>
        private void OnEnable()
        {
            if (IsServer) RpcServer.TryAdd((int)NitroCommands.SpawnRPC, OnInstantiated);
            if (IsClient)
            {
                using var buffer = NitroManager.Rent();
                buffer.SetInfo((byte)NitroCommands.SpawnRPC, Id);
                NitroManager.SendForServer(buffer.Buffer);
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
        internal void SetConfig(NitroConn conn, int id, bool isServer, bool isClient, bool isMine)
        {
            Id = id;
            this.conn = conn;
            IsServer = isServer;
            IsClient = isClient;
            IsMine = isMine;
            SetConfig();
        }

        /// <summary>
        /// Called when the identity is instantiated on the network.
        /// Invokes OnInstantiated on all child NitroBehaviours.
        /// </summary>
        internal void OnInstantiated(NitroBuffer buffer)
        {
            foreach (var nb in behaviours.Values)
            {
                nb.OnInstantiated();
            }
            callConn = conn;
        }

        /// <summary>
        /// Spawns a new identity on the server and registers it.
        /// </summary>
        /// <param name="conn">The connection associated with the new identity.</param>
        /// <param name="room">The room where the identity will be spawned.</param>
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
            // Configures the buffer for the spawn RPC
            Id = newIdentity.Id;
            this.conn = conn;
            newIdentity.conn = conn;
            newIdentity.callConn = conn;
            newIdentity.IsServer = true;
            newIdentity.namePrefab = name;
            namePrefab = name;
            newIdentity.name = $"{name}(Server)";
            newIdentity.SetConfig();

            NitroManager.RegisterIdentity(newIdentity);
            newIdentity.SendSpawnForClient(conn, Target.All, newRoom: newRoom);
            newRoom.SetIdentity(newIdentity);
            newIdentity.gameObject.SetActive(true);
            return newIdentity;
        }

        /// <summary>
        /// Sends a spawn RPC to the client.
        /// </summary>
        /// <param name="conn">The connection to send the spawn RPC to.</param>
        /// <param name="Target">The Target audience for the spawn RPC.</param>
        /// <param name="room">The room associated with the spawn RPC.</param>
        /// <returns>The current NitroIdentity.</returns>
        internal NitroIdentity SendSpawnForClient(NitroConn conn = null, Target Target = Target.All, NitroRoom newRoom = null)
        {
            using var buffer = NitroManager.Rent();
            buffer.SetInfo((byte)NitroCommands.SpawnRPC, (int)NitroCommands.ConfigsManager);
            buffer.Write(this.Id);
            buffer.Write(this.conn.Id);
            buffer.Write(SpawnInParent);
            buffer.Write(namePrefab);
            buffer.Write(transform.position);
            buffer.Write(transform.rotation.eulerAngles);
            buffer.Write(transform.parent != null ? transform.parent.name : "");
            NitroManager.SendForClient(buffer.Buffer, conn, Target: Target, room: newRoom, roomValidate: room);
            return this;
        }

        /// <summary>
        /// Sends a destroy RPC to the client.
        /// </summary>
        /// <param name="conn">The connection to send the destroy RPC to.</param>
        /// <param name="Target">The Target audience for the destroy RPC.</param>
        /// <param name="room">The room associated with the destroy RPC.</param>
        internal void SendDestroyForClient(NitroConn conn = null, Target Target = Target.All, NitroRoom newRoom = null)
        {
            var buffer = new NitroBuffer();
            buffer.SetInfo((byte)NitroCommands.DespawnIdentity, (int)NitroCommands.ConfigsManager);
            buffer.Write(Id);
            NitroManager.SendForClient(buffer.Buffer, conn, Target: Target, room: room, roomValidate: newRoom);
        }

        /// <summary>
        /// Destroys the current identity and sends a destroy RPC to the client.
        /// </summary>
        public void Destroy()
        {
            SendDestroyForClient(conn);
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
                conn?.identities.Remove(Id);
                RpcServer.Clear();
            }
        }
    }
}