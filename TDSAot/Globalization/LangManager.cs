using ConfigurationReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TDSAot.State;
using TDSAot.Utils;

namespace TDS.Globalization
{

    public class LangManager
    {      
        static LangManager instance=new LangManager();
        const string langFolder = "Lang";

        Dictionary<string, ILanguage> langCache = new Dictionary<string, ILanguage>();

        public LangManager()
        {
            var cn = new Lang_cn();
            var en = new Lang_en();
            langCache.Add(cn.ReadableName, cn);
            langCache.Add(en.ReadableName, en);
        }

        public IEnumerable<ILanguage> GetAvailableLangs()
        {
            return langCache.Values;
        }

        public static LangManager Instance=>instance;

        public ILanguage SetLang(string lang)
        {

            if(langCache.TryGetValue(lang,out var res))
            {
                CurrentLang = res;
            }
            else
            {
                CurrentLang = new Lang_en();
            }
            return CurrentLang;

        }
        public ILanguage GetLanguage(string lang)
        {
            if (langCache.TryGetValue(lang, out var res))
            {
                return res;
            }
            else
            {
                return new Lang_en();
            }
        }

        ILanguage? GetLang(string path)
        {
            try
            {
                var lang = new Lang_en();

                if (File.Exists(path))
                {
                    var conf = new Configuration(path);
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;
                    lang.AutoResizeDesc = conf.GetString("AutoResizeDesc") ?? lang.AutoResizeDesc;

                    return lang;
                }
                else
                {
                    throw new FileNotFoundException($"Language file not found.{path}");
                }
            }
            catch (Exception ex)
            {
                Message.ShowWaringOk("Error", $"Failed to load language file: {ex.Message}");
                return null;
            }
        }
        public ILanguage CurrentLang { get; private set; } = new Lang_en();

    }
}
