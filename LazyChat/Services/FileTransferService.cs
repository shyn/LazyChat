using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using LazyChat.Models;

namespace LazyChat.Services
{
    public class FileTransferService : IDisposable
    {
        private const int CHUNK_SIZE = 65536;
        private Dictionary<string, FileTransferInfo> _activeTransfers;
        private Dictionary<string, FileStream> _activeFileStreams;
        private Dictionary<string, OutgoingTransferContext> _outgoingTransfers;
        private readonly object _transfersLock = new object();
        private readonly object _outgoingLock = new object();
        private P2PCommunicationService _commService;
        private readonly string _localPeerId;

        private class OutgoingTransferContext
        {
            public string FilePath { get; set; }
            public PeerInfo TargetPeer { get; set; }
            public string SenderName { get; set; }
        }

        public event EventHandler<FileTransferInfo> TransferProgressChanged;
        public event EventHandler<FileTransferInfo> TransferCompleted;
        public event EventHandler<FileTransferInfo> TransferFailed;
        public event EventHandler<FileTransferInfo> TransferRequestReceived;
        public event EventHandler<FileTransferInfo> TransferStarted;

        public FileTransferService(P2PCommunicationService commService, string localPeerId)
        {
            _commService = commService;
            _localPeerId = localPeerId;
            _activeTransfers = new Dictionary<string, FileTransferInfo>();
            _activeFileStreams = new Dictionary<string, FileStream>();
            _outgoingTransfers = new Dictionary<string, OutgoingTransferContext>();
        }

        public void StartSendingFile(string filePath, PeerInfo targetPeer, string senderName)
        {
            Thread sendThread = new Thread(() => SendFileAsync(filePath, targetPeer, senderName));
            sendThread.IsBackground = true;
            sendThread.Start();
        }

        private void SendFileAsync(string filePath, PeerInfo targetPeer, string senderName)
        {
            FileTransferInfo transfer = null;

            try
            {
                FileInfo fileInfo = new FileInfo(filePath);

                transfer = new FileTransferInfo
                {
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    SenderId = _localPeerId,
                    SenderName = senderName,
                    ReceiverId = targetPeer.PeerId
                };

                lock (_transfersLock)
                {
                    _activeTransfers[transfer.FileId] = transfer;
                }

                lock (_outgoingLock)
                {
                    _outgoingTransfers[transfer.FileId] = new OutgoingTransferContext
                    {
                        FilePath = filePath,
                        TargetPeer = targetPeer,
                        SenderName = senderName
                    };
                }

                TransferStarted?.Invoke(this, transfer);

                bool accepted = _commService.SendFileTransferRequest(
                    transfer.FileName,
                    transfer.FileSize,
                    targetPeer,
                    senderName,
                    transfer.FileId);

                if (!accepted)
                {
                    transfer.IsCancelled = true;
                    RemoveOutgoingTransfer(transfer.FileId);
                    TransferFailed?.Invoke(this, transfer);
                    return;
                }
            }
            catch (Exception)
            {
                if (transfer != null)
                {
                    transfer.IsCancelled = true;
                    RemoveOutgoingTransfer(transfer.FileId);
                    TransferFailed?.Invoke(this, transfer);
                }
            }
        }

        public void AcceptFileTransfer(string fileId, string savePath)
        {
            lock (_transfersLock)
            {
                if (_activeTransfers.ContainsKey(fileId))
                {
                    FileTransferInfo transfer = _activeTransfers[fileId];
                    transfer.SavePath = savePath;

                    try
                    {
                        FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write);
                        _activeFileStreams[fileId] = fs;
                    }
                    catch (Exception)
                    {
                        transfer.IsCancelled = true;
                        TransferFailed?.Invoke(this, transfer);
                    }
                }
            }
        }

        public void RejectFileTransfer(string fileId)
        {
            lock (_transfersLock)
            {
                if (_activeTransfers.ContainsKey(fileId))
                {
                    FileTransferInfo transfer = _activeTransfers[fileId];
                    transfer.IsCancelled = true;
                    _activeTransfers.Remove(fileId);
                }
            }
        }

        public void ContinueSendingFile(string fileId, string filePath, PeerInfo targetPeer, string senderName)
        {
            Thread sendThread = new Thread(() => SendFileChunks(fileId, filePath, targetPeer, senderName));
            sendThread.IsBackground = true;
            sendThread.Start();
        }

        public void HandleTransferAccepted(string fileId)
        {
            OutgoingTransferContext context = null;

            lock (_outgoingLock)
            {
                if (_outgoingTransfers.ContainsKey(fileId))
                {
                    context = _outgoingTransfers[fileId];
                    _outgoingTransfers.Remove(fileId);
                }
            }

            if (context == null)
            {
                return;
            }

            ContinueSendingFile(fileId, context.FilePath, context.TargetPeer, context.SenderName);
        }

        public void HandleTransferRejected(string fileId)
        {
            FileTransferInfo transfer = null;

            lock (_transfersLock)
            {
                if (_activeTransfers.ContainsKey(fileId))
                {
                    transfer = _activeTransfers[fileId];
                    transfer.IsCancelled = true;
                    _activeTransfers.Remove(fileId);
                }
            }

            RemoveOutgoingTransfer(fileId);

            if (transfer != null)
            {
                TransferFailed?.Invoke(this, transfer);
            }
        }

