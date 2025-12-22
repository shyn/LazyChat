using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using LazyChat.Models;
using LazyChat.ViewModels;

namespace LazyChat.Views
{
    public partial class ProfileDialog : Window
    {
        private readonly ContactItem _contact;
        private readonly PeerInfo _localPeer;

        public ProfileDialog()
        {
            InitializeComponent();
        }

        public ProfileDialog(ContactItem contact, PeerInfo localPeer) : this()
        {
            _contact = contact;
            _localPeer = localPeer;

            // Set avatar
            string initial = string.IsNullOrWhiteSpace(contact.DisplayName) 
                ? "?" 
                : contact.DisplayName.Substring(0, 1).ToUpperInvariant();
            
            AvatarInitial.Text = initial;
            
            if (contact.IsOnline)
            {
                AvatarBorder.Background = new SolidColorBrush(Color.Parse("#E8F5E9"));
                AvatarInitial.Foreground = new SolidColorBrush(Color.Parse("#2E7D32"));
                StatusBadge.Background = new SolidColorBrush(Color.Parse("#E8F5E9"));
                StatusText.Text = "● 在线";
                StatusText.Foreground = new SolidColorBrush(Color.Parse("#2E7D32"));
            }
            else
            {
                AvatarBorder.Background = new SolidColorBrush(Color.Parse("#F5F5F7"));
                AvatarInitial.Foreground = new SolidColorBrush(Color.Parse("#86868B"));
                StatusBadge.Background = new SolidColorBrush(Color.Parse("#F5F5F7"));
                StatusText.Text = "○ 离线";
                StatusText.Foreground = new SolidColorBrush(Color.Parse("#86868B"));
            }

            DisplayNameText.Text = contact.DisplayName;
            PeerIdText.Text = contact.PeerId;
            IpAddressText.Text = string.IsNullOrEmpty(contact.IpAddress) ? "未知" : contact.IpAddress;
            PortText.Text = contact.Port > 0 ? contact.Port.ToString() : "未知";

            CopyIdButton.Click += CopyIdButton_Click;
            CloseButton.Click += CloseButton_Click;
        }

        private async void CopyIdButton_Click(object sender, RoutedEventArgs e)
        {
            if (_contact != null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(_contact.PeerId);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
