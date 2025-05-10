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

- Your prefab must be registered via `NitroManager.RegisterPrefab()`.
- The prefab must contain a `NitroIdentity` component.
- To spawn the object for a specific client, pass `Identity.conn` as the owner.

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
        // Get the registered player prefab by name
        var prefab = NitroManager.GetPrefab("Player");

        // Spawn the object and assign it to this client's connection
        NitroIdentity identity = prefab.Spawn(Identity.conn);

        // Optional: spawn in a specific room
        // NitroIdentity identity = prefab.Spawn(Identity.conn, "room-1");
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

