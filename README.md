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

