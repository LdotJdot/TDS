using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System.Diagnostics;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private void OnAppKeyDown(object? sender, KeyEventArgs e)
        {
            // 处理按键按下事件

            var key = e.Key;
            var modifiers = e.KeyModifiers;

            if (key == Key.Escape)
            {
                ESPPress();
            }
            else if (modifiers == KeyModifiers.Control && ((int)key > 34 && (int)key < 43))
            {
                var index = (int)key - 34;
                if (index <= fileListBox.ItemCount && fileListBox.Items[index - 1] is FrnFileOrigin frn)
                {
                    fileListBox.SelectedIndex = index - 1;
                    Execute([frn], FileActionType.Open);
                }
            }
        }

        private void ESPPress()
        {
            fileListBox.SelectedItems = null;
           
            if (inputBox.Text?.Length > 0)
            {
                if (inputBox.SelectedText.Length != inputBox.Text.Length)
                {
                    inputBox.SelectAll();   // shor characters not render selectionBrush.
                }
                else
                {
                    //istView1.SelectedIndices.Clear();
                    inputBox.Clear();
                }
            }
            inputBox.Focus();
        }
    }
}