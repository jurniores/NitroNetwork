using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using NitroNetwork.Core;
using UnityEngine;
using System.Net;

#if UNITY_EDITOR
using UnityEditorInternal;
#endif

[DefaultExecutionOrder(-100)] // Ensures this script executes early in the Unity lifecycle
[RequireComponent(typeof(LiteTransporter))] // Requires LiteTransporter component to be attached to the GameObject
public class NitroManager : MonoBehaviour
{
    internal static NitroBufferPool bufferPool; // Buffer pool for efficient memory management
    public List<int> ids = new();
    public Dictionary<string, NitroIdentity> nitroPrefabsDic = new();
    public Dictionary<int, NitroConn> peers = new();
    public Dictionary<string, NitroRoom> rooms = new();
    internal Dictionary<(ushort, byte), Action<NitroBuffer>> RpcsServer = new();
    internal Dictionary<(ushort, byte), Action<NitroBuffer>> RpcsClient = new();
    public Dictionary<string, byte> IdRpcServers = new();
    public Dictionary<string, byte> IdRpcClients = new();
    public Dictionary<ushort, NitroIdentity> identitiesServer = new();
    public Dictionary<ushort, NitroIdentity> identitiesClient = new();

    private NitroRoom firstRoom = new NitroRoom(); // The first room created
    private NitroConn connCallManager = new(); // Connection manager for handling peer connections
    private static NitroManager Instance; // Singleton instance of NitroManager

    [SerializeField]
    private string address = "127.0.0.1"; // Default server address
    [SerializeField]
    private int port = 5000; // Default server port

    public NitroConn ClientConn, ServerConn; // Connections for client and server

    Transporter transporter; // Transporter for handling network communication
    internal bool IsServer, IsClient; // Flags to indicate server or client mode

    [Header("Connect")]
    public bool Server = true; // Indicates if the server should be started
    public bool Client = true; // Indicates if the client should connect

    static byte idClient = 0; // Counter for client RPC IDs
    static byte idServer = 0; // Counter for server RPC IDs

    public List<NitroIdentity> nitroPrefabs = new(); // List of Nitro prefabs

    /// <summary>
    /// Called when the object is initialized.
    /// Sets up the NitroManager instance and initializes network components.
    /// </summary>
    private void Awake()
    {
        bufferPool = new NitroBufferPool(32000); // Initialize buffer pool
        Instance = this;
        idClient = 0;
        idServer = 0;

        // Register default RPCs for the client
        RpcsClient.Add(((ushort)NitroCommands.SpawnIdentity, (byte)NitroCommands.SpawnRPC), SpawnInClient);
        RpcsClient.Add(((ushort)NitroCommands.SpawnIdentity, (byte)NitroCommands.DespawnIdentity), DestroyBuffer);
        RpcsClient.Add(((ushort)NitroCommands.GetConnection, (byte)NitroCommands.Connected), GetConnectionClient);

        // Add Nitro prefabs to the dictionary
        foreach (var prefabs in nitroPrefabs)
        {
            if (!nitroPrefabsDic.TryAdd(prefabs.name, prefabs))
            {
                Debug.LogError($"Failed to add prefab {prefabs.name} to dictionary.");
            }
        }

        // Initialize the transporter and set up event handlers
        transporter = GetComponentInChildren<Transporter>(true);
        transporter.OnConnected += OnPeerConnected;
        transporter.OnDisconnected += OnPeerDisconnected;
        transporter.OnError += OnError;
        transporter.IPConnection += RecieveIPEndPoint;

        // Start the server and/or client based on configuration
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

        // Prevent this object from being destroyed when loading new scenes
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Ensures the LiteTransporter component is moved below NitroManager in the Inspector.
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
#endif
    }

    /// <summary>
    /// Starts the server on the specified port.
    /// </summary>
    public static void ConnectServer(int port)
    {
        Instance.transporter.ServerConnect("127.0.0.1", port);
    }

