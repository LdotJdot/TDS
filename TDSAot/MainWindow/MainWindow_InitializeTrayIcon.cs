using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Win32;
using System;
using TDS;
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

            var showItem = new NativeMenuItem("Show window");
            showItem.Click += (s, e) => ShowWindow();

            var option = new NativeMenuItem("Options...");
            option.Click += (s, e) =>ShowDialog_Option();

            var reset = new NativeMenuItem("Reindex");
            reset.Click += (s, e) =>
            {
                cache.Discard();
                Reset();
            };

            var about = new NativeMenuItem("About...");
            about.Click += (s, e) => Message.ShowWaringOk(AppInfomation.AboutTitle, AppInfomation.AboutInfo);

            var autoStartItem = new NativeMenuItem(
                StartUpUtils.IsStartUp?
                "Disable run at Windows startup":
                "Enable run at Windows startup"
                );
            autoStartItem.Click += (s, e) => {
       
                StartUpUtils.SwitchStartUp();
                if (StartUpUtils.IsStartUp)
                {
                    autoStartItem.Header = "Disable run at Windows startup";
                }
                else
                {
                    autoStartItem.Header = "Enable run at Windows startup";
                }

            };

            var exitItem = new NativeMenuItem("Exit");
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

    }
}