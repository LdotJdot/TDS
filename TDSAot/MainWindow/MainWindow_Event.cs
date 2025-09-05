using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using Avalonia.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        string? tmpInputStr;
        private void GoSearch()
        {
            if (string.IsNullOrWhiteSpace(tmpInputStr))
            {
                ChangeToRecord();
            }
            else
            {
                keyword = tmpInputStr;

                runningState.Threadrest = true;

                try
                {
                    runningState.gOs.Release();
                }
                catch
                {
                    Debug.WriteLine("no resource");
                }
            }
        }


        private async void TextChanged(object? sender, RoutedEventArgs e)
        {
            tmpInputStr = inputBox.Text;

            await Task.Run(GoSearch);
        }

        // 处理鼠标双击事件
        private void ListBox_MouseDoubleClick(object sender, TappedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
                fileAction.Execute([file], FileActionType.Open);
            }
        }

        // 处理键盘按键事件
        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        var file = Items.DisplayedData.FirstOrDefault();
                        if (file != null)
                        {
                            Execute([file], FileActionType.Open);
                        }
                        break;

                    case Key.Down:
                        if (fileListBox.ItemCount > 0)
                        {
                            fileListBox.SelectedIndex = 0;
                            Dispatcher.UIThread.Invoke(
                            () => fileListBox.Focus());
                        }
                        break;
                }
            }
        }

        private void ListBox_KeyDown(object sender, KeyEventArgs e)
        {         
            if (e.Key==Key.Delete)
            {
                Delete(null, null);
            }
            else if (e.Key==Key.P && e.KeyModifiers == KeyModifiers.Control)
            {
                CopyPath(null, null);
            }
            else if (e.Key==Key.C && e.KeyModifiers == KeyModifiers.Control)
            {
                Copy(null, null);
            }
            else if (e.Key == Key.Enter)
            {
                if (e.KeyModifiers == KeyModifiers.Alt)
                {
                    ShowProperty(null, null);
                }
                else if (e.KeyModifiers == KeyModifiers.Shift)
                {
                    OpenFileWith(null, null);
                }
                else
                {
                    Execute(GetSelectedItems(), FileActionType.Open);
                }

            }
            else if (e.Key == Key.Space)
            {
                Execute(GetSelectedItems(), FileActionType.OpenFolder);
            }           
        }

        List<FrnFileOrigin> _selectItemsList = new List<FrnFileOrigin>();

        private FrnFileOrigin[] GetSelectedItems()
        {
            _selectItemsList.Clear();
            foreach (var item in fileListBox.SelectedItems ?? new object[] { })
            {
                if (item is FrnFileOrigin frn)
                {
                    _selectItemsList.Add(frn);
                }
            }
            return _selectItemsList.ToArray();
        }
    }

}