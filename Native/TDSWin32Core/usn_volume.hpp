#pragma once

#include <windows.h>

#include <string>

#include "file_index.hpp"

/// 单卷 USN：打开 `\\.\X:`、QUERY、ENUM 写入 FileIndex（与 C# NtfsUsnJournal 骨架对齐）。
class NtfsUsnVolume {
public:
  NtfsUsnVolume() = default;
  NtfsUsnVolume(NtfsUsnVolume const&) = delete;
  NtfsUsnVolume& operator=(NtfsUsnVolume const&) = delete;
  NtfsUsnVolume(NtfsUsnVolume&& other) noexcept;
  NtfsUsnVolume& operator=(NtfsUsnVolume&& other) noexcept;
  ~NtfsUsnVolume();

  [[nodiscard]] bool open(wchar_t drive_letter);
  void close();

  /// 插入根 FRN 项后枚举 MFT USN 记录；失败返回 false，err 可展示 GetLastError。
  [[nodiscard]] bool enum_all_into(FileIndex& index, std::wstring& error_message);

private:
  HANDLE volume_ = INVALID_HANDLE_VALUE;
  wchar_t drive_letter_ = L'?';
};
