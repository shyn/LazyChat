using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace LazyChat.ViewModels
{
    public class InputDialogViewModel : INotifyPropertyChanged
    {
        private string _title;
        private string _prompt;
        private string _inputText;

        public event PropertyChangedEventHandler PropertyChanged;

        public InputDialogViewModel()
        {
            // Design-time constructor
            _title = "输入对话框";
            _prompt = "请输入内容:";
            _inputText = string.Empty;
            SetupCommands();
        }

        public InputDialogViewModel(string title, string prompt, string defaultValue)
        {
            _title = title;
            _prompt = prompt;
            _inputText = defaultValue ?? string.Empty;
            SetupCommands();
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Prompt
        {
            get => _prompt;
            set
            {
                if (_prompt != value)
                {
                    _prompt = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                if (_inputText != value)
                {
                    _inputText = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand OkCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private void SetupCommands()
        {
            OkCommand = new RelayCommand(Ok);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Ok()
        {
            CloseWindow(true);
        }

        private void Cancel()
        {
            CloseWindow(false);
        }

        private void CloseWindow(bool result)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is Window mainWindow)
                {
                    foreach (var window in mainWindow.OwnedWindows)
                    {
                        if (window is Views.InputDialog inputDialog && inputDialog.DataContext == this)
                        {
                            inputDialog.Close(result);
                            return;
                        }
                    }
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
