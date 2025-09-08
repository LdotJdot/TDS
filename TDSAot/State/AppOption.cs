using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using ConfigurationReader;
using System.IO;

namespace TDSAot.State
{
    internal class AppOption
    {
        internal static readonly string CurrentFolder = Directory.GetCurrentDirectory();
        internal static readonly string CurrentRecordPath = CurrentFolder + "\\Record.cah";
        internal static readonly string CurrentOptionPath = CurrentFolder + "\\conf.json";
        internal static readonly string CurrentCachePath = CurrentFolder + "\\cache.data";

        private Configuration? configuration;
        private int findmax = 100;
        private uint hotKey = 192;
        private uint modifierKey = 2;
        private bool hideAfterStarted = false;
        private bool usingCache = true;
        private ThemeType theme;

        public AppOption()
        {
            Reload(CurrentOptionPath);
        }

        public void Reload(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    configuration = new Configuration(path);
                    var findMax = configuration.GetInt(nameof(Findmax));
                    if (findMax == null && findMax>0 && findmax<=1000)
                    {
                        findMax = 100;
                        configuration.Set(nameof(Findmax), findMax);
                        configuration.Save();
                    }
                    else Findmax = findMax.Value;


                    var hotKey = configuration.GetInt(nameof(HotKey));
                    if (hotKey == null)
                    {
                        hotKey = 192;
                        configuration.Set(nameof(HotKey), hotKey);
                        configuration.Save();
                    }
                    else HotKey = (uint)hotKey.Value;

                    var modifierKey = configuration.GetInt(nameof(ModifierKey));
                    if (modifierKey == null)
                    {
                        modifierKey = 2;
                        configuration.Set(nameof(ModifierKey), modifierKey);
                        configuration.Save();
                    }
                    else ModifierKey = (uint)modifierKey.Value;

                    var startHide = configuration.GetBool(nameof(HideAfterStarted));
                    if (startHide == null)
                    {
                        startHide = false;
                        configuration.Set(nameof(HideAfterStarted), startHide);
                        configuration.Save();
                    }
                    else HideAfterStarted = (bool)startHide.Value;


                    var useCache = configuration.GetBool(nameof(UsingCache));
                    if (useCache == null)
                    {
                        useCache = true;
                        configuration.Set(nameof(UsingCache), useCache);
                        configuration.Save();
                    }
                    else UsingCache = (bool)useCache.Value;

                    var theme = configuration.GetInt(nameof(Theme));
                    if (theme == null)
                    {
                        theme = (int)ThemeType.Default;
                        configuration.Set(nameof(Theme), theme);
                        configuration.Save();
                    }
                    else Theme = (ThemeType)theme.Value;

                    return;
                }
                catch
                {
                    File.Delete(path);
                }
            }
            InitializeOption();
        }

        public void Save()
        {
            if (File.Exists(CurrentOptionPath)) File.Delete(CurrentOptionPath);
            configuration?.Save(CurrentOptionPath);
        }

        public void InitializeOption()
        {
            configuration = new Configuration();
            Findmax = 100;
            HotKey = 192;
            ModifierKey = 2;
            HideAfterStarted = false;
            UsingCache = true;
            Theme = ThemeType.Default;
            configuration.Save(CurrentOptionPath);
        }


        internal int Findmax { get => findmax; set { findmax = value; configuration?.Set(nameof(Findmax), findmax); } }
        internal uint HotKey { get => hotKey; set { hotKey = value; configuration?.Set(nameof(HotKey), hotKey); } }
        internal uint ModifierKey { get => modifierKey; set { modifierKey = value; configuration?.Set(nameof(ModifierKey), modifierKey); } }
        internal bool HideAfterStarted { get => hideAfterStarted; set { hideAfterStarted = value; configuration?.Set(nameof(HideAfterStarted), hideAfterStarted); } }
        internal bool UsingCache { get => usingCache; set { usingCache = value; configuration?.Set(nameof(UsingCache), usingCache); } }
        internal ThemeType Theme
        {
            get => theme; set
            {
                theme = value; configuration?.Set(nameof(Theme), (int)theme);
                if (Avalonia.Application.Current != null)
                {
                    Avalonia.Application.Current.RequestedThemeVariant = GetTheme(theme);
                }
            }
        }

        static ThemeVariant GetTheme(ThemeType theme)
        {
            return theme switch
            {
                ThemeType.Light => ThemeVariant.Light,
                ThemeType.Dark => ThemeVariant.Dark,
                ThemeType.Default => ThemeVariant.Default,
                _ => ThemeVariant.Default,
            };
        }
    }
}