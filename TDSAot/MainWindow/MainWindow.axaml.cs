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
#if !DEBUG
            if (!ApplicationSingleton.Check())
            {
                Message.ShowWaringOk("Program already running", "Another instance of the program is already running. Check the right corner of your desktop.");
                Exit();
            }
#endif
            CheckRunningAsAdministrator();
            InitializeComponent();
            InitializeTrayIcon();
            InitializeEnvironment();
            InitializeFileAction();
            InitializeEvent();
            // ?????
        }

        double DefaultHeight;
       

        private void InitializeEvent()
        {
            this.Deactivated += OnWindowDeactivated;
            // ????????????????????
            this.Activated += OnWindowActivated;

            this.KeyDown += OnAppKeyDown;
            Loaded += MainWindow_Loaded;
        }

    

        private void InitializeEnvironment()
        {
            this.Title = "TDS";
            // ????????
            DataContext = this;
            // ??????
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // UI??????,??????
            inputBox.AddHandler(
             InputElement.PointerPressedEvent,
             Keywords_MouseDown!,
             RoutingStrategies.Bubble,
             true);

            // UI??????,??????
            inputBox.AddHandler(
             InputElement.KeyDownEvent,
             Input_KeyDown!,
             RoutingStrategies.Bubble,
             true);

            // ??????
            hwnd = GetNativeHandle().Handle;    //?????????


            InitializeHotKeys(hwnd);
            fileListBox.Focusable = true;
            // ????????????

        }

        private IPlatformHandle GetNativeHandle()
        {
            var topLevel = TopLevel.GetTopLevel(this)!;

            // ?????????????????????
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