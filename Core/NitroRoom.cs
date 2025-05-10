using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public class NitroRoom
{
    public string Name;
    public int Id;
    public Dictionary<int, NitroConn> peersRoom = new();
    public Dictionary<ushort, NitroIdentity> identities = new();
    public bool autoDestroy;

    public bool JoinRoom(NitroConn conn)
    {
        if (!NitroManager.RoomExists(this))
        {
            UnityEngine.Debug.LogError($"Room {Name} does not exist.");
            return false;
        }
        if (peersRoom.TryAdd(conn.Id, conn))
        {
            conn.AddRoom(this);
            SpawnIdentities(conn);
            UnityEngine.Debug.Log($"Peer {conn.Id} added to room {Name}");
            return true;
        };
        return false;
    }
    public bool LeaveRoom(NitroConn conn)
    {
        if (peersRoom.ContainsKey(conn.Id))
        {
            for (int i = 0; i < identities.Count; i++)
            {
                var identity = identities.ElementAt(i);
                if (identity.Value.conn.Id == conn.Id)
                {
                    identity.Value.Destroy();
                    identities.Remove(identity.Key);
                    continue;
                }
                identity.Value.SendDestroyForClient(conn, room: this, target: Target.Self);
            }

            peersRoom.Remove(conn.Id);
            conn.RemoveRoom(this);
            UnityEngine.Debug.Log($"Peer {conn.Id} removed from room {Name}");

            NitroManager.RemoveRoomAuto(this);
            return true;
        }
        return false;
    }

    public bool AddIdentity(NitroIdentity identity)
    {
        if (identities.TryAdd(identity.Id, identity))
        {
            if (identity.room != null)
            {
                identity.SendDestroyForClient(identity.conn, room: identity.room, target: Target.AllExceptSelf);
                identity.room.identities.Remove(identity.Id);
            }
            identity.room = this;
            identity.SendSpawnForClient(identity.conn, room: this, target: Target.AllExceptSelf);
            UnityEngine.Debug.Log($"Identity {identity.Id} add in room {Name}");
            return true;
        }
        return false;
    }

    public void SpawnIdentity(NitroIdentity identity, NitroConn conn)
    {
        if (identities.TryGetValue(identity.Id, out NitroIdentity nitroIdentity))
        {
            nitroIdentity.SendSpawnForClient(conn, room: this);
        }
    }
    public void DestroyAllIdentities()
    {

        foreach (var identity in identities)
        {
            identity.Value.Destroy();
        }
        for (int i = peersRoom.Count - 1; i >= 0; i--)
        {
            var peer = peersRoom.ElementAt(i);
            LeaveRoom(peer.Value);
        }
        peersRoom.Clear();
        identities.Clear();
    }
    internal void SetIdentity(NitroIdentity identity)
    {
        if (identity.room != null)
        {
            UnityEngine.Debug.Log($"Identity {identity.Id} already in room {identity.room.Name}");
            return;
        };

        if (identities.TryAdd(identity.Id, identity))
        {
            identity.room = this;
            UnityEngine.Debug.Log($"Identity {identity.Id} add in room {Name}");
        }
    }
    internal void SpawnIdentities(NitroConn conn)
    {
        foreach (var identity in identities)
        {
            identity.Value.SendSpawnForClient(conn, Target.Self);
        }
    }
}