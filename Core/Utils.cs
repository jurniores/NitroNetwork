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
        ConfigsManager = 65535,
        Connecting         = 255,
        SendAES            = 254,
        CriptAes           = 253,
        Connected          = 252,
        SpawnRPC           = 251,
        DespawnIdentity    = 250,
        Ping               = 249, 
        NetVar             = 248,
            

    }
    public class AesResult
    {
        public byte[] EncryptedData;
        public byte[] IV;
    }
    public struct NitroBandWidth
    {
        public int BClientSent;
        public int BClientReceived;
        public int BServerSent;
        public int BServerReceived;
        public int PacketSentClient;
        public int PacketReceivedClient;
        public int PacketSentServer;
        public int PacketReceivedServer;
    }

}