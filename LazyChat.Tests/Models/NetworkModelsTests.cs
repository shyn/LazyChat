using System;
using System.Drawing;
using System.IO;
using NUnit.Framework;
using LazyChat.Models;
using LazyChat.Exceptions;
using System.Diagnostics;

namespace LazyChat.Tests.Models
{
    [TestFixture]
    public class NetworkModelsTests
    {
        [Test]
        public void NetworkMessage_Constructor_SetsTimestamp()
        {
            // Arrange & Act
            var message = new NetworkMessage();

            // Assert
            Assert.That(message.Timestamp, Is.Not.EqualTo(default(DateTime)));
            Assert.That((DateTime.Now - message.Timestamp).TotalSeconds, Is.LessThan(1));
        }

        [Test]
        public void NetworkMessage_Serialize_ValidTextMessage_Success()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderId = "sender-123",
                SenderName = "Test User",
                ReceiverId = "receiver-456",
                TextContent = "Hello World"
            };

            // Act
            byte[] data = message.Serialize();

            // Assert
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Length, Is.GreaterThan(0));
            // MessagePack should produce smaller payloads than BinaryFormatter
            Assert.That(data.Length, Is.LessThan(500), "MessagePack should be compact");
        }

        [Test]
        public void NetworkMessage_Serialize_Performance_IsFast()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderId = "sender-123",
                SenderName = "Test User",
                ReceiverId = "receiver-456",
                TextContent = "Hello World"
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                byte[] data = message.Serialize();
            }
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100), 
                $"1000 serializations should be fast (took {stopwatch.ElapsedMilliseconds}ms)");
        }

        [Test]
        public void NetworkMessage_Deserialize_ValidData_Success()
        {
            // Arrange
            var originalMessage = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderId = "sender-123",
                SenderName = "Test User",
                ReceiverId = "receiver-456",
                TextContent = "Hello World"
            };
            byte[] data = originalMessage.Serialize();

            // Act
            var deserializedMessage = NetworkMessage.Deserialize(data);

            // Assert
            Assert.That(deserializedMessage, Is.Not.Null);
            Assert.That(deserializedMessage.Type, Is.EqualTo(MessageType.TextMessage));
            Assert.That(deserializedMessage.SenderId, Is.EqualTo("sender-123"));
            Assert.That(deserializedMessage.SenderName, Is.EqualTo("Test User"));
            Assert.That(deserializedMessage.ReceiverId, Is.EqualTo("receiver-456"));
            Assert.That(deserializedMessage.TextContent, Is.EqualTo("Hello World"));
        }

        [Test]
        public void NetworkMessage_RoundTrip_PreservesAllData()
        {
            // Arrange
            var original = new NetworkMessage
            {
                Type = MessageType.FileTransferData,
                SenderId = "sender-123",
                SenderName = "Test User",
                ReceiverId = "receiver-456",
                FileId = "file-789",
                FileName = "test.txt",
                FileSize = 1024,
                ChunkIndex = 5,
                TotalChunks = 10,
                Data = new byte[] { 1, 2, 3, 4, 5 }
            };

            // Act
            byte[] serialized = original.Serialize();
            var deserialized = NetworkMessage.Deserialize(serialized);

            // Assert
            Assert.That(deserialized.Type, Is.EqualTo(original.Type));
            Assert.That(deserialized.SenderId, Is.EqualTo(original.SenderId));
            Assert.That(deserialized.SenderName, Is.EqualTo(original.SenderName));
            Assert.That(deserialized.ReceiverId, Is.EqualTo(original.ReceiverId));
            Assert.That(deserialized.FileId, Is.EqualTo(original.FileId));
            Assert.That(deserialized.FileName, Is.EqualTo(original.FileName));
            Assert.That(deserialized.FileSize, Is.EqualTo(original.FileSize));
            Assert.That(deserialized.ChunkIndex, Is.EqualTo(original.ChunkIndex));
            Assert.That(deserialized.TotalChunks, Is.EqualTo(original.TotalChunks));
            Assert.That(deserialized.Data, Is.EqualTo(original.Data));
        }

        [Test]
        public void NetworkMessage_Deserialize_NullData_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => NetworkMessage.Deserialize(null));
        }

        [Test]
        public void NetworkMessage_Deserialize_EmptyData_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => NetworkMessage.Deserialize(new byte[0]));
        }

        [Test]
        public void NetworkMessage_Validate_TextMessage_MissingContent_ThrowsException()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderId = "sender-123",
                SenderName = "Test User"
                // TextContent is missing
            };

            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_Validate_MissingSenderId_ThrowsException()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderName = "Test User",
                TextContent = "Hello"
                // SenderId is missing
            };

            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_Validate_MissingSenderName_ThrowsException()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderId = "sender-123",
                TextContent = "Hello"
                // SenderName is missing
            };

            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_Validate_ImageMessage_MissingData_ThrowsException()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.ImageMessage,
                SenderId = "sender-123",
                SenderName = "Test User"
                // Data is missing
            };

            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_Validate_FileTransferRequest_MissingFileName_ThrowsException()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.FileTransferRequest,
                SenderId = "sender-123",
                SenderName = "Test User",
                FileSize = 1000,
                FileId = "file-123"
                // FileName is missing
            };

            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_Validate_FileTransferRequest_InvalidFileSize_ThrowsException()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.FileTransferRequest,
                SenderId = "sender-123",
                SenderName = "Test User",
                FileName = "test.txt",
                FileSize = 0, // Invalid
                FileId = "file-123"
            };

            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_Validate_FileTransferData_InvalidChunkIndices_ThrowsException()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.FileTransferData,
                SenderId = "sender-123",
                SenderName = "Test User",
                FileId = "file-123",
                Data = new byte[] { 1, 2, 3 },
                ChunkIndex = -1, // Invalid
                TotalChunks = 10
            };

            // Act & Assert
            Assert.Throws<MessageSerializationException>(() => message.Validate());
        }

        [Test]
        public void NetworkMessage_LargeData_SerializesEfficiently()
        {
            // Arrange - Create message with large data
            var largeData = new byte[64 * 1024]; // 64KB
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            var message = new NetworkMessage
            {
                Type = MessageType.FileTransferData,
                SenderId = "sender-123",
                SenderName = "Test User",
                FileId = "file-789",
                ChunkIndex = 0,
                TotalChunks = 1,
                Data = largeData
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            byte[] serialized = message.Serialize();
            stopwatch.Stop();

            // Assert
            Assert.That(serialized, Is.Not.Null);
            // MessagePack overhead should be minimal
            Assert.That(serialized.Length, Is.LessThan(largeData.Length + 200), 
                "MessagePack overhead should be minimal");
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50), 
                "Large data serialization should be fast");
        }

        [Test]
        public void PeerInfo_Constructor_GeneratesUniqueId()
        {
            // Arrange & Act
            var peer1 = new PeerInfo();
            var peer2 = new PeerInfo();

            // Assert
            Assert.That(peer1.PeerId, Is.Not.Null);
            Assert.That(peer2.PeerId, Is.Not.Null);
            Assert.That(peer1.PeerId, Is.Not.EqualTo(peer2.PeerId));
        }

        [Test]
        public void PeerInfo_Constructor_SetsDefaultValues()
        {
            // Arrange & Act
            var peer = new PeerInfo();

            // Assert
            Assert.That(peer.IsOnline, Is.True);
            Assert.That((DateTime.Now - peer.LastSeen).TotalSeconds, Is.LessThan(1));
        }

        [Test]
        public void PeerInfo_IpAddress_SerializationWorkaround()
        {
            // Arrange
            var peer = new PeerInfo
            {
                UserName = "TestUser",
                IpAddress = System.Net.IPAddress.Parse("192.168.1.100"),
                Port = 9999
            };

            // Act - Verify the workaround works
            string ipString = peer.IpAddressString;
            var retrievedIp = peer.IpAddress;

            // Assert
            Assert.That(ipString, Is.EqualTo("192.168.1.100"));
            Assert.That(retrievedIp.ToString(), Is.EqualTo("192.168.1.100"));
        }

        [Test]
        public void PeerInfo_Equals_SamePeerId_ReturnsTrue()
        {
            // Arrange
            var peerId = Guid.NewGuid().ToString();
            var peer1 = new PeerInfo { PeerId = peerId };
            var peer2 = new PeerInfo { PeerId = peerId };

            // Act & Assert
            Assert.That(peer1.Equals(peer2), Is.True);
        }

        [Test]
        public void PeerInfo_Equals_DifferentPeerId_ReturnsFalse()
        {
            // Arrange
            var peer1 = new PeerInfo { PeerId = Guid.NewGuid().ToString() };
            var peer2 = new PeerInfo { PeerId = Guid.NewGuid().ToString() };

            // Act & Assert
            Assert.That(peer1.Equals(peer2), Is.False);
        }

        [Test]
        public void FileTransferInfo_Constructor_GeneratesFileId()
        {
            // Arrange & Act
            var transfer = new FileTransferInfo();

            // Assert
            Assert.That(transfer.FileId, Is.Not.Null);
            Assert.That(transfer.FileId, Is.Not.Empty);
        }

        [Test]
        public void FileTransferInfo_GetProgress_CalculatesCorrectly()
        {
            // Arrange
            var transfer = new FileTransferInfo
            {
                FileSize = 1000,
                BytesTransferred = 250
            };

            // Act
            int progress = transfer.GetProgress();

            // Assert
            Assert.That(progress, Is.EqualTo(25));
        }

        [Test]
        public void FileTransferInfo_GetProgress_ZeroFileSize_ReturnsZero()
        {
            // Arrange
            var transfer = new FileTransferInfo
            {
                FileSize = 0,
                BytesTransferred = 100
            };

            // Act
            int progress = transfer.GetProgress();

            // Assert
            Assert.That(progress, Is.EqualTo(0));
        }

        [Test]
        public void FileTransferInfo_GetProgress_Complete_Returns100()
        {
            // Arrange
            var transfer = new FileTransferInfo
            {
                FileSize = 1000,
                BytesTransferred = 1000
            };

            // Act
            int progress = transfer.GetProgress();

            // Assert
            Assert.That(progress, Is.EqualTo(100));
        }

        [Test]
        public void ChatMessage_Constructor_GeneratesMessageId()
        {
            // Arrange & Act
            var message = new ChatMessage();

            // Assert
            Assert.That(message.MessageId, Is.Not.Null);
            Assert.That(message.MessageId, Is.Not.Empty);
        }

        [Test]
        public void Conversation_Constructor_InitializesMessagesList()
        {
            // Arrange & Act
            var conversation = new Conversation();

            // Assert
            Assert.That(conversation.Messages, Is.Not.Null);
            Assert.That(conversation.Messages.Count, Is.EqualTo(0));
        }

        [Test]
        public void NetworkMessage_SerializeDeserialize_Benchmark()
        {
            // Arrange
            var message = new NetworkMessage
            {
                Type = MessageType.TextMessage,
                SenderId = "sender-123",
                SenderName = "Test User",
                ReceiverId = "receiver-456",
                TextContent = "Hello World! This is a test message."
            };

            const int iterations = 10000;

            // Act - Serialize
            var swSerialize = Stopwatch.StartNew();
            byte[] lastSerialized = null;
            for (int i = 0; i < iterations; i++)
            {
                lastSerialized = message.Serialize();
            }
            swSerialize.Stop();

            // Act - Deserialize
            var swDeserialize = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                NetworkMessage.Deserialize(lastSerialized);
            }
            swDeserialize.Stop();

            // Assert
            Console.WriteLine($"MessagePack Performance ({iterations} iterations):");
            Console.WriteLine($"  Serialize:   {swSerialize.ElapsedMilliseconds}ms ({swSerialize.ElapsedMilliseconds * 1000.0 / iterations:F3}¦Ìs per operation)");
            Console.WriteLine($"  Deserialize: {swDeserialize.ElapsedMilliseconds}ms ({swDeserialize.ElapsedMilliseconds * 1000.0 / iterations:F3}¦Ìs per operation)");
            Console.WriteLine($"  Data size:   {lastSerialized.Length} bytes");

            Assert.That(swSerialize.ElapsedMilliseconds, Is.LessThan(1000), "Serialization should be fast");
            Assert.That(swDeserialize.ElapsedMilliseconds, Is.LessThan(1000), "Deserialization should be fast");
        }
    }
}
