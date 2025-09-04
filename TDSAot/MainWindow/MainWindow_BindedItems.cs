using Avalonia.Controls;
using Avalonia.Media.Imaging;
using TDSAot.ViewModels;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        public MessageViewModel MessageData { get; } = new MessageViewModel();

        public DataViewModel Items { get; } = new DataViewModel();
    }
}