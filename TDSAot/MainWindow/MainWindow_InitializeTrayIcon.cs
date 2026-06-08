using Avalonia.Controls;
using Avalonia.Platform;
using TDS.Globalization;
using TDS.State;
using TDSAot.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private TrayIcon _trayIcon = null!;

        private NativeMenuItem _trayShowItem = null!;
        private NativeMenuItem _trayOptionItem = null!;
        private NativeMenuItem _trayReindexItem = null!;
        private NativeMenuItem _trayAboutItem = null!;
        private NativeMenuItem _trayExitItem = null!;

        private void InitializeTrayIcon()
        {
            _trayIcon = new TrayIcon();

            var uri = new Uri(@"avares://TDS/Assets/tds32-32.ico");
            using var asset = AssetLoader.Open(uri);
            var icon = new WindowIcon(asset);
            _trayIcon.Icon = icon;
            this.Icon = icon;

            _trayIcon.ToolTipText = "TDS";

            var menu = new NativeMenu();
            var lang = LangManager.Instance.CurrentLang;

            _trayShowItem = new NativeMenuItem(lang.ShowWindow);
            _trayShowItem.Click += (_, _) => ShowWindow();

            _trayOptionItem = new NativeMenuItem(lang.Option);
            _trayOptionItem.Click += (_, _) => ShowDialog_Option();

            _trayReindexItem = new NativeMenuItem(lang.Reindex);
            _trayReindexItem.Click += (_, _) =>
            {
                cache.Discard();
                Reset();
            };

            _trayAboutItem = new NativeMenuItem(lang.About);
            _trayAboutItem.Click += (_, _) => Message.ShowWaringOk(AppInfomation.AboutTitle, AppInfomation.AboutInfo);

            _trayExitItem = new NativeMenuItem(lang.Exit);
            _trayExitItem.Click += (_, _) => Exit();

            menu.Items.Add(_trayShowItem);
            menu.Items.Add(_trayOptionItem);
            menu.Items.Add(_trayReindexItem);
            menu.Items.Add(_trayAboutItem);
            menu.Items.Add(_trayExitItem);

            _trayIcon.Menu = menu;

            _trayIcon.Clicked += (_, _) => ShowWindow();

            _trayIcon.IsVisible = true;
        }

        public void RefreshUILanguage()
        {
            var lang = LangManager.Instance.CurrentLang;
            _trayShowItem.Header = lang.ShowWindow;
            _trayOptionItem.Header = lang.Option;
            _trayReindexItem.Header = lang.Reindex;
            _trayAboutItem.Header = lang.About;
            _trayExitItem.Header = lang.Exit;
            inputBox.Watermark = lang.InputWaterMarkInput;
        }
    }
}
