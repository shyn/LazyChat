using System;
using System.Collections.Generic;
using LazyChat.Models;

namespace LazyChat.Services.Interfaces
{
    /// <summary>
    /// Interface for peer discovery service that handles automatic discovery of peers on the local network
    /// </summary>
    public interface IPeerDiscoveryService : IDisposable
    {
        /// <summary>
        /// Event raised when a new peer is discovered
        /// </summary>
        event EventHandler<PeerInfo> PeerDiscovered;

        /// <summary>
        /// Event raised when a peer leaves the network
        /// </summary>
        event EventHandler<PeerInfo> PeerLeft;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<string> ErrorOccurred;

        /// <summary>
        /// Starts the discovery service
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the discovery service
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets the list of currently online peers
        /// </summary>
        /// <returns>List of online peers</returns>
        List<PeerInfo> GetOnlinePeers();

        /// <summary>
        /// Gets information about the local peer
        /// </summary>
        /// <returns>Local peer information</returns>
        PeerInfo GetLocalPeer();

        /// <summary>
        /// Gets whether the service is currently running
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// Interface for P2P communication service that handles message exchange between peers
    /// </summary>
    public interface IP2PCommunicationService : IDisposable
    {
        /// <summary>
        /// Event raised when a message is received
        /// </summary>
        event EventHandler<NetworkMessage> MessageReceived;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<string> ErrorOccurred;

        /// <summary>
        /// Starts the communication service
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the communication service
        /// </summary>
        void Stop();

        /// <summary>
        /// Sends a message to a target peer
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="targetPeer">The target peer</param>
        /// <returns>True if message was sent successfully</returns>
        bool SendMessage(NetworkMessage message, PeerInfo targetPeer);

        /// <summary>
        /// Sends a text message to a target peer
        /// </summary>
        /// <param name="text">The text content</param>
        /// <param name="targetPeer">The target peer</param>
        /// <param name="senderName">The sender's name</param>
        /// <returns>True if message was sent successfully</returns>
        bool SendTextMessage(string text, PeerInfo targetPeer, string senderName);

        /// <summary>
        /// Sends an image message to a target peer
        /// </summary>
        /// <param name="imageData">The image data</param>
        /// <param name="targetPeer">The target peer</param>
        /// <param name="senderName">The sender's name</param>
        /// <returns>True if message was sent successfully</returns>
        bool SendImageMessage(byte[] imageData, PeerInfo targetPeer, string senderName);

        /// <summary>
        /// Sends a file transfer request
        /// </summary>
        bool SendFileTransferRequest(string fileName, long fileSize, PeerInfo targetPeer, string senderName, string fileId);

        /// <summary>
        /// Sends a file transfer response
        /// </summary>
        bool SendFileTransferResponse(bool accepted, string fileId, PeerInfo targetPeer, string senderName);

        /// <summary>
        /// Sends a file chunk
        /// </summary>
        bool SendFileChunk(byte[] chunkData, int chunkIndex, int totalChunks, string fileId, PeerInfo targetPeer, string senderName);

        /// <summary>
        /// Sends file transfer complete notification
        /// </summary>
        bool SendFileTransferComplete(string fileId, PeerInfo targetPeer, string senderName);

        /// <summary>
        /// Gets whether the service is currently running
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// Interface for file transfer service that manages file transfers between peers
    /// </summary>
    public interface IFileTransferService : IDisposable
    {
        /// <summary>
        /// Event raised when a transfer request is received
        /// </summary>
        event EventHandler<FileTransferInfo> TransferRequestReceived;

        /// <summary>
        /// Event raised when transfer progress changes
        /// </summary>
        event EventHandler<FileTransferInfo> TransferProgressChanged;

        /// <summary>
        /// Event raised when a transfer completes
        /// </summary>
        event EventHandler<FileTransferInfo> TransferCompleted;

        /// <summary>
        /// Event raised when a transfer fails
        /// </summary>
        event EventHandler<FileTransferInfo> TransferFailed;

        /// <summary>
        /// Starts sending a file to a peer
        /// </summary>
        /// <param name="filePath">Path to the file to send</param>
        /// <param name="targetPeer">The target peer</param>
        /// <param name="senderName">The sender's name</param>
        void StartSendingFile(string filePath, PeerInfo targetPeer, string senderName);

        /// <summary>
        /// Accepts an incoming file transfer
        /// </summary>
        /// <param name="fileId">The file transfer ID</param>
        /// <param name="savePath">Path where to save the file</param>
        void AcceptFileTransfer(string fileId, string savePath);

        /// <summary>
        /// Rejects an incoming file transfer
        /// </summary>
        /// <param name="fileId">The file transfer ID</param>
        void RejectFileTransfer(string fileId);

        /// <summary>
        /// Cancels an active transfer
        /// </summary>
        /// <param name="fileId">The file transfer ID</param>
        void CancelTransfer(string fileId);

        /// <summary>
        /// Continues sending file after acceptance
        /// </summary>
        void ContinueSendingFile(string fileId, string filePath, PeerInfo targetPeer, string senderName);

        /// <summary>
        /// Handles a received file chunk
        /// </summary>
        void HandleReceivedChunk(NetworkMessage message);

        /// <summary>
        /// Handles transfer completion
        /// </summary>
        void HandleTransferComplete(string fileId);

        /// <summary>
        /// Handles a transfer request
        /// </summary>
        void HandleTransferRequest(NetworkMessage message);
    }

    /// <summary>
    /// Interface for logging service
    /// </summary>
    public interface ILogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
        void LogDebug(string message);
    }

    /// <summary>
    /// Interface for network operations to enable testing
    /// </summary>
    public interface INetworkAdapter
    {
        System.Net.IPAddress GetLocalIPAddress();
        void CreateUdpClient(int port, out System.Net.Sockets.UdpClient client);
        void CreateTcpListener(int port, out System.Net.Sockets.TcpListener listener);
        void CreateTcpClient(out System.Net.Sockets.TcpClient client);
    }

    /// <summary>
    /// Interface for file system operations to enable testing
    /// </summary>
    public interface IFileSystem
    {
        bool FileExists(string path);
        long GetFileSize(string path);
        byte[] ReadAllBytes(string path);
        void WriteAllBytes(string path, byte[] data);
        System.IO.FileStream OpenRead(string path);
        System.IO.FileStream OpenWrite(string path);
        System.IO.FileInfo GetFileInfo(string path);
    }
}
