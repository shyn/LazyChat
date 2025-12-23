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
                // Use PreviewKeyDown (tunneling) to intercept before TextBox handles it
                _messageTextBox.AddHandler(KeyDownEvent, MessageTextBox_PreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
                
                // Update AcceptsReturn based on current setting
                UpdateAcceptsReturn();
                
                // Listen for setting changes
                _viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(_viewModel.EnterToSend))
                    {
                        UpdateAcceptsReturn();
                    }
                };
            }
        }

        private void UpdateAcceptsReturn()
        {
            if (_messageTextBox != null)
            {
                // If EnterToSend is true, don't accept return (Enter sends)
                // If EnterToSend is false, accept return (Enter = newline, Ctrl+Enter sends)
                _messageTextBox.AcceptsReturn = !_viewModel.EnterToSend;
            }
        }

        private void MessageTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                bool isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                bool isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                
                if (_viewModel.EnterToSend)
                {
                    // Enter to send mode
                    if (isShiftPressed)
                    {
                        // Shift+Enter: insert newline manually
                        e.Handled = true;
                        InsertNewLine();
                    }
                    else if (!isCtrlPressed)
                    {
                        // Plain Enter: send message
                        e.Handled = true;
                        _viewModel.SendMessageCommand.Execute(null);
                    }
                }
                else
                {
                    // Ctrl+Enter to send mode
                    if (isCtrlPressed)
                    {
                        // Ctrl+Enter: send message
                        e.Handled = true;
                        _viewModel.SendMessageCommand.Execute(null);
                    }
                    // Plain Enter: let TextBox handle it (newline) - AcceptsReturn is true
                }
            }
        }

        private void InsertNewLine()
        {
            if (_messageTextBox != null)
            {
                int caretIndex = _messageTextBox.CaretIndex;
                string text = _messageTextBox.Text ?? "";
                _messageTextBox.Text = text.Insert(caretIndex, "\n");
                _messageTextBox.CaretIndex = caretIndex + 1;
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
            base.OnClosing(e);
        }
    }
}
