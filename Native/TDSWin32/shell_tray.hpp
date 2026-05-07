#pragma once

#include <windows.h>

bool tds_shell_tray_add(HWND hwnd, HINSTANCE inst);
void tds_shell_tray_remove(HWND hwnd);
UINT tds_shell_tray_taskbar_created_message();
