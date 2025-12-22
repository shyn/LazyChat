using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LazyChat.Models;
using Microsoft.Data.Sqlite;

namespace LazyChat.Services
{
    public sealed class ChatHistoryStore : IDisposable
    {
        private const int DefaultRetentionDays = 30;
        private const int DefaultMaxMessages = 10000;
        private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(1);

        private readonly object _lock = new object();
        private readonly string _connectionString;
        private DateTime _lastPruneUtc = DateTime.MinValue;
        private bool _initialized;

        public ChatHistoryStore(string databasePath = null)
        {
            string dbPath = databasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LazyChat",
                "history.db");

            string dbDir = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            _connectionString = $"Data Source={dbPath};Cache=Shared";
        }

        public void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    return;
                }

                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
CREATE TABLE IF NOT EXISTS messages (
    message_id TEXT PRIMARY KEY,
    peer_id TEXT NOT NULL,
    peer_name TEXT NOT NULL,
    sender_id TEXT NOT NULL,
    sender_name TEXT NOT NULL,
    receiver_id TEXT,
    timestamp TEXT NOT NULL,
    message_type INTEGER NOT NULL,
    text_content TEXT,
    image_data BLOB,
    file_name TEXT,
    file_size INTEGER,
    is_sent_by_me INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_messages_peer_time ON messages(peer_id, timestamp);
";
                        command.ExecuteNonQuery();
                    }
                }

                _initialized = true;
            }
        }

        public void SaveMessage(ChatMessage message, string peerId, string peerName)
        {
            if (message == null || string.IsNullOrWhiteSpace(peerId))
            {
                return;
            }

            Initialize();

            lock (_lock)
            {
                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
INSERT OR REPLACE INTO messages (
    message_id,
    peer_id,
    peer_name,
    sender_id,
    sender_name,
    receiver_id,
    timestamp,
    message_type,
    text_content,
    image_data,
    file_name,
    file_size,
    is_sent_by_me
) VALUES (
    @message_id,
    @peer_id,
    @peer_name,
    @sender_id,
    @sender_name,
    @receiver_id,
    @timestamp,
    @message_type,
    @text_content,
    @image_data,
    @file_name,
    @file_size,
    @is_sent_by_me
);
";

                        command.Parameters.AddWithValue("@message_id", message.MessageId);
                        command.Parameters.AddWithValue("@peer_id", peerId);
                        command.Parameters.AddWithValue("@peer_name", peerName ?? string.Empty);
                        command.Parameters.AddWithValue("@sender_id", message.SenderId ?? string.Empty);
                        command.Parameters.AddWithValue("@sender_name", message.SenderName ?? string.Empty);
                        command.Parameters.AddWithValue("@receiver_id", message.ReceiverId ?? string.Empty);
                        DateTime utcTimestamp = message.Timestamp.ToUniversalTime();
                        command.Parameters.AddWithValue("@timestamp", utcTimestamp.ToString("o", CultureInfo.InvariantCulture));
                        command.Parameters.AddWithValue("@message_type", (int)message.MessageType);
                        command.Parameters.AddWithValue("@text_content", message.TextContent ?? string.Empty);
                        command.Parameters.AddWithValue("@image_data", (object)message.ImageBytes ?? DBNull.Value);
                        command.Parameters.AddWithValue("@file_name", message.FileName ?? string.Empty);
                        command.Parameters.AddWithValue("@file_size", message.FileSize);
                        command.Parameters.AddWithValue("@is_sent_by_me", message.IsSentByMe ? 1 : 0);

                        command.ExecuteNonQuery();
                    }

                    PruneHistoryIfNeeded(connection);
                }
            }
        }

        public List<ConversationSummary> LoadRecentConversations(int limit)
        {
            Initialize();

            List<ConversationSummary> result = new List<ConversationSummary>();

            lock (_lock)
            {
                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT m.peer_id, m.peer_name, m.timestamp
FROM messages m
INNER JOIN (
    SELECT peer_id, MAX(timestamp) AS last_time
    FROM messages
    GROUP BY peer_id
) t ON m.peer_id = t.peer_id AND m.timestamp = t.last_time
ORDER BY t.last_time DESC
LIMIT @limit;
";
                        command.Parameters.AddWithValue("@limit", limit);

                        using (SqliteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string peerId = reader.GetString(0);
                                string peerName = reader.GetString(1);
                                string timestamp = reader.GetString(2);

                                DateTime parsedTime = DateTime.Parse(
                                    timestamp,
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind);

                                if (parsedTime.Kind == DateTimeKind.Utc)
                                {
                                    parsedTime = parsedTime.ToLocalTime();
                                }

                                result.Add(new ConversationSummary
                                {
                                    PeerId = peerId,
                                    PeerName = peerName,
                                    LastMessageTime = parsedTime
                                });
                            }
                        }
                    }
                }
            }

            return result;
        }

        public List<ChatMessage> LoadMessagesForPeer(string peerId, int limit)
        {
            Initialize();

            List<ChatMessage> result = new List<ChatMessage>();

            if (string.IsNullOrWhiteSpace(peerId))
            {
                return result;
            }

            lock (_lock)
            {
                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT
    message_id,
    sender_id,
    sender_name,
    receiver_id,
    timestamp,
    message_type,
    text_content,
    image_data,
    file_name,
    file_size,
    is_sent_by_me
FROM messages
WHERE peer_id = @peer_id
ORDER BY timestamp DESC
LIMIT @limit;
";
                        command.Parameters.AddWithValue("@peer_id", peerId);
                        command.Parameters.AddWithValue("@limit", limit);

                        using (SqliteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string messageId = reader.GetString(0);
                                string senderId = reader.GetString(1);
                                string senderName = reader.GetString(2);
                                string receiverId = reader.GetString(3);
                                string timestamp = reader.GetString(4);
                                int messageType = reader.GetInt32(5);
                                string textContent = reader.GetString(6);
                                byte[] imageData = reader.IsDBNull(7) ? null : (byte[])reader.GetValue(7);
                                string fileName = reader.GetString(8);
                                long fileSize = reader.GetInt64(9);
                                bool isSentByMe = reader.GetInt32(10) == 1;

                                DateTime parsedTime = DateTime.Parse(
                                    timestamp,
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind);

                                if (parsedTime.Kind == DateTimeKind.Utc)
                                {
                                    parsedTime = parsedTime.ToLocalTime();
                                }

                                ChatMessage message = new ChatMessage
                                {
                                    MessageId = messageId,
                                    SenderId = senderId,
                                    SenderName = senderName,
                                    ReceiverId = receiverId,
                                    Timestamp = parsedTime,
                                    MessageType = (ChatMessageType)messageType,
                                    TextContent = textContent,
                                    FileName = fileName,
                                    FileSize = fileSize,
                                    IsSentByMe = isSentByMe,
                                    ImageBytes = imageData
                                };

                                if (imageData != null && imageData.Length > 0)
                                {
                                    try
                                    {
                                        using (MemoryStream ms = new MemoryStream(imageData))
                                        {
                                            message.ImageContent = new Avalonia.Media.Imaging.Bitmap(ms);
                                        }
                                    }
                                    catch
                                    {
                                        message.MessageType = ChatMessageType.Text;
                                        message.TextContent = "图片加载失败";
                                    }
                                }

                                result.Add(message);
                            }
                        }
                    }
                }
            }

            result.Reverse();
            return result;
        }

        private void PruneHistoryIfNeeded(SqliteConnection connection)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastPruneUtc < PruneInterval)
            {
                return;
            }

            _lastPruneUtc = nowUtc;

            // TODO: Make retention policy configurable (days and max messages).
            DateTime cutoffUtc = nowUtc.AddDays(-DefaultRetentionDays);
            string cutoffText = cutoffUtc.ToString("o", CultureInfo.InvariantCulture);

            using (SqliteTransaction transaction = connection.BeginTransaction())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText = "DELETE FROM messages WHERE timestamp < @cutoff;";
                command.Parameters.AddWithValue("@cutoff", cutoffText);
                command.ExecuteNonQuery();

                command.Parameters.Clear();
                command.CommandText = @"
DELETE FROM messages
WHERE message_id IN (
    SELECT message_id FROM messages
    ORDER BY timestamp DESC
    LIMIT -1 OFFSET @max
);
";
                command.Parameters.AddWithValue("@max", DefaultMaxMessages);
                command.ExecuteNonQuery();

                transaction.Commit();
            }

            using (SqliteCommand vacuum = connection.CreateCommand())
            {
                vacuum.CommandText = "VACUUM;";
                vacuum.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
        }

        public void DeleteConversation(string peerId)
        {
            if (string.IsNullOrWhiteSpace(peerId)) return;

            Initialize();

            lock (_lock)
            {
                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM messages WHERE peer_id = @peer_id;";
                        command.Parameters.AddWithValue("@peer_id", peerId);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
