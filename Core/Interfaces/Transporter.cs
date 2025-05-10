using System;


public interface Transporter
{
    public void ServerConnect(string ip, int port);
    public void ClientConnect(string ip, int port);
    public void DisconnectServer();
    public void DisconnectClient();
    public void DisconnectPeer(int peerId);
    public void Send(int peerId, Span<byte> msg, DeliveryMode deliveryMethod, byte channel, bool IsServer);
    public event Action<NitroConn, bool> OnConnected;
    public event Action<int, bool> OnDisconnected;
    public event Action<string> OnError;
    public event Action<NitroConn, bool> IPConnection;

}
