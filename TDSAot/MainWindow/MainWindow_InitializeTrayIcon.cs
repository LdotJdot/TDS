using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Win32;
using System;
using TDS;
using TDS.Globalization;
using TDS.State;
using TDS.Utils;
using TDSAot.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private TrayIcon _trayIcon;

        private void InitializeTrayIcon()
        {
            // ����TrayIconʵ��
            _trayIcon = new TrayIcon();

            var uri = new Uri(@"avares://TDS/Assets/tds32-32.ico");
            using var asset = AssetLoader.Open(uri);
            // ����ͼ��
            var icon = new WindowIcon(asset);
            _trayIcon.Icon = icon;
            this.Icon = icon;

            // ������ʾ�ı�
            _trayIcon.ToolTipText = "TDS";

            // ���������Ĳ˵�
            var menu = new NativeMenu();

            var showItem = new NativeMenuItem(LangManager.Instance.CurrentLang.ShowWindow);
            showItem.Click += (s, e) => ShowWindow();

            var option = new NativeMenuItem(LangManager.Instance.CurrentLang.Option);
            option.Click += (s, e) =>ShowDialog_Option();

            var reset = new NativeMenuItem(LangManager.Instance.CurrentLang.Reindex);
            reset.Click += (s, e) =>
            {
                cache.Discard();
                Reset();
            };

            var about = new NativeMenuItem(LangManager.Instance.CurrentLang.About);
            about.Click += (s, e) => Message.ShowWaringOk(AppInfomation.AboutTitle, AppInfomation.AboutInfo);

            var autoStartItem = new NativeMenuItem(
                StartUpUtils.IsStartUp?
                LangManager.Instance.CurrentLang.DisableStartup:
                LangManager.Instance.CurrentLang.EnableStartup
                );
            autoStartItem.Click += (s, e) => {
       
                StartUpUtils.SwitchStartUp();
                if (StartUpUtils.IsStartUp)
                {
                    autoStartItem.Header = LangManager.Instance.CurrentLang.DisableStartup;
                }
                else
                {
                    autoStartItem.Header = LangManager.Instance.CurrentLang.EnableStartup;
                }

            };

            var exitItem = new NativeMenuItem(LangManager.Instance.CurrentLang.Exit);
            exitItem.Click += (s, e) => Exit();

            menu.Add(showItem);
            menu.Add(option);
            menu.Add(reset);
            menu.Add(about);
            menu.Add(autoStartItem);
            menu.Add(exitItem);

            _trayIcon.Menu = menu;
                      

            // �����¼�
            _trayIcon.Clicked += (s, e) =>
            {
               
                ShowWindow();
            };

            // ȷ��TrayIcon�ɼ�
            _trayIcon.IsVisible = true;
        }

        public void RefreshTrayIconMenu()
        {
            ((NativeMenuItem)_trayIcon.Menu.Items[0]).Header = LangManager.Instance.CurrentLang.ShowWindow;
            ((NativeMenuItem)_trayIcon.Menu.Items[1]).Header = LangManager.Instance.CurrentLang.Option;
            ((NativeMenuItem)_trayIcon.Menu.Items[2]).Header = LangManager.Instance.CurrentLang.Reindex;
            ((NativeMenuItem)_trayIcon.Menu.Items[3]).Header = LangManager.Instance.CurrentLang.About;
            ((NativeMenuItem)_trayIcon.Menu.Items[4]).Header = StartUpUtils.IsStartUp?
                LangManager.Instance.CurrentLang.DisableStartup:
                LangManager.Instance.CurrentLang.EnableStartup;
            ((NativeMenuItem)_trayIcon.Menu.Items[5]).Header = LangManager.Instance.CurrentLang.Exit;
        }

    }
}