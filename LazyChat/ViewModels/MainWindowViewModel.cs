using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LazyChat.Models;
using LazyChat.Services;

namespace LazyChat.ViewModels
{
    // Converters for contact list styling
    public static class BoolConverters
    {
        public static readonly IValueConverter OnlineToAvatarBackground = 
            new FuncValueConverter<bool, IBrush>(isOnline => 
                isOnline ? new SolidColorBrush(Color.Parse("#E8F5E9")) : new SolidColorBrush(Color.Parse("#F5F5F7")));
        
        public static readonly IValueConverter OnlineToAvatarForeground = 
            new FuncValueConverter<bool, IBrush>(isOnline => 
                isOnline ? new SolidColorBrush(Color.Parse("#2E7D32")) : new SolidColorBrush(Color.Parse("#86868B")));
        
        public static readonly IValueConverter OnlineToStatusColor = 
            new FuncValueConverter<bool, IBrush>(isOnline => 
                isOnline ? new SolidColorBrush(Color.Parse("#34C759")) : new SolidColorBrush(Color.Parse("#AEAEB2")));
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private const int COMMUNICATION_PORT = 9999;
        private string _userName;
        private PeerDiscoveryService _discoveryService;
        private P2PCommunicationService _commService;
        private FileTransferService _fileTransferService;
        private ChatHistoryStore _historyStore;
        private Dictionary<string, Conversation> _conversations;
        private ContactItem _selectedContact;
        private string _statusText;
        private string _messageText;
        private string _searchText;
        private Dictionary<string, Window> _activeTransferWindows;
        private bool _isInitialized;
        private const int RecentConversationLimit = 50;
        private const int HistoryLoadLimit = 200;
        
        // Settings
        private bool _enterToSend = true; // Default: Enter sends, Shift+Enter for newline

        // Unified contact list (online + offline with history)
        public ObservableCollection<ContactItem> Contacts { get; }
        public ObservableCollection<MessageViewModel> Messages { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel()
        {
            Contacts = new ObservableCollection<ContactItem>();
            Messages = new ObservableCollection<MessageViewModel>();
            _conversations = new Dictionary<string, Conversation>();
            _activeTransferWindows = new Dictionary<string, Window>();
            _statusText = "正在连接...";

            // Load settings
            LoadSettings();

            SendMessageCommand = new RelayCommand(SendTextMessage, () => CanSendMessage());
            AttachImageCommand = new RelayCommand(async () => await AttachImageAsync(), () => CanSendToSelectedPeer());
            AttachFileCommand = new RelayCommand(async () => await AttachFileAsync(), () => CanSendToSelectedPeer());
            SettingsCommand = new RelayCommand(async () => await ShowSettingsAsync());
            DeleteConversationCommand = new RelayCommand(async () => await DeleteSelectedConversationAsync(), () => SelectedContact != null);
            ShowProfileCommand = new RelayCommand(async () => await ShowProfileAsync(), () => SelectedContact != null);
            ExitCommand = new RelayCommand(Exit);
            AboutCommand = new RelayCommand(ShowAbout);

            Contacts.CollectionChanged += (_, __) => 
            {
                UpdateCommandStates();
                OnPropertyChanged(nameof(HasContacts));
            };
        }

        // ========== UI PROPERTIES ==========
        
        public string CurrentUserDisplayText => $"@ {_userName ?? Environment.UserName}";
        
        public bool HasContacts => Contacts.Count > 0;
        
        public bool HasSelectedContact => SelectedContact != null;
        
        public string SelectedContactName => SelectedContact?.DisplayName ?? "";
        
        public string SelectedContactInitial => GetInitial(SelectedContact?.DisplayName);
        
        public string SelectedContactStatus => SelectedContact?.IsOnline == true ? "在线" : "离线";
        
        public IBrush SelectedContactStatusColor => SelectedContact?.IsOnline == true 
            ? new SolidColorBrush(Color.Parse("#34C759")) 
            : new SolidColorBrush(Color.Parse("#AEAEB2"));
        
        public IBrush SelectedContactAvatarBackground => SelectedContact?.IsOnline == true
            ? new SolidColorBrush(Color.Parse("#E8F5E9"))
            : new SolidColorBrush(Color.Parse("#F5F5F7"));
        
        public IBrush SelectedContactAvatarForeground => SelectedContact?.IsOnline == true
            ? new SolidColorBrush(Color.Parse("#2E7D32"))
            : new SolidColorBrush(Color.Parse("#86868B"));

        public bool EnterToSend
        {
            get => _enterToSend;
            set
            {
                if (_enterToSend != value)
                {
                    _enterToSend = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FilteredContacts));
                }
            }
        }
        
