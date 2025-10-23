using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using DynamicData;
using System;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;
using TDS.Globalization;
using TDSAot;
using TDSAot.State;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;

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
        this.KeyDown += OnAppKeyDown;

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
        svm.Themes = [LangManager.Instance.CurrentLang.ThemeDefault,LangManager.Instance.CurrentLang.ThemeLight, LangManager.Instance.CurrentLang.ThemeDark];
        svm.Theme            = GetTheme(mainWindow.Option.Theme);
        svm.AutoHide         = mainWindow.Option.AutoHide;
        svm.AutoAdjust       = mainWindow.Option.AutoAdjust;
        svm.AlwaysTop        = mainWindow.Option.AlwaysTop;
        
        svm.Lang = LangManager.Instance.CurrentLang;
        svm.LangStrs = LangManager.Instance.GetAvailableLangs().Select(o => o.ReadableName);
        svm.LangStr = svm.Lang.ReadableName;
        return svm;
    }

    

    internal void SaveAndExit()
    {
        var svm = (SettingsViewModel)DataContext!;
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
        ChangeLang(svm);
        ChangeTheme(svm);
        mainWindow.Option.Save();
        mainWindow.RegisterHotKeys();
        mainWindow.Topmost = mainWindow.Option.AlwaysTop;
        this.Exit();
    }

    internal void ChangeLang(SettingsViewModel svm)
    {
        mainWindow.Option.Lang = svm.Lang.ReadableName;
        mainWindow.Items.SetLanguage(svm.Lang);
        mainWindow.RefreshTrayIconMenu();

    }
    internal void ChangeTheme(SettingsViewModel svm)
    {
        if(svm.Themes!=null)  mainWindow.Option.Theme = ToTheme(svm.Themes.IndexOf(svm.Theme));
    }

    ThemeType ToTheme(int theme)
    {
        return theme switch
        {
            0 => ThemeType.Default,
            1 => ThemeType.Light,
            _ => ThemeType.Dark,
        };
    }
    string GetTheme(ThemeType theme)
    {
        return theme switch
        {
            ThemeType.Default => LangManager.Instance.CurrentLang.ThemeDefault,
            ThemeType.Light => LangManager.Instance.CurrentLang.ThemeLight,
            _ => LangManager.Instance.CurrentLang.ThemeDark,
        };
    }


    internal void Exit()
    {
        this.Close();
    }

    private void OnAppKeyDown(object? sender, KeyEventArgs e)
    {
        // 处理按键按下事件

        var key = e.Key;
        
        if (key == Key.Escape)
        {
            Exit();
        }
    }
}

