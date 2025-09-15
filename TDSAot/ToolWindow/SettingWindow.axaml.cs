using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;
using TDSAot;
using TDSAot.State;
using TDSAot.Utils;

namespace TDS;

public partial class SettingWindow : Window
{
    MainWindow mainWindow;
    public SettingWindow(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
        InitializeComponent();
        this.Topmost = true;

        DataContext = LoadOption();
        this.Closed += SettingWindow_Closed;

    }

    private void SettingWindow_Closed(object? sender, System.EventArgs e)
    {
        mainWindow.isOptionWinOpen = false;
    }


    internal SettingsViewModel LoadOption()
    {
        var svm              = new SettingsViewModel(this);
        svm.Findmax          = mainWindow.Option.Findmax.ToString();
        svm.HotKey           = KeyTransfer.ReverseTransKey(mainWindow.Option.HotKey);
        svm.ModifierKey      = KeyTransfer.ReverseTransKey(mainWindow.Option.ModifierKey);
        svm.UsingCache       = mainWindow.Option.UsingCache;
        svm.HideAfterStarted = mainWindow.Option.HideAfterStarted;
        svm.Theme            = mainWindow.Option.Theme.ToString();
        svm.AutoHide         = mainWindow.Option.AutoHide;
        svm.AutoAdjust       = mainWindow.Option.AutoAdjust;
        svm.AlwaysTop        = mainWindow.Option.AlwaysTop;
        return svm;
    }

    internal void SaveAndExit()
    {
        var svm=(SettingsViewModel)DataContext!;
        if (int.TryParse(svm.Findmax, out var value) && value>0 && value<1000)
        {
            mainWindow.Option.Findmax = value;
        }
        else
        {
            mainWindow.Option.Findmax = 100;
        }
        mainWindow.Option.HotKey           = KeyTransfer.TransKey(svm.HotKey);
        mainWindow.Option.ModifierKey      = KeyTransfer.TransKey(svm.ModifierKey);
        mainWindow.Option.UsingCache       = svm.UsingCache;
        mainWindow.Option.HideAfterStarted = svm.HideAfterStarted;
        mainWindow.Option.AutoHide         = svm.AutoHide;
        mainWindow.Option.AutoAdjust       = svm.AutoAdjust;
        mainWindow.Option.AlwaysTop        = svm.AlwaysTop;
               
        mainWindow.Option.Theme            = Enum.TryParse<ThemeType>(svm.Theme, true, out var theme)?theme: ThemeType.Default;

        mainWindow.Option.Save();
        mainWindow.RegisterHotKeys();
        mainWindow.Topmost = mainWindow.Option.AlwaysTop;
        this.Exit();
    }
    internal void Exit()
    {
        this.Close();
    }
}

