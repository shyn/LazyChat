using System;
using System.IO;
using System.Net;
using System.Text;
using LazyChat.Exceptions;
using MessagePack;

namespace LazyChat.Models
{
    public enum MessageType
    {
        Discovery,
        DiscoveryResponse,
        UserJoined,
        UserLeft,
        TextMessage,
        ImageMessage,
        FileTransferRequest,
        FileTransferAccept,
        FileTransferReject,
        FileTransferData,
        FileTransferComplete,
        TypingNotification,
        Heartbeat
    }

    [MessagePackObject]
    public class NetworkMessage
    {
        [Key(0)]
        public MessageType Type { get; set; }
        
        [Key(1)]
        public string SenderId { get; set; }
        
        [Key(2)]
        public string SenderName { get; set; }
        
        [Key(3)]
        public string ReceiverId { get; set; }
        
        [Key(4)]
        public DateTime Timestamp { get; set; }
        
        [Key(5)]
        public byte[] Data { get; set; }
        
        [Key(6)]
        public string TextContent { get; set; }
        
        [Key(7)]
        public string FileName { get; set; }
        
        [Key(8)]
        public long FileSize { get; set; }
        
        [Key(9)]
        public string FileId { get; set; }
        
        [Key(10)]
        public int ChunkIndex { get; set; }
        
        [Key(11)]
        public int TotalChunks { get; set; }

        public NetworkMessage()
        {
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Validates the network message
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SenderId))
                throw new MessageSerializationException("SenderId is required");

            if (string.IsNullOrWhiteSpace(SenderName))
                throw new MessageSerializationException("SenderName is required");

            switch (Type)
            {
                case MessageType.TextMessage:
                    if (string.IsNullOrEmpty(TextContent))
                        throw new MessageSerializationException("TextContent is required for text messages");
                    break;

                case MessageType.ImageMessage:
                    if (Data == null || Data.Length == 0)
                        throw new MessageSerializationException("Data is required for image messages");
                    break;

                case MessageType.FileTransferRequest:
                    if (string.IsNullOrWhiteSpace(FileName))
                        throw new MessageSerializationException("FileName is required for file transfer request");
                    if (FileSize <= 0)
                        throw new MessageSerializationException("FileSize must be positive");
                    if (string.IsNullOrWhiteSpace(FileId))
                        throw new MessageSerializationException("FileId is required");
                    break;

                case MessageType.FileTransferData:
                    if (Data == null || Data.Length == 0)
                        throw new MessageSerializationException("Data is required for file chunks");
                    if (string.IsNullOrWhiteSpace(FileId))
                        throw new MessageSerializationException("FileId is required");
                    if (ChunkIndex < 0 || TotalChunks <= 0)
                        throw new MessageSerializationException("Invalid chunk indices");
                    break;
            }
        }

        /// <summary>
        /// Serializes the message using MessagePack (high performance, small size)
        /// </summary>
        public byte[] Serialize()
        {
            try
            {
                Validate();
                return MessagePackSerializer.Serialize(this);
            }
            catch (MessageSerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MessageSerializationException("Failed to serialize network message", ex);
            }
        }

        /// <summary>
        /// Deserializes the message using MessagePack
        /// </summary>
        public static NetworkMessage Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new MessageSerializationException("Cannot deserialize null or empty data");

            try
            {
                NetworkMessage message = MessagePackSerializer.Deserialize<NetworkMessage>(data);
                message.Validate();
                return message;
            }
            catch (MessageSerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MessageSerializationException("Failed to deserialize network message", ex);
            }
        }
    }

    [MessagePackObject]
    public class PeerInfo : System.ComponentModel.INotifyPropertyChanged
    {
        private int _unreadCount;
        private bool _isSelected;
        private string _initial;

        [Key(0)]
        public string PeerId { get; set; }
        
        [Key(1)]
        public string UserName { get; set; }
        
        [Key(2)]
        public string IpAddressString { get; set; }
        
        [Key(3)]
        public int Port { get; set; }
        
        [Key(4)]
        public DateTime LastSeen { get; set; }
        
        [Key(5)]
        public bool IsOnline { get; set; }

        [IgnoreMember]
        public IPAddress IpAddress
        {
            get => string.IsNullOrEmpty(IpAddressString) ? null : IPAddress.Parse(IpAddressString);
            set => IpAddressString = value?.ToString();
        }

        // UI properties
        [IgnoreMember]
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (_unreadCount != value)
                {
                    _unreadCount = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(UnreadCount)));
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasUnread)));
                }
            }
        }

        [IgnoreMember]
        public bool HasUnread => _unreadCount > 0;

        [IgnoreMember]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        [IgnoreMember]
        public string Initial
        {
            get => _initial ?? (string.IsNullOrWhiteSpace(UserName) ? "?" : UserName.Substring(0, 1).ToUpperInvariant());
            set => _initial = value;
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public PeerInfo()
        {
            PeerId = Guid.NewGuid().ToString();
            LastSeen = DateTime.Now;
            IsOnline = true;
        }

        public override bool Equals(object obj)
        {
            if (obj is PeerInfo peer)
            {
                return PeerId == peer.PeerId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return PeerId.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", UserName, IpAddress);
        }
    }

    [MessagePackObject]
    public class FileTransferInfo
    {
        [Key(0)]
        public string FileId { get; set; }
        
        [Key(1)]
        public string FileName { get; set; }
        
        [Key(2)]
        public long FileSize { get; set; }
        
        [Key(3)]
        public string SenderId { get; set; }
        
        [Key(4)]
        public string SenderName { get; set; }
        
        [Key(5)]
        public string ReceiverId { get; set; }
        
        [Key(6)]
        public DateTime StartTime { get; set; }
        
        [Key(7)]
        public long BytesTransferred { get; set; }
        
        [Key(8)]
        public bool IsComplete { get; set; }
        
        [Key(9)]
        public bool IsCancelled { get; set; }
        
        [Key(10)]
        public string SavePath { get; set; }

        public FileTransferInfo()
        {
            FileId = Guid.NewGuid().ToString();
            StartTime = DateTime.Now;
        }

        public int GetProgress()
        {
            if (FileSize == 0) return 0;
            return (int)((BytesTransferred * 100) / FileSize);
        }
    }
}
