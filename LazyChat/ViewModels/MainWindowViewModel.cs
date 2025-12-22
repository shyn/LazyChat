using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LazyChat.Models;
using LazyChat.Services;

namespace LazyChat.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private const int COMMUNICATION_PORT = 9999;
        private string _userName;
        private PeerDiscoveryService _discoveryService;
        private P2PCommunicationService _commService;
        private FileTransferService _fileTransferService;
        private ChatHistoryStore _historyStore;
        private Dictionary<string, Conversation> _conversations;
        private PeerInfo _selectedPeer;
        private ConversationListItem _selectedConversation;
        private string _statusText;
        private string _messageText;
        private string _searchText;
        private Dictionary<string, Window> _activeTransferWindows;
        private bool _isInitialized;
        private const int RecentConversationLimit = 50;
        private const int HistoryLoadLimit = 200;

        public ObservableCollection<PeerInfo> OnlinePeers { get; }
        public ObservableCollection<ConversationListItem> RecentConversations { get; }
        public ObservableCollection<MessageViewModel> Messages { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel()
        {
            OnlinePeers = new ObservableCollection<PeerInfo>();
            RecentConversations = new ObservableCollection<ConversationListItem>();
            Messages = new ObservableCollection<MessageViewModel>();
            _conversations = new Dictionary<string, Conversation>();
            _activeTransferWindows = new Dictionary<string, Window>();
            _statusText = "正在连接...";

            SendMessageCommand = new RelayCommand(SendTextMessage, () => CanSendMessage());
            SendMessageOnEnterCommand = new RelayCommand(SendTextMessageOnEnter);
            AttachImageCommand = new RelayCommand(async () => await AttachImageAsync(), () => CanSendToSelectedPeer());
            AttachFileCommand = new RelayCommand(async () => await AttachFileAsync(), () => CanSendToSelectedPeer());
            SetUsernameCommand = new RelayCommand(async () => await SetUsernameAsync());
            ExitCommand = new RelayCommand(Exit);
            AboutCommand = new RelayCommand(ShowAbout);

            OnlinePeers.CollectionChanged += (_, __) => 
            {
                UpdateCommandStates();
                OnPropertyChanged(nameof(HasOnlinePeers));
                OnPropertyChanged(nameof(SelectedPeerStatus));
                OnPropertyChanged(nameof(SelectedPeerStatusColor));
                OnPropertyChanged(nameof(SelectedPeerAvatarBackground));
                OnPropertyChanged(nameof(SelectedPeerAvatarForeground));
            };
            RecentConversations.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasRecentConversations));
        }

        // ========== NEW UI PROPERTIES ==========
        
        public string CurrentUserDisplayText => $"@ {_userName ?? Environment.UserName}";
        
        public bool HasOnlinePeers => OnlinePeers.Count > 0;
        
        public bool HasRecentConversations => FilteredRecentConversations.Any();
        
        public bool HasSelectedPeer => SelectedPeer != null;
        
        public string SelectedPeerName => SelectedPeer?.UserName ?? "";
        
        public string SelectedPeerInitial => GetInitial(SelectedPeer?.UserName);
        
        public string SelectedPeerStatus => IsSelectedPeerOnline() ? "在线" : "离线";
        
        public IBrush SelectedPeerStatusColor => IsSelectedPeerOnline() 
            ? new SolidColorBrush(Color.Parse("#34C759")) 
            : new SolidColorBrush(Color.Parse("#AEAEB2"));
        
        public IBrush SelectedPeerAvatarBackground => IsSelectedPeerOnline()
            ? new SolidColorBrush(Color.Parse("#E8F5E9"))
            : new SolidColorBrush(Color.Parse("#F5F5F7"));
        
        public IBrush SelectedPeerAvatarForeground => IsSelectedPeerOnline()
            ? new SolidColorBrush(Color.Parse("#2E7D32"))
            : new SolidColorBrush(Color.Parse("#86868B"));

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasRecentConversations));
                    OnPropertyChanged(nameof(FilteredRecentConversations));
                }
            }
        }
        
        public IEnumerable<ConversationListItem> FilteredRecentConversations
        {
            get
            {
                // Filter out conversations that are already in OnlinePeers
                var onlinePeerIds = OnlinePeers.Select(p => p.PeerId).ToHashSet();
                var filtered = RecentConversations.Where(c => !onlinePeerIds.Contains(c.PeerId));
                
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(c => 
                        c.PeerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                }
                
                return filtered;
            }
        }

        private string GetInitial(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            return name.Substring(0, 1).ToUpperInvariant();
        }

        // ========== EXISTING PROPERTIES ==========

        public PeerInfo SelectedPeer
        {
            get => _selectedPeer;
            set
            {
                if (_selectedPeer != value)
                {
                    _selectedPeer = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelectedPeer));
                    OnPropertyChanged(nameof(SelectedPeerName));
                    OnPropertyChanged(nameof(SelectedPeerInitial));
                    OnPropertyChanged(nameof(SelectedPeerStatus));
                    OnPropertyChanged(nameof(SelectedPeerStatusColor));
                    OnPropertyChanged(nameof(SelectedPeerAvatarBackground));
                    OnPropertyChanged(nameof(SelectedPeerAvatarForeground));
                    LoadConversation(value);
                    UpdateCommandStates();

                    if (_selectedPeer != null)
                    {
                        SyncConversationSelection(_selectedPeer.PeerId);
                    }
                }
            }
        }

        public ConversationListItem SelectedConversation
        {
            get => _selectedConversation;
            set
            {
                if (_selectedConversation != value)
                {
                    _selectedConversation = value;
                    OnPropertyChanged();
                    
                    // When selecting a conversation from the list, update the peer
                    if (_selectedConversation != null && 
                        (_selectedPeer == null || _selectedPeer.PeerId != _selectedConversation.PeerId))
                    {
                        PeerInfo peer = OnlinePeers.FirstOrDefault(p => p.PeerId == _selectedConversation.PeerId);
                        if (peer == null)
                        {
                            peer = new PeerInfo
                            {
                                PeerId = _selectedConversation.PeerId,
                                UserName = _selectedConversation.PeerName,
                                IsOnline = false
                            };
                        }
                        _selectedPeer = peer;
                        OnPropertyChanged(nameof(SelectedPeer));
                        OnPropertyChanged(nameof(HasSelectedPeer));
                        OnPropertyChanged(nameof(SelectedPeerName));
                        OnPropertyChanged(nameof(SelectedPeerInitial));
                        OnPropertyChanged(nameof(SelectedPeerStatus));
                        OnPropertyChanged(nameof(SelectedPeerStatusColor));
                        OnPropertyChanged(nameof(SelectedPeerAvatarBackground));
                        OnPropertyChanged(nameof(SelectedPeerAvatarForeground));
                        LoadConversation(peer);
                        UpdateCommandStates();
                    }
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                if (_messageText != value)
                {
                    _messageText = value;
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        // ========== COMMANDS ==========
        
        public ICommand SendMessageCommand { get; }
        public ICommand SendMessageOnEnterCommand { get; }
        public ICommand AttachImageCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand SetUsernameCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand AboutCommand { get; }

        // ========== INITIALIZATION ==========

        public async Task InitializeAsync(Window owner)
        {
            if (_isInitialized) return;
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            _userName = LoadSavedUsername() ?? Environment.UserName;

            if (string.IsNullOrWhiteSpace(LoadSavedUsername()))
            {
                var inputDialog = new Views.InputDialog("设置用户名", "请输入您的用户名:", _userName);
                var result = await inputDialog.ShowDialog<bool>(owner);
                if (result && !string.IsNullOrWhiteSpace(inputDialog.InputText))
                {
                    _userName = inputDialog.InputText;
                    SaveUsername(_userName);
                }
            }

            OnPropertyChanged(nameof(CurrentUserDisplayText));
            InitializeServices();
            StartServices();
            InitializeHistory();
            _isInitialized = true;
        }

        private void InitializeServices()
        {
            _discoveryService = new PeerDiscoveryService(_userName, COMMUNICATION_PORT);
            var localPeerId = _discoveryService.GetLocalPeer().PeerId;

            _commService = new P2PCommunicationService(COMMUNICATION_PORT, localPeerId);
            _fileTransferService = new FileTransferService(_commService, localPeerId);

            _discoveryService.PeerDiscovered += DiscoveryService_PeerDiscovered;
            _discoveryService.PeerLeft += DiscoveryService_PeerLeft;
            _discoveryService.ErrorOccurred += Service_ErrorOccurred;

            _commService.MessageReceived += CommService_MessageReceived;
            _commService.ErrorOccurred += Service_ErrorOccurred;

            _fileTransferService.TransferRequestReceived += FileTransferService_TransferRequestReceived;
            _fileTransferService.TransferProgressChanged += FileTransferService_TransferProgressChanged;
            _fileTransferService.TransferCompleted += FileTransferService_TransferCompleted;
            _fileTransferService.TransferFailed += FileTransferService_TransferFailed;
            _fileTransferService.TransferStarted += FileTransferService_TransferStarted;
        }

        private void StartServices()
        {
            try
            {
                _commService.Start();
                _discoveryService.Start();
                StatusText = "正在搜索局域网用户...";
            }
            catch (Exception ex)
            {
                ShowError("启动服务失败: " + ex.Message);
            }
        }

        private void InitializeHistory()
        {
            try
            {
                _historyStore = new ChatHistoryStore();
                _historyStore.Initialize();
                LoadRecentConversations();
            }
            catch (Exception ex)
            {
                ShowError("初始化历史记录失败: " + ex.Message);
            }
        }

        // ========== PEER SELECTION ==========

        private void SyncConversationSelection(string peerId)
        {
            var item = RecentConversations.FirstOrDefault(c => c.PeerId == peerId);
            if (item != null && _selectedConversation != item)
            {
                if (_selectedConversation != null)
                    _selectedConversation.IsSelected = false;
                
                _selectedConversation = item;
                _selectedConversation.IsSelected = true;
                OnPropertyChanged(nameof(SelectedConversation));
            }
        }

        // ========== PEER DISCOVERY EVENTS ==========

        private void DiscoveryService_PeerDiscovered(object sender, PeerInfo peer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var existing = OnlinePeers.FirstOrDefault(p => p.PeerId == peer.PeerId);
                if (existing == null)
                {
                    peer.Initial = GetInitial(peer.UserName);
                    OnlinePeers.Add(peer);
                    StatusText = $"{peer.UserName} 已上线";
                    UpdateCommandStates();
                    UpdateConversationPresence(peer.PeerId, true);
                    OnPropertyChanged(nameof(FilteredRecentConversations));
                    OnPropertyChanged(nameof(HasRecentConversations));

                    if (SelectedPeer != null && SelectedPeer.PeerId == peer.PeerId)
                    {
                        SelectedPeer = peer;
                    }
                }
            });
        }

        private void DiscoveryService_PeerLeft(object sender, PeerInfo peer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var existingPeer = OnlinePeers.FirstOrDefault(p => p.PeerId == peer.PeerId);
                if (existingPeer != null)
                {
                    OnlinePeers.Remove(existingPeer);
                    StatusText = $"{peer.UserName} 已离线";

                    if (SelectedPeer?.PeerId == peer.PeerId)
                    {
                        OnPropertyChanged(nameof(SelectedPeerStatus));
                        OnPropertyChanged(nameof(SelectedPeerStatusColor));
                        OnPropertyChanged(nameof(SelectedPeerAvatarBackground));
                        OnPropertyChanged(nameof(SelectedPeerAvatarForeground));
                    }

                    UpdateCommandStates();
                    UpdateConversationPresence(peer.PeerId, false);
                    OnPropertyChanged(nameof(FilteredRecentConversations));
                    OnPropertyChanged(nameof(HasRecentConversations));
                }
            });
        }

        // ========== MESSAGE HANDLING ==========

        private void CommService_MessageReceived(object sender, NetworkMessage message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                switch (message.Type)
                {
                    case MessageType.TextMessage:
                        HandleTextMessage(message);
                        break;
                    case MessageType.ImageMessage:
                        HandleImageMessage(message);
                        break;
                    case MessageType.FileTransferRequest:
                        _fileTransferService.HandleTransferRequest(message);
                        break;
                    case MessageType.FileTransferAccept:
                        HandleFileTransferAccept(message);
                        break;
                    case MessageType.FileTransferReject:
                        HandleFileTransferReject(message);
                        break;
                    case MessageType.FileTransferData:
                        _fileTransferService.HandleReceivedChunk(message);
                        break;
                    case MessageType.FileTransferComplete:
                        _fileTransferService.HandleTransferComplete(message.FileId);
                        break;
                }
            });
        }

        private void HandleTextMessage(NetworkMessage message)
        {
            ChatMessage chatMsg = new ChatMessage
            {
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                ReceiverId = _discoveryService.GetLocalPeer().PeerId,
                TextContent = message.TextContent,
                MessageType = ChatMessageType.Text,
                Timestamp = message.Timestamp,
                IsSentByMe = false
            };

            AddMessageToConversation(message.SenderId, message.SenderName, chatMsg);
        }

        private void HandleImageMessage(NetworkMessage message)
        {
            ChatMessage chatMsg = new ChatMessage
            {
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                ReceiverId = _discoveryService.GetLocalPeer().PeerId,
                MessageType = ChatMessageType.Image,
                Timestamp = message.Timestamp,
                IsSentByMe = false,
                ImageBytes = message.Data
            };

            try
            {
                using (MemoryStream ms = new MemoryStream(message.Data))
                {
                    chatMsg.ImageContent = new Bitmap(ms);
                }
            }
            catch
            {
                chatMsg.MessageType = ChatMessageType.System;
                chatMsg.TextContent = "无法加载图片";
            }

            AddMessageToConversation(message.SenderId, message.SenderName, chatMsg);
        }

        private void HandleFileTransferAccept(NetworkMessage message)
        {
            StatusText = $"{message.SenderName} 接受了文件传输";
            _fileTransferService.HandleTransferAccepted(message.FileId);
            if (_activeTransferWindows.ContainsKey(message.FileId) && 
                _activeTransferWindows[message.FileId] is Views.FileTransferWindow window)
            {
                window.UpdateInfoText($"正在发送文件给 {message.SenderName}...");
                window.MarkSending();
            }
        }

        private void HandleFileTransferReject(NetworkMessage message)
        {
            StatusText = $"{message.SenderName} 拒绝了文件传输";
            _fileTransferService.HandleTransferRejected(message.FileId);
            if (_activeTransferWindows.ContainsKey(message.FileId))
            {
                _activeTransferWindows[message.FileId].Close();
                _activeTransferWindows.Remove(message.FileId);
            }
        }

        // ========== FILE TRANSFER ==========

        private void FileTransferService_TransferRequestReceived(object sender, FileTransferInfo transfer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                ChatMessage fileMessage = new ChatMessage
                {
                    SenderId = transfer.SenderId,
                    SenderName = transfer.SenderName,
                    ReceiverId = _discoveryService.GetLocalPeer().PeerId,
                    MessageType = ChatMessageType.File,
                    FileName = transfer.FileName,
                    FileSize = transfer.FileSize,
                    IsSentByMe = false
                };
                AddMessageToConversation(transfer.SenderId, transfer.SenderName, fileMessage);

                var dialog = new Views.FileTransferWindow(transfer, true);
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    _activeTransferWindows[transfer.FileId] = dialog;
                    var result = await dialog.ShowDialog<bool>(desktop.MainWindow);

                    if (result && dialog.IsAccepted)
                    {
                        _fileTransferService.AcceptFileTransfer(transfer.FileId, dialog.SavePath);
                        PeerInfo peer = OnlinePeers.FirstOrDefault(p => p.PeerId == transfer.SenderId);
                        if (peer != null)
                        {
                            _commService.SendFileTransferResponse(true, transfer.FileId, peer, _userName);
                        }
                    }
                    else
                    {
                        _fileTransferService.RejectFileTransfer(transfer.FileId);
                        PeerInfo peer = OnlinePeers.FirstOrDefault(p => p.PeerId == transfer.SenderId);
                        if (peer != null)
                        {
                            _commService.SendFileTransferResponse(false, transfer.FileId, peer, _userName);
                        }
                        _activeTransferWindows.Remove(transfer.FileId);
                    }
                }
            });
        }

        private void FileTransferService_TransferStarted(object sender, FileTransferInfo transfer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                string peerName = GetPeerDisplayName(transfer.ReceiverId);
                var dialog = new Views.FileTransferWindow(transfer, false);
                dialog.UpdateInfoText($"等待 {peerName} 接受文件...");

                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    _activeTransferWindows[transfer.FileId] = dialog;
                    dialog.Show(desktop.MainWindow);
                }
            });
        }

        private void FileTransferService_TransferProgressChanged(object sender, FileTransferInfo transfer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_activeTransferWindows.ContainsKey(transfer.FileId) && 
                    _activeTransferWindows[transfer.FileId] is Views.FileTransferWindow window)
                {
                    window.UpdateProgress(transfer.GetProgress());
                }
            });
        }

        private void FileTransferService_TransferCompleted(object sender, FileTransferInfo transfer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"文件传输完成: {transfer.FileName}";
                if (_activeTransferWindows.ContainsKey(transfer.FileId))
                {
                    _activeTransferWindows[transfer.FileId].Close();
                    _activeTransferWindows.Remove(transfer.FileId);
                }
            });
        }

        private void FileTransferService_TransferFailed(object sender, FileTransferInfo transfer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"文件传输失败: {transfer.FileName}";
                ShowError("文件传输失败!");
                if (_activeTransferWindows.ContainsKey(transfer.FileId))
                {
                    _activeTransferWindows[transfer.FileId].Close();
                    _activeTransferWindows.Remove(transfer.FileId);
                }
            });
        }

        // ========== CONVERSATION MANAGEMENT ==========

        private void AddMessageToConversation(string peerId, string peerName, ChatMessage message)
        {
            if (_historyStore != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        _historyStore.SaveMessage(message, peerId, peerName);
                    }
                    catch (Exception ex)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = "保存历史记录失败: " + ex.Message;
                        });
                    }
                });
            }

            if (!_conversations.ContainsKey(peerId))
            {
                _conversations[peerId] = new Conversation
                {
                    PeerId = peerId,
                    PeerName = peerName
                };
            }

            Conversation conv = _conversations[peerId];
            conv.PeerName = peerName;
            conv.Messages.Add(message);
            conv.LastMessageTime = message.Timestamp;

            if (SelectedPeer != null && SelectedPeer.PeerId == peerId)
            {
                DisplayMessage(message);
                ScrollToBottom();
            }
            else
            {
                conv.UnreadCount++;
                
                // Update unread badge on peer
                var peer = OnlinePeers.FirstOrDefault(p => p.PeerId == peerId);
                if (peer != null)
                {
                    peer.UnreadCount = conv.UnreadCount;
                }
            }

            UpsertRecentConversation(peerId, peerName, message.Timestamp, conv.UnreadCount);
            UpdateConversationUnread(peerId, conv.UnreadCount);
        }

        private void DisplayMessage(ChatMessage message)
        {
            // Check if we need a date separator
            bool showDateSeparator = false;
            if (Messages.Count == 0)
            {
                showDateSeparator = true;
            }
            else
            {
                var lastMsg = Messages.LastOrDefault();
                if (lastMsg != null && lastMsg.Timestamp.Date != message.Timestamp.Date)
                {
                    showDateSeparator = true;
                }
            }

            // Determine if we should show sender name
            bool showSenderName = !message.IsSentByMe;
            if (showSenderName && Messages.Count > 0)
            {
                var lastMsg = Messages.LastOrDefault();
                if (lastMsg != null && !lastMsg.IsSentByMe && lastMsg.SenderName == message.SenderName &&
                    (message.Timestamp - lastMsg.Timestamp).TotalMinutes < 5)
                {
                    showSenderName = false;
                }
            }

            Messages.Add(new MessageViewModel(message, showDateSeparator, showSenderName));
        }

        private void LoadConversation(PeerInfo peer)
        {
            Messages.Clear();

            if (peer != null)
            {
                Conversation conv;
                if (!_conversations.ContainsKey(peer.PeerId))
                {
                    conv = new Conversation
                    {
                        PeerId = peer.PeerId,
                        PeerName = peer.UserName
                    };
                    _conversations[peer.PeerId] = conv;
                }
                else
                {
                    conv = _conversations[peer.PeerId];
                }

                conv.PeerName = peer.UserName;
                conv.Messages.Clear();
                conv.UnreadCount = 0;
                
                // Clear unread badge on peer
                var onlinePeer = OnlinePeers.FirstOrDefault(p => p.PeerId == peer.PeerId);
                if (onlinePeer != null)
                {
                    onlinePeer.UnreadCount = 0;
                }

                string currentPeerId = peer.PeerId;
                Task.Run(() =>
                {
                    return _historyStore?.LoadMessagesForPeer(currentPeerId, HistoryLoadLimit) ?? new List<ChatMessage>();
                }).ContinueWith(t =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (SelectedPeer == null || SelectedPeer.PeerId != currentPeerId) return;

                        foreach (ChatMessage msg in t.Result)
                        {
                            conv.Messages.Add(msg);
                            DisplayMessage(msg);
                        }
                        
                        ScrollToBottom();
                    });
                });

                UpdateConversationUnread(peer.PeerId, conv.UnreadCount);
            }
        }

        private void LoadRecentConversations()
        {
            RecentConversations.Clear();

            Task.Run(() =>
            {
                return _historyStore?.LoadRecentConversations(RecentConversationLimit) ?? new List<ConversationSummary>();
            }).ContinueWith(t =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    foreach (ConversationSummary item in t.Result)
                    {
                        bool isOnline = OnlinePeers.Any(p => p.PeerId == item.PeerId);
                        RecentConversations.Add(new ConversationListItem(item.PeerId, item.PeerName, item.LastMessageTime, isOnline, 0));
                    }
                    OnPropertyChanged(nameof(HasRecentConversations));
                    OnPropertyChanged(nameof(FilteredRecentConversations));
                });
            });
        }

        private void UpsertRecentConversation(string peerId, string peerName, DateTime lastMessageTime, int unreadCount)
        {
            ConversationListItem existing = RecentConversations.FirstOrDefault(c => c.PeerId == peerId);
            bool isOnline = OnlinePeers.Any(p => p.PeerId == peerId);

            if (existing == null)
            {
                RecentConversations.Insert(0, new ConversationListItem(peerId, peerName, lastMessageTime, isOnline, unreadCount));
            }
            else
            {
                existing.PeerName = peerName;
                existing.LastMessageTime = lastMessageTime;
                existing.IsOnline = isOnline;
                existing.UnreadCount = unreadCount;

                int index = RecentConversations.IndexOf(existing);
                if (index > 0)
                {
                    RecentConversations.Move(index, 0);
                }
            }

            while (RecentConversations.Count > RecentConversationLimit)
            {
                RecentConversations.RemoveAt(RecentConversations.Count - 1);
            }

            OnPropertyChanged(nameof(HasRecentConversations));
            OnPropertyChanged(nameof(FilteredRecentConversations));
        }

        private void UpdateConversationPresence(string peerId, bool isOnline)
        {
            ConversationListItem item = RecentConversations.FirstOrDefault(c => c.PeerId == peerId);
            if (item != null)
            {
                item.IsOnline = isOnline;
            }
        }

        private void UpdateConversationUnread(string peerId, int unreadCount)
        {
            ConversationListItem item = RecentConversations.FirstOrDefault(c => c.PeerId == peerId);
            if (item != null)
            {
                item.UnreadCount = unreadCount;
            }
        }

        private void ScrollToBottom()
        {
            // This will be handled by the view via behavior
        }

        // ========== SEND MESSAGES ==========

        private void SendTextMessage()
        {
            if (SelectedPeer == null || string.IsNullOrWhiteSpace(MessageText)) return;

            if (!IsSelectedPeerOnline())
            {
                ShowInfo("对方离线，无法发送消息!");
                return;
            }

            bool sent = _commService.SendTextMessage(MessageText, SelectedPeer, _userName);

            if (sent)
            {
                ChatMessage message = new ChatMessage
                {
                    SenderId = _discoveryService.GetLocalPeer().PeerId,
                    SenderName = _userName,
                    ReceiverId = SelectedPeer.PeerId,
                    TextContent = MessageText,
                    MessageType = ChatMessageType.Text,
                    IsSentByMe = true
                };

                AddMessageToConversation(SelectedPeer.PeerId, SelectedPeer.UserName, message);
                MessageText = string.Empty;
            }
            else
            {
                ShowError("发送失败!");
            }
        }

        private void SendTextMessageOnEnter()
        {
            // Only send if it's a single-line message (no newlines in current text)
            if (!string.IsNullOrWhiteSpace(MessageText) && !MessageText.Contains('\n'))
            {
                SendTextMessage();
            }
        }

        private async Task AttachImageAsync()
        {
            if (SelectedPeer == null)
            {
                ShowInfo("请先选择一个用户!");
                return;
            }

            if (!IsSelectedPeerOnline())
            {
                ShowInfo("对方离线，无法发送图片!");
                return;
            }

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "选择图片",
                    AllowMultiple = false,
                    FileTypeFilter = new[] {
                        new FilePickerFileType("图片文件") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp" } }
                    }
                });

                if (files.Count > 0)
                {
                    var filePath = files[0].Path.LocalPath;
                    await SendImageMessageAsync(filePath);
                }
            }
        }

        private async Task AttachFileAsync()
        {
            if (SelectedPeer == null)
            {
                ShowInfo("请先选择一个用户!");
                return;
            }

            if (!IsSelectedPeerOnline())
            {
                ShowInfo("对方离线，无法发送文件!");
                return;
            }

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "选择文件",
                    AllowMultiple = false
                });

                if (files.Count > 0)
                {
                    var filePath = files[0].Path.LocalPath;
                    FileInfo fileInfo = new FileInfo(filePath);
                    ChatMessage fileMessage = new ChatMessage
                    {
                        SenderId = _discoveryService.GetLocalPeer().PeerId,
                        SenderName = _userName,
                        ReceiverId = SelectedPeer.PeerId,
                        MessageType = ChatMessageType.File,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length,
                        IsSentByMe = true
                    };
                    AddMessageToConversation(SelectedPeer.PeerId, SelectedPeer.UserName, fileMessage);
                    _fileTransferService.StartSendingFile(filePath, SelectedPeer, _userName);
                }
            }
        }

        private async Task SendImageMessageAsync(string imagePath)
        {
            try
            {
                byte[] imageData = await Task.Run(() => File.ReadAllBytes(imagePath));
                if (!IsSelectedPeerOnline())
                {
                    ShowInfo("对方离线，无法发送图片!");
                    return;
                }

                bool sent = _commService.SendImageMessage(imageData, SelectedPeer, _userName);

                if (sent)
                {
                    ChatMessage message = new ChatMessage
                    {
                        SenderId = _discoveryService.GetLocalPeer().PeerId,
                        SenderName = _userName,
                        ReceiverId = SelectedPeer.PeerId,
                        ImageContent = new Bitmap(imagePath),
                        ImageBytes = imageData,
                        MessageType = ChatMessageType.Image,
                        IsSentByMe = true
                    };

                    AddMessageToConversation(SelectedPeer.PeerId, SelectedPeer.UserName, message);
                }
                else
                {
                    ShowError("发送图片失败!");
                }
            }
            catch (Exception ex)
            {
                ShowError("无法加载图片: " + ex.Message);
            }
        }

        // ========== SETTINGS ==========

        private async Task SetUsernameAsync()
        {
            var inputDialog = new Views.InputDialog("设置用户名", "请输入新的用户名:", _userName);
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await inputDialog.ShowDialog<bool>(desktop.MainWindow);
                if (result && !string.IsNullOrWhiteSpace(inputDialog.InputText))
                {
                    _userName = inputDialog.InputText;
                    SaveUsername(_userName);
                    OnPropertyChanged(nameof(CurrentUserDisplayText));
                    StatusText = "用户名已更新 (需要重启才能生效)";
                }
            }
        }

        private string LoadSavedUsername()
        {
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LazyChat", "config.txt");
                
                if (File.Exists(configPath))
                {
                    return File.ReadAllText(configPath).Trim();
                }
            }
            catch { }
            return null;
        }

        private void SaveUsername(string username)
        {
            try
            {
                string configDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LazyChat");
                
                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);
                
                string configPath = Path.Combine(configDir, "config.txt");
                File.WriteAllText(configPath, username);
            }
            catch { }
        }

        private void Exit()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        private void ShowAbout()
        {
            ShowInfo("LazyChat - 局域网聊天工具\n\n" +
                    "✨ 支持文字、图片和文件传输\n" +
                    "🔍 自动发现局域网用户\n" +
                    "💻 跨平台运行\n\n" +
                    "Version 2.0");
        }

        // ========== HELPERS ==========

        private void Service_ErrorOccurred(object sender, string error)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = "错误: " + error;
            });
        }

        private void ShowError(string message)
        {
            ShowMessageDialog("错误", message);
        }

        private void ShowInfo(string message)
        {
            ShowMessageDialog("提示", message);
        }

        private async void ShowMessageDialog(string title, string message)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dialog = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    Content = new Border
                    {
                        Padding = new Thickness(24),
                        Child = new StackPanel
                        {
                            Spacing = 20,
                            Children =
                            {
                                new TextBlock 
                                { 
                                    Text = message, 
                                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                    FontSize = 14
                                },
                                new Button 
                                { 
                                    Content = "确定", 
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                                    Padding = new Thickness(24, 8),
                                    Classes = { "primary" }
                                }
                            }
                        }
                    }
                };

                if (dialog.Content is Border border && 
                    border.Child is StackPanel panel && 
                    panel.Children.Count > 1 && 
                    panel.Children[1] is Button okButton)
                {
                    okButton.Click += (_, __) => dialog.Close(true);
                }

                await dialog.ShowDialog<bool>(desktop.MainWindow);
            }
        }

        private bool CanSendToSelectedPeer()
        {
            return SelectedPeer != null && IsSelectedPeerOnline();
        }

        private bool CanSendMessage()
        {
            return CanSendToSelectedPeer() && !string.IsNullOrWhiteSpace(MessageText);
        }

        private bool IsSelectedPeerOnline()
        {
            if (SelectedPeer == null) return false;
            return OnlinePeers.Any(p => p.PeerId == SelectedPeer.PeerId);
        }

        private string GetPeerDisplayName(string peerId)
        {
            var peer = OnlinePeers.FirstOrDefault(p => p.PeerId == peerId);
            return peer?.UserName ?? peerId;
        }

        private void UpdateCommandStates()
        {
            (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AttachImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AttachFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public void Cleanup()
        {
            _discoveryService?.Stop();
            _discoveryService?.Dispose();
            _commService?.Stop();
            _commService?.Dispose();
            _fileTransferService?.Dispose();
            _historyStore?.Dispose();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ========== VIEW MODELS ==========

    public class ConversationListItem : INotifyPropertyChanged
    {
        private string _peerName;
        private DateTime _lastMessageTime;
        private bool _isOnline;
        private int _unreadCount;
        private bool _isSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        public ConversationListItem(string peerId, string peerName, DateTime lastMessageTime, bool isOnline, int unreadCount)
        {
            PeerId = peerId;
            _peerName = peerName;
            _lastMessageTime = lastMessageTime;
            _isOnline = isOnline;
            _unreadCount = unreadCount;
        }

        public string PeerId { get; }

        public string PeerName
        {
            get => _peerName;
            set { if (_peerName != value) { _peerName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Initial)); } }
        }

        public DateTime LastMessageTime
        {
            get => _lastMessageTime;
            set { if (_lastMessageTime != value) { _lastMessageTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastMessageTimeText)); } }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set { if (_isOnline != value) { _isOnline = value; OnPropertyChanged(); } }
        }

        public int UnreadCount
        {
            get => _unreadCount;
            set { if (_unreadCount != value) { _unreadCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnread)); } }
        }

        public bool HasUnread => _unreadCount > 0;
        
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public string Initial => string.IsNullOrWhiteSpace(_peerName) ? "?" : _peerName.Substring(0, 1).ToUpperInvariant();

        public string LastMessageTimeText
        {
            get
            {
                var now = DateTime.Now;
                if (_lastMessageTime.Date == now.Date)
                    return _lastMessageTime.ToString("HH:mm");
                else if (_lastMessageTime.Date == now.Date.AddDays(-1))
                    return "昨天";
                else if (_lastMessageTime.Year == now.Year)
                    return _lastMessageTime.ToString("MM-dd");
                else
                    return _lastMessageTime.ToString("yyyy-MM-dd");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MessageViewModel
    {
        private readonly ChatMessage _message;
        private readonly bool _showDateSeparator;
        private readonly bool _showSenderName;

        public MessageViewModel(ChatMessage message, bool showDateSeparator = false, bool showSenderName = false)
        {
            _message = message;
            _showDateSeparator = showDateSeparator;
            _showSenderName = showSenderName;
        }

        public string SenderName => _message.SenderName;
        public DateTime Timestamp => _message.Timestamp;
        public string TextContent => _message.TextContent;
        public bool IsTextMessage => _message.MessageType == ChatMessageType.Text;
        public bool IsImageMessage => _message.MessageType == ChatMessageType.Image;
        public bool IsFileMessage => _message.MessageType == ChatMessageType.File;
        public bool IsSentByMe => _message.IsSentByMe;

        // Date separator
        public bool ShowDateSeparator => _showDateSeparator && !IsSentByMe;
        public string DateSeparatorText
        {
            get
            {
                var now = DateTime.Now;
                if (_message.Timestamp.Date == now.Date)
                    return "今天";
                else if (_message.Timestamp.Date == now.Date.AddDays(-1))
                    return "昨天";
                else if (_message.Timestamp.Year == now.Year)
                    return _message.Timestamp.ToString("M月d日");
                else
                    return _message.Timestamp.ToString("yyyy年M月d日");
            }
        }

        // Sender name visibility
        public bool ShowSenderName => _showSenderName && !_message.IsSentByMe;

        // Bubble styling
        public IBrush BubbleBackground => _message.IsSentByMe 
            ? new SolidColorBrush(Color.Parse("#0A84FF")) 
            : new SolidColorBrush(Color.Parse("#E9E9EB"));
        
        public Avalonia.Layout.HorizontalAlignment BubbleAlignment => _message.IsSentByMe 
            ? Avalonia.Layout.HorizontalAlignment.Right 
            : Avalonia.Layout.HorizontalAlignment.Left;

        public Avalonia.Layout.HorizontalAlignment TimestampAlignment => _message.IsSentByMe 
            ? Avalonia.Layout.HorizontalAlignment.Right 
            : Avalonia.Layout.HorizontalAlignment.Left;

        public CornerRadius BubbleCornerRadius => _message.IsSentByMe
            ? new CornerRadius(18, 18, 4, 18)
            : new CornerRadius(18, 18, 18, 4);

        public IBrush TextForeground => _message.IsSentByMe 
            ? Brushes.White 
            : new SolidColorBrush(Color.Parse("#1D1D1F"));

        // File styling
        public string FileName => _message.FileName;
        public string FileSizeText => FormatFileSize(_message.FileSize);
        
        public IBrush FileBoxBackground => _message.IsSentByMe
            ? new SolidColorBrush(Color.Parse("#0070E0"))
            : new SolidColorBrush(Color.Parse("#F5F5F7"));
        
        public IBrush FileIconBackground => _message.IsSentByMe
            ? new SolidColorBrush(Color.Parse("#FFFFFF"), 0.2)
            : new SolidColorBrush(Color.Parse("#E5E5EA"));
        
        public IBrush FileMetaForeground => _message.IsSentByMe
            ? new SolidColorBrush(Color.Parse("#FFFFFF"), 0.7)
            : new SolidColorBrush(Color.Parse("#86868B"));

        // Time display
        public string TimeText => _message.Timestamp.ToString("HH:mm");

        public Bitmap ImageBitmap => _message.ImageContent;

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }
    }

    // ========== COMMANDS ==========

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);
        public void Execute(object parameter) => _execute((T)parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
