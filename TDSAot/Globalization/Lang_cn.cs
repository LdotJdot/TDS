using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDS.Globalization
{

    public class Lang_cn : ILanguage
    {
        public string Name { get; set; } = "zh-hans";
        public string ReadableName { get; set; } = "中文";

        // Menu text
        public string OpenFile { get; set; } = "打开 (Enter)";
        public string OpenFileWith { get; set; } = "打开方式...(Shift+Enter)";
        public string OpenFolderh { get; set; } = "打开文件夹 (Space)";
        public string Copy { get; set; } = "复制 (Ctrl+C)";
        public string CopyPath { get; set; } = "复制路径 (Ctrl+P)";
        public string Delete { get; set; } = "删除 (Del)";
        public string Property { get; set; } = "属性... (Alt+Enter)";
        public string Option { get; set; } = "选项...";


        // TrayIcon
        public string ShowWindow { get; set; } = "显示窗口";
        public string Reindex { get; set; } = "重建索引";
        public string About { get; set; } = "关于...";

        public string Exit { get; set; } = "退出";

        //Settings        
        public string DefaultResultCount { get; set; } = "默认显示结果数量:";
        public string DefaultResultCountTip { get; set; } = "增加 /a 关键字以强制显示所有结果";
        public string DefaultResultCountDesc { get; set; } = "设定搜索时显示的最大结果数量，1-999";
        public string HotkeySetting { get; set; } = "快捷键设定";
        public string HotkeySettingDesc { get; set; } = "默认ESC返回输入框，再按ESC清楚输入框，选择条目回车打开，空格打开目录";
        public string ActivateHotkeySetting { get; set; } = "显示窗口快捷键:";
        public string ModifyHotkeySetting { get; set; } = "修饰键:";
        public string ModifyHotkeySettingDesc { get; set; } = "设定快捷键组合以显示窗口";
        public string ScreenshotSetting { get; set; } = "截屏功能";
        public string ScreenshotEnabled { get; set; } = "启用区域截屏";
        public string ScreenshotEnabledDesc { get; set; } = "开启后可用独立快捷键唤起截屏；确认复制到剪贴板，保存弹出对话框；截屏中再次按快捷键自动保存到下方目录";
        public string ScreenshotHotkeySetting { get; set; } = "截屏快捷键:";
        public string ScreenshotModifierSetting { get; set; } = "截屏修饰键:";
        public string ScreenshotHotkeyDesc { get; set; } = "默认 Alt+Shift+A，与主窗口 Ctrl+~ 互不冲突";
        public string ScreenshotSavePath { get; set; } = "自动保存路径:";
        public string ScreenshotSavePathDesc { get; set; } = "截屏编辑中再次按下截屏快捷键时自动保存 PNG；目录不存在时会自动创建（默认为程序运行目录）";
        public string ScreenshotSaveFailed { get; set; } = "截屏保存失败";
        public string ScreenshotSaveNoSelection { get; set; } = "请先框选截屏区域后再保存。";
        public string InterfaceSetting { get; set; } = "界面设定";
        public string LanguageSetting { get; set; } = "界面语言:";
        public string LanguageSettingDesc { get; set; } = "选择应用界面显示语言";
        public string BehaviorSettings { get; set; } = "行为设定";
        public string AutoHide { get; set; } = "启动后自动隐藏";
        public string AutoHideDesc { get; set; } = "在程序启动后自动隐藏缩小到系统托盘";
        public string AlwaysTop { get; set; } = "窗口置顶";
        public string AlwaysTopDesc { get; set; } = "保持窗口永远置顶";
        public string HideLostFocus { get; set; } = "失去焦点自动隐藏";
        public string HideLostFocusDesc { get; set; } = "当窗口失去焦点时自动隐藏回到系统托盘";
        public string AutoResize { get; set; } = "自动调整窗口大小";
        public string AutoResizeDesc { get; set; } = "根据结果数量自动调整窗口大小";
        public string DiskCache { get; set; } = "开启磁盘缓存";
        public string DiskCacheDesc { get; set; } = "开启磁盘缓存加速启动时索引，如出现异常请关闭缓存或重建索引";
        public string Theme { get; set; } = "界面主题:";
        public string ThemeDefault { get; set; } = "随系统默认";
        public string ThemeDark { get; set; } = "深色";
        public string ThemeLight { get; set; } = "浅色";
        public string ThemeDesc { get; set; } = "选择应用的界面主题风格";
        public string Cancel { get; set; } = "取消";
        public string Ok { get; set; } = "确定";
        public string Error { get; set; } = "错误";
        public string Error_CachingFailed { get; set; } = "缓存失败";


        // message
        public string InputWaterMarkInput { get; set; } = "输入关键词";
        public string InputWaterMarkPending { get; set; } = "等待初始化...";
        public string Loading { get; set; } = "加载中...";
        public string Indexing { get; set; } = "索引中 ";
        public string Item { get; set; } = "条结果";
        public string Items { get; set; } = "条结果";
    }
}