    /// <summary>
    /// Connects the client to the specified address and port.
    /// </summary>
    public static void ConnectClient(string address, int port)
    {
        Instance.transporter.ClientConnect(address, port);
    }
    public static void Disconnect()
    {
        Instance.transporter.DisconnectServer();
        Instance.transporter.DisconnectClient();
    }
    public static void DisconnectConn(NitroConn conn)
    {
        Instance.transporter.DisconnectPeer(conn.Id);
    }
    /// <summary>
    /// Creates the first room when the object starts.
    /// </summary>
    void Start()
    {
        firstRoom = CreateRoom(Guid.NewGuid().ToString(), false);
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
    /// Gets the first room created.
    /// </summary>
    public static NitroRoom GetFirstRoom()
    {
        return Instance.firstRoom;
    }

    /// <summary>
    /// Registers an RPC method for a NitroIdentity.
    /// </summary>
    internal static void RegisterRPC(Action<NitroBuffer> action, NitroType type, NitroIdentity identity)
    {
        if (type == NitroType.Server)
        {
            if (Instance.RpcsServer.TryAdd((identity.Id, idServer), action))
            {
                Instance.IdRpcServers.TryAdd(action.Method.Name, idServer);
            }
            else
            {
                Debug.LogError($"RPC {action.Method.Name} no regristed for server identity {identity.Id}");
            }
            idServer++;

        }
        else if (type == NitroType.Client)
        {

            if (Instance.RpcsClient.TryAdd((identity.Id, idClient), action))
            {
                Instance.IdRpcClients.TryAdd(action.Method.Name, idClient);
            }
            else
            {
                Debug.LogError($"RPC {action.Method.Name} no regristed for client identity {identity.Id}");
            }
            idClient++;
        }
    }

    /// <summary>
    /// Registers a NitroIdentity with the manager.
    /// </summary>
    internal static void RegisterIdentity(NitroIdentity identity, bool IsServer = true, bool IsStatic = false)
    {
        if (IsStatic)
        {
            if (!Instance.identitiesServer.TryAdd(identity.Id, identity))
            {
                Debug.LogError($"Failed to add identity static {identity.Id} allready exists in server manager.");
            }

            if (!Instance.identitiesClient.TryAdd(identity.Id, identity))
            {
                Debug.LogError($"Failed to add identity static {identity.Id} allready exists in client manager.");
            }
            //Instance.ReflectGetRPCs(identity);
            return;
        }
        ushort id = 0;
        if (IsServer)
        {
            while (!Instance.identitiesServer.TryAdd(id, identity)) id++;
            identity.Id = id;
        }
        else
        {
            while (!Instance.identitiesClient.TryAdd(id, identity)) id++;
            identity.Id = id;
        }
        //Instance.ReflectGetRPCs(identity);
    }

    /// <summary>
    /// Unregisters a NitroIdentity from the manager.
    /// </summary>
    internal static void UnRegisterIdentity(NitroIdentity identity, bool IsServer = true, bool IsStatic = false)
    {
        if (IsStatic)
        {
            if (!Instance.identitiesServer.Remove(identity.Id))
            {
                Debug.LogError($"Failed to Remove identity static {identity.Id} to server manager.");
            }

            if (!Instance.identitiesClient.Remove(identity.Id))
            {
                Debug.LogError($"Failed to Remove identity static {identity.Id} to client");
            }

            return;
        }

        if (IsServer)
        {
            if (!Instance.identitiesServer.Remove(identity.Id))
            {
                Debug.LogError($"Failed to Remove identity {identity.Id} to server manager.");
            }
        }
        else
        {
            if (!Instance.identitiesClient.Remove(identity.Id))
            {
                Debug.LogError($"Failed to Remove identity {identity.Id} to client");
            }
        }
    }

    /// <summary>
    /// Handles peer connection events.
    /// </summary>
    private void OnPeerConnected(NitroConn conn, bool IsServer)
    {
        if (IsServer)
        {
            peers.TryAdd(conn.Id, conn);
            ids.Add(conn.Id);
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
            foreach (var identity in identitiesClient)
            {
                identity.Value.SetConfig();
            }
            Debug.Log($"Peer {conn.Id} connected from server {conn.iPEndPoint.Address}:{conn.iPEndPoint.Port}");
        }

        void SendInfoInitialForClient()
        {
            var buffer = new NitroBuffer();
            buffer.SetInfo((byte)NitroCommands.Connected, (ushort)NitroCommands.GetConnection);
            buffer.Write(conn.Id);
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
                connCallManager.DestroyAllIdentities();
                connCallManager.LeaveAllRooms();
                ids.Remove(connCallManager.Id);

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
            Id = (ushort)Instance.rooms.Count,
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
    void RecieveIPEndPoint(NitroConn conn, bool IsRemote)
    {
        if (IsRemote)
        {
            IsServer = true;
            ServerConn = conn;
        }
        else
        {
            IsClient = true;
            ClientConn = conn;
        }
    }

    /// <summary>
    /// Handles incoming messages from peers.
    /// </summary>
    internal void Recieve(ReadOnlySpan<byte> message, int peerId, bool IsServer)
    {
        byte id = message[0];
        ushort identityId = (ushort)((message[1] & 0xFF) | ((message[2] & 0xFF) << 8));
        NitroBuffer buffer = new NitroBuffer();

        buffer.SetInfo(id, identityId);
        buffer.WriteForRead(message);

        if (IsServer)
        {
            if (RpcsServer.TryGetValue((identityId, id), out var action))
            {
                action?.Invoke(buffer);
                return;
            }

            peers.TryGetValue(peerId, out var conn);
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
    public static void SendForClient(Span<byte> message, NitroConn conn, NitroRoom room = null, Target target = Target.All, DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered, byte channel = 0)
    {
        if (conn != null && target == Target.Self)
        {
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
                if (target == Target.AllExceptSelf)
                {
                    if (id == conn.Id) continue;
                }
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
        if (Instance.ClientConn != null) Send(Instance.ServerConn, message, deliveryMode, channel, false);
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
    void DestroyBuffer(NitroBuffer buffer)
    {
        var identityId = buffer.Read<ushort>();
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
    public static NitroIdentity GetPrefab(ushort id)
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
    /// Handles client connection to the server.
    /// </summary>
    void GetConnectionClient(NitroBuffer buffer)
    {
        var connId = buffer.Read<int>();

        ClientConn.Id = connId;
    }

    /// <summary>
    /// Spawns an identity on the client.
    /// </summary>
    void SpawnInClient(NitroBuffer buffer)
    {
        var identityId = buffer.Read<ushort>();
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
            RegisterIdentity(identity, false);

            identity.SetConfig(connCallManager, identityId, false, true, connCallManager.Id == Instance.ClientConn.Id);
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
            Instance.identitiesServer.TryAdd(identityId, identity);
        }
        else
        {
            Debug.LogError($"Prefab {namePrefab} not found in dictionary.");
        }
    }

    public static NitroBuffer Rent()
    {
        return bufferPool.Rent();
    }
}
