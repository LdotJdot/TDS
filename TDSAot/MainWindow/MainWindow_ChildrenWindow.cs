using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using TDS;
using TDSAot.State;
using TDSAot.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        internal bool isOptionWinOpen = false;
        SettingWindow optionWindow;

        void ShowDialog_Option()
        {
            if (!isOptionWinOpen)
            {             
                optionWindow?.Close();
                optionWindow = new SettingWindow(this);

                optionWindow.Show(); // 这将使新窗口作为模态对话框打开
                isOptionWinOpen = true;
            }
            else
            {
                optionWindow.WindowState = WindowState.Normal;
                optionWindow.Topmost = true;
                optionWindow.Activate();
            }
        }
    }
}