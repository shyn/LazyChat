using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using LazyChat.Models;
using LazyChat.Services;

namespace LazyChat.Tests.Services
{
    [TestFixture]
    public class ChatHistoryStoreTests
    {
        private string _dbPath;
        private ChatHistoryStore _store;

        [SetUp]
        public void SetUp()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "LazyChatTests");
            Directory.CreateDirectory(tempDir);
            _dbPath = Path.Combine(tempDir, $"history_{Guid.NewGuid():N}.db");
            _store = new ChatHistoryStore(_dbPath);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        [Test]
        public void TryGetFirstUnreadAnchor_ReturnsFirstUnread()
        {
            // Arrange
            string peerId = "peer-1";
            DateTime baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            List<ChatMessage> messages = BuildMessages(baseTime, 5);

            _store.SaveMessage(messages[0], peerId, "peer", isRead: true);
            _store.SaveMessage(messages[1], peerId, "peer", isRead: true);
            _store.SaveMessage(messages[2], peerId, "peer", isRead: false);
            _store.SaveMessage(messages[3], peerId, "peer", isRead: false);
            _store.SaveMessage(messages[4], peerId, "peer", isRead: true);

            // Act
            bool found = _store.TryGetFirstUnreadAnchor(peerId, out DateTime? anchorUtc, out string anchorId);

            // Assert
            Assert.That(found, Is.True);
            Assert.That(anchorId, Is.EqualTo("m2"));
            Assert.That(anchorUtc.HasValue, Is.True);
            Assert.That(anchorUtc.Value.ToUniversalTime(), Is.EqualTo(baseTime.AddMinutes(2)));
        }

        [Test]
        public void LoadMessagesEndingAt_Limit_ReturnsTailBeforeAnchor()
        {
            // Arrange
            string peerId = "peer-2";
            DateTime baseTime = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            List<ChatMessage> messages = BuildMessages(baseTime, 5);
            foreach (ChatMessage message in messages)
            {
                _store.SaveMessage(message, peerId, "peer", isRead: true);
            }

            // Act
            List<ChatMessage> result = _store.LoadMessagesEndingAt(peerId, baseTime.AddMinutes(2), "m2", 2, out bool hasMoreBefore);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].MessageId, Is.EqualTo("m1"));
            Assert.That(result[1].MessageId, Is.EqualTo("m2"));
            Assert.That(hasMoreBefore, Is.True);
        }

        [Test]
        public void LoadMessagesAfter_Limit_ReturnsHeadAfterAnchor()
        {
            // Arrange
            string peerId = "peer-3";
            DateTime baseTime = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc);
            List<ChatMessage> messages = BuildMessages(baseTime, 5);
            foreach (ChatMessage message in messages)
            {
                _store.SaveMessage(message, peerId, "peer", isRead: false);
            }

            // Act
            List<ChatMessage> result = _store.LoadMessagesAfter(peerId, baseTime.AddMinutes(2), "m2", 1, out bool hasMoreAfter);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].MessageId, Is.EqualTo("m3"));
            Assert.That(hasMoreAfter, Is.True);
        }

        [Test]
        public void LoadMessagesStartingAt_IncludesAnchorAndAfter()
        {
            // Arrange
            string peerId = "peer-5";
            DateTime baseTime = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            List<ChatMessage> messages = BuildMessages(baseTime, 4);
            foreach (ChatMessage message in messages)
            {
                _store.SaveMessage(message, peerId, "peer", isRead: false);
            }

            // Act
            List<ChatMessage> result = _store.LoadMessagesStartingAt(peerId, baseTime.AddMinutes(1), "m1", 2, out bool hasMoreAfter);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].MessageId, Is.EqualTo("m1"));
            Assert.That(result[1].MessageId, Is.EqualTo("m2"));
            Assert.That(hasMoreAfter, Is.True);
        }

        [Test]
        public void HasMessagesBefore_ReturnsTrueWhenOlderExists()
        {
            // Arrange
            string peerId = "peer-6";
            DateTime baseTime = new DateTime(2025, 1, 6, 0, 0, 0, DateTimeKind.Utc);
            List<ChatMessage> messages = BuildMessages(baseTime, 3);
            foreach (ChatMessage message in messages)
            {
                _store.SaveMessage(message, peerId, "peer", isRead: true);
            }

            // Act
            bool hasBefore = _store.HasMessagesBefore(peerId, baseTime.AddMinutes(1), "m1");

            // Assert
            Assert.That(hasBefore, Is.True);
        }

        [Test]
        public void HasMessagesAfter_ReturnsFalseForLastMessage()
        {
            // Arrange
            string peerId = "peer-4";
            DateTime baseTime = new DateTime(2025, 1, 4, 0, 0, 0, DateTimeKind.Utc);
            List<ChatMessage> messages = BuildMessages(baseTime, 3);
            foreach (ChatMessage message in messages)
            {
                _store.SaveMessage(message, peerId, "peer", isRead: true);
            }

            // Act
            bool hasAfter = _store.HasMessagesAfter(peerId, baseTime.AddMinutes(2), "m2");

            // Assert
            Assert.That(hasAfter, Is.False);
        }

        private static List<ChatMessage> BuildMessages(DateTime baseTimeUtc, int count)
        {
            List<ChatMessage> messages = new List<ChatMessage>(count);
            for (int i = 0; i < count; i++)
            {
                messages.Add(new ChatMessage
                {
                    MessageId = "m" + i,
                    SenderId = "sender",
                    SenderName = "sender",
                    ReceiverId = "receiver",
                    Timestamp = baseTimeUtc.AddMinutes(i),
                    MessageType = ChatMessageType.Text,
                    TextContent = "msg-" + i,
                    IsSentByMe = false
                });
            }

            return messages;
        }
    }
}
