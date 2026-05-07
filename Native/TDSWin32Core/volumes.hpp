#pragma once

#include <optional>
#include <vector>

/// 枚举本地固定 NTFS 盘符（与 C# DriverUtils 语义相近）。
std::vector<wchar_t> tds_enum_fixed_ntfs_drive_letters();

/// 用于骨架：取第一个固定 NTFS 卷；多卷 FRN 冲突需在索引中加卷维度后再合并。
std::optional<wchar_t> tds_first_fixed_ntfs_drive_letter();
