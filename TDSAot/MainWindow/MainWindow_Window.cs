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
                this.IsVisible = false;
               // this.WindowState = WindowState.Minimized;
            }
        }

        public void ShowWindow()
        {
            if (!this.IsVisible)
            {
                this.IsVisible = true;
                this.Activate();
                lastFocused = inputBox;
                inputBox.Focus();
                inputBox.SelectAll();
            }
            //this.WindowState = WindowState.Normal;
            WindowUtils.ForceForegroundWindow(this.hwnd);
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            lastFocused = FocusManager?.GetFocusedElement();
            if (Option!=null && Option.AutoHide)
            {
                HideWindow();
            }
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            if (initialFinished)
            {
                StaticState.CanBeHide = true;
                RefreshFileData();
            }
        }

        private void AdjustWindowForSize(int count)
        {
            this.Height = Math.Min(
                        inputBox.Margin.Top +      //top margin
                        inputBox.Height +           //textHeight
                        inputBox.Margin.Bottom +      //bottom margin
                        59 * count    // listItem desiredSize
                    , DefaultHeight);
        }
        private void AdjustWindowForTitleOnly()
        {
            this.Height =
                        inputBox.Margin.Top +      //top margin
                        inputBox.Height +           //textHeight
                        inputBox.Margin.Bottom;      //bottom margin
        }
        private void AdjustToDefault()
        {
            this.Height = DefaultHeight;

        }

        private void SetWindowSizeByScreenRatio(double widthRatio, double heightRatio)
        {
            this.WindowState = WindowState.Normal;
            var screen = Screens.Primary;
            if (screen != null)
            {
                this.IsVisible = false;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                var workingArea = screen.WorkingArea;
                Width = workingArea.Width * widthRatio;
                DefaultHeight = workingArea.Height * heightRatio;
            }
            else
            {
                // 备用方案
                Width = 1024 * widthRatio;
                DefaultHeight = 768 * heightRatio;
            }
            AdjustWindowForTitleOnly();
        }
    }
}