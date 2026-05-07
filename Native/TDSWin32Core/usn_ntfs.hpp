#pragma once

#include <windows.h>
#include <winioctl.h>

// USN / MFT 布局与 C# Win32Api 一致（Pack=1）
#pragma pack(push, 1)
struct TdsUsnJournalData {
  DWORDLONG UsnJournalID{};
  LONGLONG FirstUsn{};
  LONGLONG NextUsn{};
  LONGLONG LowestValidUsn{};
  LONGLONG MaxUsn{};
  DWORDLONG MaximumSize{};
  DWORDLONG AllocationDelta{};
};

struct TdsMftEnumData {
  DWORDLONG StartFileReferenceNumber{};
  LONGLONG LowUsn{};
  LONGLONG HighUsn{};
};
#pragma pack(pop)

// 与 EngineCore NtfsUsnJournal 一致
inline constexpr DWORDLONG kTdsRootFrn = 0x5000000000005ULL;

// USN_RECORD 解析偏移（见 Win32Api.UsnEntry）
inline constexpr int kUsnFrnOffset = 8;
inline constexpr int kUsnParentFrnOffset = 16;
inline constexpr int kUsnReasonOffset = 40;
inline constexpr int kUsnFileAttributesOffset = 52;
inline constexpr int kUsnFileNameLengthOffset = 56;
inline constexpr int kUsnFileNameOffsetField = 58;
