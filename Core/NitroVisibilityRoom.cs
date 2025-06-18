using NitroNetwork.Core;
using UnityEngine;

public partial class NitroVisibilityRoom : NitroBehaviour
{
    [Tooltip("To procedurally change the room, use the RoomId property in real-time within the script")]
    [SerializeField]
    private string roomId;
    private NitroRoom room;
    public string RoomId { get => roomId; set { roomId = value; SetInRoom(); } }

    void Start()
    {
        if (IsServer)
            SetInRoom();
    }
    void SetInRoom()
    {
        room = NitroManager.GetRoom(RoomId);
        if (room == null)
        {
            room = NitroManager.CreateRoom(RoomId);
            if (!Identity.conn.rooms.ContainsKey(RoomId))
            {
                room.JoinRoom(Identity.conn);
            }
            room.AddIdentity(Identity);
        }
        else
        {
            if (!Identity.conn.rooms.ContainsKey(RoomId))
            {
                room.JoinRoom(Identity.conn);
            }
            room.AddIdentity(Identity);
        }
    }

    void OnDestroy()
    {
        if (IsServer)
        {
            if (room != null)
            {
                room?.identities.Remove(Identity.Id);
                Identity.conn.AddIdentityVisibility(room, -1);
            }
        }
    }
}