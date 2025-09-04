
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TDSAot.Utils
{

    public class ClipboardUtils
    {
        private static IClipboard Get()
        {

            //Desktop
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                return window.Clipboard!;

            }
            //Android (and iOS?)
            else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
            {
                var visualRoot = mainView.GetVisualRoot();
                if (visualRoot is TopLevel topLevel)
                {
                    return topLevel.Clipboard!;
                }
            }

            return null!;
        }

        public static async Task SetText(string text)
        {
            var clipboard = Get();
            if (clipboard == null) return;
            await clipboard.SetTextAsync(text);
        }

        // 将文件列表设置到剪贴板
        public static async Task SetFileDropList(string[] filePaths, bool isCutOperation=false)
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
    desktop.MainWindow?.Clipboard is not { } clipboard || desktop.MainWindow?.StorageProvider is not { } storageProvider)
            {
                Message.ShowWaringOk("异常", "剪贴板访问失败");
                return;
            }

            var files = filePaths
                .Select(o =>  storageProvider.TryGetFileFromPathAsync(o).GetAwaiter().GetResult())
                .Where(o=>o!=null)
                ;
            DataObject dataObject = new();
            dataObject.Set(
                DataFormats.Files,
                files.ToArray());
            await clipboard.SetDataObjectAsync(dataObject);



        }
    }
}
