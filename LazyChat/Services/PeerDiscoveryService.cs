using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.IO;
using LazyChat.Models;
using LazyChat.Services.Interfaces;
using LazyChat.Exceptions;

namespace LazyChat.Services
{
    /// <summary>
    /// Peer discovery service using UDP broadcast for automatic peer detection
    /// Enhanced with multi-interface broadcasting and peer caching for VPN/complex network scenarios
    /// </summary>
    public class PeerDiscoveryService : IPeerDiscoveryService
    {
        private const int DISCOVERY_PORT = 8888;
        private const int BROADCAST_INTERVAL = 5000;
        private const int PEER_TIMEOUT = 15000;
        private const int UNICAST_PROBE_INTERVAL = 10000; // Probe cached peers every 10s

        private UdpClient _udpClient;
        private List<UdpClient> _broadcastClients; // One per interface
        private List<NetworkInterfaceInfo> _activeInterfaces;
        private Thread _listenThread;
        private Thread _broadcastThread;
        private bool _isRunning;
        private readonly object _peersLock = new object();
        private Dictionary<string, PeerInfo> _discoveredPeers;
        private Dictionary<string, CachedPeerAddress> _peerCache; // Persistent cache
        private PeerInfo _localPeer;
        private readonly ILogger _logger;
        private readonly INetworkAdapter _networkAdapter;
        private string _cacheFilePath;

        public event EventHandler<PeerInfo> PeerDiscovered;
        public event EventHandler<PeerInfo> PeerLeft;
        public event EventHandler<string> ErrorOccurred;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Represents a network interface with its broadcast address
        /// </summary>
        private class NetworkInterfaceInfo
        {
            public IPAddress LocalAddress { get; set; }
            public IPAddress BroadcastAddress { get; set; }
            public string InterfaceName { get; set; }
        }

        /// <summary>
        /// Cached peer address for fallback probing
        /// </summary>
        [Serializable]
        private class CachedPeerAddress
        {
            public string PeerId { get; set; }
            public string UserName { get; set; }
            public string IpAddress { get; set; }
            public int Port { get; set; }
            public DateTime LastSeen { get; set; }
        }

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
            _peerCache = new Dictionary<string, CachedPeerAddress>();
            _broadcastClients = new List<UdpClient>();
            _activeInterfaces = new List<NetworkInterfaceInfo>();
            
            _localPeer = new PeerInfo
            {
                UserName = userName,
                Port = listeningPort,
                IpAddress = _networkAdapter.GetLocalIPAddress()
            };

            // Setup cache file path
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LazyChat");
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            _cacheFilePath = Path.Combine(appDataPath, "peer_cache.dat");

            LoadPeerCache();

            _logger.LogInfo($"PeerDiscoveryService initialized for user '{userName}' on port {listeningPort}");
            _logger.LogInfo($"Primary local IP: {_localPeer.IpAddress}");
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

                // Discover all usable network interfaces
                DiscoverNetworkInterfaces();

                // listener socket (binds to all interfaces)
                _udpClient = new UdpClient(DISCOVERY_PORT)
                {
                    EnableBroadcast = true
                };
                // Enable address reuse for multi-instance scenarios
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // Create dedicated sender sockets, one per interface
                CreateBroadcastClients();

                // Probe cached peers immediately on startup
                ProbeKnownPeers();

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

                _logger.LogInfo($"Peer discovery service started on {_activeInterfaces.Count} interface(s)");
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

                if (_broadcastClients != null)
                {
                    foreach (var client in _broadcastClients)
                    {
                        try { client?.Close(); } catch { }
                    }
                    _broadcastClients.Clear();
                }

                if (_listenThread != null && _listenThread.IsAlive)
                {
                    _listenThread.Join(1000);
                }

                if (_broadcastThread != null && _broadcastThread.IsAlive)
                {
                    _broadcastThread.Join(1000);
                }

