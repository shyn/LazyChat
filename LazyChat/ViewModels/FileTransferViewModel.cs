using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using LazyChat.Models;

namespace LazyChat.ViewModels
{
    public class FileTransferViewModel : INotifyPropertyChanged
    {
        private readonly FileTransferInfo _transferInfo;
        private readonly bool _isReceiving;
        private int _progress;
        private string _progressText;
        private bool _showProgress;
        private bool _showAcceptReject;
        private bool _showClose;

        public event PropertyChangedEventHandler PropertyChanged;

        public FileTransferViewModel()
        {
            // Design-time constructor
            _transferInfo = new FileTransferInfo
            {
                FileName = "example.txt",
                FileSize = 1024000,
                SenderName = "Test User"
            };
            _isReceiving = true;
            SetupCommands();
            SetupDialog();
        }

        public FileTransferViewModel(FileTransferInfo transferInfo, bool isReceiving)
        {
            _transferInfo = transferInfo;
            _isReceiving = isReceiving;
            SetupCommands();
            SetupDialog();
        }

        public string InfoText { get; private set; }
        public string FileNameText => $"文件名: {_transferInfo.FileName}";
        public string FileSizeText => $"大小: {FormatFileSize(_transferInfo.FileSize)}";

        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set
            {
                if (_progressText != value)
                {
                    _progressText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowProgress
        {
            get => _showProgress;
            set
            {
                if (_showProgress != value)
                {
                    _showProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowAcceptReject
        {
            get => _showAcceptReject;
            set
            {
                if (_showAcceptReject != value)
                {
                    _showAcceptReject = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowClose
        {
            get => _showClose;
            set
            {
                if (_showClose != value)
                {
                    _showClose = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand AcceptCommand { get; private set; }
        public ICommand RejectCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        private void SetupCommands()
        {
            AcceptCommand = new RelayCommand(Accept);
            RejectCommand = new RelayCommand(Reject);
            CloseCommand = new RelayCommand(Close);
        }

        private void SetupDialog()
        {
            if (_isReceiving)
            {
                InfoText = $"{_transferInfo.SenderName} 想要发送文件给您:";
                ShowProgress = false;
                ShowAcceptReject = true;
                ShowClose = false;
            }
            else
            {
                InfoText = $"正在发送文件到 {_transferInfo.ReceiverId}:";
                ShowProgress = true;
                ShowAcceptReject = false;
                ShowClose = false;
                ProgressText = "进度: 0%";
            }
        }

        public void UpdateProgress(int progress)
        {
            Progress = Math.Min(progress, 100);
            ProgressText = $"进度: {progress}%";

            if (progress >= 100)
            {
                ProgressText = "传输完成!";
                ShowClose = true;
            }
        }

        private async void Accept()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "保存文件",
                    SuggestedFileName = _transferInfo.FileName
                });

                if (file != null)
                {
                    SavePath = file.Path.LocalPath;
                    IsAccepted = true;

                    InfoText = "正在接收文件...";
                    ShowProgress = true;
                    ShowAcceptReject = false;
                    ProgressText = "进度: 0%";
                }
                else
                {
                    IsAccepted = false;
                    CloseWindow(false);
                }
            }
        }

        private void Reject()
        {
            IsAccepted = false;
            CloseWindow(false);
        }

        private void Close()
        {
            CloseWindow(true);
        }

        private void CloseWindow(bool result)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is Window mainWindow)
                {
                    foreach (var window in mainWindow.OwnedWindows)
                    {
                        if (window is Views.FileTransferWindow transferWindow && transferWindow.DataContext == this)
                        {
                            transferWindow.Close(result);
                            return;
                        }
                    }
                }
            }
        }

        public bool IsAccepted { get; private set; }
        public string SavePath { get; private set; }

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

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
