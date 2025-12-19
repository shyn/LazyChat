using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using LazyChat.Models;
using LazyChat.Services;
using LazyChat.Controls;

namespace LazyChat
{
    public partial class Form1 : Form
    {
        private const int COMMUNICATION_PORT = 9999;
        private string _userName;
        private PeerDiscoveryService _discoveryService;
        private P2PCommunicationService _commService;
        private FileTransferService _fileTransferService;
        private Dictionary<string, PeerInfo> _onlinePeers;
        private Dictionary<string, Conversation> _conversations;
        private PeerInfo _currentChatPeer;
        private Dictionary<string, FileTransferDialog> _activeTransferDialogs;

        public Form1()
        {
            InitializeComponent();
            _onlinePeers = new Dictionary<string, PeerInfo>();
            _conversations = new Dictionary<string, Conversation>();
            _activeTransferDialogs = new Dictionary<string, FileTransferDialog>();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _userName = Environment.UserName;
            
            using (InputDialog dlg = new InputDialog("设置用户名", "请输入您的用户名:", _userName))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _userName = dlg.InputText;
                }
            }

            InitializeServices();
            StartServices();
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
                UpdateStatus("服务已启动,正在发现局域网用户...");
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动服务失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DiscoveryService_PeerDiscovered(object sender, PeerInfo peer)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<PeerInfo>(DiscoveryService_PeerDiscovered), sender, peer);
                return;
            }

            if (!_onlinePeers.ContainsKey(peer.PeerId))
            {
                _onlinePeers[peer.PeerId] = peer;
                _listUsers.Items.Add(peer);
                UpdateUserCount();
                UpdateStatus(string.Format("{0} 上线了", peer.UserName));
            }
        }

        private void DiscoveryService_PeerLeft(object sender, PeerInfo peer)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<PeerInfo>(DiscoveryService_PeerLeft), sender, peer);
                return;
            }

            if (_onlinePeers.ContainsKey(peer.PeerId))
            {
                _onlinePeers.Remove(peer.PeerId);
                _listUsers.Items.Remove(peer);
                UpdateUserCount();
                UpdateStatus(string.Format("{0} 下线了", peer.UserName));

                if (_currentChatPeer != null && _currentChatPeer.PeerId == peer.PeerId)
                {
                    _lblChatWith.Text = string.Format("{0} (离线)", peer.UserName);
                }
            }
        }

        private void CommService_MessageReceived(object sender, NetworkMessage message)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<NetworkMessage>(CommService_MessageReceived), sender, message);
                return;
            }

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
                    chatMsg.ImageContent = Image.FromStream(ms);
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
            UpdateStatus(string.Format("{0} 接受了文件传输", message.SenderName));
        }

        private void HandleFileTransferReject(NetworkMessage message)
        {
            UpdateStatus(string.Format("{0} 拒绝了文件传输", message.SenderName));
            if (_activeTransferDialogs.ContainsKey(message.FileId))
            {
                _activeTransferDialogs[message.FileId].Close();
                _activeTransferDialogs.Remove(message.FileId);
            }
        }

        private void FileTransferService_TransferRequestReceived(object sender, FileTransferInfo transfer)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<FileTransferInfo>(FileTransferService_TransferRequestReceived), sender, transfer);
                return;
            }

            FileTransferDialog dlg = new FileTransferDialog(transfer, true);
            _activeTransferDialogs[transfer.FileId] = dlg;

            if (dlg.ShowDialog() == DialogResult.OK && dlg.IsAccepted)
            {
                _fileTransferService.AcceptFileTransfer(transfer.FileId, dlg.SavePath);

                PeerInfo peer = _onlinePeers.Values.FirstOrDefault(p => p.PeerId == transfer.SenderId);
                if (peer != null)
                {
                    _commService.SendFileTransferResponse(true, transfer.FileId, peer, _userName);
                }
            }
            else
            {
                _fileTransferService.RejectFileTransfer(transfer.FileId);

                PeerInfo peer = _onlinePeers.Values.FirstOrDefault(p => p.PeerId == transfer.SenderId);
                if (peer != null)
                {
                    _commService.SendFileTransferResponse(false, transfer.FileId, peer, _userName);
                }

                _activeTransferDialogs.Remove(transfer.FileId);
            }
        }

        private void FileTransferService_TransferProgressChanged(object sender, FileTransferInfo transfer)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<FileTransferInfo>(FileTransferService_TransferProgressChanged), sender, transfer);
                return;
            }

            if (_activeTransferDialogs.ContainsKey(transfer.FileId))
            {
                _activeTransferDialogs[transfer.FileId].UpdateProgress(transfer.GetProgress());
            }
        }

        private void FileTransferService_TransferCompleted(object sender, FileTransferInfo transfer)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<FileTransferInfo>(FileTransferService_TransferCompleted), sender, transfer);
                return;
            }

            UpdateStatus(string.Format("文件传输完成: {0}", transfer.FileName));

            if (_activeTransferDialogs.ContainsKey(transfer.FileId))
            {
                _activeTransferDialogs[transfer.FileId].UpdateProgress(100);
            }
        }

        private void FileTransferService_TransferFailed(object sender, FileTransferInfo transfer)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<FileTransferInfo>(FileTransferService_TransferFailed), sender, transfer);
                return;
            }

            UpdateStatus(string.Format("文件传输失败: {0}", transfer.FileName));
            MessageBox.Show("文件传输失败!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            if (_currentChatPeer != null && _currentChatPeer.PeerId == peerId)
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
            MessageBubbleControl bubble = new MessageBubbleControl(message);
            _panelMessages.Controls.Add(bubble);
            bubble.BringToFront();

            _panelMessages.ScrollControlIntoView(bubble);
        }

        private void LoadConversation(PeerInfo peer)
        {
            _currentChatPeer = peer;
            _lblChatWith.Text = string.Format("与 {0} 聊天", peer.UserName);
            _panelMessages.Controls.Clear();

            if (_conversations.ContainsKey(peer.PeerId))
            {
                Conversation conv = _conversations[peer.PeerId];
                conv.UnreadCount = 0;

                foreach (ChatMessage msg in conv.Messages)
                {
                    DisplayMessage(msg);
                }
            }

            _txtMessage.Focus();
        }

        private void SendTextMessage()
        {
            if (_currentChatPeer == null)
            {
                MessageBox.Show("请先选择一个用户!", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string text = _txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            bool sent = _commService.SendTextMessage(text, _currentChatPeer, _userName);

            if (sent)
            {
                ChatMessage message = new ChatMessage
                {
                    SenderId = _discoveryService.GetLocalPeer().PeerId,
                    SenderName = _userName,
                    TextContent = text,
                    MessageType = ChatMessageType.Text,
                    IsSentByMe = true
                };

                AddMessageToConversation(_currentChatPeer.PeerId, _currentChatPeer.UserName, message);
                _txtMessage.Clear();
            }
            else
            {
                MessageBox.Show("发送失败!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendImageMessage(string imagePath)
        {
            if (_currentChatPeer == null)
            {
                MessageBox.Show("请先选择一个用户!", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                byte[] imageData = File.ReadAllBytes(imagePath);
                bool sent = _commService.SendImageMessage(imageData, _currentChatPeer, _userName);

                if (sent)
                {
                    ChatMessage message = new ChatMessage
                    {
                        SenderId = _discoveryService.GetLocalPeer().PeerId,
                        SenderName = _userName,
                        ImageContent = Image.FromFile(imagePath),
                        MessageType = ChatMessageType.Image,
                        IsSentByMe = true
                    };

                    AddMessageToConversation(_currentChatPeer.PeerId, _currentChatPeer.UserName, message);
                }
                else
                {
                    MessageBox.Show("发送图片失败!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法加载图片: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateUserCount()
        {
            _lblUsers.Text = string.Format("在线用户 ({0})", _onlinePeers.Count);
        }

        private void UpdateStatus(string message)
        {
            _lblStatus.Text = message;
        }

        private void Service_ErrorOccurred(object sender, string error)
        {
            if (InvokeRequired)
            {
                Invoke(new EventHandler<string>(Service_ErrorOccurred), sender, error);
                return;
            }

            UpdateStatus("错误: " + error);
        }

        private void ListUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listUsers.SelectedItem is PeerInfo peer)
            {
                LoadConversation(peer);
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendTextMessage();
        }

        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                SendTextMessage();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void BtnAttachImage_Click(object sender, EventArgs e)
        {
            if (_currentChatPeer == null)
            {
                MessageBox.Show("请先选择一个用户!", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
                ofd.Title = "选择图片";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    SendImageMessage(ofd.FileName);
                }
            }
        }

        private void BtnAttachFile_Click(object sender, EventArgs e)
        {
            if (_currentChatPeer == null)
            {
                MessageBox.Show("请先选择一个用户!", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "所有文件|*.*";
                ofd.Title = "选择文件";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _fileTransferService.StartSendingFile(ofd.FileName, _currentChatPeer, _userName);
                }
            }
        }

        private void MenuSetUsername_Click(object sender, EventArgs e)
        {
            using (InputDialog dlg = new InputDialog("设置用户名", "请输入新的用户名:", _userName))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _userName = dlg.InputText;
                    UpdateStatus("用户名已更新");
                }
            }
        }

        private void MenuExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void MenuAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show("LazyChat - 局域网聊天工具\n\n支持文字消息、图片和文件传输\n无需服务器,自动发现局域网用户\n\nVersion 1.0", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_discoveryService != null)
            {
                _discoveryService.Stop();
                _discoveryService.Dispose();
            }

            if (_commService != null)
            {
                _commService.Stop();
                _commService.Dispose();
            }

            if (_fileTransferService != null)
            {
                _fileTransferService.Dispose();
            }
        }
    }

    public class InputDialog : Form
    {
        private Label _lblPrompt;
        private TextBox _txtInput;
        private Button _btnOK;
        private Button _btnCancel;

        public string InputText { get; private set; }

        public InputDialog(string title, string prompt, string defaultValue)
        {
            this.Text = title;
            this.Size = new Size(400, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _lblPrompt = new Label();
            _lblPrompt.Text = prompt;
            _lblPrompt.Location = new Point(20, 20);
            _lblPrompt.Size = new Size(350, 20);

            _txtInput = new TextBox();
            _txtInput.Text = defaultValue;
            _txtInput.Location = new Point(20, 50);
            _txtInput.Size = new Size(350, 25);

            _btnOK = new Button();
            _btnOK.Text = "确定";
            _btnOK.Location = new Point(200, 85);
            _btnOK.Size = new Size(80, 30);
            _btnOK.DialogResult = DialogResult.OK;
            _btnOK.Click += (s, e) => { InputText = _txtInput.Text; };

            _btnCancel = new Button();
            _btnCancel.Text = "取消";
            _btnCancel.Location = new Point(290, 85);
            _btnCancel.Size = new Size(80, 30);
            _btnCancel.DialogResult = DialogResult.Cancel;

            this.Controls.Add(_lblPrompt);
            this.Controls.Add(_txtInput);
            this.Controls.Add(_btnOK);
            this.Controls.Add(_btnCancel);
            this.AcceptButton = _btnOK;
            this.CancelButton = _btnCancel;
        }
    }
}
