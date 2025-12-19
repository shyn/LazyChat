using Avalonia.Controls;
using LazyChat.ViewModels;

namespace LazyChat.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            Opened += async (_, _) => await _viewModel.InitializeAsync(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _viewModel?.Cleanup();
            base.OnClosing(e);
        }
    }
}
