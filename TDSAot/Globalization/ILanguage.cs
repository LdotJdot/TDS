using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDS.Globalization
{
    public interface ILanguage
    {
        public string Name { get; set;         }
        public string ReadableName { get; set;      }

        // Menu text
        public string OpenFile { get; set;     }
        public string OpenFileWith { get; set; }
        public string OpenFolderh { get; set;  }
        public string Copy { get; set;         }
        public string CopyPath { get; set;     }
        public string Delete { get; set;       }
        public string Property { get; set;     }
        public string Option { get; set;       }
        public string ShowWindow { get; set; }
        public string Reindex { get; set; }
        public string About { get; set; }
        public string DisableStartup { get; set; }
        public string EnableStartup { get; set; }
        public string Exit { get; set; }

        public string DefaultResultCount { get; set;     }
        public string DefaultResultCountTip { get; set;  }
        public string DefaultResultCountDesc { get; set; }
        public string HotkeySetting { get; set;          }
        public string HotkeySettingDesc { get; set;      } 

        public string ActivateHotkeySetting { get; set;   }
        public string ModifyHotkeySetting { get; set;     }
        public string ModifyHotkeySettingDesc { get; set; }
        public string BehaviorSettings { get; set;        }
        public string AutoHide { get; set;                }
        public string AutoHideDesc { get; set;            }
        public string AlwaysTop { get; set;               }
        public string AlwaysTopDesc { get; set;           }
        public string HideLostFocus { get; set;           }
        public string HideLostFocusDesc { get; set;       }
        public string AutoResize { get; set;              }
        public string AutoResizeDesc { get; set;          }
        public string DiskCache { get; set;               }
        public string DiskCacheDesc { get; set;           }
        public string Theme { get; set;                   }
        public string ThemeDefault { get; set;            }
        public string ThemeDark { get; set;               }
        public string ThemeLight { get; set;              }
        public string ThemeDesc { get; set;               }
        public string Cancel { get; set;                  }
        public string Ok { get; set;                      }
        public string Error { get; set;                      }

        public string Error_CachingFailed { get; set; }

        public string InputWaterMarkInput { get; set; }
        public string InputWaterMarkPending { get; set; }
        public string Loading { get; set; }
        public string Indexing { get; set; }
        public string Item  { get; set; }
        public string Items { get; set; }

    }

}
