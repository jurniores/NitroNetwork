using System;
using NitroNetwork.Core;
using UnityEngine;

[RequireComponent(typeof(NitroStatic))]
public partial class NitroSpawn : NitroBehaviour
{
    [SerializeField]
    private NitroIdentity _identity;
    void Start()
    {
        NitroManager.OnConnectConn += OnConnect;
    }

    private void OnConnect(NitroConn conn)
    {
        _identity.Spawn(conn);
    }
}
