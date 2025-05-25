# 🔥 NitroNetwork - Unity Networking Framework

**NitroNetwork** is a lightweight and fast networking solution for Unity, designed to simplify multiplayer development while offering flexibility and control for both client and server behaviors.

This repository contains an example implementation of a basic connection flow between client and server using `NitroBehaviour`, `NitroStatic`, and `NitroRPC`.

---

## 📂 Example: `ConnectPeers.cs`

This script demonstrates how to automatically connect a client to the server when the scene starts. It also shows how to use RPCs to send messages between peers.

### ✅ Features:
- Uses `[NitroRPC(RPC.Server)]` to call a server-only method from the client.
- Ensures the object exists in all peers using `NitroStatic`.
- Inherits from `NitroBehaviour` to enable network features.
- When using static, choose an Id for it; the default is 0

### 📄 Script

```csharp
using UnityEngine;
using NitroNetwork.Core;

[RequireComponent(typeof(NitroStatic))]
public partial class ConnectPeers : NitroBehaviour
{
    void Update()
    {
        // Press the spacebar on the keyboard
        if (IsClient && Input.GetKeyDown(KeyCode.Space))
        {
            CallConnectToServer();
        }
    }
    // RPC method that will be executed on the server when called by a client.
    [NitroRPC(RPC.Server)]
    void ConnectToServer()
    {
        Debug.Log("ConnectToServer");
        CallConnectToClient();
    }
    
    [NitroRPC(RPC.Client)]
    void ConnectToClient()
    {
        Debug.Log("Hello Client");
    }
}
```
## 🧠 NitroBehaviour: Enabling RPC and Network Logic

All networked scripts in **NitroNetwork** must inherit from `NitroBehaviour`.  
To enable Nitro's code generation and RPC binding, **your class must also be declared as `partial`**.

> ⚠️ If you forget to mark your class as `partial`, RPCs will **not work**.

---

### 🏗️ Basic Setup

- Inherit from `NitroBehaviour`
- Mark your class as `partial`
- Attach a `NitroIdentity` (or `NitroStatic`) to the parent GameObject
- ## 🔎 Role Detection with NitroBehaviour

When your script inherits from `NitroBehaviour`, you gain access to key role-checking properties. These are essential to safely structure logic that runs only on the server, client, or when the object belongs to the current player.

| Property     | Type    | Used On   | Description                                                                 |
|--------------|---------|-----------|-----------------------------------------------------------------------------|
| `IsMine`     | `bool`  | Client    | `true` if this identity belongs to the local client (i.e. the local player) |
| `IsClient`   | `bool`  | Client    | `true` when the script is running on any client (not necessarily owner)     |
| `IsServer`   | `bool`  | Server    | `true` when running on the server                                           |

These flags are safe to use anywhere inside your `NitroBehaviour` class to control RPC logic and authority.

### ✅ Usage Example

```csharp
public partial class MyNetworkScript : NitroBehaviour
{
    void Update()
    {
        if (IsMine)
        {
            // Only the owning client can execute this block
        }
    
        if (IsClient)
        {
            // Executes on all clients (owners or not)
        }
    
        if (IsServer)
        {
            // Executes only on the server
        }
    }
}


```
## 📡 Remote Procedure Calls (RPC) in NitroNetwork

In **NitroNetwork**, you can create network-executable methods using the `[NitroRPC]` attribute.  
These methods must be declared inside a `partial` class that inherits from `NitroBehaviour`.

---

### 🧪 Example RPC Declaration

```csharp
void Start()
{
    // Logic to trigger RPCs
}

[NitroRPC(RPC.Server)]
void MethodOfServer()
{
    print("Hello Server");
}

[NitroRPC(RPC.Client)]
void MethodOfClient()
{
    Debug.Log("Hello Client");
}
```

## 📡 NitroRPC Attribute

The `[NitroRPC]` attribute is used to define methods that can be executed remotely over the network. These methods allow communication between the server and clients in **NitroNetwork**.

### 🔧 NitroRPC Attribute Parameters

