#pragma once

#include <cstddef>
#include <cstdint>
#include <functional>
#include <mutex>
#include <shared_mutex>
#include <string>
#include <unordered_map>
#include <vector>

struct FileRecord {
  wchar_t drive_letter = L'?';  // 'C' 等；骨架单卷避免跨卷 FRN 冲突
  std::uint64_t file_reference_number{};
  std::uint64_t parent_file_reference_number{};
  std::wstring inner_file_name;
  /// 与 C# `FrnFileOrigin.keyindex` 同用途：`(queryMask | keyindex) != keyindex` 时可直接排除。
  std::uint64_t keyindex = 0;
};

class FileIndex {
public:
  void clear();
  void add_record(wchar_t drive, std::uint64_t frn, std::uint64_t parent, std::wstring name);
  std::size_t size() const;

  FileRecord const& at(std::size_t i) const;
  bool try_get_frn(wchar_t drive, std::uint64_t frn, FileRecord& out) const;

  /// 自根回溯拼路径（仅用于显示；深度大时较慢）。
  std::wstring build_path(std::size_t record_index) const;

  /// 在**单次** shared_lock 下遍历；回调返回 false 时提前结束（命中上限 / 取消搜索）。
  void for_each_record_readlocked(std::function<bool(FileRecord const&, std::size_t)> const& fn) const;

private:
  using MapKey = std::uint64_t;  // (drive<<56)|frn_low56 — 骨架限定单卷时亦可用裸 frn
  static MapKey make_key(wchar_t drive, std::uint64_t frn) {
    const auto d = static_cast<std::uint64_t>(towupper(drive) & 0xFFu);
    return (d << 56) | (frn & 0x00FFFFFFFFFFFFFFULL);
  }

  mutable std::shared_mutex mutex_;
  std::vector<FileRecord> records_;
  std::unordered_map<MapKey, std::size_t> frn_to_index_;
};
