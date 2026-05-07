#include "file_index.hpp"

#include "tds_keyindex.hpp"
#include "usn_ntfs.hpp"

#include <cwctype>

void FileIndex::clear() {
  std::unique_lock lk(mutex_);
  records_.clear();
  frn_to_index_.clear();
}

void FileIndex::add_record(wchar_t drive, std::uint64_t frn, std::uint64_t parent, std::wstring name) {
  std::unique_lock lk(mutex_);
  const std::size_t idx = records_.size();
  FileRecord r;
  r.drive_letter = static_cast<wchar_t>(towupper(drive));
  r.file_reference_number = frn;
  r.parent_file_reference_number = parent;
  r.inner_file_name = std::move(name);
  r.keyindex = tds::keyindex::file_keyindex_from_leaf(r.inner_file_name);
  records_.push_back(std::move(r));
  frn_to_index_[make_key(drive, frn)] = idx;
}

std::size_t FileIndex::size() const {
  std::shared_lock lk(mutex_);
  return records_.size();
}

FileRecord const& FileIndex::at(std::size_t i) const {
  std::shared_lock lk(mutex_);
  return records_.at(i);
}

void FileIndex::for_each_record_readlocked(std::function<bool(FileRecord const&, std::size_t)> const& fn) const {
  std::shared_lock lk(mutex_);
  for (std::size_t i = 0; i < records_.size(); ++i) {
    if (!fn(records_[i], i))
      break;
  }
}

bool FileIndex::try_get_frn(wchar_t drive, std::uint64_t frn, FileRecord& out) const {
  std::shared_lock lk(mutex_);
  const auto it = frn_to_index_.find(make_key(drive, frn));
  if (it == frn_to_index_.end())
    return false;
  out = records_[it->second];
  return true;
}

std::wstring FileIndex::build_path(std::size_t record_index) const {
  std::shared_lock lk(mutex_);
  if (record_index >= records_.size())
    return {};
  wchar_t const drive = records_[record_index].drive_letter;
  std::vector<std::wstring> parts;
  std::size_t idx = record_index;
  for (int depth = 0; depth < 65536; ++depth) {
    FileRecord const& r = records_[idx];
    if (r.file_reference_number == kTdsRootFrn)
      break;
    parts.push_back(r.inner_file_name);
    if (r.parent_file_reference_number == ULLONG_MAX)
      break;
    auto const it = frn_to_index_.find(make_key(r.drive_letter, r.parent_file_reference_number));
    if (it == frn_to_index_.end())
      break;
    idx = it->second;
  }
  std::wstring out;
  out.push_back(drive);
  out += L':';
  for (auto it = parts.rbegin(); it != parts.rend(); ++it) {
    out += L'\\';
    out += *it;
  }
  return out;
}