                SavePeerCache();

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
            PeerInfo discoveredPeer = null;
            PeerInfo leftPeer = null;
            bool shouldRespond = false;

            lock (_peersLock)
            {
                PeerInfo peer = null;
                bool isNewPeer = false;

                if (_discoveredPeers.ContainsKey(message.SenderId))
                {
                    peer = _discoveredPeers[message.SenderId];
                    peer.LastSeen = DateTime.Now;
                }
                else if (message.Type == MessageType.Discovery || message.Type == MessageType.DiscoveryResponse)
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

                    // Update cache
                    UpdatePeerCache(peer);
                }

                if (message.Type == MessageType.Discovery && peer != null)
                {
                    shouldRespond = true;
                    if (isNewPeer)
                    {
                        discoveredPeer = peer;
                    }
                }
                else if (message.Type == MessageType.DiscoveryResponse && isNewPeer && peer != null)
                {
                    discoveredPeer = peer;
                }
                else if (message.Type == MessageType.UserLeft && peer != null)
                {
                    _discoveredPeers.Remove(message.SenderId);
                    peer.IsOnline = false;
                    leftPeer = peer;
                }
            }

            if (shouldRespond)
            {
                SendDiscoveryResponse(senderIp);
            }

            if (discoveredPeer != null)
            {
                OnPeerDiscovered(discoveredPeer);
            }