| Parameter       | Type            | Default Value                   | Description                                                                                       |
|-----------------|-----------------|---------------------------------|---------------------------------------------------------------------------------------------------|
| `type`          | `RPC`     | **Required**                    | Specifies whether the RPC is for the `Server` or `Client`.                                        |
| `requiresOwner` | `bool`          | `true`                          | Indicates if the caller must own the object to invoke the RPC.                                    |
| `Target`        | `Target`        | `Target.All`                    | Defines the Target audience for the RPC (e.g., `All`, `AllExceptSelf`, `Self`). **Only used for `RPC.Client` RPCs.** |
| `DeliveryMode`  | `DeliveryMode`  | `DeliveryMode.ReliableOrdered`  | Specifies the delivery method (e.g., `ReliableOrdered`, `Unreliable`).                            |
| `channel`       | `int`           | `0`                             | Specifies the communication channel for the RPC.                                                  |
| `encrypt`   | `bool`          | `false`                         | If `true` and `RPC.Server`, the message is encrypted with the client's AES key. If `RPC.Client`, it is encrypted with the server's public key. For increased security and to avoid `MITM` attacks, use `Target.Self`, the server will encrypt with the client's key and send only to that client. |

---

### 🧪 Example: Using NitroRPC
```csharp
public partial class MyNetworkScript : NitroBehaviour
{
    void Start()
    {
        // Trigger an RPC call to the server
        CallServerMethod();
    }

    // Server-side RPC
    [NitroRPC(RPC.Server, requiresOwner = true)]
    void ServerMethod()
    {
        Debug.Log("This method runs on the server.");
        CallClientMethod(); // Call a client RPC from the server
    }

    // Client-side RPC
    [NitroRPC(RPC.Client, Target = Target.AllExceptSelf, DeliveryMode = DeliveryMode.Sequenced)]
    void ClientMethod()
    {
        Debug.Log("This method runs on all clients except the caller.");
    }
}

```

### ☎️ Calling RPCs (Always Use `Call` Prefix)

```csharp
if (IsClient || IsMine)
{
    CallMethodOfServer(); // Client → Server
}

if (IsServer)
{
    CallMethodOfClient(); // Server → Client
}

```
> ⚠️ Always use the `Call` prefix. Never invoke the method directly.

⚠️ Static Variable Warning
A note: always be careful with static variables, as they are not bound to any specific connection and can cause silent errors. Make sure you're always calling the method from the opposite network context (e.g., client calling a server method, and vice versa).


# 🧩 Delta Compression System

This module implements an efficient **Delta Compression** system for `Vector3` and `Quaternion` structures. It is ideal for reducing data traffic when synchronizing state in network systems, sending only the differences between states.

---

## ✅ How It Works

- **Bitmask (`byte`)**: Indicates which components (`x`, `y`, `z`, `w`) have changed.
- **Optimized Serialization**: Only the modified components are transmitted.
- **Performance**: Minimizes allocations and bandwidth usage by utilizing `NitroManager` for buffer pooling.

---

## 🛠️ Usage Example

### Generating and sending a `Vector3` delta

```csharp

Vector3 current = new Vector3(1, 2, 3);
Vector3 previous = new Vector3(1, 2, 0);

// Generate delta only for the components that changed
byte[] deltaBuffer = Delta(current, ref previous);

// Send deltaBuffer over the network...

//In receiving RPC  
ReadDelta(deltaBuffer, ref receivedVector);


//overloads
//Write
byte[] Delta(Vector3 delta, ref Vector3 compare);

byte[] Delta(Quaternion delta, ref Quaternion compare);

byte[] Delta(Vector3 delta, Quaternion delta2, ref Vector3 compareV, ref Quaternion compareQ);

byte[] Delta(Vector3 delta1, Vector3 delta2, ref Vector3 compareV, ref Vector3 compareV2);

byte[] Delta(Quaternion delta1, Quaternion delta2, ref Quaternion compareV, ref Quaternion compareQ);


//overloads
//Read
ReadDelta(byte[] buffer, ref Vector3 newV);

ReadDelta(byte[] buffer, ref Quaternion newQ);

ReadDelta(byte[] buffer, ref Vector3 newV, ref Quaternion newQ);

ReadDelta(byte[] buffer, ref Vector3 newV, ref Vector3 newV2);

ReadDelta(byte[] buffer, ref Quaternion newQ, ref Quaternion newQ2);



```


