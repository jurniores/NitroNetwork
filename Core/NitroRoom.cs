using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
namespace NitroNetwork.Core
{
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
                NitroLogs.LogError($"Room {Name} does not exist.");
                return false;
            }
            if (peersRoom.TryAdd(conn.Id, conn))
            {
                conn.AddRoom(this);
                SpawnIdentities(conn);
                return true;
            }
            else
            {
                NitroLogs.LogError($"Peer {conn.Id} already in room {Name}");
            }
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
                    identity.Value.SendDestroyForClient(conn, target: Target.Self);
                }

                peersRoom.Remove(conn.Id);
                conn.RemoveRoom(this);
                NitroLogs.Log($"Peer {conn.Id} removed from room {Name}");

                NitroManager.RemoveRoomAuto(this);
                return true;
            }
            return false;
        }
        public bool AddIdentity(NitroIdentity identity)
        {
            if (!peersRoom.ContainsKey(identity.conn.Id))
            {
                NitroLogs.LogError($"Peer {identity.conn.Id} not in room {Name}");
                return false;
            }
            if (!identities.ContainsKey(identity.Id))
            {
                if (identity.room != null)
                {
                    identity.SendDestroyForClient(identity.conn, newRoom: this, target: Target.AllExceptSelf);
                    identity.room.identities.Remove(identity.Id);
                }
                identity.SetRoom(this);
                identity.SendSpawnForClient(identity.conn, newRoom: this, target: Target.AllExceptSelf);
                identities.Add(identity.Id, identity);
                NitroLogs.Log($"Identity {identity.Id} add in room {Name}");
                return true;
            }
            return false;
        }

        internal void DestroyAllIdentities()
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
                NitroLogs.Log($"Identity {identity.Id} already in room {identity.room.Name}");
                return;
            };

            if (identities.TryAdd(identity.Id, identity))
            {
                identity.SetRoom(this);
            }else{
                NitroLogs.Log($"Identity {identity.Id} already in room {Name}");
            }
        }
        internal void SpawnIdentities(NitroConn conn)
        {
            foreach (var identity in identities)
            {
                identity.Value.SendSpawnForClient(conn, Target.Self, newRoom: this);
            }
        }
    }
}