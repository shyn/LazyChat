using System.Collections.Specialized;
using Avalonia.Controls;
using LazyChat.ViewModels;

namespace LazyChat.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;
        private ScrollViewer _messagesScrollViewer;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            Opened += async (_, _) => await _viewModel.InitializeAsync(this);
            
            // Subscribe to messages collection changes for auto-scroll
            _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
        }

        protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _messagesScrollViewer = this.FindControl<ScrollViewer>("MessagesScrollViewer");
        }

        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // Scroll to bottom when new message is added
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _messagesScrollViewer?.ScrollToEnd();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _viewModel?.Cleanup();
            _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            base.OnClosing(e);
        }
    }
}
