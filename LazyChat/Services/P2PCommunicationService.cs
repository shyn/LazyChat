using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LazyChat.Models;

namespace LazyChat.Services
{
    public class P2PCommunicationService : IDisposable
    {
        private const int BUFFER_SIZE = 8192;
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _isRunning;
        private int _port;
        private string _localPeerId;
        private Dictionary<string, TcpClient> _activeConnections;
        private readonly object _connectionsLock = new object();

        public event EventHandler<NetworkMessage> MessageReceived;
        public event EventHandler<string> ErrorOccurred;

        public P2PCommunicationService(int port, string localPeerId)
        {
            _port = port;
            _localPeerId = localPeerId;
            _activeConnections = new Dictionary<string, TcpClient>();
        }

        public void Start()
        {
            if (_isRunning)
                return;

            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                _listenThread = new Thread(ListenForConnections);
                _listenThread.IsBackground = true;
                _listenThread.Start();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, "启动通信服务失败: " + ex.Message);
            }
        }

        public void Stop()
        {
            _isRunning = false;

            if (_listener != null)
            {
                _listener.Stop();
            }

            lock (_connectionsLock)
            {
                foreach (var client in _activeConnections.Values)
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }
                }
                _activeConnections.Clear();
            }

            if (_listenThread != null && _listenThread.IsAlive)
            {
                _listenThread.Join(1000);
            }
        }

        private void ListenForConnections()
        {
            while (_isRunning)
            {
                try
                {
                    if (_listener.Pending())
                    {
                        TcpClient client = _listener.AcceptTcpClient();
                        Thread handleThread = new Thread(() => HandleClient(client));
                        handleThread.IsBackground = true;
                        handleThread.Start();
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (SocketException)
                {
                    if (_isRunning)
                        continue;
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        ErrorOccurred?.Invoke(this, "接受连接失败: " + ex.Message);
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = null;
            string peerId = null;

            try
            {
                stream = client.GetStream();

                while (_isRunning && client.Connected)
                {
                    byte[] lengthBytes = new byte[4];
                    int bytesRead = stream.Read(lengthBytes, 0, 4);

                    if (bytesRead == 0)
                        break;

                    int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                    byte[] messageData = new byte[messageLength];
                    int totalRead = 0;

                    while (totalRead < messageLength)
                    {
                        bytesRead = stream.Read(messageData, totalRead, messageLength - totalRead);
                        if (bytesRead == 0)
                            break;
                        totalRead += bytesRead;
                    }

                    if (totalRead == messageLength)
                    {
                        NetworkMessage message = NetworkMessage.Deserialize(messageData);
                        peerId = message.SenderId;

                        lock (_connectionsLock)
                        {
                            if (!_activeConnections.ContainsKey(peerId))
                            {
                                _activeConnections[peerId] = client;
                            }
                        }

                        MessageReceived?.Invoke(this, message);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, "处理客户端消息失败: " + ex.Message);
            }
            finally
            {
                if (peerId != null)
                {
                    lock (_connectionsLock)
                    {
                        _activeConnections.Remove(peerId);
                    }
                }

                if (stream != null)
                {
                    stream.Close();
                }

                if (client != null)
                {
                    client.Close();
                }
            }
        }

        public bool SendMessage(NetworkMessage message, PeerInfo targetPeer)
        {
            TcpClient client = null;
            bool isNewConnection = false;

            try
            {
                lock (_connectionsLock)
                {
                    if (_activeConnections.ContainsKey(targetPeer.PeerId))
                    {
                        client = _activeConnections[targetPeer.PeerId];
                    }
                }

                if (client == null || !client.Connected)
                {
                    client = new TcpClient();
                    client.Connect(targetPeer.IpAddress.ToString(), targetPeer.Port);
                    isNewConnection = true;

                    lock (_connectionsLock)
                    {
                        _activeConnections[targetPeer.PeerId] = client;
                    }

                    Thread receiveThread = new Thread(() => HandleClient(client));
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }

                byte[] messageData = message.Serialize();
                byte[] lengthBytes = BitConverter.GetBytes(messageData.Length);

                NetworkStream stream = client.GetStream();
                stream.Write(lengthBytes, 0, lengthBytes.Length);
                stream.Write(messageData, 0, messageData.Length);
                stream.Flush();

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, "发送消息失败: " + ex.Message);

                if (isNewConnection && client != null)
                {
                    lock (_connectionsLock)
                    {
                        _activeConnections.Remove(targetPeer.PeerId);
                    }
                    client.Close();
                }

                return false;
            }
        }

        public bool SendTextMessage(string text, PeerInfo targetPeer, string senderName)
        {
            NetworkMessage message = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderId = _localPeerId,
                SenderName = senderName,
                ReceiverId = targetPeer.PeerId,
                TextContent = text
            };

            return SendMessage(message, targetPeer);
        }

        public bool SendImageMessage(byte[] imageData, PeerInfo targetPeer, string senderName)
        {
            NetworkMessage message = new NetworkMessage
            {
                Type = MessageType.ImageMessage,
                SenderId = _localPeerId,
                SenderName = senderName,
                ReceiverId = targetPeer.PeerId,
                Data = imageData
            };

            return SendMessage(message, targetPeer);
        }

        public bool SendFileTransferRequest(string fileName, long fileSize, PeerInfo targetPeer, string senderName, string fileId)
        {
            NetworkMessage message = new NetworkMessage
            {
                Type = MessageType.FileTransferRequest,
                SenderId = _localPeerId,
                SenderName = senderName,
                ReceiverId = targetPeer.PeerId,
                FileName = fileName,
                FileSize = fileSize,
                FileId = fileId
            };

            return SendMessage(message, targetPeer);
        }

        public bool SendFileTransferResponse(bool accepted, string fileId, PeerInfo targetPeer, string senderName)
        {
            NetworkMessage message = new NetworkMessage
            {
                Type = accepted ? MessageType.FileTransferAccept : MessageType.FileTransferReject,
                SenderId = _localPeerId,
                SenderName = senderName,
                ReceiverId = targetPeer.PeerId,
                FileId = fileId
            };

            return SendMessage(message, targetPeer);
        }

        public bool SendFileChunk(byte[] chunkData, int chunkIndex, int totalChunks, string fileId, PeerInfo targetPeer, string senderName)
        {
            NetworkMessage message = new NetworkMessage
            {
                Type = MessageType.FileTransferData,
                SenderId = _localPeerId,
                SenderName = senderName,
                ReceiverId = targetPeer.PeerId,
                FileId = fileId,
                Data = chunkData,
                ChunkIndex = chunkIndex,
                TotalChunks = totalChunks
            };

            return SendMessage(message, targetPeer);
        }

        public bool SendFileTransferComplete(string fileId, PeerInfo targetPeer, string senderName)
        {
            NetworkMessage message = new NetworkMessage
            {
                Type = MessageType.FileTransferComplete,
                SenderId = _localPeerId,
                SenderName = senderName,
                ReceiverId = targetPeer.PeerId,
                FileId = fileId
            };

            return SendMessage(message, targetPeer);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
