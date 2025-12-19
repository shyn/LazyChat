using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LazyChat.Models;
using LazyChat.Services.Interfaces;
using LazyChat.Exceptions;

namespace LazyChat.Services
{
    /// <summary>
    /// Peer discovery service using UDP broadcast for automatic peer detection
    /// </summary>
    public class PeerDiscoveryService : IPeerDiscoveryService
    {
        private const int DISCOVERY_PORT = 8888;
        private const int BROADCAST_INTERVAL = 5000;
        private const int PEER_TIMEOUT = 15000;

        private UdpClient _udpClient;
        private Thread _listenThread;
        private Thread _broadcastThread;
        private bool _isRunning;
        private readonly object _peersLock = new object();
        private Dictionary<string, PeerInfo> _discoveredPeers;
        private PeerInfo _localPeer;
        private readonly ILogger _logger;
        private readonly INetworkAdapter _networkAdapter;

        public event EventHandler<PeerInfo> PeerDiscovered;
        public event EventHandler<PeerInfo> PeerLeft;
        public event EventHandler<string> ErrorOccurred;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Creates a new peer discovery service
        /// </summary>
        /// <param name="userName">Local user name</param>
        /// <param name="listeningPort">Port for P2P communication</param>
        /// <param name="logger">Logger instance (optional)</param>
        /// <param name="networkAdapter">Network adapter (optional, for testing)</param>
        public PeerDiscoveryService(
            string userName, 
            int listeningPort, 
            ILogger logger = null,
            INetworkAdapter networkAdapter = null)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("User name cannot be empty", nameof(userName));

            if (listeningPort <= 0 || listeningPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(listeningPort), "Port must be between 1 and 65535");

            _logger = logger ?? new Infrastructure.FileLogger();
            _networkAdapter = networkAdapter ?? new Infrastructure.NetworkAdapter();
            _discoveredPeers = new Dictionary<string, PeerInfo>();
            
            _localPeer = new PeerInfo
            {
                UserName = userName,
                Port = listeningPort,
                IpAddress = _networkAdapter.GetLocalIPAddress()
            };

            _logger.LogInfo($"PeerDiscoveryService initialized for user '{userName}' on port {listeningPort}");
        }

        public void Start()
        {
            if (_isRunning)
            {
                _logger.LogWarning("Discovery service already running");
                return;
            }

            try
            {
                _isRunning = true;
                _udpClient = new UdpClient(DISCOVERY_PORT);
                _udpClient.EnableBroadcast = true;

                _listenThread = new Thread(ListenForPeers)
                {
                    IsBackground = true,
                    Name = "PeerDiscovery-Listen"
                };
                _listenThread.Start();

                _broadcastThread = new Thread(BroadcastPresence)
                {
                    IsBackground = true,
                    Name = "PeerDiscovery-Broadcast"
                };
                _broadcastThread.Start();

                _logger.LogInfo("Peer discovery service started successfully");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.LogError("Failed to start discovery service", ex);
                OnErrorOccurred("启动发现服务失败: " + ex.Message);
                throw new PeerDiscoveryException("Failed to start discovery service", ex);
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _logger.LogInfo("Stopping peer discovery service");
            _isRunning = false;

            SendGoodbyeMessage();

            try
            {
                if (_udpClient != null)
                {
                    _udpClient.Close();
                    _udpClient = null;
                }

                if (_listenThread != null && _listenThread.IsAlive)
                {
                    _listenThread.Join(1000);
                }

                if (_broadcastThread != null && _broadcastThread.IsAlive)
                {
                    _broadcastThread.Join(1000);
                }

                _logger.LogInfo("Peer discovery service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error stopping discovery service", ex);
            }
        }

        private void ListenForPeers()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _logger.LogDebug("Listen thread started");

            while (_isRunning)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref remoteEndPoint);
                    NetworkMessage message = NetworkMessage.Deserialize(data);

                    if (message.SenderId == _localPeer.PeerId)
                        continue;

