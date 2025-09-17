using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using TDSAot.ViewModels;
using TDSNET.Engine.Actions.USN;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        public MessageViewModel MessageData { get; } = new MessageViewModel();

        public DataViewModel Items { get; } = new DataViewModel();

        void UpdateData(IList<FrnFileOrigin> data, int count)
        {
            Items.Bind(data);
            Items.SetDisplayCount(count);

            if (Option?.AutoAdjust == true)
            {
                Dispatcher.UIThread.InvokeAsync(()=>AdjustWindowForSize(count));
            }
        }
    }
}