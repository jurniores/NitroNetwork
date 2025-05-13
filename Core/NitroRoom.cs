using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace NitroNetwork.Core
{
    /// <summary>
    /// Represents a room in the NitroNetwork system.
    /// A room manages a group of peers (connections) and their associated network identities.
    /// It provides methods for joining/leaving peers, adding identities, and synchronizing state.
    /// </summary>
    public class NitroRoom
    {
        /// <summary>
        /// The name of the room.
        /// </summary>
        public string Name;

        /// <summary>
        /// The unique identifier of the room.
        /// </summary>
        public int Id;

        /// <summary>
        /// Dictionary of peers (connections) currently in the room.
        /// Key: Connection ID, Value: NitroConn instance.
        /// </summary>
        public Dictionary<int, NitroConn> peersRoom = new();

        /// <summary>
        /// Dictionary of network identities currently in the room.
        /// Key: Identity ID, Value: NitroIdentity instance.
        /// </summary>
        public Dictionary<ushort, NitroIdentity> identities = new();

        /// <summary>
        /// If true, the room will be automatically destroyed when empty.
        /// </summary>
        public bool autoDestroy;

        /// <summary>
        /// Adds a peer (connection) to the room.
        /// Spawns all current identities for the new peer.
        /// </summary>
        /// <param name="conn">The connection to add.</param>
        /// <returns>True if the peer was added, false otherwise.</returns>
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

        /// <summary>
        /// Removes a peer (connection) from the room.
        /// Destroys all identities owned by this peer and notifies others.
        /// </summary>
        /// <param name="conn">The connection to remove.</param>
        /// <returns>True if the peer was removed, false otherwise.</returns>
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

        internal void RemoveConn(NitroConn conn)
        {
            if (peersRoom.ContainsKey(conn.Id))
            {
                peersRoom.Remove(conn.Id);
                conn.RemoveRoom(this);
            }
            else
            {
                NitroLogs.LogError($"Peer {conn.Id} not in room {Name}");
            }
        }

        /// <summary>
        /// Adds a network identity to the room.
        /// If the identity is already in another room, it is moved here.
        /// Notifies all peers in the room about the new identity.
        /// </summary>
        /// <param name="identity">The identity to add.</param>
        /// <returns>True if the identity was added, false otherwise.</returns>
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
                    identity.SendDestroyForClient(identity.conn, newRoom: this, target: Target.ExceptSelf);
                    identity.room.identities.Remove(identity.Id);
                }
                identity.SetRoom(this);
                identity.SendSpawnForClient(identity.conn, newRoom: this, target: Target.ExceptSelf);
                identities.Add(identity.Id, identity);
                NitroLogs.Log($"Identity {identity.Id} add in room {Name}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Destroys all identities and removes all peers from the room.
        /// </summary>
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

        /// <summary>
        /// Sets the room for a given identity, if not already set.
        /// </summary>
        /// <param name="identity">The identity to set.</param>
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
            }
            else
            {
                NitroLogs.Log($"Identity {identity.Id} already in room {Name}");
            }
        }

        /// <summary>
        /// Spawns all identities in the room for a specific peer (connection).
        /// Used when a new peer joins the room.
        /// </summary>
        /// <param name="conn">The connection to send spawn messages to.</param>
        internal void SpawnIdentities(NitroConn conn)
        {
            foreach (var identity in identities)
            {
                identity.Value.SendSpawnForClient(conn, Target.Self, newRoom: this);
            }
        }
    }
}