## 🧱 Spawning Networked Objects

In **NitroNetwork**, to spawn an object over the network, you must:

1. Attach the `NitroIdentity` component to your prefab.
2. Optionally attach `NitroStatic` if the object must always exist in the scene.
3. Use the `Spawn()` method to instantiate and synchronize the object across the network.

---

### ✅ Requirements

- Your prefab must be registered via `NitroManager`.
- The prefab must contain a `NitroIdentity` component.
- To spawn the object for a specific client, pass `Identity.callConn` as the owner.
### ✅ Key Properties of `NitroIdentity`

Below are the key properties available in the `NitroIdentity` component:
| Property       | Type            | Description                                                     |
|----------------|-----------------|-----------------------------------------------------------------|
| `Id`           | `int`           | Unique identifier for this object across the network. For static identities, this ID must be manually assigned |
| `conn`         | `NitroConn`     | The connection that owns this identity (null for static objects)|
| `IsStatic`     | `bool`          | Whether this identity is static (pre-existing in scene)         |
| `IsServer`     | `bool`          | True if this identity is running on the server                  |
| `IsClient`     | `bool`          | True if this identity is running on a client                    |
| `IsMine`       | `bool`          | True if the local connection owns this identity                 |
| `Room`         | `NitroRoom`     | The room this identity belongs to                               |
| `callConn`     | `NitroConn`     | The connection that initiated the last RPC call                 |
| `lastClientId` | `int`           | ID of the last client to interact with this identity            |
| `Hide`         | `bool`          | When set in inspector, hides the object from network spawning   |
| `SpawnInParent`| `bool`          | When enabled, spawns the object as a child of a specified parent|

## 📜 Multiple NitroBehaviour Scripts Per Object

In **NitroNetwork**, you can attach multiple `NitroBehaviour` scripts to a single networked object. This allows you to organize your code logically, separating different aspects of functionality (movement, health, inventory, etc.) into different components.

### ⚠️ RPC Limits Per Identity

While you can add as many `NitroBehaviour` scripts as you need to a GameObject, there are important limits to be aware of:

- Each `NitroIdentity` can have up to **250 client RPCs** across all its NitroBehaviour components
- Each `NitroIdentity` can have up to **250 server RPCs** across all its NitroBehaviour components

These limits apply to the total number of RPC methods defined across all `NitroBehaviour` scripts attached to the same GameObject with a `NitroIdentity` component.

### 🧪 Example: Distributing RPCs Across Multiple Scripts

```csharp
// PlayerMovement.cs
[RequireComponent(typeof(NitroIdentity))]
public partial class PlayerMovement : NitroBehaviour
{
    [NitroRPC(RPC.Server)]
    void MovePlayer(Vector3 position) { /* ... */ }
    
    [NitroRPC(RPC.Client)]
    void SyncPosition(Vector3 position) { /* ... */ }
}

// PlayerCombat.cs - Attached to the same GameObject
public partial class PlayerCombat : NitroBehaviour
{
    [NitroRPC(RPC.Server)]
    void AttackTarget(int TargetId) { /* ... */ }
    
    [NitroRPC(RPC.Client)]
    void PlayAttackAnimation(int animId) { /* ... */ }
}
```

All these RPCs count toward the 250 client and 250 server RPC limits for the single `NitroIdentity` on the GameObject.

---

### 🧪 Example: Server Spawning a Player

This example shows how the server spawns a `Player` object for a newly connected client.

```csharp
using UnityEngine;

[RequireComponent(typeof(NitroIdentity))]
[RequireComponent(typeof(NitroStatic))]
public partial class ConnectPeers : NitroBehaviour
{
    void Start()
    {
        // Trigger the server-side spawn when this component initializes
        CallSpawnServer();
    }

    /// <summary>
    /// Server-side RPC that spawns a prefab and assigns it to the connecting client.
    /// </summary>
    [NitroRPC(RPC.Server)]
    void SpawnServer()
    {
        var prefab = NitroManager.GetPrefab("Player");
        NitroIdentity identity = prefab.Spawn(Identity.callConn);
    }

    /// <summary>
    /// Client-side RPC. Called by the server to send feedback to the player.
    /// </summary>
    [NitroRPC(RPC.Client)]
    void ConnectToClient()
    {
        Debug.Log("Hello Client");
    }
}
```
### 🟢 OnInstantiated

