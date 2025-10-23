using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using TDS.Globalization;
using TDSNET.Engine.Actions.USN;

namespace TDSAot.ViewModels
{
    // ViewModel
    public class DataViewModel : ReactiveObject
    {
        private IList<FrnFileOrigin> _allData = [];
        private IEnumerable<FrnFileOrigin> _displayedData = [];
        private int _displayCount = 100;
        private ILanguage _lang = LangManager.Instance.CurrentLang;

        public ILanguage Lang
        {
            get => _lang;
            private set => this.RaiseAndSetIfChanged(ref _lang, value);
        }

        public void SetLanguage(ILanguage lang)
        {
            Lang = lang;
        }


        public IEnumerable<FrnFileOrigin> DisplayedData
        {
            get => _displayedData;
            private set => this.RaiseAndSetIfChanged(ref _displayedData, value);
        }

        bool isShowOpenWith = true;
        public bool IsShowOpenWith
        {
            get => isShowOpenWith;
            set
            {
                 this.RaiseAndSetIfChanged(ref isShowOpenWith, value);
            }
        }

        public int DisplayCount
        {
            get => _displayCount;
            private set
            {
                _displayCount = value;

            }
        }

        public DataViewModel()
        {
        }

        public void Bind(IList<FrnFileOrigin> _allData)
        {
            if (this._allData != _allData)
            {
                // 生成测试数据（实际中可能从文件或数据库加载）
                this._allData = _allData;
                //UpdateDisplayedData();
            }
        }

        public void UpdateDisplayedData()
        {
            // 使用 LINQ 的 Take()，这是惰性求值的，性能很好
            DisplayedData = _allData.Take(DisplayCount);
        }

        // 快速切换到不同数量级
        public void SetDisplayCount(int count)
        {
            DisplayCount = count;
        }
    }
}