using Avalonia.Controls;
using System.Collections.Generic;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private FileAction fileAction;

        private void InitializeFileAction()
        {
            fileAction = new FileAction(UpdateRecord);
        }

        private void Execute(FrnFileOrigin[] file, FileActionType action)
        {
            if (file == null || file.Length == 0) { return; }

            fileAction.Execute(file, action);
        }
    }
}