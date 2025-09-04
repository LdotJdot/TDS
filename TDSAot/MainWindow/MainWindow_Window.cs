using Avalonia.Controls;
using Avalonia.Input;
using System;
using TDSAot.State;
using TDSAot.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private nint hwnd;

        private void Keywords_MouseDown(object sender, PointerPressedEventArgs e)
        {
            this.BeginMoveDrag(e);
        }

        public void AutoShowOrHide()
        {
            if (this.IsActive && this.IsVisible == true)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        public void HideWindow()
        {
            if (StaticState.CanBeHide)
            {
                this.WindowState = WindowState.Minimized;
                this.IsVisible = false;
            }
        }

        public void ShowWindow()
        {
            if (!this.IsVisible)
            {
                this.WindowState = WindowState.Normal;
                this.IsVisible = true;
                this.Activate();
            }
            WindowUtils.ForceForegroundWindow(this.hwnd);
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            lastFocused = FocusManager?.GetFocusedElement();
            HideWindow();
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            if (initialFinished)
            {
                StaticState.CanBeHide = true;
                RefreshFileData();
            }
        }
    }
}