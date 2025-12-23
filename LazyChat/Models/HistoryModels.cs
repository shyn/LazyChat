using System;

namespace LazyChat.Models
{
    public class ConversationSummary
    {
        public string PeerId { get; set; }
        public string PeerName { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
    }
}
