#include "settings.hpp"

#include <shlwapi.h>
#include <string>

#pragma comment(lib, "shlwapi.lib")

std::wstring tds_settings_ini_path() {
  std::wstring path(MAX_PATH, L'\0');
  DWORD const n = GetModuleFileNameW(nullptr, path.data(), MAX_PATH - 1);
  if (n == 0)
    return L"tdsnative.ini";
  path.resize(n);
  PathRemoveFileSpecW(path.data());
  path.resize(wcslen(path.data()));
  path += L"\\tdsnative.ini";
  return path;
}

void tds_load_settings(AppSettings& s) {
  std::wstring const ini = tds_settings_ini_path();
  s.hotkey_mod = static_cast<UINT>(GetPrivateProfileIntW(L"Hotkey", L"Mod", static_cast<int>(s.hotkey_mod), ini.c_str()));
  s.hotkey_vk = static_cast<UINT>(GetPrivateProfileIntW(L"Hotkey", L"Vk", static_cast<int>(s.hotkey_vk), ini.c_str()));
  s.find_max = GetPrivateProfileIntW(L"Search", L"FindMax", s.find_max, ini.c_str());
}

void tds_save_settings(AppSettings const& s) {
  std::wstring const ini = tds_settings_ini_path();
  WritePrivateProfileStringW(L"Hotkey", L"Mod", std::to_wstring(s.hotkey_mod).c_str(), ini.c_str());
  WritePrivateProfileStringW(L"Hotkey", L"Vk", std::to_wstring(s.hotkey_vk).c_str(), ini.c_str());
  WritePrivateProfileStringW(L"Search", L"FindMax", std::to_wstring(s.find_max).c_str(), ini.c_str());
}
