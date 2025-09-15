using Avalonia;
using ReactiveUI;
using System.ComponentModel.DataAnnotations;
using System.Reactive;

namespace TDS
{
    public class SettingsViewModel : ReactiveObject
    {
        SettingWindow settingWindow;
        public SettingsViewModel(SettingWindow window)
        {
            this.settingWindow = window;
            // 初始化命令
            SaveCommand = ReactiveCommand.Create(SaveSettings);
            CancelCommand = ReactiveCommand.Create(Cancel);

        }

        string findmax  = "100";

        [FindmaxValidation]
        public string Findmax
        {
            get => findmax;
            set => this.RaiseAndSetIfChanged(ref findmax, value);
        }

        string hotKey="~";
        public string HotKey
        {
            get => hotKey;
            set
            {
                hotKey = value;
                this.RaiseAndSetIfChanged(ref hotKey, value);
            }
        }

        string modifierKey = "Ctrl";
        public string ModifierKey
        {
            get => modifierKey;
            set{
                modifierKey = value;
                this.RaiseAndSetIfChanged(ref modifierKey, value);
            }
        }

        bool hideAfterStarted = false;
        public bool HideAfterStarted
        {
            get => hideAfterStarted;
            set
            {
                hideAfterStarted = value;
                this.RaiseAndSetIfChanged(ref hideAfterStarted, value);
            }
        }

        bool usingCache = true;
        public bool UsingCache
        {
            get => usingCache;
            set
            {
                usingCache = value;
                this.RaiseAndSetIfChanged(ref usingCache, value);
            }
        }

        bool autoHide = true;
        public bool AutoHide
        {
            get => autoHide;
            set
            {
                autoHide = value;
                this.RaiseAndSetIfChanged(ref autoHide, value);
            }
        }

        bool alwaysTop = true;
        public bool AlwaysTop
        {
            get => alwaysTop;
            set
            {
                alwaysTop = value;
                this.RaiseAndSetIfChanged(ref alwaysTop, value);
            }
        }

        bool autoAdjust = true;
        public bool AutoAdjust
        {
            get => autoAdjust;
            set
            {
                autoAdjust = value;
                this.RaiseAndSetIfChanged(ref autoAdjust, value);
            }
        }

        string theme="Default";
        public string Theme
        {
            get => theme;
            set
            {
                theme = value;
                this.RaiseAndSetIfChanged(ref theme, value);
            }
        }


        // 命令

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        private void SaveSettings()
        {
            settingWindow.SaveAndExit();
        }

        private void Cancel()
        {
            settingWindow.Exit();
        }
    }

    internal class FindmaxValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            
            if (value is string lan && int.TryParse(lan, out var num))
            {

                if (num < 1)
                {
                    return new ValidationResult($"不可小于1");

                }
                else if (num >1000)
                {
                    return new ValidationResult($"不可大于1000");
                }
            }
            else
            {
                return new ValidationResult($"数字格式非法");
            }

                return ValidationResult.Success;
        }
    }
}