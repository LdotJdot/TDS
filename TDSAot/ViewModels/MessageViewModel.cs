using ReactiveUI;
using System;

namespace TDSAot.ViewModels
{
    public class MessageViewModel : ReactiveObject
    {
        private string _msg = "Hello, Avalonia!";

        public MessageViewModel()
        {
            Message = $"Initialized at {DateTime.Now}";
        }

        public string Message
        {
            get => _msg;
            set => this.RaiseAndSetIfChanged(ref _msg, value);
        }
    }
}