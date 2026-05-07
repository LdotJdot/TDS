# Native (TDSWin32) 与 C# 版功能对齐清单

**范围**：与 [TDSAot.sln](../TDSAot.sln) 中现有 `TDS` / `EngineCore` 行为对齐；**明确不包含** [TDSAot/PeekDesktop](../TDSAot/PeekDesktop)。

## 验收标准（阶段目标）

| 能力 | C# 参考位置 | Native 状态 |
|------|-------------|---------------|
| 枚举固定盘 NTFS 卷 | `DriverUtils` / `MainWindow_Loaded` | `volumes.cpp` |
| USN 查询 / MFT 枚举建索引 | `NtfsUsnJournal.GetNtfsVolumeAllentries` | `usn_volume.cpp` |
| USN 增量（读 journal） | `FileSys.DoWhileFileChanges` | 骨架：常量与 IOCTL 已对齐，应用循环待接 |
| 多词 / 盘符前缀搜索 | `MainWindow_TaskLoop` | `search_worker.cpp`：`keyindex`/`TBS` 预筛 + 多词 `StrStrIW`；**单次读锁**整表扫描；**未**接 `SpellCN` / 盘符目录语法 / `Parallel.For` |
| 结果上限 Findmax | `Option.Findmax` | `settings.hpp` 字段 + 搜索截断 |
| 磁盘缓存 | `DiskDataCache`（LZ4 + UTF8 二进制） | [cache_format.h](TDSWin32/cache_format.h) 定义未来兼容头；读 C# 缓存待 LZ4 依赖 |
| 托盘图标 | `MainWindow_InitializeTrayIcon` | `shell_tray.cpp` |
| 全局热键 | `MainWindow_KeyGlobalWin` / `HookManager` | `RegisterHotKey` + INI |
| 多语言 | `Lang_*` / `LangManager` | `TDSWin32.rc` STRINGTABLE |
| 选项持久化 | `AppOption` / `SettingWindow` | `settings.cpp` INI |
| PeekDesktop | `PeekDesktop/*` | **不做** |

## 非目标（首版可省略）

- Avalonia 主题与 XAML 等价视觉
- 与 C# 版完全一致的拼音 `SpellCN` 行为（需逐用例移植）
- 从旧版 `DiskDataCache` 直接导入（需 LZ4 流格式兼容层）
