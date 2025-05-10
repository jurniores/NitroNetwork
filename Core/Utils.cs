using UnityEngine;
public static class Utils
{

    public static string GetTypeName(object obj)
    {
        return obj.GetType().Name;
    }

    public static void Spawn2(this NitroIdentity identity, NitroConn conn)
    {
        
    }
  
}

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
