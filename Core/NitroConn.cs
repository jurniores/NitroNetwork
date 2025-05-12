using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace NitroNetwork.Core
{
    public class NitroConn
    {
        public int Id; // Unique identifier for the connection
        public IPEndPoint iPEndPoint; // IP endpoint associated with this connection

        //public Action<Span<byte>, DeliveryMode, byte> Send; // Delegate for sending data through this connection
        public Dictionary<string, NitroRoom> rooms = new(); // Dictionary of rooms associated with this connection
        internal Dictionary<int, NitroIdentity> identitiesOnDestroy = new(); // Dictionary of identities to be destroyed when the connection is terminated

        /// <summary>
        /// Adds an identity to the list of identities associated with this connection.
        /// </summary>
        /// <param name="identity">The identity to add.</param>
        public void AddIdentity(NitroIdentity identity)
        {
            identitiesOnDestroy.Add(identity.Id, identity);
        }

        /// <summary>
        /// Adds a room to the list of rooms associated with this connection.
        /// </summary>
        /// <param name="room">The room to add.</param>
        /// <returns>True if the room was successfully added; otherwise, false.</returns>
        internal bool AddRoom(NitroRoom room)
        {
            if (rooms.TryAdd(room.Name, room))
            {
                return true;
            }
            else
            {
                NitroLogs.LogWarning($"Failed to add room {room.Name} to peer {Id}");
            }
            return false;
        }

        /// <summary>
        /// Removes all rooms associated with this connection.
        /// </summary>
        internal void LeaveAllRooms()
        {
            for (int i = rooms.Values.Count - 1; i >= 0; i--)
            {
                var room = new List<NitroRoom>(rooms.Values)[i];
                room.LeaveRoom(this);
            }
            rooms.Clear();
        }

        /// <summary>
        /// Removes a specific room from the list of rooms associated with this connection.
        /// </summary>
        /// <param name="room">The room to remove.</param>
        /// <returns>True if the room was successfully removed; otherwise, false.</returns>
        internal bool RemoveRoom(NitroRoom room)
        {
            if (rooms.Remove(room.Name))
            {
                return true;
            }
            NitroLogs.LogWarning($"Failed to remove room {room.Name} from peer {Id}");
            return false;
        }

        /// <summary>
        /// Destroys all identities associated with this connection.
        /// </summary>
        internal void DestroyAllIdentities()
        {
            foreach (var identity in identitiesOnDestroy)
            {
                identity.Value.Destroy();
            }
            identitiesOnDestroy.Clear();
        }
    }
}