            if (leftPeer != null)
            {
                _logger.LogInfo($"Peer left: {leftPeer.UserName} ({leftPeer.IpAddress})");
                OnPeerLeft(leftPeer);
            }
        }

        private void BroadcastPresence()
        {
            _logger.LogDebug("Broadcast thread started");
            int probeCounter = 0;

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

                    // Broadcast on all interfaces
                    foreach (var iface in _activeInterfaces)
                    {
                        try
                        {
                            var client = _broadcastClients.FirstOrDefault(c => 
                                c.Client.LocalEndPoint is IPEndPoint ep && 
                                ep.Address.Equals(iface.LocalAddress));

                            if (client != null)
                            {
                                IPEndPoint broadcastEndPoint = new IPEndPoint(iface.BroadcastAddress, DISCOVERY_PORT);
                                client.Send(data, data.Length, broadcastEndPoint);
                                _logger.LogDebug($"Broadcasted on {iface.InterfaceName} to {iface.BroadcastAddress}");
                            }
                        }
                        catch (SocketException sex)
                        {
                            _logger.LogDebug($"Broadcast failed on {iface.InterfaceName}: {sex.Message}");
                        }
                    }

                    // Periodically probe cached peers (fallback for broadcast failures)
                    probeCounter++;
                    if (probeCounter * BROADCAST_INTERVAL >= UNICAST_PROBE_INTERVAL)
                    {
                        ProbeKnownPeers();
                        probeCounter = 0;
                    }

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
                OnErrorOccurred($"发送发现响应失败: {ex.Message}");
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

                // Send goodbye on all interfaces
                foreach (var iface in _activeInterfaces)
                {
                    try
                    {
                        var client = _broadcastClients.FirstOrDefault(c => 
                            c.Client.LocalEndPoint is IPEndPoint ep && 
                            ep.Address.Equals(iface.LocalAddress));

                        if (client != null)
                        {
                            IPEndPoint broadcastEndPoint = new IPEndPoint(iface.BroadcastAddress, DISCOVERY_PORT);
                            client.Send(data, data.Length, broadcastEndPoint);
                        }
                    }
                    catch { }
                }

                _logger.LogDebug("Sent goodbye message");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send goodbye message", ex);
            }
        }

        private void CheckPeerTimeouts()
        {
            List<PeerInfo> timedOutPeers = new List<PeerInfo>();

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
                    timedOutPeers.Add(peer);
                }
            }

            foreach (PeerInfo peer in timedOutPeers)
            {
                _logger.LogInfo($"Peer timeout: {peer.UserName} ({peer.IpAddress})");
                OnPeerLeft(peer);
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

        /// <summary>
        /// Discovers all usable network interfaces and calculates their broadcast addresses
        /// Filters out VPN/virtual/tunnel interfaces to prioritize physical LAN connections
        /// </summary>
        private void DiscoverNetworkInterfaces()
        {
            _activeInterfaces.Clear();
            
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Skip non-operational, loopback, and tunnel/VPN interfaces
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;

                    // Heuristic: skip interfaces with VPN-like names (common on Windows/macOS)
                    string name = ni.Name.ToLowerInvariant();
                    if (name.Contains("vpn") || name.Contains("virtual") || 
                        name.Contains("tun") || name.Contains("tap") ||
                        name.Contains("utun") || name.Contains("ppp"))
                    {
                        _logger.LogDebug($"Skipping VPN/virtual interface: {ni.Name}");
                        continue;
                    }

                    var ipProps = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation unicast in ipProps.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        if (IPAddress.IsLoopback(unicast.Address))
                            continue;

                        // Calculate broadcast address
                        IPAddress broadcastAddr = CalculateBroadcastAddress(unicast.Address, unicast.IPv4Mask);
                        
                        var ifaceInfo = new NetworkInterfaceInfo
                        {
                            LocalAddress = unicast.Address,
                            BroadcastAddress = broadcastAddr,
                            InterfaceName = ni.Name
                        };

                        _activeInterfaces.Add(ifaceInfo);
                        _logger.LogInfo($"Active interface: {ni.Name} - {unicast.Address} (broadcast: {broadcastAddr})");
                    }
                }

                if (_activeInterfaces.Count == 0)
                {
                    _logger.LogWarning("No usable network interfaces found, using fallback");
                    // Fallback: use primary adapter
                    var fallbackAddr = _localPeer.IpAddress;
                    _activeInterfaces.Add(new NetworkInterfaceInfo
                    {
                        LocalAddress = fallbackAddr,
                        BroadcastAddress = GetFallbackBroadcast(fallbackAddr),
                        InterfaceName = "Fallback"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to discover network interfaces", ex);
                // Fallback
                _activeInterfaces.Add(new NetworkInterfaceInfo
                {
                    LocalAddress = _localPeer.IpAddress,
                    BroadcastAddress = GetFallbackBroadcast(_localPeer.IpAddress),
                    InterfaceName = "Fallback"
                });
            }
        }

        /// <summary>
        /// Creates dedicated UDP clients for broadcasting, one per interface
        /// Each client is bound to a specific local address to ensure packets go out the right interface
        /// </summary>
        private void CreateBroadcastClients()
        {
            foreach (var iface in _activeInterfaces)
            {
                try
                {
                    var client = new UdpClient(new IPEndPoint(iface.LocalAddress, 0))
                    {
                        EnableBroadcast = true
                    };
                    _broadcastClients.Add(client);
                    _logger.LogDebug($"Created broadcast client for {iface.LocalAddress}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to create broadcast client for {iface.LocalAddress}: {ex.Message}");
                }
            }

            if (_broadcastClients.Count == 0)
            {
                _logger.LogWarning("No broadcast clients created, creating unbound fallback");
                _broadcastClients.Add(new UdpClient { EnableBroadcast = true });
            }
        }

        /// <summary>
        /// Calculates broadcast address from IP and subnet mask
        /// </summary>
        private IPAddress CalculateBroadcastAddress(IPAddress ip, IPAddress mask)
        {
            if (mask == null)
                return GetFallbackBroadcast(ip);

            try
            {
                byte[] ipBytes = ip.GetAddressBytes();
                byte[] maskBytes = mask.GetAddressBytes();
                byte[] broadcastBytes = new byte[4];

                for (int i = 0; i < 4; i++)
                {
                    broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                }

                return new IPAddress(broadcastBytes);
            }
            catch
            {
                return GetFallbackBroadcast(ip);
            }
        }

        /// <summary>
        /// Fallback broadcast calculation (assumes /24)
        /// </summary>
        private IPAddress GetFallbackBroadcast(IPAddress ip)
        {
            try
            {
                byte[] ipBytes = ip.GetAddressBytes();
                ipBytes[3] = 255; // x.x.x.255
                return new IPAddress(ipBytes);
            }
            catch
            {
                return IPAddress.Broadcast;
            }
        }

        /// <summary>
        /// Probes known peers from cache with unicast messages (fallback for broadcast failures)
        /// </summary>
        private void ProbeKnownPeers()
        {
            if (_peerCache.Count == 0)
                return;

            _logger.LogDebug($"Probing {_peerCache.Count} cached peer(s)");

            NetworkMessage message = new NetworkMessage
            {
                Type = MessageType.Discovery,
                SenderId = _localPeer.PeerId,
                SenderName = _localPeer.UserName,
                TextContent = _localPeer.Port.ToString()
            };

            byte[] data = message.Serialize();

            foreach (var cached in _peerCache.Values.ToList())
            {
                // Skip if already discovered
                if (_discoveredPeers.ContainsKey(cached.PeerId))
                    continue;

                try
                {
                    IPAddress targetIp = IPAddress.Parse(cached.IpAddress);
                    IPEndPoint endPoint = new IPEndPoint(targetIp, DISCOVERY_PORT);

                    // Send unicast probe
                    _udpClient?.Send(data, data.Length, endPoint);
                    _logger.LogDebug($"Probed cached peer {cached.UserName} at {cached.IpAddress}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Failed to probe {cached.IpAddress}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Updates peer cache with newly discovered peer
        /// </summary>
        private void UpdatePeerCache(PeerInfo peer)
        {
            _peerCache[peer.PeerId] = new CachedPeerAddress
            {
                PeerId = peer.PeerId,
                UserName = peer.UserName,
                IpAddress = peer.IpAddress.ToString(),
                Port = peer.Port,
                LastSeen = DateTime.Now
            };
        }

        /// <summary>
        /// Loads peer cache from disk
        /// </summary>
        private void LoadPeerCache()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return;

                using (var reader = new StreamReader(_cacheFilePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 4)
                        {
                            var cached = new CachedPeerAddress
                            {
                                PeerId = parts[0],
                                UserName = parts[1],
                                IpAddress = parts[2],
                                Port = int.Parse(parts[3]),
                                LastSeen = parts.Length > 4 ? DateTime.Parse(parts[4]) : DateTime.Now
                            };
                            _peerCache[cached.PeerId] = cached;
                        }
                    }
                }
                _logger.LogInfo($"Loaded {_peerCache.Count} peer(s) from cache");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to load peer cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves peer cache to disk
        /// </summary>
        private void SavePeerCache()
        {
            try
            {
                // Only keep peers seen in the last 7 days
                var recentPeers = _peerCache.Values
                    .Where(p => (DateTime.Now - p.LastSeen).TotalDays < 7)
                    .ToList();

                using (var writer = new StreamWriter(_cacheFilePath, false))
                {
                    foreach (var peer in recentPeers)
                    {
                        writer.WriteLine($"{peer.PeerId}|{peer.UserName}|{peer.IpAddress}|{peer.Port}|{peer.LastSeen:O}");
                    }
                }
                _logger.LogDebug($"Saved {recentPeers.Count} peer(s) to cache");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to save peer cache: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _logger?.LogInfo("PeerDiscoveryService disposed");
        }

        // For diagnostics: returns current local IP and interface info
        public (IPAddress LocalIp, bool BroadcastAvailable, int InterfaceCount) GetDiagnostics()
        {
            return (
                _localPeer?.IpAddress ?? IPAddress.None, 
                _broadcastClients != null && _broadcastClients.Count > 0,
                _activeInterfaces?.Count ?? 0
            );
        }
    }
}
