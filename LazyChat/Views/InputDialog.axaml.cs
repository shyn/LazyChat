using Avalonia.Controls;
using LazyChat.ViewModels;

namespace LazyChat.Views
{
    public partial class InputDialog : Window
    {
        private InputDialogViewModel _viewModel;

        public InputDialog()
        {
            InitializeComponent();
        }

        public InputDialog(string title, string prompt, string defaultValue)
        {
            InitializeComponent();
            _viewModel = new InputDialogViewModel(title, prompt, defaultValue);
            DataContext = _viewModel;
        }

        public string InputText => _viewModel?.InputText ?? string.Empty;
    }
}
