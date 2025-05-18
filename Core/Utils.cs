#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace NitroNetwork.Core
{
    public enum DeliveryMode : byte
    {
        ReliableOrdered,
        Unreliable,
        ReliableUnordered,
        Sequenced,
        ReliableSequenced
    }

    public enum Target
    {
        All,
        ExceptSelf,
        Self,
    }

    internal enum NitroCommands
    {
        GetConnection = 65334,
        Connected = 255,
        SpawnRPC = 255,
        SpawnIdentity = 65535,
        DespawnIdentity = 254,

    }
}