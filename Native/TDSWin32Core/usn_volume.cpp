#include "usn_volume.hpp"

#include <cwctype>
#include <vector>

#include "usn_ntfs.hpp"

#include <winioctl.h>

static constexpr DWORD kEnumOutBytes = 1024u * 1024u;

static wchar_t upper_drive(wchar_t d) {
  if (d >= L'a' && d <= L'z')
    return static_cast<wchar_t>(d - (L'a' - L'A'));
  return d;
}

NtfsUsnVolume::NtfsUsnVolume(NtfsUsnVolume&& other) noexcept {
  volume_ = other.volume_;
  drive_letter_ = other.drive_letter_;
  other.volume_ = INVALID_HANDLE_VALUE;
}

NtfsUsnVolume& NtfsUsnVolume::operator=(NtfsUsnVolume&& other) noexcept {
  if (this == &other)
    return *this;
  close();
  volume_ = other.volume_;
  drive_letter_ = other.drive_letter_;
  other.volume_ = INVALID_HANDLE_VALUE;
  return *this;
}

NtfsUsnVolume::~NtfsUsnVolume() {
  close();
}

void NtfsUsnVolume::close() {
  if (volume_ != INVALID_HANDLE_VALUE) {
    CloseHandle(volume_);
    volume_ = INVALID_HANDLE_VALUE;
  }
}

bool NtfsUsnVolume::open(wchar_t drive_letter) {
  close();
  drive_letter_ = upper_drive(drive_letter);
  wchar_t path[8] = LR"(\\.\ :)";
  path[4] = drive_letter_;
  volume_ =
      CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING,
                  FILE_FLAG_BACKUP_SEMANTICS, nullptr);
  return volume_ != INVALID_HANDLE_VALUE;
}

bool NtfsUsnVolume::enum_all_into(FileIndex& index, std::wstring& error_message) {
  error_message.clear();
  if (volume_ == INVALID_HANDLE_VALUE) {
    error_message = L"Volume not open.";
    return false;
  }

  TdsUsnJournalData ujd{};
  DWORD bytes = 0;
  if (!DeviceIoControl(volume_, FSCTL_QUERY_USN_JOURNAL, nullptr, 0, &ujd, sizeof(ujd), &bytes, nullptr)) {
    error_message = L"FSCTL_QUERY_USN_JOURNAL failed (run as Administrator?).";
    return false;
  }

  {
    std::wstring root_label;
    root_label += drive_letter_;
    root_label += L':';
    index.add_record(drive_letter_, kTdsRootFrn, ULLONG_MAX, root_label);
  }

  TdsMftEnumData med{};
  med.StartFileReferenceNumber = 0;
  med.LowUsn = 0;
  med.HighUsn = ujd.NextUsn;

  std::vector<std::uint8_t> out_buf(kEnumOutBytes);

  for (;;) {
    DWORD returned = 0;
    const BOOL ioctl_ok =
        DeviceIoControl(volume_, FSCTL_ENUM_USN_DATA, &med, sizeof(med), out_buf.data(),
                        static_cast<DWORD>(out_buf.size()), &returned, nullptr);
    if (!ioctl_ok) {
      const DWORD err = GetLastError();
      if (err == ERROR_HANDLE_EOF)
        return true;
      error_message = L"FSCTL_ENUM_USN_DATA failed.";
      return false;
    }
    if (returned < sizeof(DWORDLONG)) {
      error_message = L"ENUM buffer too small.";
      return false;
    }

    DWORDLONG const next_start = *reinterpret_cast<DWORDLONG*>(out_buf.data());
    med.StartFileReferenceNumber = next_start;

    BYTE* rec = out_buf.data() + sizeof(DWORDLONG);
    DWORD remain = returned - static_cast<DWORD>(sizeof(DWORDLONG));
    while (remain > 60) {
      BYTE* p = rec;
      const DWORD rec_len = *reinterpret_cast<DWORD*>(p);
      if (rec_len < 60 || rec_len > remain)
        break;

      const std::uint64_t frn = *reinterpret_cast<std::uint64_t*>(p + kUsnFrnOffset);
      const std::uint64_t parent = *reinterpret_cast<std::uint64_t*>(p + kUsnParentFrnOffset);
      const USHORT fn_len = *reinterpret_cast<USHORT*>(p + kUsnFileNameLengthOffset);
      const USHORT fn_off = *reinterpret_cast<USHORT*>(p + kUsnFileNameOffsetField);
      if (static_cast<DWORD>(fn_off) + static_cast<DWORD>(fn_len) > rec_len || (fn_len % sizeof(WCHAR)) != 0) {
        rec += rec_len;
        remain -= rec_len;
        continue;
      }
      const WCHAR* name_ptr = reinterpret_cast<WCHAR*>(p + fn_off);
      const std::size_t nchars = fn_len / sizeof(WCHAR);
      std::wstring name(name_ptr, nchars);

      bool blank = true;
      for (wchar_t c : name) {
        if (!iswspace(c)) {
          blank = false;
          break;
        }
      }
      if (!blank)
        index.add_record(drive_letter_, frn, parent, std::move(name));

      rec += rec_len;
      remain -= rec_len;
    }
  }
}
