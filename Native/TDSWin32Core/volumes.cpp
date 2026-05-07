#include "volumes.hpp"

#include <array>
#include <cstring>
#include <cwctype>

#include <windows.h>

static bool is_ntfs_volume(wchar_t letter) {
  std::array<wchar_t, 8> root{};
  root[0] = letter;
  root[1] = L':';
  root[2] = L'\\';
  root[3] = L'\0';
  if (GetDriveTypeW(root.data()) != DRIVE_FIXED)
    return false;
  std::array<wchar_t, MAX_PATH + 1> fs{};
  DWORD ignored_serial = 0;
  DWORD ignored_max = 0;
  DWORD ignored_flags = 0;
  if (!GetVolumeInformationW(root.data(), nullptr, 0, &ignored_serial, &ignored_max, &ignored_flags, fs.data(),
                               static_cast<DWORD>(fs.size())))
    return false;
  // NTFS，大小写不敏感
  if (_wcsicmp(fs.data(), L"NTFS") != 0)
    return false;
  return true;
}

std::vector<wchar_t> tds_enum_fixed_ntfs_drive_letters() {
  std::vector<wchar_t> out;
  std::array<wchar_t, 64> buf{};
  if (GetLogicalDriveStringsW(static_cast<DWORD>(buf.size()), buf.data()) == 0)
    return out;
  for (wchar_t* p = buf.data(); *p;) {
    const size_t len = wcslen(p);
    if (len >= 2 && p[1] == L':') {
      const wchar_t letter = static_cast<wchar_t>(towupper(p[0]));
      if (letter >= L'A' && letter <= L'Z' && is_ntfs_volume(letter))
        out.push_back(letter);
    }
    p += len + 1;
  }
  return out;
}

std::optional<wchar_t> tds_first_fixed_ntfs_drive_letter() {
  const auto v = tds_enum_fixed_ntfs_drive_letters();
  if (v.empty())
    return std::nullopt;
  return v.front();
}
