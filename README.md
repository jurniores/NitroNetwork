# 🔥 NitroNetwork - Unity Networking Framework

**NitroNetwork** is a lightweight and fast networking solution for Unity, designed to simplify multiplayer development while offering flexibility and control for both client and server behaviors.

This repository contains an example implementation of a basic connection flow between client and server using `NitroBehaviour`, `NitroStatic`, and `NitroRPC`.

---

## 📂 Example: `ConnectPeers.cs`

This script demonstrates how to automatically connect a client to the server when the scene starts. It also shows how to use RPCs to send messages between peers.

### ✅ Features:
- Uses `[NitroRPC(NitroType.Server)]` to call a server-only method from the client.
- Ensures the object exists in all peers using `NitroStatic`.
- Inherits from `NitroBehaviour` to enable network features.

### 📄 Script

```csharp
using UnityEngine;
[RequireComponent(typeof(NitroIdentity))]
[RequireComponent(typeof(NitroStatic))]
public partial class ConnectPeers : NitroBehaviour
{
    void Start()
    {
        CallConnectToServer(); // This will send an RPC to the server.
    }

    // RPC method that will be executed on the server when called by a client.
    [NitroRPC(NitroType.Server)]
    void ConnectToServer()
    {
        Debug.Log("ConnectToServer");
        CallConnectToClient();
    }
    [NitroRPC(NitroType.Client)]
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

[NitroRPC(NitroType.Server)]
void MethodOfServer()
{
    print("Hello Server");
}

[NitroRPC(NitroType.Client)]
void MethodOfClient()
{
    Debug.Log("Hello Client");
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
    [NitroRPC(NitroType.Server)]
    void SpawnServer()
    {
        var prefab = NitroManager.GetPrefab("Player");
        NitroIdentity identity = prefab.Spawn(Identity.callConn);
    }

    /// <summary>
    /// Client-side RPC. Called by the server to send feedback to the player.
    /// </summary>
    [NitroRPC(NitroType.Client)]
    void ConnectToClient()
    {
        Debug.Log("Hello Client");
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

- ✅ It will be **spawned** (i.e., become visible and synchronized) for all clients that are currently in the **target room**.
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

