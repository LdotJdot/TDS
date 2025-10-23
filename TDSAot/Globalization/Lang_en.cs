using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDS.Globalization
{

    public class Lang_en : ILanguage
    {
        public string Name { get; set; } = "en-us";
        public string ReadableName { get; set; } = "English";

        // Menu text
        public string OpenFile { get; set; } = "Open (Enter)";
        public string OpenFileWith { get; set; } = "Open with...(Shift+Enter)";
        public string OpenFolderh { get; set; } = "Open folder (Space)";
        public string Copy { get; set; } = "Copy (Ctrl+C)";
        public string CopyPath { get; set; } = "Copy path (Ctrl+P)";
        public string Delete { get; set; } = "Delete (Del)";
        public string Property { get; set; } = "Property... (Alt+Enter)";
        public string Option { get; set; } = "Option...";


        // TrayIcon
        public string ShowWindow { get; set; } = "Show window";
        public string Reindex { get; set; } = "Reindex";
        public string About { get; set; } = "About...";

        public string DisableStartup { get; set; } = "Disable run at Windows startup";
        public string EnableStartup { get; set; } = "Enable run at Windows startup";
        public string Exit { get; set; } = "Exit";

        //Settings
        public string DefaultResultCount { get; set; } = "Default Result Count:";
        public string DefaultResultCountTip { get; set; } = "Add /a keyword during search to force list all results";
        public string DefaultResultCountDesc { get; set; } = "Set the maximum number of result entries to display during search";
        public string HotkeySetting { get; set; } = "Hotkey Settings";
        public string HotkeySettingDesc { get; set; } = "Default keys: ESC to return to input box, Space to open directory, Enter to open current item";
        public string ActivateHotkeySetting { get; set; } = "Activation Hotkey:";
        public string ModifyHotkeySetting { get; set; } = "Modifier Key:";
        public string ModifyHotkeySettingDesc { get; set; } = "Set the hotkey combination to activate the application window";
        public string BehaviorSettings { get; set; } = "Behavior Settings";
        public string AutoHide { get; set; } = "Hide on startup";
        public string AutoHideDesc { get; set; } = "Automatically hide to the system tray when the app starts";
        public string AlwaysTop { get; set; } = "Always on top";
        public string AlwaysTopDesc { get; set; } = "Keep the window always on top of others";
        public string HideLostFocus { get; set; } = "Hide on focus loss";
        public string HideLostFocusDesc { get; set; } = "Automatically hide the window when it loses focus";
        public string AutoResize { get; set; } = "Auto-resize";
        public string AutoResizeDesc { get; set; } = "Auto-adjust the window size based on the number of results";
        public string DiskCache { get; set; } = "Enable disk cache";
        public string DiskCacheDesc { get; set; } = "Use disk cache to speed up application loading process";
        public string Theme { get; set; } = "Interface Theme:";
        public string ThemeDefault { get; set; } = "Default";
        public string ThemeDark { get; set; } = "Dark";
        public string ThemeLight { get; set; } = "Light";
        public string ThemeDesc { get; set; } = "Select the interface theme for the application";
        public string Cancel { get; set; } = "Cancel";
        public string Ok { get; set; } = "Ok";
        public string Error { get; set; } = "Error";
        public string Error_CachingFailed { get; set; } = "Caching failed";


        // message
        public string InputWaterMarkInput { get; set; } = "Please input keywords";
        public string InputWaterMarkPending { get; set; } = "Initialization pending...";
        public string Loading { get; set; } = "Loading...";
        public string Indexing { get; set; } = "Indexing ";
        public string Item { get; set; }= "item";
        public string Items { get; set; } = "items";




    }
}
