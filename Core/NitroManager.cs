using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;


#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace NitroNetwork.Core
{
    /// <summary>
    /// NitroManager is the central network manager for the NitroNetwork framework.
    /// It handles server/client initialization, connection management, encryption key exchange,
    /// prefab registration, room management, and message dispatching for multiplayer games.
    /// 
    /// Key responsibilities:
    /// - Manages server and client connections, including LAN and direct connections.
    /// - Handles the generation and exchange of RSA and AES keys for secure communication.
    /// - Maintains dictionaries for prefabs, rooms, peers, and network identities.
    /// - Registers and dispatches RPCs for both server and client.
    /// - Manages the lifecycle of networked objects and rooms.
    /// - Provides buffer pooling for efficient memory usage.
    /// - Integrates with a custom Transporter for low-level network operations.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(LiteTransporter))]
    public class NitroManager : MonoBehaviour
    {
        [ReadOnly]
        public string publicKey, privateKey; // RSA keys for encryption
        internal static NitroBufferPool bufferPool; // Pool for NitroBuffer objects
        public Dictionary<string, NitroIdentity> nitroPrefabsDic = new(); // Prefab lookup by name
        public Dictionary<int, NitroConn> peers = new(); // Connected peers by ID
        public Dictionary<string, NitroRoom> rooms = new(); // Active rooms by name
        internal Dictionary<(int, byte), Action<NitroBuffer, NitroConn>> RpcsServer = new(); // Server RPCs
        internal Dictionary<(int, byte), Action<NitroBuffer>> RpcsClient = new(); // Client RPCs
        public Dictionary<string, byte> IdRpcServers = new(); // Server RPC IDs
        public Dictionary<string, byte> IdRpcClients = new(); // Client RPC IDs
        public Dictionary<int, NitroIdentity> identitiesServer = new(); // Server-side identities
        public Dictionary<int, NitroIdentity> identitiesClient = new(); // Client-side identities
        private NitroRoom firstRoom = new NitroRoom(); // The initial room created on startup
        private NitroConn connCallManager = new(); // Helper connection for internal use
        private static NitroManager Instance; // Singleton instance
        public bool ConnectInLan = false; // LAN mode flag
        private int bandWithServer = 0, bandaWithValidateServer = 0, bandWithClient = 0, bandaWithValidateClient = 0;

        [SerializeField, HideIf(nameof(ConnectInLan))]
        private string address = "127.0.0.1"; // Default server address
        [SerializeField]
        private int port = 5000; // Default server port

        public static NitroConn ClientConn, ServerConn; // Static references to client/server connections
        Transporter transporter; // Network transporter component
        internal bool IsServer, IsClient; // Flags for server/client mode
        [Header("Connect"), HideIf(nameof(ConnectInLan))]
        public bool Server = true; // Should this instance act as server
        [HideIf(nameof(ConnectInLan))]
        public bool Client = true; // Should this instance act as client
        [Range(1, 1000)]
        public uint msgForDisconnectPeer = 60;
        private ulong faseValidadeSpeed = 0;
        public List<NitroIdentity> nitroPrefabs = new(); // List of registered Nitro prefabs
        public static Action<NitroConn> OnConnectConn, OnDisconnectConn; // Connection event callbacks
        public static Action OnClientConnected; // Client connection event callback
        private uint LastPing; // Stores the last calculated ping (in ms)
        Queue<double> queuePing = new(); // Queue to store ping values for averaging
        private System.Diagnostics.Stopwatch stopwatch;
        /// <summary>
        /// Unity Awake lifecycle method.
        /// Initializes the NitroManager, registers RPCs, sets up the transporter, and generates encryption keys.
        /// </summary>
        private async void Awake()
        {
            OnClientConnected = null;
            OnConnectConn = null;
            OnDisconnectConn = null;
            ServerConn = null;
            ClientConn = null;
            bufferPool = new NitroBufferPool(32000);
            Instance = this;
            // Register default RPCs for server and client
            RpcsServer.Add(((int)NitroCommands.GetConnection, (byte)NitroCommands.SendAES), ReceiveKeyAesServerRPC);
            RpcsServer.Add(((int)NitroCommands.ConfigsManager, (byte)NitroCommands.Ping), PingServerRPC);
            //Rpcs Clientes
            RpcsClient.Add(((int)NitroCommands.ConfigsManager, (byte)NitroCommands.SpawnRPC), SpawnInClient);
            RpcsClient.Add(((int)NitroCommands.ConfigsManager, (byte)NitroCommands.DespawnIdentity), DestroyIdentity);
            RpcsClient.Add(((int)NitroCommands.GetConnection, (byte)NitroCommands.Connecting), GetConnectionClientRPC);
            RpcsClient.Add(((int)NitroCommands.GetConnection, (byte)NitroCommands.Connected), ClientConnectedClientRPC);
            RpcsClient.Add(((int)NitroCommands.ConfigsManager, (byte)NitroCommands.Ping), PingClientRPC);

            // Register Nitro prefabs
            foreach (var prefabs in nitroPrefabs)
            {
                if (!nitroPrefabsDic.TryAdd(prefabs.name, prefabs))
                {
                    Debug.LogError($"Failed to add prefab {prefabs.name} to dictionary.");
                }
            }

            // Setup transporter and event handlers
            transporter = GetComponentInChildren<Transporter>(true);
            transporter.OnConnected += OnPeerConnected;
            transporter.OnDisconnected += OnPeerDisconnected;
            transporter.OnError += OnError;
            transporter.IPConnection += ReceiveIPEndPoint;

            // Generate RSA keys if needed
            if (Client && Server || ConnectInLan)
            {
                await GenerateKeys();
            }
            if (ConnectInLan)
            {
                ConnectClientLan(port, () =>
                {
                    IsServer = true;
                    IsClient = true;
                    ValidateConnect();
                });
            }
            else
            {
                ValidateConnect();
            }
            DontDestroyOnLoad(gameObject);
            StartCoroutine(IECalculate());
        }
        /// <summary>
        /// Unity Reset lifecycle method (editor only).
        /// Ensures LiteTransporter is ordered after NitroManager and generates keys.
        /// </summary>
        private void Reset()
        {
#if UNITY_EDITOR

            var liteTransporter = GetComponent<LiteTransporter>();
            var nitroManager = GetComponent<NitroManager>();

            if (liteTransporter != null && nitroManager != null)
            {
                while (ComponentUtility.MoveComponentDown(liteTransporter)) { }
            }
            NitroCriptografyRSA.GenerateKeys(out publicKey, out privateKey);
#endif
        }

        /// <summary>
        /// Validates and establishes server/client connections based on configuration.
        /// </summary>
        void ValidateConnect()
        {
            if (!IsUdpPortInUse(port) && Server)
            {
                ConnectServer(port);
                if (Client)
                {
                    ConnectClient(address, port);
                }
            }
            else if (Client)
            {
                ConnectClient(address, port);
            }
        }

        /// <summary>
        /// Generates RSA public/private key pairs asynchronously.
        /// </summary>
        public static async Task GenerateKeys()
        {
            await Task.Run(() =>
            {
                NitroCriptografyRSA.GenerateKeys(out Instance.publicKey, out Instance.privateKey);
            });
        }

        /// <summary>
        /// Starts the server on the specified port.
        /// </summary>
        public static void ConnectServer(int port)
        {
            Instance.transporter.ServerConnect("127.0.0.1", port);
            Instance.firstRoom = CreateRoom(Guid.NewGuid().ToString(), false);
            Instance.firstRoom.JoinRoom(ServerConn);
            Instance.IsServer = true;
        }

        /// <summary>
        /// Connects the client to the specified address and port.
        /// </summary>
        public static void ConnectClient(string address, int port)
        {
            Instance.transporter.ClientConnect(address, port);
        }

        /// <summary>
        /// Connects the client in LAN mode.
        /// </summary>
        public static void ConnectClientLan(int port, Action actionLanValidation)
        {
            Instance.transporter.ClientConnectLan(port, actionLanValidation);
        }

        /// <summary>
        /// Disconnects both server and client.
        /// </summary>
        public static void Disconnect()
        {
            Instance.transporter.DisconnectServer();
            Instance.transporter.DisconnectClient();
        }

        /// <summary>
        /// Disconnects a specific peer connection.
        /// </summary>
        public static void DisconnectConn(NitroConn conn)
        {
            Instance.transporter.DisconnectPeer(conn.Id);
        }

        /// <summary>
        /// Sets the server and client flags for a NitroIdentity.
        /// </summary>
        internal static void IsServerAndClient(ref bool isServer, ref bool isClient, NitroIdentity identity)
        {
            isServer = Instance.IsServer;
            isClient = Instance.IsClient;
        }

        /// <summary>
        /// Gets the first room created by the manager.
        /// </summary>
        public static NitroRoom GetFirstRoom()
        {
            return Instance.firstRoom;
        }

        /// <summary>
        /// Registers a NitroIdentity with the manager.
        /// </summary>
        internal static void RegisterIdentity(NitroIdentity identity, bool IsServer = true, bool IsStatic = false)
        {
            if (IsStatic)
            {
                Instance.identitiesServer[identity.Id] = identity;
                Instance.identitiesClient[identity.Id] = identity;
                return;
            }

            if (IsServer)
            {
                int id = 0;
                while (!Instance.identitiesServer.TryAdd(id, identity)) id++;
                identity.Id = id;
            }
            else
            {
                Instance.identitiesClient[identity.Id] = identity;
            }
        }

        /// <summary>
        /// Unregisters a NitroIdentity from the manager.
        /// </summary>
        internal static void UnRegisterIdentity(NitroIdentity identity, bool IsServer = true, bool IsStatic = false)
        {
            if (IsStatic)
            {
                if (Instance.identitiesServer.TryGetValue(identity.Id, out var identityServer))
                {
                    if (identityServer == identity)
                    {
                        Instance.identitiesServer.Remove(identity.Id);
                    }
                }

                if (Instance.identitiesClient.TryGetValue(identity.Id, out var identityClient))
                {
                    if (identityClient == identity)
                    {
                        Instance.identitiesClient.Remove(identity.Id);
                    }
                }
                return;
            }

            if (IsServer)
            {
                if (Instance.identitiesServer.TryGetValue(identity.Id, out var identityServer))
                {
                    if (identityServer == identity)
                    {
                        Instance.identitiesServer.Remove(identity.Id);
                    }
                }
            }
            else
            {
                if (Instance.identitiesClient.TryGetValue(identity.Id, out var identityClient))
                {
                    if (identityClient == identity)
                    {
                        Instance.identitiesClient.Remove(identity.Id);
                    }
                }
            }
        }
        /// <summary>
        /// Gets the public key for encryption.
        /// </summary>
        public static uint GetPingClient()
        {
            return Instance.LastPing;
        }
        /// <summary>
        /// Gets the public key for encryption.
        /// </summary>
        public static int GetBandWithServer()
        {
            return Instance.bandWithServer;
        }
        /// <summary>
        /// Gets the public key for encryption.
        /// </summary>
        public static int GetBandWithClient()
        {
            return Instance.bandWithClient;
        }
        /// <summary>
        /// Handles peer connection events for both server and client.
        /// </summary>
        private void OnPeerConnected(NitroConn conn, bool IsServer)
        {
            if (IsServer)
            {
                OnConnectConn?.Invoke(conn);
                peers.TryAdd(conn.Id, conn);
                SendInfoInitialForClient();
                firstRoom.JoinRoom(conn);

                if (peers.ContainsKey(conn.Id))
                {
                    Debug.Log($"Peer {conn.Id} connected from server {conn.iPEndPoint.Address}:{conn.iPEndPoint.Port}");
                }
                else
                {
                    Debug.Log($"Failed to add peer {conn.Id}");
                }
            }
            else
            {
                if (ServerConn == null) ServerConn = new NitroConn();
                ServerConn.Id = -1;
                ServerConn.iPEndPoint = conn.iPEndPoint;
                Debug.Log($"Peer {conn.Id} connected from server {conn.iPEndPoint.Address}:{conn.iPEndPoint.Port}");
            }

            void SendInfoInitialForClient()
            {
                using var buffer = Rent();
                buffer.SetInfo((byte)NitroCommands.Connecting, (int)NitroCommands.GetConnection);
                buffer.Write(conn.Id);
                buffer.Write(publicKey);
                Send(conn, buffer.Buffer, DeliveryMode.ReliableOrdered, 0);
            }
        }

        /// <summary>
        /// Handles peer disconnection events.
        /// </summary>
        private void OnPeerDisconnected(int peerId, bool IsServer)
        {
            if (IsServer)
            {
                if (peers.TryGetValue(peerId, out connCallManager))
                {
                    OnDisconnectConn?.Invoke(connCallManager);
                    connCallManager.LeaveAllRooms();
                    connCallManager.DestroyAllIdentities();
                    if (peers.Remove(connCallManager.Id))
                    {
                        Debug.Log($"Peer {connCallManager.Id} disconnected from server remove {connCallManager.iPEndPoint.Address}:{connCallManager.iPEndPoint.Port}");
                    }
                    else
                    {
                        Debug.Log($"Failed to remove peer {connCallManager.Id}");
                    };
                }
            }
            else if (IsClient)
            {
                Debug.Log($"Disconnected Peer");
            }
        }

        /// <summary>
        /// Creates a new room with the specified name and auto-destroy setting.
        /// </summary>
        public static NitroRoom CreateRoom(string name, bool autoDestroy = true)
        {
            NitroRoom room = new()
            {
                Name = name,
                autoDestroy = autoDestroy,
                Id = Instance.rooms.Count,
            };
            if (Instance.rooms.TryAdd(name, room))
            {
                return room;
            }
            return null;
        }

        /// <summary>
        /// Checks if a room exists in the manager.
        /// </summary>
        public static bool RoomExists(NitroRoom room)
        {
            return Instance.rooms.ContainsKey(room.Name);
        }

        /// <summary>
        /// Removes a room automatically if it is empty and marked for auto-destruction.
        /// </summary>
        internal static bool RemoveRoomAuto(NitroRoom room)
        {
            if (room.autoDestroy && room.peersRoom.Count == 0 && Instance.rooms.Remove(room.Name))
            {
                room.DestroyAllIdentities();
                Debug.Log($"Room {room.Name} removed from manager.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sends a message to a specific peer.
        /// </summary>
        internal static void Send(NitroConn conn, Span<byte> message, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0, bool IsServer = true)
        {
            Instance.transporter.Send(conn.Id, message, deliveryMode, channel, IsServer);
        }

        /// <summary>
        /// Removes a room from the manager.
        /// </summary>
        public static void RemoveRoom(NitroRoom room)
        {
            if (room.Id != Instance.firstRoom.Id && room != null && Instance.rooms.Remove(room.Name))
            {
                room.DestroyAllIdentities();
                Debug.Log($"Room {room.Name} removed from manager.");
            }
        }

        /// <summary>
        /// Handles the reception of a new peer's endpoint and initializes encryption keys.
        /// </summary>
        void ReceiveIPEndPoint(NitroConn conn, bool IsRemote)
        {
            if (IsRemote)
            {
                Debug.Log($"Server connected in {conn.iPEndPoint.Address}:{conn.iPEndPoint.Port}");
                IsServer = true;
                ServerConn = conn;
                ServerConn.keyAes = NitroCriptografyAES.GenerateKeys();
                StartCoroutine(IESpeedHackValidate());
            }
        }

        /// <summary>
        /// Handles incoming messages from peers and dispatches them to the appropriate RPCs.
        /// </summary>
        internal void ReceiveMessage(ReadOnlySpan<byte> message, int peerId, bool IsServer)
        {
            byte id = message[0];
            int identityId = (message[1] & 0xFF) | ((message[2] & 0xFF) << 8) | ((message[3] & 0xFF) << 16) | ((message[4] & 0xFF) << 24);
            using var buffer = Rent();

            buffer.WriteForRead(message);
            buffer.Length = message.Length;

            if (IsServer)
            {
                peers.TryGetValue(peerId, out var conn);
                SpeedHackValidate(conn);
                if (RpcsServer.TryGetValue((identityId, id), out var action))
                {
                    action?.Invoke(buffer, conn);
                    return;
                }

                identitiesServer.TryGetValue(identityId, out var identity);

                if (identity != null)
                {
                    identity.callConn = conn;
                    identity.RpcServer[id]?.Invoke(buffer);
                }
            }
            else
            {
                if (RpcsClient.TryGetValue((identityId, id), out var action))
                {
                    action?.Invoke(buffer);
                    return;
                }
                peers.TryGetValue(peerId, out var conn);
                identitiesClient.TryGetValue(identityId, out var identity);

                if (identity != null)
                {
                    identity.RpcClient[id]?.Invoke(buffer);
                }
            }
        }

        /// <summary>
        /// Sends a message to all clients in a room or to a specific client.
        /// </summary>
        public static void SendForClient(Span<byte> message, NitroConn conn, NitroRoom room = null, NitroRoom roomValidate = null, Target target = Target.All, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
        {

            if (Instance.peers.Count == 0)
            {
                Debug.LogWarning("No peers connected to send messages.");
                return;
            }
            if (conn != null && target == Target.Self)
            {
                Instance.bandaWithValidateServer += message.Length;
                if (conn.keyAes == null) return;
                Send(conn, message, deliveryMode, channel, true);
                return;
            }

            if (room == null)
            {
                room = GetFirstRoom();
            }

            if (room != null)
            {
                foreach (var (id, connRoom) in room.peersRoom)
                {
                    if (connRoom.keyAes == null) continue;
                    if (target == Target.ExceptSelf && conn != null)
                    {
                        if (id == conn.Id && conn.Id != ServerConn.Id) continue;
                    }
                    if (roomValidate != null && roomValidate.peersRoom.ContainsKey(id))
                    {
                        continue;
                    }
                    Instance.bandaWithValidateServer += message.Length;
                    Send(connRoom, message, deliveryMode, channel, true);
                }
                return;
            }
        }

        /// <summary>
        /// Sends a message to the server.
        /// </summary>
        public static void SendForServer(Span<byte> message, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
        {
            if (Instance.IsClient)
            {
                Instance.bandaWithValidateClient += message.Length;
                if (ServerConn.keyAes == null) return;
                Send(ServerConn, message, deliveryMode, channel, false);
            }
            else
            {
                Debug.LogError("Client is not connected to the server. Use the OnClientConnected event to send initial messages.");
            }
        }

        /// <summary>
        /// Handles errors during network communication.
        /// </summary>
        private void OnError(string error)
        {
            Debug.Log($"Error: {error}");
        }

        /// <summary>
        /// Checks if a UDP port is in use.
        /// </summary>
        private bool IsUdpPortInUse(int port)
        {
            try
            {
                using (UdpClient udpClient = new UdpClient(port))
                {
                    return false; // Port is free
                }
            }
            catch (SocketException)
            {
                return true; // Port is in use
            }
        }

        /// <summary>
        /// Destroys a buffer and removes the associated identity.
        /// </summary>
        void DestroyIdentity(NitroBuffer buffer)
        {
            var identityId = buffer.Read<int>();
            if (Instance.identitiesClient.TryGetValue(identityId, out var identity))
            {
                Destroy(identity.gameObject);
            }
        }

        /// <summary>
        /// Retrieves a Nitro prefab by name.
        /// </summary>
        public static NitroIdentity GetPrefab(string name)
        {
            if (Instance.nitroPrefabsDic.TryGetValue(name, out var prefab))
            {
                return prefab;
            }
            Debug.LogError($"Prefab {name} not found in dictionary.");
            return null;
        }

        /// <summary>
        /// Retrieves a Nitro prefab by ID.
        /// </summary>
        public static NitroIdentity GetPrefab(int id)
        {
            var identity = Instance.nitroPrefabs.ElementAt(id);
            if (identity != null)
            {
                return identity;
            }
            Debug.LogError($"Prefab {id} not found in dictionary.");
            return null;
        }

        /// <summary>
        /// Spawns an identity on the client based on data received from the server.
        /// </summary>
        void SpawnInClient(NitroBuffer buffer)
        {
            var identityId = buffer.Read<int>();
            var coonId = buffer.Read<int>();
            var spawnInParent = buffer.Read<bool>();
            var namePrefab = buffer.Read<string>();
            var pos = buffer.Read<Vector3>();
            var rot = buffer.Read<Vector3>();
            var nameParent = buffer.Read<string>();

            connCallManager.Id = coonId;

            if (Instance.nitroPrefabsDic.TryGetValue(namePrefab, out var prefab))
            {
                prefab.gameObject.SetActive(false);
                var identity = Instantiate(prefab);
                //RegisterIdentity(identity, false);
                identity.SetConfig(connCallManager, identityId, false, true, connCallManager.Id == ClientConn.Id);
                identity.name = namePrefab + "(Client)";
                if (!string.IsNullOrEmpty(nameParent) && spawnInParent)
                {
                    var transformParent = GameObject.Find(nameParent).transform;
                    if (transformParent == null) return;
                    identity.transform.SetParent(transformParent);
                }
                identity.transform.position = pos;
                identity.transform.rotation = Quaternion.Euler(rot);
                identity.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogError($"Prefab {namePrefab} not found in dictionary.");
            }
        }

        /// <summary>
        /// Handles the initial connection of a client to the server and performs AES key exchange.
        /// </summary>
        void GetConnectionClientRPC(NitroBuffer buffer)
        {
            var connId = buffer.Read<int>();
            var pubKey = buffer.Read<string>();

            if (Client && Server || ConnectInLan) publicKey = pubKey;

            var conn = new NitroConn
            {
                Id = connId,
            };

            ClientConn = conn;
            // Generate AES key for encryption
            byte[] keyAes = NitroCriptografyAES.GenerateKeys();
            ClientConn.keyAes = keyAes;

            using var bufferAes = Rent();
            bufferAes.SetInfo((byte)NitroCommands.SendAES, (int)NitroCommands.GetConnection);
            bufferAes.Write(keyAes);
            bufferAes.EncriptRSA(GetPublicKey());
            Send(conn, bufferAes.Buffer, DeliveryMode.ReliableOrdered, 0, false);
        }

        /// <summary>
        /// Receives and decrypts the AES key sent by the client, then responds with the server's AES key.
        /// </summary>
        async void ReceiveKeyAesServerRPC(NitroBuffer buffer, NitroConn conn)
        {
            NitroBuffer bufferNew = new()
            {
                buffer = buffer.buffer,
                Length = buffer.Length
            };

            await Task.Run(() =>
            {
                bufferNew.DecryptRSA(GetPrivateKey());
            });

            var keyAes = bufferNew.Read<byte[]>();

            if (keyAes == null || keyAes.Length == 0)
            {
                Debug.LogError("Received empty AES key.");
                return;
            }

            conn.keyAes = keyAes;
            var bufferSend = Rent();
            bufferSend.SetInfo((byte)NitroCommands.Connected, (int)NitroCommands.GetConnection);
            bufferSend.Write(ServerConn.keyAes);
            bufferSend.EncriptAes(conn.keyAes);
            Send(conn, bufferSend.Buffer, DeliveryMode.ReliableOrdered, 0);
        }

        /// <summary>
        /// Handles the final step of the handshake, decrypting the server's AES key on the client.
        /// </summary>
        private void ClientConnectedClientRPC(NitroBuffer buffer)
        {
            buffer.DecryptAes(ClientConn.keyAes);
            var serverAes = buffer.Read<byte[]>();
            ServerConn.keyAes = serverAes;
            IsClient = true;
            foreach (var identity in identitiesClient)
            {
                identity.Value.SetConfig();
            }
            OnClientConnected?.Invoke();
        }
        /// <summary>
        /// Validates the speed of incoming messages from a peer and disconnects if the limit is exceeded.
        /// </summary>
        /// <param name="conn">The connection to validate.</param>
        private void SpeedHackValidate(NitroConn conn)
        {
            if (conn.countMsg > msgForDisconnectPeer && conn.fase == faseValidadeSpeed)
            {
                Debug.LogWarning($"Peer {conn.Id} disconnected for exceeding message limit ({conn.countMsg} > {msgForDisconnectPeer})");
                DisconnectConn(conn);
            }
            if (conn.fase != faseValidadeSpeed)
            {
                conn.countMsg = 0;
                conn.fase = faseValidadeSpeed;
            }
            conn.countMsg++;
        }
        /// <summary>
        /// Handles the ping request from the client and responds with a ping message.
        /// </summary>
        private void PingServerRPC(NitroBuffer buffer, NitroConn conn)
        {
            Send(conn, buffer.Buffer, DeliveryMode.Unreliable, 0);
        }
        /// <summary>
        /// Handles the ping response from the client.
        /// </summary>
        private void PingClientRPC(NitroBuffer buffer)
        {
            // Adiciona o tempo atual de resposta à fila
            stopwatch.Stop();
            double ping = (stopwatch.ElapsedMilliseconds / 2) - Time.deltaTime * 1000;

            queuePing.Enqueue(ping);

            if (queuePing.Count > 20)
            {
                queuePing.Dequeue();
            }

            double pingSum = 0;
            foreach (var p in queuePing)
            {
                pingSum += p;
            }
            LastPing = (uint)(queuePing.Count > 0 ? pingSum / queuePing.Count : 0);
        }

        /// <summary>
        /// Rents a NitroBuffer from the pool.
        /// </summary>
        public static NitroBuffer Rent()
        {
            return bufferPool.Rent();
        }

        /// <summary>
        /// Gets the current public RSA key.
        /// </summary>
        internal static string GetPublicKey()
        {
            return Instance.publicKey;
        }

        /// <summary>
        /// Gets the current private RSA key.
        /// </summary>
        internal static string GetPrivateKey()
        {
            return Instance.privateKey;
        }

        IEnumerator IESpeedHackValidate()
        {
            while (true)
            {
                faseValidadeSpeed++;
                yield return new WaitForSeconds(1f);
            }
        }
        IEnumerator IECalculate()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                bandWithServer = bandaWithValidateServer;
                bandaWithValidateServer = 0;
                bandWithClient = bandaWithValidateClient;
                bandaWithValidateClient = 0;
                // Script Ping
                if (IsClient)
                {
                    using var buffer = Rent();
                    buffer.SetInfo((byte)NitroCommands.Ping, (int)NitroCommands.ConfigsManager);
                    // Save the current time in milliseconds and write to buffer
                    stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    Send(ClientConn, buffer.Buffer, DeliveryMode.Unreliable, 0, false);
                }
            }
        }

    }
}
