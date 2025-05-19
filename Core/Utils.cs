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
        SendAES = 254,
        CriptAes = 253,
        Connecting = 255,
        Connected = 252,
        SpawnRPC = 255,
        SpawnIdentity = 65535,
        DespawnIdentity = 253,

    }
    public class AesResult
    {
        public byte[] EncryptedData;
        public byte[] IV;
    }

}