#include "resource.h"
#include "shell_tray.hpp"

#include <shellapi.h>

#pragma comment(lib, "shell32.lib")

static constexpr UINT kTrayId = 1;
static UINT s_taskbarCreated = 0;

UINT tds_shell_tray_taskbar_created_message() {
  if (s_taskbarCreated == 0)
    s_taskbarCreated = RegisterWindowMessageW(L"TaskbarCreated");
  return s_taskbarCreated;
}

bool tds_shell_tray_add(HWND hwnd, HINSTANCE inst) {
  (void)inst;
  NOTIFYICONDATAW nid{};
  nid.cbSize = sizeof(nid);
  nid.hWnd = hwnd;
  nid.uID = kTrayId;
  nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
  nid.uCallbackMessage = WM_APP_TRAYICON;
  nid.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
  wchar_t tip[128]{};
  LoadStringW(inst, IDS_TRAY_TIP, tip, 128);
  if (tip[0] == L'\0')
    lstrcpyW(tip, L"TDS Native");
  lstrcpyW(nid.szTip, tip);
  return Shell_NotifyIconW(NIM_ADD, &nid) == TRUE;
}

void tds_shell_tray_remove(HWND hwnd) {
  NOTIFYICONDATAW nid{};
  nid.cbSize = sizeof(nid);
  nid.hWnd = hwnd;
  nid.uID = kTrayId;
  Shell_NotifyIconW(NIM_DELETE, &nid);
}
