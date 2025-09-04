using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private void ShowProperty(object sender, RoutedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
                FilePropertiesOpener.ShowFileProperties(file.FilePath);
                UpdateRecord(file);
            }
        }

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
                Execute(file, FileActionType.Open);
            }
        }

        private void OpenFileWith(object sender, RoutedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
                FilePropertiesOpener.ShowFileOpenWith(file.FilePath);
                UpdateRecord(file);
            }
        }

        private void OpenFolder(object sender, RoutedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
                Execute(file, FileActionType.OpenFolder);
            }
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
                Execute(file, FileActionType.Delete);
                RefreshFileData();
            }
        }

        private async void Copy(object sender, RoutedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
               await ClipboardUtils.SetFileDropList([file.FilePath]);
               UpdateRecord(file);
            }
        }

        private async void CopyPath(object sender, RoutedEventArgs e)
        {
            if (fileListBox.SelectedItem is FrnFileOrigin file)
            {
                await ClipboardUtils.SetText(file.FilePath);
                UpdateRecord(file);
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

                foreach (var visual in visualsAtPoint)
                {
                    // 查找 ListBoxItem
                    var listBoxItem = FindVisualParent<ListBoxItem>(visual);
                    if (listBoxItem != null && listBoxItem.DataContext != null)
                    {
                        listBox.SelectedItem = listBoxItem.DataContext;
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