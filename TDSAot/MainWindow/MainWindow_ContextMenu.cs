using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using TDSAot.State;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private void ShowProperty(object sender, RoutedEventArgs e)
        {
            foreach (var file in GetSelectedItems())
            {
                StaticState.CanBeHide = false;
                this.Topmost = false;
                FilePropertiesOpener.ShowFileProperties(file.FilePath);
                this.Topmost = Option.AlwaysTop;
                StaticState.CanBeHide = true;
                UpdateRecord(file);
            }
        }

        private void OpenFile(object sender, RoutedEventArgs e)
        {
           Execute(GetSelectedItems(), FileActionType.Open);
        }

        private void OpenFileWith(object sender, RoutedEventArgs e)
        {
            foreach (var file in GetSelectedItems())
            {
                StaticState.CanBeHide = false;
                this.Topmost = false;
                FilePropertiesOpener.ShowFileOpenWith(file.FilePath);
                this.Topmost = Option.AlwaysTop;
                StaticState.CanBeHide = true;

                UpdateRecord(file);
            }
        }

        private void OpenFolder(object sender, RoutedEventArgs e)
        {
            Execute(GetSelectedItems(), FileActionType.OpenFolder);
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            StaticState.CanBeHide = false;
            this.Topmost = false;
            Execute(GetSelectedItems(), FileActionType.Delete);
            this.Topmost = Option.AlwaysTop;
            StaticState.CanBeHide = true;

            RefreshFileData();
        }

        private async void Copy(object sender, RoutedEventArgs e)
        {
            var files = GetSelectedItems();
            var filePathes = files.Select(o => o.FilePath).ToArray();
            await ClipboardUtils.SetFileDropList(filePathes);
            foreach (var file in files)
            {
                UpdateRecord(file);
            }
        }

        private async void CopyPath(object sender, RoutedEventArgs e)
        {
            var files = GetSelectedItems();

            if (files.Length > 0)
            {
                await ClipboardUtils.SetText(string.Join("\r\n",files.Select(o=>o.FilePath)));
                foreach (var file in files)
                {
                    UpdateRecord(file);
                }
            }

        }

        private void ListBoxPointerRelease(object sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Right)
            {
                var listBox = sender as ListBox;
                var point = e.GetPosition(listBox);

                // 使用 GetVisualsAt 获取指定位置的所有视觉元素
                var visualsAtPoint = listBox.GetVisualsAt(point);

                var currentSelected = GetSelectedItems();
                foreach (var visual in visualsAtPoint)
                {
                    // 查找 ListBoxItem
                    var listBoxItem = FindVisualParent<ListBoxItem>(visual);
                    if (listBoxItem != null && listBoxItem.DataContext != null)
                    {
                        if (!currentSelected.Contains(listBoxItem.DataContext))
                        {
                            listBox.SelectedItem = listBoxItem.DataContext;
                        }

                        break;
                    }
                }
            }
        }

        private static T FindVisualParent<T>(Visual visual) where T : Visual
        {
            var current = visual;
            while (current != null)
            {
                if (current is T result)
                {
                    return result;
                }
                current = current.GetVisualParent();
            }
            return default;
        }
    }
}