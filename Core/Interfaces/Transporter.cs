using System;
using NitroNetwork.Core;


public interface Transporter
{
    public void ServerConnect(string ip, int port);
    public void ClientConnect(string ip, int port);
    public void ClientConnectLan(int port, Action actionanValidation);
    public void DisconnectServer();
    public void DisconnectClient();
    public void DisconnectPeer(int peerId);
    public int GetMyPing(int id);
    public int GetPingClient();
    public void Send(int peerId, Span<byte> msg, DeliveryMode deliveryMethod, byte channel, bool IsServer);
    public event Action<NitroConn, bool> OnConnected;
    public event Action<int, bool> OnDisconnected;
    public event Action<string> OnError;
    public event Action<NitroConn, bool> IPConnection;

}
