using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using System;
using System.Security.Principal;
using System.Text;
using TDS.Utils;
using TDSAot.State;
using TDSAot.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        internal AppOption Option;

        public static void CheckRunningAsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                Utils.Message.ShowWaringOk("The program is about to exit", "Please run the program with administrator privileges.");
                Environment.Exit(-1);
            }
        }

        public MainWindow()
        {
            if (!ApplicationSingleton.Check())
            {
                Message.ShowWaringOk("Program already running", "Another instance of the program is already running. Check the right corner of your desktop.");
                Exit();
            }
            CheckRunningAsAdministrator();
            InitializeComponent();
            SetWindowSizeByScreenRatio(0.4, 0.5);
            InitializeTrayIcon();
            InitializeEnvironment();
            InitializeFileAction();
            InitializeEvent();
            // 初始化
        }

        double DefaultHeight;
        private void SetWindowSizeByScreenRatio(double widthRatio, double heightRatio)
        {

            var screen = Screens.Primary;
            if (screen != null)
            {
                var workingArea = screen.WorkingArea;
                Width = workingArea.Width * widthRatio;
                DefaultHeight = workingArea.Height * heightRatio;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                // 备用方案
                Width = 1024 * widthRatio;
                DefaultHeight = 768 * heightRatio;
            }
            AdjustWindowForTitleOnly();
        }

        private void InitializeEvent()
        {
            this.Deactivated += OnWindowDeactivated;
            // 可选：订阅窗口获得焦点事件
            this.Activated += OnWindowActivated;

            this.KeyDown += OnAppKeyDown;
            Loaded += MainWindow_Loaded;
        }

    

        private void InitializeEnvironment()
        {
            this.Title = "TDS";
            // 数据绑定注册
            DataContext = this;
            // 字符注册
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // UI事件冒泡,解决冲突
            inputBox.AddHandler(
             InputElement.PointerPressedEvent,
             Keywords_MouseDown!,
             RoutingStrategies.Bubble,
             true);

            // UI事件冒泡,解决冲突
            inputBox.AddHandler(
             InputElement.KeyDownEvent,
             Input_KeyDown!,
             RoutingStrategies.Bubble,
             true);

            // 句柄注册
            hwnd = GetNativeHandle().Handle;    //获取窗口句柄


            InitializeHotKeys(hwnd);
            fileListBox.Focusable = true;
            // 初始化配置文件

        }

        private IPlatformHandle GetNativeHandle()
        {
            var topLevel = TopLevel.GetTopLevel(this)!;

            // 通过窗口获取，方法更加简单：
            return topLevel.TryGetPlatformHandle()!;
        }

        private void Exit()
        {
            try
            {
                OnAppClosed(null,null);
                ApplicationSingleton.appMutex?.ReleaseMutex();
                ApplicationSingleton.appMutex?.Dispose();
            }
            catch
            {

            }
            _trayIcon?.Dispose();
            Environment.Exit(0);
        }



    }
}