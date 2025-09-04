using ConfigurationReader;
using System.IO;

namespace TDSAot.State
{
    internal class AppOption
    {
        private string path;
        private Configuration? configuration;

        public AppOption()
        {
            this.path = System.IO.Directory.GetCurrentDirectory() + "\\conf.json";
            Reload(this.path);
        }

        public void Reload(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    configuration = new Configuration(path);
                    var findMax = configuration.GetInt(nameof(Findmax));
                    if (findMax == null)
                    {
                        configuration.Set(nameof(Findmax), 100);
                        configuration.Save();
                    }
                    else Findmax = findMax.Value;
                    

                    var hotKey = configuration.GetInt(nameof(HotKey));
                    if (hotKey == null)
                    {
                        configuration.Set(nameof(HotKey), 192);
                        configuration.Save();
                    }
                    else HotKey = (uint)hotKey.Value;

                    var modifierKey = configuration.GetInt(nameof(ModifierKey));
                    if (modifierKey == null)
                    {
                        configuration.Set(nameof(ModifierKey), 2);
                        configuration.Save();
                    }
                    else ModifierKey = (uint)modifierKey.Value;

                    return;
                }
                catch
                {
                    File.Delete(path);
                }
            }
            InitializeOption();
        }

        public void InitializeOption()
        {
            configuration = new Configuration();
            configuration.Set("Findmax", 100);
            configuration.Set("HotKey", 192);
            configuration.Set("ModifierKey", 2);
            configuration.Save(path);
        }

        internal int Findmax { get; private set; }  //最大显示数量
        internal uint HotKey { get; private set; }  //最大显示数量
        internal uint ModifierKey { get; private set; }  //最大显示数量
    }
}