using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections;

namespace NitroNetwork.Core
{
    public class LiteTransporter : MonoBehaviour, INetEventListener, INetLogger, Transporter
    {
        [ReadOnly]
        [SerializeField]
        private string guidConnect = "";

        private NetManager _netServer, _netClient;
        private NitroManager nitroManager;
        public event Action<byte[], int, bool> OnMessage;
        public event Action<NitroConn, bool> OnConnected;
        public event Action<int, bool> OnDisconnected;
        public event Action<string> OnError;
        public event Action<NitroConn, bool> IPConnection;
        public bool iPv6Enabled = false;
        [Range(0, 10)]
        public float timeWaitConnectLan = 2f;
        [Range(0, 360)]
        public int disconnectedTimeoutSeconds = 15;
        [Range(0, 255)]
        public byte channels = 5;
        [Range(0, 1000)]
        public int MaxEventPerFrame = 0;
        [Header("Others Settings")]
        public bool SimulateLatency;
        public bool SimulatePacketLoss;
        [Range(0, 1000)]
        public int minLatence;
        [Range(0, 1000)]
        public int maxLatence;
        [Range(0, 1000)]
        public int SimulationPacketLossChance;



        int portServer = 0;


        void Start()
        {

            nitroManager = GetComponent<NitroManager>();
        }

        private void Reset()
        {
            guidConnect = Guid.NewGuid().ToString();
        }

        public void ServerConnect(string ip, int port)
        {
            try
            {
                portServer = port;
                NetDebug.Logger = this;
                _netServer = new NetManager(this)
                {
                    IPv6Enabled = iPv6Enabled,
                    BroadcastReceiveEnabled = true,
                    UpdateTime = 1,
                    DisconnectTimeout = disconnectedTimeoutSeconds * 1000,
                    ChannelsCount = (byte)(channels + 1),
                    SimulateLatency = SimulateLatency,
                    SimulationMinLatency = minLatence,
                    SimulationMaxLatency = maxLatence,
                    SimulatePacketLoss = SimulatePacketLoss,
                    SimulationPacketLossChance = SimulationPacketLossChance
                    
                };
                _netServer.Start(port);

                NitroConn conn = new NitroConn();
                conn.Id = -1;
                conn.iPEndPoint = new IPEndPoint(IPAddress.Parse(ip), portServer);
                IPConnected(conn, true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LiteTransporter] Error starting server: {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }
        void InfoClient(int port)
        {
            portServer = port;
            _netClient = new NetManager(this)
            {
                DisconnectTimeout = disconnectedTimeoutSeconds * 1000,
                ChannelsCount = (byte)(channels + 1),
                SimulateLatency = SimulateLatency,
                SimulationMinLatency = minLatence,
                SimulationMaxLatency = maxLatence,
                SimulatePacketLoss = SimulatePacketLoss,
                IPv6Enabled = iPv6Enabled,
                SimulationPacketLossChance = SimulationPacketLossChance,
                UnconnectedMessagesEnabled = true,
                UpdateTime = 1
            };
            _netClient.Start();
            _netClient.PingInterval = 1000; // Set ping interval to 1 second
        }
        public void ClientConnectLan(int port, Action actionLanValidation)
        {
            InfoClient(port);
            _netClient.SendBroadcast(new byte[] { 1 }, port);
            StartCoroutine(WaitAndInvoke());

            IEnumerator WaitAndInvoke()
            {
                yield return new WaitForSeconds(timeWaitConnectLan);
                actionLanValidation?.Invoke();
            }

        }
        public void ClientConnect(string ip, int port)
        {
            InfoClient(port);
            _netClient.Connect(ip, port, guidConnect);
        }
        private void Update()
        {
            _netServer?.PollEvents(MaxEventPerFrame);
            _netClient?.PollEvents(MaxEventPerFrame);
        }
        private void OnDestroy()
        {
            NetDebug.Logger = null;
            if (_netServer != null)
                _netServer.Stop();
        }

        void IPConnected(NitroConn endPoint, bool IsRemote)
        {
            IPConnection?.Invoke(endPoint, IsRemote);
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {

            NitroConn conn = new NitroConn();
            conn.Id = peer.Id;
            conn.iPEndPoint = new IPEndPoint(peer.Address, peer.Port);
            OnConnected?.Invoke(conn, peer.Port != portServer);
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
        {
            OnError?.Invoke(socketErrorCode.ToString());
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
            UnconnectedMessageType messageType)
        {
            if (messageType == UnconnectedMessageType.Broadcast)
            {
                NetDataWriter resp = new NetDataWriter();
                resp.Put(1);
                _netServer.SendUnconnectedMessage(resp, remoteEndPoint);
            }

            if (messageType == UnconnectedMessageType.BasicMessage && _netClient.ConnectedPeersCount == 0 && reader.GetInt() == 1)
            {
                StopAllCoroutines();

                _netClient.Connect(remoteEndPoint, guidConnect);
            }
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            request.AcceptIfKey(guidConnect);
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            OnDisconnected?.Invoke(peer.Id, peer.Port != portServer);
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            nitroManager.ReceiveMessage(reader.GetRemainingBytesSpan(), peer.Ping, peer.Id, peer.Port != portServer);
        }

        void INetLogger.WriteNet(NetLogLevel level, string str, params object[] args)
        {
            Debug.LogFormat(str, args);
        }

        public void DisconnectServer()
        {
            if (_netServer != null)
            {
                _netServer.Stop();
                _netServer = null;
            }
        }

        public void DisconnectClient()
        {
            if (_netClient != null)
            {
                _netClient.Stop();
                _netClient = null;
            }
        }
        public void Disconnect()
        {
            DisconnectClient();
            DisconnectServer();
        }
        public void DisconnectPeer(int peerId)
        {
            if (_netServer.GetPeerById(peerId) != null)
            {
                _netServer.DisconnectPeer(_netServer.GetPeerById(peerId));
            }
        }
        public int GetPingClient()
        {
            if (_netClient != null)
            {
                return _netClient.FirstPeer?.Ping ?? 0;
            }
            return 0;
        }
        public int GetMyPing(int id)
        {
            if (_netServer != null)
            {
                var peer = _netServer.GetPeerById(id);
                if (peer != null)
                {
                    return peer.Ping;
                }
            }
            return 0;
        }

        public void Send(int peerId, Span<byte> msg, DeliveryMode deliveryMethod, byte channel, bool IsServer)
        {
            if (IsServer)
            {
                if (peerId == -1) return;

                var peer = _netServer.GetPeerById(peerId);
                if (peer == null)
                {
                    Debug.LogError("[LiteTransporter] Peer not found");
                    return;
                }
                peer.Send(msg, channel, GetDeliveryMethod(deliveryMethod));
            }
            else
            {
                _netClient.FirstPeer?.Send(msg, channel, GetDeliveryMethod(deliveryMethod));
            }
        }
        private DeliveryMethod GetDeliveryMethod(DeliveryMode DeliveryMode)
        {
            return DeliveryMode switch
            {
                DeliveryMode.ReliableOrdered => DeliveryMethod.ReliableOrdered,
                DeliveryMode.ReliableSequenced => DeliveryMethod.ReliableSequenced,
                DeliveryMode.ReliableUnordered => DeliveryMethod.ReliableUnordered,
                DeliveryMode.Unreliable => DeliveryMethod.Unreliable,
                DeliveryMode.Sequenced => DeliveryMethod.Sequenced,
                _ => throw new NotImplementedException(
                     $"[LiteTransporter] Unsupported delivery mode '{DeliveryMode}'"),
            };
        }
    }

}