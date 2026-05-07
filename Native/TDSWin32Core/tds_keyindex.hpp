#pragma once

#include <cstdint>
#include <cwctype>
#include <string>
#include <string_view>

/// 与 C# FrnFileClass 中 alphbet / TBS / |name| 管道格式对齐（暂不接 SpellCN，中文检索位图可能与旧版略有差异）。
namespace tds::keyindex {

inline constexpr int kScreenCharNum = 45;

inline constexpr wchar_t const kAlphabet[kScreenCharNum] = {
    L'@', L'.', L'0', L'1', L'2', L'3', L'4', L'5', L'6', L'7', L'8', L'9', L'A', L'B', L'C', L'D', L'E', L'F',
    L'G', L'H', L'I', L'J', L'K', L'L', L'M', L'N', L'O', L'P', L'Q', L'R', L'S', L'T', L'U', L'V', L'W', L'X',
    L'Y', L'Z', L'-', L'_', L'[', L']', L'(', L')', L'/'};

inline void set_bit(std::uint64_t& v, int pos) {
  v |= (static_cast<std::uint64_t>(1) << pos);
}

/// 等价于 C# `TBS(string txt)`：按 `alphbet` 各字符是否在串中出现（OrdinalIgnoreCase）置位。
inline std::uint64_t tbs(std::wstring_view txt) {
  std::uint64_t index_value = 0;
  for (int i = 0; i < kScreenCharNum; ++i) {
    wchar_t const needle = kAlphabet[i];
    for (wchar_t ch : txt) {
      if (std::towupper(static_cast<std::wint_t>(ch)) == std::towupper(static_cast<std::wint_t>(needle))) {
        set_bit(index_value, i);
        break;
      }
    }
  }
  return index_value;
}

/// 无 SpellCN 时与 C# `GetNACNNameAndIndex` 在「拼音等于原名」分支一致：`|name|`
inline std::wstring nacn_pipe_for_leaf(std::wstring const& name) {
  if (name.empty())
    return {};
  return std::wstring(L"|") + name + L"|";
}

inline std::uint64_t file_keyindex_from_leaf(std::wstring const& leaf_name) {
  return tbs(nacn_pipe_for_leaf(leaf_name));
}

/// 对应 C# 侧 `uniwords = FileSys.TBS(SpellCN.GetSpellCode(tmpword))` 的占位：对去空格、大写后的查询串做 TBS（未接 SpellCN 时纯 ASCII 与旧版更接近）。
inline std::uint64_t query_mask_from_flat_keyword(std::wstring flat_no_space) {
  for (wchar_t& c : flat_no_space)
    c = static_cast<wchar_t>(std::towupper(static_cast<std::wint_t>(c)));
  return tbs(flat_no_space);
}

}  // namespace tds::keyindex
