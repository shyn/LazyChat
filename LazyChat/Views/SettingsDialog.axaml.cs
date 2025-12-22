using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyChat.Views
{
    public partial class SettingsDialog : Window
    {
        public string UserName { get; private set; }
        public bool EnterToSend { get; private set; }

        public SettingsDialog()
        {
            InitializeComponent();
        }

        public SettingsDialog(string currentUserName, bool enterToSend) : this()
        {
            UserName = currentUserName;
            EnterToSend = enterToSend;

            UsernameTextBox.Text = currentUserName;
            EnterToSendRadio.IsChecked = enterToSend;
            CtrlEnterToSendRadio.IsChecked = !enterToSend;

            SaveButton.Click += SaveButton_Click;
            CancelButton.Click += CancelButton_Click;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            UserName = UsernameTextBox.Text?.Trim() ?? "";
            EnterToSend = EnterToSendRadio.IsChecked == true;
            Close(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
