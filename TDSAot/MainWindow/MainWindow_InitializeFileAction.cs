using Avalonia.Controls;
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

        private void Execute(FrnFileOrigin file, FileActionType action)
        {
            fileAction.Execute(file, action);
        }
    }
}