The `OnInstantiated` method in NitroBehaviour is a virtual callback that is automatically called on all networked scripts (`NitroBehaviour` components) attached to a prefab **immediately after the prefab is instantiated over the network** (either on the server or client).

This method is ideal for initializing logic, setting up references, or triggering custom events right after a networked object appears in the scene.  
Override this method in your own scripts to execute code when your object is spawned by NitroNetwork.

**Example:**
```csharp
public partial class Player : NitroBehaviour
{
    protected override void OnInstantiated()
    {
        // Custom initialization logic for when the player is spawned
        Debug.Log("Player spawned and ready!");
    }
}
```
## 🏠 Dynamic Room Assignment with `NitroRoom.AddIdentity`

In **NitroNetwork**, you are not required to assign a room at the moment of spawning.  
You can dynamically assign a network identity to a room later using the `NitroRoom.AddIdentity()` method.

This is useful for:
- Moving players between lobbies and matches.
- Dynamically organizing instances by region, game mode, etc.
- Deferring room logic until after initialization.

---

### 🧪 Example: Assigning an Identity to a Room After Spawn

```csharp
using UnityEngine;

[RequireComponent(typeof(NitroIdentity))]
[RequireComponent(typeof(NitroStatic))]
public partial class ConnectPeers : NitroBehaviour
{
    NitroIdentity nitroIdentity;

    void Start()
    {
        // Create or fetch a room with the given ID
        NitroRoom nitroRoom = NitroManager.CreateRoom("RoomTest");

        // Add this identity to the room so it becomes visible to all peers in that room
        nitroRoom.AddIdentity(nitroIdentity);
    }
}
```
## 🧭 Room Lifecycle & Default Room Behavior

In **NitroNetwork**, rooms (instances of `NitroRoom`) are responsible for organizing which peers can see which identities. Nitro uses a **room-based architecture**, meaning objects only exist for the clients that are "listening" to a specific room.

---

### 🏗️ Creating a Room That Persists

By default, rooms are automatically destroyed when they no longer have any connected clients.  
If you want to **keep a room alive permanently**, pass `false` as the second parameter:

```csharp
// This room will not be destroyed automatically
NitroRoom nitroRoom = NitroManager.CreateRoom("RoomTest", false);
````
### ⚠️ Important: Identity Transitions Between Rooms

When you assign a `NitroIdentity` to a room using `AddIdentity(identity)`:

- ✅ It will be **spawned** (i.e., become visible and synchronized) for all clients that are currently in the **Target room**.
- ❌ It will be **destroyed** (unspawned) for all clients that were in the **previous room**.
- 🌀 If the identity was not part of any room before, it is automatically assigned to the **default universal room**.

You can retrieve the default room using:

```csharp
NitroRoom defaultRoom = NitroManager.GetFirstRoom();
```
## 🧩 NitroIdentity: Core of Networked Objects

`NitroIdentity` is the key component responsible for synchronizing objects over the network.  
It determines how the object is spawned, who owns it, and in which room it's visible.

---

### 🧱 Static vs Dynamic Identities

Every identity can be either **static** or **dynamic**:

| Property           | Static (`IsStatic = true`)                  | Dynamic (`IsStatic = false`)                      |
|-------------------|---------------------------------------------|--------------------------------------------------|
| Lives in scene?   | Yes, predefined in the scene                | No, instantiated at runtime                      |
| Has connection?   | ❌ No fixed connection                       | ✅ Bound to a connection via `conn`              |
| Spawning?         | Already exists; no spawn required           | Spawned via `Spawn()` method                     |
| Ownership         | Shared across all peers                     | Owned by a specific peer                         |
| Destroy   | Destroys on the server and in the room it belongs to   | Triggered via the `Destroy()` method        |



You can check whether an identity is static via:

```csharp
if (identity.IsStatic)
{
    // Handle static logic
}
```
## 🧭 Manual Room Control with `NitroRoom`

The `NitroRoom` API provides fine-grained control over connection membership, identity visibility, and room lifecycle.

Below is a complete example with line-by-line explanation:

```csharp
NitroRoom nitroRoom = NitroManager.CreateRoom("RoomTest");

