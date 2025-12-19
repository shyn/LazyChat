using System;
using System.Drawing;
using System.Windows.Forms;
using LazyChat.Models;

namespace LazyChat
{
    public partial class FileTransferDialog : Form
    {
        private FileTransferInfo _transferInfo;
        private bool _isReceiving;

        public FileTransferDialog(FileTransferInfo transferInfo, bool isReceiving)
        {
            _transferInfo = transferInfo;
            _isReceiving = isReceiving;
            InitializeComponent();
            SetupDialog();
        }

        public bool IsAccepted { get; private set; }
        public string SavePath { get; private set; }

        private void SetupDialog()
        {
            if (_isReceiving)
            {
                this.Text = "接收文件";
                _lblInfo.Text = string.Format("{0} 想要发送文件给您:", _transferInfo.SenderName);
                _lblFileName.Text = string.Format("文件名: {0}", _transferInfo.FileName);
                _lblFileSize.Text = string.Format("大小: {0}", FormatFileSize(_transferInfo.FileSize));
                _progressBar.Visible = false;
                _lblProgress.Visible = false;
                _btnAccept.Visible = true;
                _btnReject.Visible = true;
                _btnClose.Visible = false;
            }
            else
            {
                this.Text = "发送文件";
                _lblInfo.Text = string.Format("正在发送文件到 {0}:", _transferInfo.ReceiverId);
                _lblFileName.Text = string.Format("文件名: {0}", _transferInfo.FileName);
                _lblFileSize.Text = string.Format("大小: {0}", FormatFileSize(_transferInfo.FileSize));
                _progressBar.Visible = true;
                _lblProgress.Visible = true;
                _btnAccept.Visible = false;
                _btnReject.Visible = false;
                _btnClose.Visible = false;
            }
        }

        public void UpdateProgress(int progress)
        {
            if (_progressBar.InvokeRequired)
            {
                _progressBar.Invoke(new Action<int>(UpdateProgress), progress);
                return;
            }

            _progressBar.Value = Math.Min(progress, 100);
            _lblProgress.Text = string.Format("进度: {0}%", progress);

            if (progress >= 100)
            {
                _lblProgress.Text = "传输完成!";
                _btnClose.Visible = true;
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

        private void BtnAccept_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = _transferInfo.FileName;
                sfd.Filter = "所有文件 (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SavePath = sfd.FileName;
                    IsAccepted = true;

                    _lblInfo.Text = "正在接收文件...";
                    _progressBar.Visible = true;
                    _lblProgress.Visible = true;
                    _btnAccept.Visible = false;
                    _btnReject.Visible = false;
                }
                else
                {
                    IsAccepted = false;
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            }
        }

        private void BtnReject_Click(object sender, EventArgs e)
        {
            IsAccepted = false;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    partial class FileTransferDialog
    {
        private System.ComponentModel.IContainer components = null;
        private Label _lblInfo;
        private Label _lblFileName;
        private Label _lblFileSize;
        private ProgressBar _progressBar;
        private Label _lblProgress;
        private Button _btnAccept;
        private Button _btnReject;
        private Button _btnClose;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this._lblInfo = new System.Windows.Forms.Label();
            this._lblFileName = new System.Windows.Forms.Label();
            this._lblFileSize = new System.Windows.Forms.Label();
            this._progressBar = new System.Windows.Forms.ProgressBar();
            this._lblProgress = new System.Windows.Forms.Label();
            this._btnAccept = new System.Windows.Forms.Button();
            this._btnReject = new System.Windows.Forms.Button();
            this._btnClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            
            this._lblInfo.AutoSize = true;
            this._lblInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this._lblInfo.Location = new System.Drawing.Point(20, 20);
            this._lblInfo.Name = "_lblInfo";
            this._lblInfo.Size = new System.Drawing.Size(200, 18);
            this._lblInfo.TabIndex = 0;
            
            this._lblFileName.AutoSize = true;
            this._lblFileName.Location = new System.Drawing.Point(20, 50);
            this._lblFileName.Name = "_lblFileName";
            this._lblFileName.Size = new System.Drawing.Size(100, 17);
            this._lblFileName.TabIndex = 1;
            
            this._lblFileSize.AutoSize = true;
            this._lblFileSize.Location = new System.Drawing.Point(20, 75);
            this._lblFileSize.Name = "_lblFileSize";
            this._lblFileSize.Size = new System.Drawing.Size(100, 17);
            this._lblFileSize.TabIndex = 2;
            
            this._progressBar.Location = new System.Drawing.Point(20, 110);
            this._progressBar.Name = "_progressBar";
            this._progressBar.Size = new System.Drawing.Size(360, 25);
            this._progressBar.TabIndex = 3;
            
            this._lblProgress.AutoSize = true;
            this._lblProgress.Location = new System.Drawing.Point(20, 145);
            this._lblProgress.Name = "_lblProgress";
            this._lblProgress.Size = new System.Drawing.Size(70, 17);
            this._lblProgress.TabIndex = 4;
            this._lblProgress.Text = "进度: 0%";
            
            this._btnAccept.Location = new System.Drawing.Point(120, 180);
            this._btnAccept.Name = "_btnAccept";
            this._btnAccept.Size = new System.Drawing.Size(100, 30);
            this._btnAccept.TabIndex = 5;
            this._btnAccept.Text = "接受";
            this._btnAccept.UseVisualStyleBackColor = true;
            this._btnAccept.Click += new System.EventHandler(this.BtnAccept_Click);
            
            this._btnReject.Location = new System.Drawing.Point(230, 180);
            this._btnReject.Name = "_btnReject";
            this._btnReject.Size = new System.Drawing.Size(100, 30);
            this._btnReject.TabIndex = 6;
            this._btnReject.Text = "拒绝";
            this._btnReject.UseVisualStyleBackColor = true;
            this._btnReject.Click += new System.EventHandler(this.BtnReject_Click);
            
            this._btnClose.Location = new System.Drawing.Point(175, 180);
            this._btnClose.Name = "_btnClose";
            this._btnClose.Size = new System.Drawing.Size(100, 30);
            this._btnClose.TabIndex = 7;
            this._btnClose.Text = "关闭";
            this._btnClose.UseVisualStyleBackColor = true;
            this._btnClose.Visible = false;
            this._btnClose.Click += new System.EventHandler(this.BtnClose_Click);
            
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 230);
            this.Controls.Add(this._btnClose);
            this.Controls.Add(this._btnReject);
            this.Controls.Add(this._btnAccept);
            this.Controls.Add(this._lblProgress);
            this.Controls.Add(this._progressBar);
            this.Controls.Add(this._lblFileSize);
            this.Controls.Add(this._lblFileName);
            this.Controls.Add(this._lblInfo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FileTransferDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "文件传输";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