        public IEnumerable<ContactItem> FilteredContacts
        {
            get
            {
                var filtered = Contacts.AsEnumerable();
                
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(c => 
                        c.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                }
                
                // Sort: online first, then by last message time
                return filtered.OrderByDescending(c => c.IsOnline)
                               .ThenByDescending(c => c.LastMessageTime);
            }
        }

        private string GetInitial(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            return name.Substring(0, 1).ToUpperInvariant();
        }

        // ========== SELECTED CONTACT ==========

        public ContactItem SelectedContact
        {
            get => _selectedContact;
            set
            {
                if (_selectedContact != value)
                {
                    _selectedContact = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelectedContact));
                    OnPropertyChanged(nameof(SelectedContactName));
                    OnPropertyChanged(nameof(SelectedContactInitial));
                    OnPropertyChanged(nameof(SelectedContactStatus));
                    OnPropertyChanged(nameof(SelectedContactStatusColor));
                    OnPropertyChanged(nameof(SelectedContactAvatarBackground));
                    OnPropertyChanged(nameof(SelectedContactAvatarForeground));
                    
                    if (_selectedContact != null)
                    {
                        LoadConversation(_selectedContact);
                        // Clear unread when selecting
                        _selectedContact.UnreadCount = 0;
                    }
                    
                    UpdateCommandStates();
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
        public ICommand AttachImageCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand DeleteConversationCommand { get; }
        public ICommand ShowProfileCommand { get; }
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
                LoadContacts();
            }
            catch (Exception ex)
            {
                ShowError("初始化历史记录失败: " + ex.Message);
            }
        }

        // ========== PEER DISCOVERY EVENTS ==========

        private void DiscoveryService_PeerDiscovered(object sender, PeerInfo peer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var existing = Contacts.FirstOrDefault(c => c.PeerId == peer.PeerId);
                if (existing != null)
                {
                    // Update existing contact to online
                    existing.IsOnline = true;
                    existing.DisplayName = peer.UserName;
                    existing.IpAddress = peer.IpAddressString;
                    existing.Port = peer.Port;
                }
                else
                {
                    // Add new online contact
                    Contacts.Add(new ContactItem
                    {
                        PeerId = peer.PeerId,
                        DisplayName = peer.UserName,
                        IpAddress = peer.IpAddressString,
                        Port = peer.Port,
                        IsOnline = true,
                        LastMessageTime = DateTime.Now
                    });
                }
                
                StatusText = $"{peer.UserName} 已上线";
                OnPropertyChanged(nameof(FilteredContacts));
                
                // Update selected contact status if it's the same peer
                if (SelectedContact?.PeerId == peer.PeerId)
                {
                    OnPropertyChanged(nameof(SelectedContactStatus));
                    OnPropertyChanged(nameof(SelectedContactStatusColor));
                    OnPropertyChanged(nameof(SelectedContactAvatarBackground));
                    OnPropertyChanged(nameof(SelectedContactAvatarForeground));
                }
                
                UpdateCommandStates();
            });
        }

        private void DiscoveryService_PeerLeft(object sender, PeerInfo peer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var existing = Contacts.FirstOrDefault(c => c.PeerId == peer.PeerId);
                if (existing != null)
                {
                    existing.IsOnline = false;
                    StatusText = $"{peer.UserName} 已离线";
                    OnPropertyChanged(nameof(FilteredContacts));
                    
                    // Update selected contact status
                    if (SelectedContact?.PeerId == peer.PeerId)
                    {
                        OnPropertyChanged(nameof(SelectedContactStatus));
                        OnPropertyChanged(nameof(SelectedContactStatusColor));
                        OnPropertyChanged(nameof(SelectedContactAvatarBackground));
                        OnPropertyChanged(nameof(SelectedContactAvatarForeground));
                    }
                }
                
                UpdateCommandStates();
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
                        var contact = Contacts.FirstOrDefault(c => c.PeerId == transfer.SenderId);
                        if (contact != null && contact.IsOnline)
                        {
                            var peer = CreatePeerInfoFromContact(contact);
                            _commService.SendFileTransferResponse(true, transfer.FileId, peer, _userName);
                        }
                    }
                    else
                    {
                        _fileTransferService.RejectFileTransfer(transfer.FileId);
                        var contact = Contacts.FirstOrDefault(c => c.PeerId == transfer.SenderId);
                        if (contact != null && contact.IsOnline)
                        {
                            var peer = CreatePeerInfoFromContact(contact);
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
                var contact = Contacts.FirstOrDefault(c => c.PeerId == transfer.ReceiverId);
                string peerName = contact?.DisplayName ?? transfer.ReceiverId;
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

            // Update or create contact
            var contact = Contacts.FirstOrDefault(c => c.PeerId == peerId);
            if (contact == null)
            {
                contact = new ContactItem
                {
                    PeerId = peerId,
                    DisplayName = peerName,
                    IsOnline = false,
                    LastMessageTime = message.Timestamp
                };
                Contacts.Add(contact);
            }
            else
            {
                contact.LastMessageTime = message.Timestamp;
                contact.DisplayName = peerName;
            }

            if (SelectedContact != null && SelectedContact.PeerId == peerId)
            {
                DisplayMessage(message);
                ScrollToBottom();
            }
            else
            {
                conv.UnreadCount++;
                contact.UnreadCount = conv.UnreadCount;
            }

            OnPropertyChanged(nameof(FilteredContacts));
        }

        private void DisplayMessage(ChatMessage message)
        {
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

        private void LoadConversation(ContactItem contact)
        {
            Messages.Clear();

            if (contact != null)
            {
                Conversation conv;
                if (!_conversations.ContainsKey(contact.PeerId))
                {
                    conv = new Conversation
                    {
                        PeerId = contact.PeerId,
                        PeerName = contact.DisplayName
                    };
                    _conversations[contact.PeerId] = conv;
                }
                else
                {
                    conv = _conversations[contact.PeerId];
                }

                conv.PeerName = contact.DisplayName;
                conv.Messages.Clear();
                conv.UnreadCount = 0;
                contact.UnreadCount = 0;

                string currentPeerId = contact.PeerId;
                Task.Run(() =>
                {
                    return _historyStore?.LoadMessagesForPeer(currentPeerId, HistoryLoadLimit) ?? new List<ChatMessage>();
                }).ContinueWith(t =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (SelectedContact == null || SelectedContact.PeerId != currentPeerId) return;

                        foreach (ChatMessage msg in t.Result)
                        {
                            conv.Messages.Add(msg);
                            DisplayMessage(msg);
                        }
                        
                        ScrollToBottom();
                    });
                });
            }
        }

        private void LoadContacts()
        {
            Contacts.Clear();

            Task.Run(() =>
            {
                return _historyStore?.LoadRecentConversations(RecentConversationLimit) ?? new List<ConversationSummary>();
            }).ContinueWith(t =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    foreach (ConversationSummary item in t.Result)
                    {
                        // Only add if not already in contacts (from online discovery)
                        if (!Contacts.Any(c => c.PeerId == item.PeerId))
                        {
                            Contacts.Add(new ContactItem
                            {
                                PeerId = item.PeerId,
                                DisplayName = item.PeerName,
                                IsOnline = false,
                                LastMessageTime = item.LastMessageTime
                            });
                        }
                    }
                    OnPropertyChanged(nameof(HasContacts));
                    OnPropertyChanged(nameof(FilteredContacts));
                });
            });
        }

        private void ScrollToBottom()
        {
            // Handled by view
        }

        // ========== SEND MESSAGES ==========

        private void SendTextMessage()
        {
            if (SelectedContact == null || string.IsNullOrWhiteSpace(MessageText)) return;

            if (!SelectedContact.IsOnline)
            {
                ShowInfo("对方离线，无法发送消息!");
                return;
            }

            var peer = CreatePeerInfoFromContact(SelectedContact);
            bool sent = _commService.SendTextMessage(MessageText, peer, _userName);

            if (sent)
            {
                ChatMessage message = new ChatMessage
                {
                    SenderId = _discoveryService.GetLocalPeer().PeerId,
                    SenderName = _userName,
                    ReceiverId = SelectedContact.PeerId,
                    TextContent = MessageText,
                    MessageType = ChatMessageType.Text,
                    IsSentByMe = true
                };

                AddMessageToConversation(SelectedContact.PeerId, SelectedContact.DisplayName, message);
                MessageText = string.Empty;
            }
            else
            {
                ShowError("发送失败!");
            }
        }

        public void HandleKeyDown(bool isShiftPressed)
        {
            if (string.IsNullOrWhiteSpace(MessageText)) return;
            
            if (_enterToSend)
            {
                // Enter to send, Shift+Enter for newline
                if (!isShiftPressed)
                {
                    SendTextMessage();
                }
                // else: let the TextBox handle newline naturally
            }
            else
            {
                // Ctrl+Enter to send (handled by KeyBinding), Enter for newline
                // This method won't be called for Ctrl+Enter
            }
        }

        private async Task AttachImageAsync()
        {
            if (SelectedContact == null)
            {
                ShowInfo("请先选择一个联系人!");
                return;
            }

            if (!SelectedContact.IsOnline)
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
            if (SelectedContact == null)
            {
                ShowInfo("请先选择一个联系人!");
                return;
            }

            if (!SelectedContact.IsOnline)
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
                        ReceiverId = SelectedContact.PeerId,
                        MessageType = ChatMessageType.File,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length,
                        IsSentByMe = true
                    };
                    AddMessageToConversation(SelectedContact.PeerId, SelectedContact.DisplayName, fileMessage);
                    var peer = CreatePeerInfoFromContact(SelectedContact);
                    _fileTransferService.StartSendingFile(filePath, peer, _userName);
                }
            }
        }

        private async Task SendImageMessageAsync(string imagePath)
        {
            try
            {
                byte[] imageData = await Task.Run(() => File.ReadAllBytes(imagePath));
                if (!SelectedContact.IsOnline)
                {
                    ShowInfo("对方离线，无法发送图片!");
                    return;
                }

                var peer = CreatePeerInfoFromContact(SelectedContact);
                bool sent = _commService.SendImageMessage(imageData, peer, _userName);

                if (sent)
                {
                    ChatMessage message = new ChatMessage
                    {
                        SenderId = _discoveryService.GetLocalPeer().PeerId,
                        SenderName = _userName,
                        ReceiverId = SelectedContact.PeerId,
                        ImageContent = new Bitmap(imagePath),
                        ImageBytes = imageData,
                        MessageType = ChatMessageType.Image,
                        IsSentByMe = true
                    };

                    AddMessageToConversation(SelectedContact.PeerId, SelectedContact.DisplayName, message);
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

        // ========== DELETE CONVERSATION ==========

        private async Task DeleteSelectedConversationAsync()
        {
            if (SelectedContact == null) return;
            
            // Don't allow deleting online contacts
            if (SelectedContact.IsOnline)
            {
                ShowInfo("无法删除在线联系人的会话。");
                return;
            }

            bool confirmed = await ShowConfirmDialogAsync("删除会话", $"确定要删除与 {SelectedContact.DisplayName} 的所有聊天记录吗？\n此操作不可撤销。");
            
            if (confirmed)
            {
                string peerId = SelectedContact.PeerId;
                
                // Remove from UI
                Contacts.Remove(SelectedContact);
                SelectedContact = null;
                Messages.Clear();
                
                // Remove from memory
                _conversations.Remove(peerId);
                
                // Remove from database
                await Task.Run(() => _historyStore?.DeleteConversation(peerId));
                
                StatusText = "会话已删除";
                OnPropertyChanged(nameof(FilteredContacts));
            }
        }

        // ========== PROFILE DIALOG ==========

        private async Task ShowProfileAsync()
        {
            if (SelectedContact == null) return;
            
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dialog = new Views.ProfileDialog(SelectedContact, _discoveryService?.GetLocalPeer());
                await dialog.ShowDialog<bool>(desktop.MainWindow);
            }
        }

        // ========== SETTINGS ==========

        private async Task ShowSettingsAsync()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dialog = new Views.SettingsDialog(_userName, _enterToSend);
                var result = await dialog.ShowDialog<bool>(desktop.MainWindow);
                
                if (result)
                {
                    if (dialog.UserName != _userName)
                    {
                        _userName = dialog.UserName;
                        SaveUsername(_userName);
                        OnPropertyChanged(nameof(CurrentUserDisplayText));
                        StatusText = "用户名已更新 (需要重启才能生效)";
                    }
                    
                    EnterToSend = dialog.EnterToSend;
                }
            }
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Load();
            _enterToSend = settings.EnterToSend;
        }

        private void SaveSettings()
        {
            SettingsManager.SetEnterToSend(_enterToSend);
        }

        private string LoadSavedUsername()
        {
            return SettingsManager.GetUserName();
        }

        private void SaveUsername(string username)
        {
            SettingsManager.SetUserName(username);
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

        private PeerInfo CreatePeerInfoFromContact(ContactItem contact)
        {
            return new PeerInfo
            {
                PeerId = contact.PeerId,
                UserName = contact.DisplayName,
                IpAddressString = contact.IpAddress,
                Port = contact.Port,
                IsOnline = contact.IsOnline
            };
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

        private async Task<bool> ShowConfirmDialogAsync(string title, string message)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                bool result = false;
                var dialog = new Window
                {
                    Title = title,
                    Width = 420,
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
                                new StackPanel
                                {
                                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                                    Spacing = 12,
                                    Children =
                                    {
                                        new Button 
                                        { 
                                            Content = "取消", 
                                            Padding = new Thickness(24, 8),
                                            Classes = { "secondary" }
                                        },
                                        new Button 
                                        { 
                                            Content = "确定", 
                                            Padding = new Thickness(24, 8),
                                            Classes = { "primary" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                if (dialog.Content is Border border && 
                    border.Child is StackPanel panel && 
                    panel.Children.Count > 1 && 
                    panel.Children[1] is StackPanel buttonPanel &&
                    buttonPanel.Children.Count >= 2)
                {
                    if (buttonPanel.Children[0] is Button cancelButton)
                        cancelButton.Click += (_, __) => { result = false; dialog.Close(false); };
                    if (buttonPanel.Children[1] is Button okButton)
                        okButton.Click += (_, __) => { result = true; dialog.Close(true); };
                }

                await dialog.ShowDialog<bool>(desktop.MainWindow);
                return result;
            }
            return false;
        }

        private bool CanSendToSelectedPeer()
        {
            return SelectedContact != null && SelectedContact.IsOnline;
        }

        private bool CanSendMessage()
        {
            return CanSendToSelectedPeer() && !string.IsNullOrWhiteSpace(MessageText);
        }

        private void UpdateCommandStates()
        {
            (SendMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AttachImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AttachFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteConversationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ShowProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

    // ========== MODELS ==========

    public class ContactItem : INotifyPropertyChanged
    {
        private string _displayName;
        private bool _isOnline;
        private int _unreadCount;
        private DateTime _lastMessageTime;

        public event PropertyChangedEventHandler PropertyChanged;

        public string PeerId { get; set; }
        
        public string DisplayName
        {
            get => _displayName;
            set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Initial)); } }
        }
        
        public string IpAddress { get; set; }
        public int Port { get; set; }
        
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
        
        public DateTime LastMessageTime
        {
            get => _lastMessageTime;
            set { if (_lastMessageTime != value) { _lastMessageTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastMessageTimeText)); } }
        }

        public string Initial => string.IsNullOrWhiteSpace(_displayName) ? "?" : _displayName.Substring(0, 1).ToUpperInvariant();

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

        public bool ShowSenderName => _showSenderName && !_message.IsSentByMe;

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
