using Avalonia.Controls;
using LazyChat.Models;
using LazyChat.ViewModels;

namespace LazyChat.Views
{
    public partial class FileTransferWindow : Window
    {
        private FileTransferViewModel _viewModel;

        public FileTransferWindow()
        {
            InitializeComponent();
        }

        public FileTransferWindow(FileTransferInfo transferInfo, bool isReceiving)
        {
            InitializeComponent();
            _viewModel = new FileTransferViewModel(transferInfo, isReceiving);
            DataContext = _viewModel;
        }

        public bool IsAccepted => _viewModel?.IsAccepted ?? false;
        public string SavePath => _viewModel?.SavePath;

        public void UpdateProgress(int progress)
        {
            _viewModel?.UpdateProgress(progress);
        }
    }
}
