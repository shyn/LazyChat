using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace LazyChat.Models
{
    public class ChatUser
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string IpAddress { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastActivity { get; set; }

        public ChatUser()
        {
            LastActivity = DateTime.Now;
            IsOnline = true;
        }
    }

    public class ChatMessage
    {
        public string MessageId { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string ReceiverId { get; set; }
        public DateTime Timestamp { get; set; }
        public ChatMessageType MessageType { get; set; }
        public string TextContent { get; set; }
        public Bitmap ImageContent { get; set; }
        public byte[] ImageBytes { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public bool IsGroupMessage { get; set; }
        public bool IsSentByMe { get; set; }

        public ChatMessage()
        {
            MessageId = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
        }
    }

    public enum ChatMessageType
    {
        Text,
        Image,
        File,
        System
    }

    public class Conversation
    {
        public string ConversationId { get; set; }
        public string PeerId { get; set; }
        public string PeerName { get; set; }
        public List<ChatMessage> Messages { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool IsGroupChat { get; set; }

        public Conversation()
        {
            ConversationId = Guid.NewGuid().ToString();
            Messages = new List<ChatMessage>();
            LastMessageTime = DateTime.Now;
        }
    }
}
