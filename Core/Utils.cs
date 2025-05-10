using UnityEngine;

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
    AllExceptSelf,
    Self
}

internal enum NitroCommands
{
    GetConnection = 65334,
    Connected = 255,
    SpawnRPC = 255,
    SpawnIdentity = 65535,
    DespawnIdentity = 254,
  
}