        private void SendFileChunks(string fileId, string filePath, PeerInfo targetPeer, string senderName)
        {
            FileTransferInfo transfer = null;

            try
            {
                lock (_transfersLock)
                {
                    if (!_activeTransfers.ContainsKey(fileId))
                        return;
                    transfer = _activeTransfers[fileId];
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    long totalBytes = fs.Length;
                    int totalChunks = (int)Math.Ceiling((double)totalBytes / CHUNK_SIZE);
                    byte[] buffer = new byte[CHUNK_SIZE];
                    int chunkIndex = 0;

                    while (fs.Position < totalBytes && !transfer.IsCancelled)
                    {
                        int bytesRead = fs.Read(buffer, 0, CHUNK_SIZE);
                        byte[] chunk = new byte[bytesRead];
                        Array.Copy(buffer, chunk, bytesRead);

                        bool sent = _commService.SendFileChunk(
                            chunk,
                            chunkIndex,
                            totalChunks,
                            fileId,
                            targetPeer,
                            senderName);

                        if (!sent)
                        {
                            transfer.IsCancelled = true;
                            TransferFailed?.Invoke(this, transfer);
                            return;
                        }

                        transfer.BytesTransferred += bytesRead;
                        chunkIndex++;

                        TransferProgressChanged?.Invoke(this, transfer);
                        Thread.Sleep(10);
                    }

                    if (!transfer.IsCancelled)
                    {
                        _commService.SendFileTransferComplete(fileId, targetPeer, senderName);
                        transfer.IsComplete = true;
                        TransferCompleted?.Invoke(this, transfer);
                    }
                }
            }
            catch (Exception)
            {
                if (transfer != null)
                {
                    transfer.IsCancelled = true;
                    TransferFailed?.Invoke(this, transfer);
                }
            }
            finally
            {
                lock (_transfersLock)
                {
                    _activeTransfers.Remove(fileId);
                }
            }
        }

        public void HandleReceivedChunk(NetworkMessage message)
        {
            lock (_transfersLock)
            {
                if (!_activeTransfers.ContainsKey(message.FileId))
                    return;

                FileTransferInfo transfer = _activeTransfers[message.FileId];

                if (!_activeFileStreams.ContainsKey(message.FileId))
                    return;

                try
                {
                    FileStream fs = _activeFileStreams[message.FileId];
                    fs.Write(message.Data, 0, message.Data.Length);
                    fs.Flush();

                    transfer.BytesTransferred += message.Data.Length;
                    TransferProgressChanged?.Invoke(this, transfer);
                }
                catch (Exception)
                {
                    transfer.IsCancelled = true;
                    CloseFileStream(message.FileId);
                    TransferFailed?.Invoke(this, transfer);
                }
            }
        }

        public void HandleTransferComplete(string fileId)
        {
            lock (_transfersLock)
            {
                if (_activeTransfers.ContainsKey(fileId))
                {
                    FileTransferInfo transfer = _activeTransfers[fileId];
                    transfer.IsComplete = true;

                    CloseFileStream(fileId);
                    TransferCompleted?.Invoke(this, transfer);

                    _activeTransfers.Remove(fileId);
                }
            }
        }

        public void HandleTransferRequest(NetworkMessage message)
        {
            FileTransferInfo transfer = new FileTransferInfo
            {
                FileId = message.FileId,
                FileName = message.FileName,
                FileSize = message.FileSize,
                SenderId = message.SenderId,
                SenderName = message.SenderName
            };

            lock (_transfersLock)
            {
                _activeTransfers[transfer.FileId] = transfer;
            }

            TransferRequestReceived?.Invoke(this, transfer);
        }

        private void CloseFileStream(string fileId)
        {
            if (_activeFileStreams.ContainsKey(fileId))
            {
                try
                {
                    _activeFileStreams[fileId].Close();
                }
                catch
                {
                }
                _activeFileStreams.Remove(fileId);
            }
        }

        private void RemoveOutgoingTransfer(string fileId)
        {
            lock (_outgoingLock)
            {
                if (_outgoingTransfers.ContainsKey(fileId))
                {
                    _outgoingTransfers.Remove(fileId);
                }
            }
        }

        public void CancelTransfer(string fileId)
        {
            lock (_transfersLock)
            {
                if (_activeTransfers.ContainsKey(fileId))
                {
                    FileTransferInfo transfer = _activeTransfers[fileId];
                    transfer.IsCancelled = true;
                    CloseFileStream(fileId);
                    _activeTransfers.Remove(fileId);
                }
            }
        }

        public void Dispose()
        {
            lock (_transfersLock)
            {
                foreach (var fs in _activeFileStreams.Values)
                {
                    try
                    {
                        fs.Close();
                    }
                    catch
                    {
                    }
                }
                _activeFileStreams.Clear();
                _activeTransfers.Clear();
            }

            lock (_outgoingLock)
            {
                _outgoingTransfers.Clear();
            }
        }
    }
}