                    _logger.LogDebug($"Received discovery message from {message.SenderName} ({remoteEndPoint.Address})");
                    ProcessDiscoveryMessage(message, remoteEndPoint.Address);
                }
                catch (SocketException)
                {
                    if (_isRunning)
                        continue;
                    break;
                }
                catch (MessageSerializationException ex)
                {
                    _logger.LogError("Failed to deserialize discovery message", ex);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        _logger.LogError("Error receiving discovery message", ex);
                        OnErrorOccurred("接收发现消息失败: " + ex.Message);
                    }
                }
            }

            _logger.LogDebug("Listen thread stopped");
        }

        private void ProcessDiscoveryMessage(NetworkMessage message, IPAddress senderIp)
        {
            lock (_peersLock)
            {
                PeerInfo peer = null;
                bool isNewPeer = false;

                if (_discoveredPeers.ContainsKey(message.SenderId))
                {
                    peer = _discoveredPeers[message.SenderId];
                    peer.LastSeen = DateTime.Now;
                }
                else if (message.Type == MessageType.Discovery)
                {
                    peer = new PeerInfo
                    {
                        PeerId = message.SenderId,
                        UserName = message.SenderName,
                        IpAddress = senderIp,
                        Port = int.Parse(message.TextContent),
                        LastSeen = DateTime.Now,
                        IsOnline = true
                    };
                    _discoveredPeers[message.SenderId] = peer;
                    isNewPeer = true;
                    _logger.LogInfo($"New peer discovered: {peer.UserName} ({peer.IpAddress}:{peer.Port})");
                }

                if (message.Type == MessageType.Discovery && peer != null)
                {
                    SendDiscoveryResponse(senderIp);

                    if (isNewPeer)
                    {
                        OnPeerDiscovered(peer);
                    }
                }
                else if (message.Type == MessageType.DiscoveryResponse && isNewPeer && peer != null)
                {
                    OnPeerDiscovered(peer);
                }
                else if (message.Type == MessageType.UserLeft && peer != null)
                {
                    _discoveredPeers.Remove(message.SenderId);
                    peer.IsOnline = false;
                    _logger.LogInfo($"Peer left: {peer.UserName} ({peer.IpAddress})");
                    OnPeerLeft(peer);
                }
            }
        }

        private void BroadcastPresence()
        {
            _logger.LogDebug("Broadcast thread started");

            while (_isRunning)
            {
                try
                {
                    NetworkMessage message = new NetworkMessage
                    {
                        Type = MessageType.Discovery,
                        SenderId = _localPeer.PeerId,
                        SenderName = _localPeer.UserName,
                        TextContent = _localPeer.Port.ToString()
                    };

                    byte[] data = message.Serialize();
                    IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                    _udpClient.Send(data, data.Length, broadcastEndPoint);

                    CheckPeerTimeouts();

                    Thread.Sleep(BROADCAST_INTERVAL);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        _logger.LogError("Broadcast failed", ex);
                        OnErrorOccurred("广播失败: " + ex.Message);
                    }
                }
            }

            _logger.LogDebug("Broadcast thread stopped");
        }

        private void SendDiscoveryResponse(IPAddress targetIp)
        {
            try
            {
                NetworkMessage message = new NetworkMessage
                {
                    Type = MessageType.DiscoveryResponse,
                    SenderId = _localPeer.PeerId,
                    SenderName = _localPeer.UserName,
                    TextContent = _localPeer.Port.ToString()
                };

                byte[] data = message.Serialize();
                IPEndPoint endPoint = new IPEndPoint(targetIp, DISCOVERY_PORT);
                _udpClient.Send(data, data.Length, endPoint);
                _logger.LogDebug($"Sent discovery response to {targetIp}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send discovery response to {targetIp}", ex);
            }
        }

        private void SendGoodbyeMessage()
        {
            try
            {
                NetworkMessage message = new NetworkMessage
                {
                    Type = MessageType.UserLeft,
                    SenderId = _localPeer.PeerId,
                    SenderName = _localPeer.UserName
                };

                byte[] data = message.Serialize();
                IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                _udpClient.Send(data, data.Length, broadcastEndPoint);
                _logger.LogDebug("Sent goodbye message");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send goodbye message", ex);
            }
        }

        private void CheckPeerTimeouts()
        {
            lock (_peersLock)
            {
                List<string> timeoutPeers = new List<string>();
                DateTime now = DateTime.Now;

                foreach (var kvp in _discoveredPeers)
                {
                    if ((now - kvp.Value.LastSeen).TotalMilliseconds > PEER_TIMEOUT)
                    {
                        timeoutPeers.Add(kvp.Key);
                    }
                }

                foreach (string peerId in timeoutPeers)
                {
                    PeerInfo peer = _discoveredPeers[peerId];
                    _discoveredPeers.Remove(peerId);
                    peer.IsOnline = false;
                    _logger.LogInfo($"Peer timeout: {peer.UserName} ({peer.IpAddress})");
                    OnPeerLeft(peer);
                }
            }
        }

        public List<PeerInfo> GetOnlinePeers()
        {
            lock (_peersLock)
            {
                return _discoveredPeers.Values.ToList();
            }
        }

        public PeerInfo GetLocalPeer()
        {
            return _localPeer;
        }

        protected virtual void OnPeerDiscovered(PeerInfo peer)
        {
            PeerDiscovered?.Invoke(this, peer);
        }

        protected virtual void OnPeerLeft(PeerInfo peer)
        {
            PeerLeft?.Invoke(this, peer);
        }

        protected virtual void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        public void Dispose()
        {
            Stop();
            _logger?.LogInfo("PeerDiscoveryService disposed");
        }
    }
}
