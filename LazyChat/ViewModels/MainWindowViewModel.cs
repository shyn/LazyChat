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
        private Dictionary<string, Conversation> _conversations;
        private PeerInfo _selectedPeer;
        private string _statusText;
        private string _messageText;
        private Dictionary<string, Window> _activeTransferWindows;
        private bool _isInitialized;

        public ObservableCollection<PeerInfo> OnlinePeers { get; }
        public ObservableCollection<MessageViewModel> Messages { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel()
        {
            OnlinePeers = new ObservableCollection<PeerInfo>();
            Messages = new ObservableCollection<MessageViewModel>();
            _conversations = new Dictionary<string, Conversation>();
            _activeTransferWindows = new Dictionary<string, Window>();
            _statusText = "就绪";

            SendMessageCommand = new RelayCommand(SendTextMessage, () => SelectedPeer != null && !string.IsNullOrWhiteSpace(MessageText));
            AttachImageCommand = new RelayCommand(async () => await AttachImageAsync(), () => SelectedPeer != null);
            AttachFileCommand = new RelayCommand(async () => await AttachFileAsync(), () => SelectedPeer != null);
            SetUsernameCommand = new RelayCommand(async () => await SetUsernameAsync());
            ExitCommand = new RelayCommand(Exit);
            AboutCommand = new RelayCommand(ShowAbout);
        }

        public string UserListHeader => $"在线用户 ({OnlinePeers.Count})";

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

            _userName = Environment.UserName;

            var inputDialog = new Views.InputDialog("设置用户名", "请输入您的用户名:", _userName);
            var result = await inputDialog.ShowDialog<bool>(owner);
            if (result && !string.IsNullOrWhiteSpace(inputDialog.InputText))
            {
                _userName = inputDialog.InputText;
            }

            InitializeServices();
            StartServices();
            _isInitialized = true;
        }

        private void InitializeServices()
        {
            _commService = new P2PCommunicationService(COMMUNICATION_PORT, Guid.NewGuid().ToString());
            _discoveryService = new PeerDiscoveryService(_userName, COMMUNICATION_PORT);
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

        private void DiscoveryService_PeerDiscovered(object sender, PeerInfo peer)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!OnlinePeers.Any(p => p.PeerId == peer.PeerId))
                {
                    OnlinePeers.Add(peer);
                    OnPropertyChanged(nameof(UserListHeader));
                    StatusText = $"{peer.UserName} 已上线";
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
                MessageType = ChatMessageType.Image,
                Timestamp = message.Timestamp,
                IsSentByMe = false
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
            if (!_conversations.ContainsKey(peerId))
            {
                _conversations[peerId] = new Conversation
                {
                    PeerId = peerId,
                    PeerName = peerName
                };
            }

            Conversation conv = _conversations[peerId];
            conv.Messages.Add(message);
            conv.LastMessageTime = message.Timestamp;

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

            if (peer != null && _conversations.ContainsKey(peer.PeerId))
            {
                Conversation conv = _conversations[peer.PeerId];
                conv.UnreadCount = 0;

                foreach (ChatMessage msg in conv.Messages)
                {
                    DisplayMessage(msg);
                }
            }
        }

        private void SendTextMessage()
        {
            if (SelectedPeer == null || string.IsNullOrWhiteSpace(MessageText))
                return;

            bool sent = _commService.SendTextMessage(MessageText, SelectedPeer, _userName);

            if (sent)
            {
                ChatMessage message = new ChatMessage
                {
                    SenderId = _discoveryService.GetLocalPeer().PeerId,
                    SenderName = _userName,
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
                    _fileTransferService.StartSendingFile(filePath, SelectedPeer, _userName);
                }
            }
        }

        private void SendImageMessage(string imagePath)
        {
            try
            {
                byte[] imageData = File.ReadAllBytes(imagePath);
                bool sent = _commService.SendImageMessage(imageData, SelectedPeer, _userName);

                if (sent)
                {
                    ChatMessage message = new ChatMessage
                    {
                        SenderId = _discoveryService.GetLocalPeer().PeerId,
                        SenderName = _userName,
                        ImageContent = new Bitmap(imagePath),
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
                    StatusText = "用户名已更新";
                }
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

        public void Cleanup()
        {
            _discoveryService?.Stop();
            _discoveryService?.Dispose();
            _commService?.Stop();
            _commService?.Dispose();
            _fileTransferService?.Dispose();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
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
