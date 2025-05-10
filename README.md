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
