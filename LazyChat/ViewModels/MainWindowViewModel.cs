using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
            _statusText = "就绪";

            SendMessageCommand = new RelayCommand(SendTextMessage, () => CanSendMessage());
            AttachImageCommand = new RelayCommand(async () => await AttachImageAsync(), () => CanSendToSelectedPeer());
            AttachFileCommand = new RelayCommand(async () => await AttachFileAsync(), () => CanSendToSelectedPeer());
            SetUsernameCommand = new RelayCommand(async () => await SetUsernameAsync());
            ExitCommand = new RelayCommand(Exit);
            AboutCommand = new RelayCommand(ShowAbout);

            OnlinePeers.CollectionChanged += (_, __) => UpdateCommandStates();
            RecentConversations.CollectionChanged += (_, __) => OnPropertyChanged(nameof(RecentListHeader));
        }

        public string UserListHeader => $"在线用户 ({OnlinePeers.Count})";
        public string RecentListHeader => $"Recent Conversations ({RecentConversations.Count})";

        public string ChatHeaderText
        {
            get
            {
                if (SelectedPeer == null)
                    return "请选择一个用户开始聊天";
                return $"与 {SelectedPeer.UserName} 聊天";
            }
        }

        public PeerInfo SelectedPeer
        {
            get => _selectedPeer;
            set
            {
                if (_selectedPeer != value)
                {
                    _selectedPeer = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ChatHeaderText));
                    LoadConversation(value);
                    
                    // Update command states
                    UpdateCommandStates();

                    if (_selectedPeer != null)
                    {
                        SelectConversationItem(_selectedPeer.PeerId);
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

                    if (_selectedConversation != null)
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
                        SelectedPeer = peer;
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
                    
                    // Update send command state
                    UpdateCommandStates();
                }
            }
        }

        public ICommand SendMessageCommand { get; }
        public ICommand AttachImageCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand SetUsernameCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand AboutCommand { get; }

        public async Task InitializeAsync(Window owner)

        {
            if (_isInitialized)
            {
                return;
            }

            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            // Load saved username or use system username as default
            _userName = LoadSavedUsername() ?? Environment.UserName;

            // Only prompt for username if none saved
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

            InitializeServices();
            StartServices();
            InitializeHistory();
            _isInitialized = true;
        }

        private void InitializeServices()
        {
            // 先初始化发现服务，获取本机 PeerId，确保通信与发现使用同一 PeerId
            _discoveryService = new PeerDiscoveryService(_userName, COMMUNICATION_PORT);
            var localPeerId = _discoveryService.GetLocalPeer().PeerId;

            _commService = new P2PCommunicationService(COMMUNICATION_PORT, localPeerId);
            _fileTransferService = new FileTransferService(_commService);

            _discoveryService.PeerDiscovered += DiscoveryService_PeerDiscovered;
            _discoveryService.PeerLeft += DiscoveryService_PeerLeft;
            _discoveryService.ErrorOccurred += Service_ErrorOccurred;

            _commService.MessageReceived += CommService_MessageReceived;
            _commService.ErrorOccurred += Service_ErrorOccurred;

            _fileTransferService.TransferRequestReceived += FileTransferService_TransferRequestReceived;
            _fileTransferService.TransferProgressChanged += FileTransferService_TransferProgressChanged;
            _fileTransferService.TransferCompleted += FileTransferService_TransferCompleted;
            _fileTransferService.TransferFailed += FileTransferService_TransferFailed;
        }

        private void StartServices()
        {
            try
            {
                _commService.Start();
                _discoveryService.Start();
                StatusText = "服务已启动,正在发现局域网用户...";
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
                ShowError("Failed to initialize history: " + ex.Message);
            }
        }

        private void DiscoveryService_PeerDiscovered(object sender, PeerInfo peer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!OnlinePeers.Any(p => p.PeerId == peer.PeerId))
                {
                    OnlinePeers.Add(peer);
                    OnPropertyChanged(nameof(UserListHeader));
                    StatusText = $"{peer.UserName} 已上线";
                    UpdateCommandStates();
                    UpdateConversationPresence(peer.PeerId, true);
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
                    OnPropertyChanged(nameof(UserListHeader));
                    StatusText = $"{peer.UserName} 已离线";

                    if (SelectedPeer?.PeerId == peer.PeerId)
                    {
                        OnPropertyChanged(nameof(ChatHeaderText));
                    }

                    UpdateCommandStates();
                    UpdateConversationPresence(peer.PeerId, false);
                }
            });
        }

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
        }

        private void HandleFileTransferReject(NetworkMessage message)
        {
            StatusText = $"{message.SenderName} 拒绝了文件传输";
            if (_activeTransferWindows.ContainsKey(message.FileId))
            {
                _activeTransferWindows[message.FileId].Close();
                _activeTransferWindows.Remove(message.FileId);
            }
        }

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

        private void FileTransferService_TransferProgressChanged(object sender, FileTransferInfo transfer)
        {
            // Update progress in transfer window if exists
        }

        private void FileTransferService_TransferCompleted(object sender, FileTransferInfo transfer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"文件传输完成: {transfer.FileName}";
            });
        }

        private void FileTransferService_TransferFailed(object sender, FileTransferInfo transfer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"文件传输失败: {transfer.FileName}";
                ShowError("文件传输失败!");
            });
        }

        private void AddMessageToConversation(string peerId, string peerName, ChatMessage message)
        {
            if (_historyStore != null)
            {
                try
                {
                    _historyStore.SaveMessage(message, peerId, peerName);
                }
                catch (Exception ex)
                {
                    StatusText = "History save failed: " + ex.Message;
                }
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

            UpsertRecentConversation(peerId, peerName, message.Timestamp);

            if (SelectedPeer != null && SelectedPeer.PeerId == peerId)
            {
                DisplayMessage(message);
            }
            else
            {
                conv.UnreadCount++;
            }
        }

        private void DisplayMessage(ChatMessage message)
        {
            Messages.Add(new MessageViewModel(message));
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

                List<ChatMessage> history = _historyStore?.LoadMessagesForPeer(peer.PeerId, HistoryLoadLimit) ?? new List<ChatMessage>();
                foreach (ChatMessage msg in history)
                {
                    conv.Messages.Add(msg);
                    DisplayMessage(msg);
                }
            }
        }

        private void LoadRecentConversations()
        {
            RecentConversations.Clear();

            List<ConversationSummary> recent = _historyStore?.LoadRecentConversations(RecentConversationLimit) ?? new List<ConversationSummary>();
            foreach (ConversationSummary item in recent)
            {
                bool isOnline = OnlinePeers.Any(p => p.PeerId == item.PeerId);
                RecentConversations.Add(new ConversationListItem(item.PeerId, item.PeerName, item.LastMessageTime, isOnline));
            }

            OnPropertyChanged(nameof(RecentListHeader));
        }

        private void UpsertRecentConversation(string peerId, string peerName, DateTime lastMessageTime)
        {
            ConversationListItem existing = RecentConversations.FirstOrDefault(c => c.PeerId == peerId);
            bool isOnline = OnlinePeers.Any(p => p.PeerId == peerId);

            if (existing == null)
            {
                RecentConversations.Insert(0, new ConversationListItem(peerId, peerName, lastMessageTime, isOnline));
            }
            else
            {
                existing.PeerName = peerName;
                existing.LastMessageTime = lastMessageTime;
                existing.IsOnline = isOnline;

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

            OnPropertyChanged(nameof(RecentListHeader));
        }

        private void UpdateConversationPresence(string peerId, bool isOnline)
        {
            ConversationListItem item = RecentConversations.FirstOrDefault(c => c.PeerId == peerId);
            if (item != null)
            {
                item.IsOnline = isOnline;
            }
        }

        private void SelectConversationItem(string peerId)
        {
            ConversationListItem item = RecentConversations.FirstOrDefault(c => c.PeerId == peerId);
            if (item != null && _selectedConversation != item)
            {
                _selectedConversation = item;
                OnPropertyChanged(nameof(SelectedConversation));
            }
        }

        private void SendTextMessage()
        {
            if (SelectedPeer == null || string.IsNullOrWhiteSpace(MessageText))
                return;

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
                    SendImageMessage(filePath);
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

        private void SendImageMessage(string imagePath)
        {
            try
            {
                byte[] imageData = File.ReadAllBytes(imagePath);
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
                    "LazyChat",
                    "config.txt");
                
                if (File.Exists(configPath))
                {
                    return File.ReadAllText(configPath).Trim();
                }
            }
            catch
            {
                // Fail silently
            }
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
                {
                    Directory.CreateDirectory(configDir);
                }
                
                string configPath = Path.Combine(configDir, "config.txt");
                File.WriteAllText(configPath, username);
            }
            catch
            {
                // Fail silently
            }
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
            ShowInfo("LazyChat - 局域网聊天工具\n\n支持文字消息、图片和文件传输\n无需服务器,自动发现局域网用户\n\nVersion 1.0\n\n现在支持跨平台运行!");
        }

        private void Service_ErrorOccurred(object sender, string error)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = "错误: " + error;
            });
        }

        private void ShowError(string message)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dialog = new Window
                {
                    Title = "错误",
                    Width = 400,
                    Height = 150,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            new Button { Content = "确定", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                        }
                    }
                };
                dialog.ShowDialog(desktop.MainWindow);
            }
        }

        private void ShowInfo(string message)
        {
            ShowError(message);
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
            if (SelectedPeer == null)
            {
                return false;
            }

            return OnlinePeers.Any(p => p.PeerId == SelectedPeer.PeerId);
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

    public class ConversationListItem : INotifyPropertyChanged
    {
        private string _peerName;
        private DateTime _lastMessageTime;
        private bool _isOnline;

        public event PropertyChangedEventHandler PropertyChanged;

        public ConversationListItem(string peerId, string peerName, DateTime lastMessageTime, bool isOnline)
        {
            PeerId = peerId;
            _peerName = peerName;
            _lastMessageTime = lastMessageTime;
            _isOnline = isOnline;
        }

        public string PeerId { get; }

        public string PeerName
        {
            get => _peerName;
            set
            {
                if (_peerName != value)
                {
                    _peerName = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastMessageTime
        {
            get => _lastMessageTime;
            set
            {
                if (_lastMessageTime != value)
                {
                    _lastMessageTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    OnPropertyChanged();
                }
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

        public MessageViewModel(ChatMessage message)
        {
            _message = message;
        }

        public string SenderName => _message.SenderName;
        public DateTime Timestamp => _message.Timestamp;
        public string TextContent => _message.TextContent;
        public bool IsTextMessage => _message.MessageType == ChatMessageType.Text;
        public bool IsImageMessage => _message.MessageType == ChatMessageType.Image;
        public bool IsFileMessage => _message.MessageType == ChatMessageType.File;

        public string BubbleBackground => _message.IsSentByMe ? "#DCF0FF" : "#F0F0F0";
        public Avalonia.Layout.HorizontalAlignment BubbleAlignment => _message.IsSentByMe 
            ? Avalonia.Layout.HorizontalAlignment.Right 
            : Avalonia.Layout.HorizontalAlignment.Left;
        public string SenderColor => _message.IsSentByMe ? "#0066CC" : "#009933";

        public Bitmap ImageBitmap => _message.ImageContent;

        public string FileDisplayText => $"📎 {_message.FileName} ({FormatFileSize(_message.FileSize)})";

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

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
