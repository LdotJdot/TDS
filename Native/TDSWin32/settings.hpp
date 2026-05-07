#pragma once

#include <windows.h>

#include <string>

struct AppSettings {
  UINT hotkey_mod = MOD_CONTROL | MOD_ALT;
  UINT hotkey_vk = 'T';
  int find_max = 5000;
};

void tds_load_settings(AppSettings& s);
void tds_save_settings(AppSettings const& s);

std::wstring tds_settings_ini_path();
