using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using LazyChat.ViewModels;

namespace LazyChat.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;
        private ListBox _messagesList;
        private ScrollViewer _messagesScrollViewer;
        private TextBox _messageTextBox;
        private bool _isNearBottom = true;
        private bool _suppressAutoScroll;
        private bool _isLoadingHistory;
        private bool _isLoadingRecent;
        private readonly List<MessageViewModel> _pendingReadMessages = new List<MessageViewModel>();
        private readonly HashSet<string> _pendingReadMessageIds = new HashSet<string>();
        private bool _isMarkReadScheduled;
        private string _pendingScrollToMessageId;
        private bool _pendingScrollToBottom;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            Opened += async (_, _) => await _viewModel.InitializeAsync(this);
            
            // Subscribe to messages collection changes for auto-scroll
            _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            _viewModel.ScrollToMessageRequested += ViewModel_ScrollToMessageRequested;
            _viewModel.ScrollToBottomRequested += ViewModel_ScrollToBottomRequested;
        }

        protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _messagesList = this.FindControl<ListBox>("MessagesList");
            AttachMessagesScrollViewer();
            _messageTextBox = this.FindControl<TextBox>("MessageTextBox");

            if (_messagesList != null)
            {
                _messagesList.ContainerPrepared += MessagesList_ContainerPrepared;
            }

            ProcessPendingScrollRequests();
            
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
                if (_suppressAutoScroll || !_isNearBottom || _viewModel?.IsRebuildingMessages == true)
                {
                    return;
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(ScrollToLastMessage, Avalonia.Threading.DispatcherPriority.Background);
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _pendingReadMessages.Clear();
                _pendingReadMessageIds.Clear();
                Avalonia.Threading.Dispatcher.UIThread.Post(EnsureScrollableOrLoadMore, Avalonia.Threading.DispatcherPriority.Background);
            }

            if (_messagesScrollViewer == null)
            {
                AttachMessagesScrollViewer();
            }
        }

        private void AttachMessagesScrollViewer()
        {
            if (_messagesList == null)
            {
                return;
            }

            _messagesScrollViewer = _messagesList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (_messagesScrollViewer != null)
            {
                _messagesScrollViewer.ScrollChanged += MessagesScrollViewer_ScrollChanged;
                UpdateNearBottom();
            }
        }

        private void MessagesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateNearBottom();

            if (_messagesScrollViewer == null || _isLoadingHistory || _isLoadingRecent)
            {
                return;
            }

            if (_messagesScrollViewer.Offset.Y <= 0)
            {
                _ = TryLoadMoreHistoryAsync();
            }
            else if (_isNearBottom && _viewModel?.HasMoreRecentForSelected() == true)
            {
                _ = TryLoadMoreRecentAsync(true);
            }
        }

        private void UpdateNearBottom()
        {
            if (_messagesScrollViewer == null)
            {
                return;
            }

            double remaining = _messagesScrollViewer.Extent.Height - _messagesScrollViewer.Viewport.Height - _messagesScrollViewer.Offset.Y;
            _isNearBottom = remaining <= 16;
        }

        private async Task TryLoadMoreHistoryAsync()
        {
            if (_viewModel == null || _messagesList == null || _isLoadingHistory)
            {
                return;
            }

            _isLoadingHistory = true;
            _suppressAutoScroll = true;

            string anchorMessageId = _viewModel.Messages.FirstOrDefault()?.MessageId;

            try
            {
                bool loaded = await _viewModel.LoadMoreHistoryAsync();
                if (loaded && !string.IsNullOrWhiteSpace(anchorMessageId))
                {
                    MessageViewModel anchor = _viewModel.Messages.FirstOrDefault(m => m.MessageId == anchorMessageId);
                    if (anchor != null)
                    {
                        _messagesList.ScrollIntoView(anchor);
                    }
                }
            }
            finally
            {
                _suppressAutoScroll = false;
                _isLoadingHistory = false;
            }
        }

        private async Task TryLoadMoreRecentAsync(bool scrollToBottom)
        {
            if (_viewModel == null || _messagesList == null || _isLoadingRecent)
            {
                return;
            }

            _isLoadingRecent = true;
            _suppressAutoScroll = true;
            string anchorMessageId = null;
            if (!scrollToBottom)
            {
                anchorMessageId = _viewModel.Messages.LastOrDefault()?.MessageId;
            }

            try
            {
                bool loaded = await _viewModel.LoadMoreRecentAsync(scrollToBottom);
                if (loaded && !scrollToBottom && !string.IsNullOrWhiteSpace(anchorMessageId))
                {
                    MessageViewModel anchor = _viewModel.Messages.FirstOrDefault(m => m.MessageId == anchorMessageId);
                    if (anchor != null)
                    {
                        _messagesList.ScrollIntoView(anchor);
                    }
                }
            }
            finally
            {
                _suppressAutoScroll = false;
                _isLoadingRecent = false;
            }
        }

        private void ScrollToLastMessage()
        {
            if (_messagesList == null || _viewModel == null)
            {
                return;
            }

            MessageViewModel last = _viewModel.Messages.LastOrDefault();
            if (last != null)
            {
                _messagesList.ScrollIntoView(last);
            }
        }

        private void MessagesList_ContainerPrepared(object sender, ContainerPreparedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            if (e.Container is ListBoxItem item && item.DataContext is MessageViewModel message)
            {
                if (message.IsSentByMe || message.IsRead)
                {
                    return;
                }

                if (_pendingReadMessageIds.Add(message.MessageId))
                {
                    _pendingReadMessages.Add(message);
                    ScheduleMarkReadBatch();
                }
            }
        }

        private void ScheduleMarkReadBatch()
        {
            if (_isMarkReadScheduled)
            {
                return;
            }

            _isMarkReadScheduled = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _isMarkReadScheduled = false;
                if (_pendingReadMessages.Count == 0 || _viewModel == null)
                {
                    return;
                }

                List<MessageViewModel> batch = new List<MessageViewModel>(_pendingReadMessages);
                _pendingReadMessages.Clear();
                _pendingReadMessageIds.Clear();
                _viewModel.MarkMessagesAsRead(batch);
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void ViewModel_ScrollToMessageRequested(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId) || _viewModel == null)
            {
                return;
            }

            if (_messagesList == null)
            {
                _pendingScrollToMessageId = messageId;
                return;
            }

            _pendingScrollToMessageId = messageId;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_messagesList == null || _viewModel == null)
                {
                    return;
                }

                AttachMessagesScrollViewer();
                MessageViewModel target = _viewModel.Messages.FirstOrDefault(m => m.MessageId == _pendingScrollToMessageId);
                if (target != null)
                {
                    _messagesList.ScrollIntoView(target);
                    _pendingScrollToMessageId = null;
                }

                EnsureScrollableOrLoadMore();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void ViewModel_ScrollToBottomRequested()
        {
            if (_messagesList == null)
            {
                _pendingScrollToBottom = true;
                return;
            }

            _pendingScrollToBottom = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_messagesList == null)
                {
                    return;
                }

                ScrollToLastMessage();
                _pendingScrollToBottom = false;
                EnsureScrollableOrLoadMore();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void EnsureScrollableOrLoadMore()
        {
            if (_viewModel == null || _isLoadingRecent || _isLoadingHistory)
            {
                return;
            }

            if (_messagesScrollViewer == null)
            {
                AttachMessagesScrollViewer();
            }

            if (_messagesScrollViewer == null)
            {
                return;
            }

            double extent = _messagesScrollViewer.Extent.Height;
            double viewport = _messagesScrollViewer.Viewport.Height;
            if (extent <= viewport + 1)
            {
                if (_viewModel.HasMoreHistoryForSelected())
                {
                    _ = TryLoadMoreHistoryAsync();
                }
                else if (_viewModel.HasMoreRecentForSelected())
                {
                    _ = TryLoadMoreRecentAsync(false);
                }
            }
        }

        private void ProcessPendingScrollRequests()
        {
            if (_messagesList == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_pendingScrollToMessageId))
            {
                ViewModel_ScrollToMessageRequested(_pendingScrollToMessageId);
            }
            else if (_pendingScrollToBottom)
            {
                ViewModel_ScrollToBottomRequested();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _viewModel?.Cleanup();
            _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            _viewModel.ScrollToMessageRequested -= ViewModel_ScrollToMessageRequested;
            _viewModel.ScrollToBottomRequested -= ViewModel_ScrollToBottomRequested;
            if (_messagesList != null)
            {
                _messagesList.ContainerPrepared -= MessagesList_ContainerPrepared;
            }
            base.OnClosing(e);
        }
    }
}