// Instantiates all identities currently in the room 
// and subscribes to all events sent by those identities
nitroRoom.JoinRoom(Identity.callConn);

// Adds a network identity to the room and automatically spawns it 
// for all connections currently listening to this room
nitroRoom.AddIdentity(Identity);

// Configures whether the room should be auto-destroyed 
// when there are no more active connections in it
nitroRoom.autoDestroy = false;

// Removes the specified connection from the room,
// and destroys all identities (from that room) for that connection
nitroRoom.LeaveRoom(Identity.callConn);


```
## 🔗 NitroConn: Managing Peer Connections

`NitroConn` represents a single peer connection in NitroNetwork.  
It contains the connection's ID, network endpoint, room memberships, and identities it owns.

### 🧪 Example Usage

```csharp
NitroConn conn = new NitroConn();

// Unique ID of the connection (assigned on creation)
conn.Id;
// Remote IP and port of the peer (UnityEngine.IPEndPoint)
conn.iPEndPoint;
 // Dictionary for storing custom data associated with this connection
conn.customData;
//Ping of the conn, in the server
conn.Ping;
// Associates a network identity with this connection.
// If the connection is lost, the identity is automatically destroyed.
conn.AddIdentity(nitroIdentity);
// Removes a network identity from this connection.
// This is useful for manual cleanup or transferring ownership.
conn.RemoveIdentity(NitroIdentity identity);
// List of rooms that this connection is currently listening to
conn.rooms;
```
## 🧰 NitroManager: Central API for Server, Client, Rooms and Prefabs

`NitroManager` is the main entry point for managing network connections, rooms, and registered prefabs in NitroNetwork.  
It provides static methods for controlling both the server and client lifecycle, as well as managing the game's networking topology.

---

### 🧪 Example Usage

```csharp
//Enable for connections to start automatically.
NitroManager.Instance.Server;
NitroManager.Instance.Client;

// If true, preserves the generated RSA key; if false, generates at runtime and is accepted by the client.
// The first approach is more secure, but you must remove the private key when building for client distribution.
NitroManager.Instance.usePresetRsaKeys;

// Connects in LAN mode (likely initializes the LAN network). Searches for a local server; if none exists, creates one.
NitroManager.Instance.ConnectInLan;

// Disconnects a connection based on the number of messages sent per second.
// This helps prevent speed hackers from exploiting the system. If set to 0, validation is ignored.
NitroManager.msgForDisconnectPeer;

// Connects the client to a server
NitroManager.ConnectClient("127.0.0.1", 7777);

// Starts the server on the specified port
NitroManager.ConnectServer(7777);

// Disconnects the current active client or server
NitroManager.Disconnect();

// Disconnects a specific client by its connection object
NitroManager.DisconnectConn(Identity.conn);

// Removes a specific room instance
NitroManager.RemoveRoom(room);

// Gets the first (default/universal) room
NitroManager.GetFirstRoom();

// Checks whether a room is registered
NitroManager.RoomExists(room);

//Adds a prefab to the list of spawnable prefabs
NitroManager.AddPrefab(NitroIdentity prefab)

// Fetches a prefab by ID (int)
NitroManager.GetPrefab(1);

// Fetches a prefab by name (string)
NitroManager.GetPrefab("Player");

// Returns the current ping (latency in milliseconds) for the connected client.
// Useful for displaying network quality or debugging connection issues.
NitroManager.GetPingClient();

// These are keys used for game encryption
NitroManager.GetPublicKey();
NitroManager.GetPrivateKey();

// Creates a new room with the given ID
var room = NitroManager.CreateRoom("Room1");

// Events triggered on peer connect/disconnect
NitroManager.OnConnectConn;
NitroManager.OnDisconnectConn;

//Events fired on your server and client when you are connected
NitroManager.OnClientConnected;

//Event BandWidth Server and Client
NitroManager.OnBandWidth

//Events Ping Client
NitroManager.OnPingClient;

```
`NitroTransform` is intended solely for prototyping purposes in NitroNetwork.  
While it simplifies the management of networked objects and their transformations, it is not optimized for production use. For robust and scalable implementations, consider creating custom scripts tailored to your project's specific requirements.

