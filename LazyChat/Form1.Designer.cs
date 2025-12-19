namespace LazyChat
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源,为 true;否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this._splitContainer = new System.Windows.Forms.SplitContainer();
            this._lblUsers = new System.Windows.Forms.Label();
            this._listUsers = new System.Windows.Forms.ListBox();
            this._panelChat = new System.Windows.Forms.Panel();
            this._lblChatWith = new System.Windows.Forms.Label();
            this._panelMessages = new System.Windows.Forms.Panel();
            this._panelInput = new System.Windows.Forms.Panel();
            this._btnSend = new System.Windows.Forms.Button();
            this._btnAttachFile = new System.Windows.Forms.Button();
            this._btnAttachImage = new System.Windows.Forms.Button();
            this._txtMessage = new System.Windows.Forms.TextBox();
            this._statusStrip = new System.Windows.Forms.StatusStrip();
            this._lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this._menuStrip = new System.Windows.Forms.MenuStrip();
            this._menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this._menuSetUsername = new System.Windows.Forms.ToolStripMenuItem();
            this._menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this._menuHelp = new System.Windows.Forms.ToolStripMenuItem();
            this._menuAbout = new System.Windows.Forms.ToolStripMenuItem();
            
            ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).BeginInit();
            this._splitContainer.Panel1.SuspendLayout();
            this._splitContainer.Panel2.SuspendLayout();
            this._splitContainer.SuspendLayout();
            this._panelChat.SuspendLayout();
            this._panelInput.SuspendLayout();
            this._statusStrip.SuspendLayout();
            this._menuStrip.SuspendLayout();
            this.SuspendLayout();
            
            this._splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this._splitContainer.Location = new System.Drawing.Point(0, 28);
            this._splitContainer.Name = "_splitContainer";
            this._splitContainer.Size = new System.Drawing.Size(1024, 570);
            this._splitContainer.SplitterDistance = 250;
            this._splitContainer.TabIndex = 0;
            
            this._lblUsers.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblUsers.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this._lblUsers.Location = new System.Drawing.Point(0, 0);
            this._lblUsers.Name = "_lblUsers";
            this._lblUsers.Padding = new System.Windows.Forms.Padding(5);
            this._lblUsers.Size = new System.Drawing.Size(250, 35);
            this._lblUsers.TabIndex = 0;
            this._lblUsers.Text = "在线用户 (0)";
            
            this._listUsers.Dock = System.Windows.Forms.DockStyle.Fill;
            this._listUsers.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this._listUsers.FormattingEnabled = true;
            this._listUsers.ItemHeight = 18;
            this._listUsers.Location = new System.Drawing.Point(0, 35);
            this._listUsers.Name = "_listUsers";
            this._listUsers.Size = new System.Drawing.Size(250, 535);
            this._listUsers.TabIndex = 1;
            this._listUsers.SelectedIndexChanged += new System.EventHandler(this.ListUsers_SelectedIndexChanged);
            
            this._panelChat.Dock = System.Windows.Forms.DockStyle.Fill;
            this._panelChat.Location = new System.Drawing.Point(0, 0);
            this._panelChat.Name = "_panelChat";
            this._panelChat.Size = new System.Drawing.Size(770, 570);
            this._panelChat.TabIndex = 0;
            
            this._lblChatWith.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblChatWith.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this._lblChatWith.Location = new System.Drawing.Point(0, 0);
            this._lblChatWith.Name = "_lblChatWith";
            this._lblChatWith.Padding = new System.Windows.Forms.Padding(5);
            this._lblChatWith.Size = new System.Drawing.Size(770, 35);
            this._lblChatWith.TabIndex = 0;
            this._lblChatWith.Text = "请选择一个用户开始聊天";
            
            this._panelMessages.AutoScroll = true;
            this._panelMessages.BackColor = System.Drawing.Color.White;
            this._panelMessages.Dock = System.Windows.Forms.DockStyle.Fill;
            this._panelMessages.Location = new System.Drawing.Point(0, 35);
            this._panelMessages.Name = "_panelMessages";
            this._panelMessages.Padding = new System.Windows.Forms.Padding(10);
            this._panelMessages.Size = new System.Drawing.Size(770, 435);
            this._panelMessages.TabIndex = 1;
            
            this._panelInput.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._panelInput.Location = new System.Drawing.Point(0, 470);
            this._panelInput.Name = "_panelInput";
            this._panelInput.Padding = new System.Windows.Forms.Padding(5);
            this._panelInput.Size = new System.Drawing.Size(770, 100);
            this._panelInput.TabIndex = 2;
            
            this._btnSend.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this._btnSend.Location = new System.Drawing.Point(660, 60);
            this._btnSend.Name = "_btnSend";
            this._btnSend.Size = new System.Drawing.Size(100, 30);
            this._btnSend.TabIndex = 3;
            this._btnSend.Text = "发送(&S)";
            this._btnSend.UseVisualStyleBackColor = true;
            this._btnSend.Click += new System.EventHandler(this.BtnSend_Click);
            
            this._btnAttachFile.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this._btnAttachFile.Location = new System.Drawing.Point(550, 60);
            this._btnAttachFile.Name = "_btnAttachFile";
            this._btnAttachFile.Size = new System.Drawing.Size(100, 30);
            this._btnAttachFile.TabIndex = 2;
            this._btnAttachFile.Text = "文件(&F)";
            this._btnAttachFile.UseVisualStyleBackColor = true;
            this._btnAttachFile.Click += new System.EventHandler(this.BtnAttachFile_Click);
            
            this._btnAttachImage.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this._btnAttachImage.Location = new System.Drawing.Point(440, 60);
            this._btnAttachImage.Name = "_btnAttachImage";
            this._btnAttachImage.Size = new System.Drawing.Size(100, 30);
            this._btnAttachImage.TabIndex = 1;
            this._btnAttachImage.Text = "图片(&I)";
            this._btnAttachImage.UseVisualStyleBackColor = true;
            this._btnAttachImage.Click += new System.EventHandler(this.BtnAttachImage_Click);
            
            this._txtMessage.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;
            this._txtMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this._txtMessage.Location = new System.Drawing.Point(10, 10);
            this._txtMessage.Multiline = true;
            this._txtMessage.Name = "_txtMessage";
            this._txtMessage.Size = new System.Drawing.Size(750, 45);
            this._txtMessage.TabIndex = 0;
            this._txtMessage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TxtMessage_KeyDown);
            
            this._statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this._statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this._lblStatus });
            this._statusStrip.Location = new System.Drawing.Point(0, 598);
            this._statusStrip.Name = "_statusStrip";
            this._statusStrip.Size = new System.Drawing.Size(1024, 22);
            this._statusStrip.TabIndex = 1;
            
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new System.Drawing.Size(56, 17);
            this._lblStatus.Text = "就绪";
            
            this._menuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this._menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this._menuFile, this._menuHelp });
            this._menuStrip.Location = new System.Drawing.Point(0, 0);
            this._menuStrip.Name = "_menuStrip";
            this._menuStrip.Size = new System.Drawing.Size(1024, 28);
            this._menuStrip.TabIndex = 2;
            
            this._menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this._menuSetUsername, this._menuExit });
            this._menuFile.Name = "_menuFile";
            this._menuFile.Size = new System.Drawing.Size(58, 24);
            this._menuFile.Text = "文件(&F)";
            
            this._menuSetUsername.Name = "_menuSetUsername";
            this._menuSetUsername.Size = new System.Drawing.Size(180, 26);
            this._menuSetUsername.Text = "设置用户名(&U)";
            this._menuSetUsername.Click += new System.EventHandler(this.MenuSetUsername_Click);
            
            this._menuExit.Name = "_menuExit";
            this._menuExit.Size = new System.Drawing.Size(180, 26);
            this._menuExit.Text = "退出(&X)";
            this._menuExit.Click += new System.EventHandler(this.MenuExit_Click);
            
            this._menuHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this._menuAbout });
            this._menuHelp.Name = "_menuHelp";
            this._menuHelp.Size = new System.Drawing.Size(64, 24);
            this._menuHelp.Text = "帮助(&H)";
            
            this._menuAbout.Name = "_menuAbout";
            this._menuAbout.Size = new System.Drawing.Size(180, 26);
            this._menuAbout.Text = "关于(&A)";
            this._menuAbout.Click += new System.EventHandler(this.MenuAbout_Click);
            
            this._splitContainer.Panel1.Controls.Add(this._listUsers);
            this._splitContainer.Panel1.Controls.Add(this._lblUsers);
            this._splitContainer.Panel2.Controls.Add(this._panelChat);
            
            this._panelChat.Controls.Add(this._panelMessages);
            this._panelChat.Controls.Add(this._panelInput);
            this._panelChat.Controls.Add(this._lblChatWith);
            
            this._panelInput.Controls.Add(this._txtMessage);
            this._panelInput.Controls.Add(this._btnAttachImage);
            this._panelInput.Controls.Add(this._btnAttachFile);
            this._panelInput.Controls.Add(this._btnSend);
            
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1024, 620);
            this.Controls.Add(this._splitContainer);
            this.Controls.Add(this._statusStrip);
            this.Controls.Add(this._menuStrip);
            this.MainMenuStrip = this._menuStrip;
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "局域网聊天 - LazyChat";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            
            this._splitContainer.Panel1.ResumeLayout(false);
            this._splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).EndInit();
            this._splitContainer.ResumeLayout(false);
            this._panelChat.ResumeLayout(false);
            this._panelInput.ResumeLayout(false);
            this._panelInput.PerformLayout();
            this._statusStrip.ResumeLayout(false);
            this._statusStrip.PerformLayout();
            this._menuStrip.ResumeLayout(false);
            this._menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.SplitContainer _splitContainer;
        private System.Windows.Forms.Label _lblUsers;
        private System.Windows.Forms.ListBox _listUsers;
        private System.Windows.Forms.Panel _panelChat;
        private System.Windows.Forms.Label _lblChatWith;
        private System.Windows.Forms.Panel _panelMessages;
        private System.Windows.Forms.Panel _panelInput;
        private System.Windows.Forms.TextBox _txtMessage;
        private System.Windows.Forms.Button _btnAttachImage;
        private System.Windows.Forms.Button _btnAttachFile;
        private System.Windows.Forms.Button _btnSend;
        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _lblStatus;
        private System.Windows.Forms.MenuStrip _menuStrip;
        private System.Windows.Forms.ToolStripMenuItem _menuFile;
        private System.Windows.Forms.ToolStripMenuItem _menuSetUsername;
        private System.Windows.Forms.ToolStripMenuItem _menuExit;
        private System.Windows.Forms.ToolStripMenuItem _menuHelp;
        private System.Windows.Forms.ToolStripMenuItem _menuAbout;
    }
}

