# TDS Native（Win32 C++）

与仓库内 `TDS`（C# Avalonia）并存的可执行程序，用于全栈 native 迁移。

## 要求

- Visual Studio 2022，工作负载「使用 C++ 的桌面开发」
- Windows 10 1809+（x64），`app.manifest` 中 per-monitor DPI aware

## 构建

在 Visual Studio 中打开 [TDSAot.sln](../TDSAot.sln)，配置选 **x64**，生成项目 **TDSWin32**。

或命令行（按本机 VS 安装路径调整）：

```bat
"%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ..\TDSAot.sln /p:Configuration=Debug /p:Platform=x64 /t:TDSWin32
```

输出：`bin\Native\x64\Debug\TDSWin32.exe`（相对仓库根目录 [TDSAot.sln](TDSAot.sln) 所在目录）。

## 管理员

与 C# 版相同，枚举 USN / 打开卷设备通常需**提升权限**；未提升时索引可能为空，状态栏会提示。

## 文档

- 功能对齐与排除项：[PARITY.md](PARITY.md)
