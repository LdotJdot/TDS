using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TDSAot
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // 创建主窗口但不立即显示
                var mainWindow = new MainWindow();

                // 设置窗口初始状态为隐藏
                mainWindow.ShowInTaskbar = false;
                mainWindow.WindowState = WindowState.Minimized;

                desktop.MainWindow = mainWindow;

            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}