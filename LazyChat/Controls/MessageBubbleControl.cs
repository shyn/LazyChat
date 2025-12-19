using System;
using System.Drawing;
using System.Windows.Forms;
using LazyChat.Models;

namespace LazyChat.Controls
{
    public class MessageBubbleControl : Panel
    {
        private Label _lblSender;
        private Label _lblTime;
        private Panel _bubblePanel;
        private Label _lblMessage;
        private PictureBox _picImage;
        private Label _lblFileName;
        private ChatMessage _message;

        public MessageBubbleControl(ChatMessage message)
        {
            _message = message;
            InitializeComponent();
            SetupMessage();
        }

        private void InitializeComponent()
        {
            _lblSender = new Label();
            _lblTime = new Label();
            _bubblePanel = new Panel();
            _lblMessage = new Label();
            _picImage = new PictureBox();
            _lblFileName = new Label();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(_picImage)).BeginInit();

            this.Padding = new Padding(5);
            this.AutoSize = true;
            this.MinimumSize = new Size(400, 50);
            this.MaximumSize = new Size(600, 0);

            _lblSender.AutoSize = true;
            _lblSender.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Bold);
            _lblSender.Location = new Point(5, 5);

            _lblTime.AutoSize = true;
            _lblTime.Font = new Font("Microsoft Sans Serif", 7F);
            _lblTime.ForeColor = Color.Gray;
            _lblTime.Location = new Point(100, 7);

            _bubblePanel.AutoSize = true;
            _bubblePanel.Padding = new Padding(10);
            _bubblePanel.Location = new Point(5, 25);
            _bubblePanel.MaximumSize = new Size(500, 0);

            _lblMessage.AutoSize = true;
            _lblMessage.MaximumSize = new Size(480, 0);
            _lblMessage.Font = new Font("Microsoft Sans Serif", 9F);
            _lblMessage.Location = new Point(10, 10);

            _picImage.SizeMode = PictureBoxSizeMode.Zoom;
            _picImage.MaximumSize = new Size(300, 300);
            _picImage.Location = new Point(10, 10);
            _picImage.Visible = false;

            _lblFileName.AutoSize = true;
            _lblFileName.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Underline);
            _lblFileName.ForeColor = Color.Blue;
            _lblFileName.Cursor = Cursors.Hand;
            _lblFileName.Location = new Point(10, 10);
            _lblFileName.Visible = false;

            this.Controls.Add(_lblSender);
            this.Controls.Add(_lblTime);
            this.Controls.Add(_bubblePanel);
            _bubblePanel.Controls.Add(_lblMessage);
            _bubblePanel.Controls.Add(_picImage);
            _bubblePanel.Controls.Add(_lblFileName);

            ((System.ComponentModel.ISupportInitialize)(_picImage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void SetupMessage()
        {
            _lblSender.Text = _message.SenderName;
            _lblTime.Text = _message.Timestamp.ToString("HH:mm:ss");

            if (_message.IsSentByMe)
            {
                _lblSender.ForeColor = Color.Blue;
                _bubblePanel.BackColor = Color.FromArgb(220, 240, 255);
                this.Dock = DockStyle.Top;
            }
            else
            {
                _lblSender.ForeColor = Color.Green;
                _bubblePanel.BackColor = Color.FromArgb(240, 240, 240);
                this.Dock = DockStyle.Top;
            }

            switch (_message.MessageType)
            {
                case ChatMessageType.Text:
                    _lblMessage.Text = _message.TextContent;
                    _lblMessage.Visible = true;
                    break;

                case ChatMessageType.Image:
                    if (_message.ImageContent != null)
                    {
                        _picImage.Image = _message.ImageContent;
                        Size imageSize = _message.ImageContent.Size;
                        float scale = Math.Min(300f / imageSize.Width, 300f / imageSize.Height);
                        if (scale < 1)
                        {
                            _picImage.Size = new Size((int)(imageSize.Width * scale), (int)(imageSize.Height * scale));
                        }
                        else
                        {
                            _picImage.Size = imageSize;
                        }
                        _picImage.Visible = true;
                        _lblMessage.Visible = false;
                    }
                    break;

                case ChatMessageType.File:
                    _lblFileName.Text = string.Format("?? {0} ({1})", _message.FileName, FormatFileSize(_message.FileSize));
                    _lblFileName.Visible = true;
                    _lblMessage.Visible = false;
                    break;

                case ChatMessageType.System:
                    _lblMessage.Text = _message.TextContent;
                    _lblMessage.ForeColor = Color.Gray;
                    _lblMessage.Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Italic);
                    _lblMessage.Visible = true;
                    _bubblePanel.BackColor = Color.FromArgb(250, 250, 250);
                    break;
            }
        }

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
}
