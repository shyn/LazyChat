using System;

namespace LazyChat.Exceptions
{
    /// <summary>
    /// Base exception for LazyChat application
    /// </summary>
    public class LazyChatException : Exception
    {
        public LazyChatException(string message) : base(message) { }
        public LazyChatException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when network operations fail
    /// </summary>
    public class NetworkException : LazyChatException
    {
        public string Operation { get; set; }

        public NetworkException(string message, string operation = null) : base(message)
        {
            Operation = operation;
        }

        public NetworkException(string message, Exception innerException, string operation = null) 
            : base(message, innerException)
        {
            Operation = operation;
        }
    }

    /// <summary>
    /// Exception thrown when file transfer operations fail
    /// </summary>
    public class FileTransferException : LazyChatException
    {
        public string FileId { get; set; }
        public string FileName { get; set; }

        public FileTransferException(string message, string fileId = null, string fileName = null) : base(message)
        {
            FileId = fileId;
            FileName = fileName;
        }

        public FileTransferException(string message, Exception innerException, string fileId = null, string fileName = null) 
            : base(message, innerException)
        {
            FileId = fileId;
            FileName = fileName;
        }
    }

    /// <summary>
    /// Exception thrown when peer discovery fails
    /// </summary>
    public class PeerDiscoveryException : LazyChatException
    {
        public PeerDiscoveryException(string message) : base(message) { }
        public PeerDiscoveryException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when message serialization/deserialization fails
    /// </summary>
    public class MessageSerializationException : LazyChatException
    {
        public MessageSerializationException(string message) : base(message) { }
        public MessageSerializationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
