#pragma once

#include <cstdint>

/// 与 C# [DiskDataCache.cs](EngineCore/Engine/Actions/USN/DiskDataCache.cs) 的磁盘缓存格式说明。
/// 当前 C# 实现：LZ4 帧封装（K4os.Compression.LZ4.Streams）+ BinaryWriter UTF-8 字段序列。
namespace tds::cache {

/// 未来若由 C++ 写出自有缓存，建议文件头魔数与版本（**与 C# 版不兼容**，需迁移工具或双读）。
inline constexpr std::uint32_t kNativeProposedMagic = 0x54445301u;  // 'TDS\1'
inline constexpr std::uint16_t kNativeProposedVersion = 1;

/// C# 版无统一魔数；首字段为 per-drive 字符串，解析需按顺序读至 FINALENDTAG。
inline constexpr char const kLegacyCSharpFinalTag[] = "#FINALEND";

}  // namespace tds::cache
