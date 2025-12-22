using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using LazyChat.ViewModels;

namespace LazyChat.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;
        private ScrollViewer _messagesScrollViewer;
        private TextBox _messageTextBox;

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
            _messageTextBox = this.FindControl<TextBox>("MessageTextBox");
            
            if (_messageTextBox != null)
            {
                _messageTextBox.KeyDown += MessageTextBox_KeyDown;
            }
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                bool isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                bool isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                
                if (_viewModel.EnterToSend)
                {
                    // Enter to send mode
                    if (!isShiftPressed && !isCtrlPressed)
                    {
                        // Plain Enter: send message
                        e.Handled = true;
                        _viewModel.SendMessageCommand.Execute(null);
                    }
                    // Shift+Enter: let default behavior (newline) happen
                }
                else
                {
                    // Ctrl+Enter to send mode (handled by KeyBinding)
                    // Plain Enter: let default behavior (newline) happen
                }
            }
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
            
            if (_messageTextBox != null)
            {
                _messageTextBox.KeyDown -= MessageTextBox_KeyDown;
            }
            
            base.OnClosing(e);
        }
    }
}
