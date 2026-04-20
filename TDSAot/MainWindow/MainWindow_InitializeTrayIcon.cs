using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using TDS.Globalization;
using TDS.PeekDesktop;
using TDS.State;
using TDS.Utils;
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
        private NativeMenuItem _trayPeekRoot = null!;
        private NativeMenuItem _trayStartupItem = null!;
        private NativeMenuItem _trayExitItem = null!;

        private NativeMenuItem _peekEnabledItem = null!;
        private NativeMenuItem _peekDoubleClickItem = null!;
        private NativeMenuItem _peekTaskbarItem = null!;
        private NativeMenuItem _peekGameGuardItem = null!;
        private NativeMenuItem _peekModeNativeItem = null!;
        private NativeMenuItem _peekModeFlyItem = null!;
        private NativeMenuItem _peekModeMinItem = null!;

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

            var peekMenu = new NativeMenu();

            _peekEnabledItem = new NativeMenuItem(lang.PeekEnabled)
            {
                ToggleType = NativeMenuItemToggleType.CheckBox,
                IsChecked = true
            };
            _peekEnabledItem.Click += (_, _) =>
            {
                PeekDesktopHost.ToggleEnabled();
                SchedulePeekTrayRefresh();
            };

            _peekDoubleClickItem = new NativeMenuItem(lang.PeekDoubleClick)
            {
                ToggleType = NativeMenuItemToggleType.CheckBox,
                IsChecked = false
            };
            _peekDoubleClickItem.Click += (_, _) =>
            {
                PeekDesktopHost.ToggleRequireDoubleClick();
                SchedulePeekTrayRefresh();
            };

            _peekTaskbarItem = new NativeMenuItem(lang.PeekTaskbarClick)
            {
                ToggleType = NativeMenuItemToggleType.CheckBox,
                IsChecked = false
            };
            _peekTaskbarItem.Click += (_, _) =>
            {
                PeekDesktopHost.TogglePeekOnTaskbarClick();
                SchedulePeekTrayRefresh();
            };

            _peekGameGuardItem = new NativeMenuItem(lang.PeekGameGuard)
            {
                ToggleType = NativeMenuItemToggleType.CheckBox,
                IsChecked = true
            };
            _peekGameGuardItem.Click += (_, _) =>
            {
                PeekDesktopHost.TogglePauseWhileFullscreen();
                SchedulePeekTrayRefresh();
            };

            _peekModeNativeItem = new NativeMenuItem(lang.PeekModeNative)
            {
                ToggleType = NativeMenuItemToggleType.Radio,
                IsChecked = true
            };
            _peekModeNativeItem.Click += (_, _) =>
            {
                PeekDesktopHost.SetPeekMode(PeekMode.NativeShowDesktop);
                SchedulePeekTrayRefresh();
            };

            _peekModeFlyItem = new NativeMenuItem(lang.PeekModeFlyAway)
            {
                ToggleType = NativeMenuItemToggleType.Radio,
                IsChecked = false
            };
            _peekModeFlyItem.Click += (_, _) =>
            {
                PeekDesktopHost.SetPeekMode(PeekMode.FlyAway);
                SchedulePeekTrayRefresh();
            };

            _peekModeMinItem = new NativeMenuItem(lang.PeekModeMinimize)
            {
                ToggleType = NativeMenuItemToggleType.Radio,
                IsChecked = false
            };
            _peekModeMinItem.Click += (_, _) =>
            {
                PeekDesktopHost.SetPeekMode(PeekMode.Minimize);
                SchedulePeekTrayRefresh();
            };

            peekMenu.Items.Add(_peekEnabledItem);
            peekMenu.Items.Add(_peekDoubleClickItem);
            peekMenu.Items.Add(_peekTaskbarItem);
            peekMenu.Items.Add(_peekGameGuardItem);
            peekMenu.Items.Add(new NativeMenuItemSeparator());
            peekMenu.Items.Add(_peekModeNativeItem);
            peekMenu.Items.Add(_peekModeFlyItem);
            peekMenu.Items.Add(_peekModeMinItem);

            _trayPeekRoot = new NativeMenuItem(lang.TrayPeekDesktop) { Menu = peekMenu };

            _trayStartupItem = new NativeMenuItem(
                StartUpUtils.IsStartUp ? lang.DisableStartup : lang.EnableStartup);
            _trayStartupItem.Click += (_, _) =>
            {
                StartUpUtils.SwitchStartUp();
                var L = LangManager.Instance.CurrentLang;
                _trayStartupItem.Header = StartUpUtils.IsStartUp ? L.DisableStartup : L.EnableStartup;
            };

            _trayExitItem = new NativeMenuItem(lang.Exit);
            _trayExitItem.Click += (_, _) => Exit();

            menu.Items.Add(_trayShowItem);
            menu.Items.Add(_trayOptionItem);
            menu.Items.Add(_trayReindexItem);
            menu.Items.Add(_trayAboutItem);
            menu.Items.Add(_trayPeekRoot);
            menu.Items.Add(_trayStartupItem);
            menu.Items.Add(_trayExitItem);

            _trayIcon.Menu = menu;

            _trayIcon.Clicked += (_, _) => ShowWindow();

            _trayIcon.IsVisible = true;
        }

        /// <summary>
        /// Delayed initialization of PeekDesktop after UI is shown.
        /// </summary>
        public void InitializePeekDesktopAfterUiShown()
        {
            if (!OperatingSystem.IsWindows())
                return;

            // Start PeekDesktop on background thread to avoid blocking UI
            Task.Run(() =>
            {
                PeekDesktopHost.EnsureStartedIfEnabledInSettings();
                Dispatcher.UIThread.InvokeAsync(() => RefreshPeekTrayChecks());
            });
        }

        private void SchedulePeekTrayRefresh()
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(120);
                RefreshPeekTrayChecks();
            });
        }

        private void RefreshPeekTrayChecks()
        {
            if (!OperatingSystem.IsWindows())
                return;

            var s = PeekDesktopHost.SnapshotSettingsForMenu();
            _peekEnabledItem.IsChecked = s.Enabled;
            _peekDoubleClickItem.IsChecked = s.RequireDoubleClick;
            _peekTaskbarItem.IsChecked = s.PeekOnTaskbarClick;
            _peekGameGuardItem.IsChecked = s.PauseWhileFullscreenAppActive;
            _peekModeNativeItem.IsChecked = s.PeekMode == PeekMode.NativeShowDesktop;
            _peekModeFlyItem.IsChecked = s.PeekMode == PeekMode.FlyAway;
            _peekModeMinItem.IsChecked = s.PeekMode == PeekMode.Minimize;
        }

        public void RefreshUILanguage()
        {
            var lang = LangManager.Instance.CurrentLang;
            _trayShowItem.Header = lang.ShowWindow;
            _trayOptionItem.Header = lang.Option;
            _trayReindexItem.Header = lang.Reindex;
            _trayAboutItem.Header = lang.About;
            _trayPeekRoot.Header = lang.TrayPeekDesktop;
            _peekEnabledItem.Header = lang.PeekEnabled;
            _peekDoubleClickItem.Header = lang.PeekDoubleClick;
            _peekTaskbarItem.Header = lang.PeekTaskbarClick;
            _peekGameGuardItem.Header = lang.PeekGameGuard;
            _peekModeNativeItem.Header = lang.PeekModeNative;
            _peekModeFlyItem.Header = lang.PeekModeFlyAway;
            _peekModeMinItem.Header = lang.PeekModeMinimize;
            _trayStartupItem.Header = StartUpUtils.IsStartUp ? lang.DisableStartup : lang.EnableStartup;
            _trayExitItem.Header = lang.Exit;
            inputBox.Watermark = lang.InputWaterMarkInput;
            RefreshPeekTrayChecks();
        }
    }
}
