#include <windows.h>
#include <commctrl.h>
#include <shellapi.h>

#include <cstdio>
#include <iterator>
#include <memory>
#include <string>
#include <thread>
#include <vector>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")

#include "resource.h"
#include "settings.hpp"
#include "shell_tray.hpp"

#include "file_index.hpp"
#include "search_worker.hpp"
#include "usn_volume.hpp"
#include "volumes.hpp"

struct IndexPayload {
  std::unique_ptr<FileIndex> index;
  std::wstring error;
  bool ok = false;
};

struct SearchPayload {
  std::vector<std::size_t> hits;
};

struct AppState {
  HWND hwnd_main = nullptr;
  HWND hwnd_edit = nullptr;
  HWND hwnd_list = nullptr;
  HWND hwnd_status = nullptr;
  HINSTANCE inst = nullptr;
  std::unique_ptr<FileIndex> index;
  std::vector<std::size_t> hits;
  SearchWorker search;
  AppSettings settings{};
};

static std::unique_ptr<AppState> g_app;
static UINT s_taskbarCreatedMsg = 0;

static void layout_children(HWND hwnd) {
  if (!g_app || !g_app->hwnd_status)
    return;
  RECT rc{};
  GetClientRect(hwnd, &rc);
  RECT rs{};
  GetWindowRect(g_app->hwnd_status, &rs);
  const int status_h = rs.bottom - rs.top;
  const int edit_h = 28;
  const int top = 4;
  const int margin = 6;
  MoveWindow(g_app->hwnd_edit, margin, top, rc.right - 2 * margin, edit_h, TRUE);
  MoveWindow(g_app->hwnd_list, margin, top + edit_h + margin, rc.right - 2 * margin,
             rc.bottom - status_h - edit_h - top - 2 * margin, TRUE);
  MoveWindow(g_app->hwnd_status, 0, rc.bottom - status_h, rc.right, status_h, TRUE);
}

static void set_status_text(wchar_t const* text) {
  if (g_app && g_app->hwnd_status)
    SendMessageW(g_app->hwnd_status, SB_SETTEXTW, 0, reinterpret_cast<LPARAM>(text));
}

static void load_string_buf(HINSTANCE hi, UINT id, wchar_t* buf, int cch) {
  if (LoadStringW(hi, id, buf, cch) <= 0)
    buf[0] = L'\0';
}

static void index_thread_main(HWND notify_hwnd) {
  auto payload = std::make_unique<IndexPayload>();
  payload->index = std::make_unique<FileIndex>();
  auto const letter = tds_first_fixed_ntfs_drive_letter();
  if (!letter.has_value()) {
    payload->error = L"No fixed NTFS volume found.";
    payload->ok = false;
  } else {
    NtfsUsnVolume vol;
    if (!vol.open(*letter)) {
      payload->error = L"CreateFile on volume failed (Administrator?).";
      payload->ok = false;
    } else {
      payload->ok = vol.enum_all_into(*payload->index, payload->error);
      vol.close();
    }
  }
  PostMessageW(notify_hwnd, WM_APP_INDEX_DONE, 0, reinterpret_cast<LPARAM>(payload.release()));
}

static void start_index_build(HWND hwnd) {
  wchar_t buf[256]{};
  load_string_buf(g_app->inst, IDS_STATUS_INDEXING, buf, 256);
  set_status_text(buf);
  std::thread(index_thread_main, hwnd).detach();
}

static void run_search_from_edit(HWND hwnd) {
  if (!g_app || !g_app->hwnd_list)
    return;
  if (!g_app->index || g_app->index->size() == 0) {
    g_app->hits.clear();
    ListView_SetItemCountEx(g_app->hwnd_list, 0, 0);
    return;
  }
  wchar_t q[512]{};
  GetWindowTextW(g_app->hwnd_edit, q, static_cast<int>(std::size(q)));
  g_app->search.cancel();
  g_app->search.start_search(
      g_app->index.get(), q, g_app->settings.find_max,
      [hwnd](std::vector<std::size_t> hits) {
        auto* p = new SearchPayload{std::move(hits)};
        PostMessageW(hwnd, WM_APP_SEARCH_DONE, 0, reinterpret_cast<LPARAM>(p));
      });
}

static LRESULT CALLBACK main_wnd_proc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam) {
  if (s_taskbarCreatedMsg == 0)
    s_taskbarCreatedMsg = tds_shell_tray_taskbar_created_message();
  if (msg == s_taskbarCreatedMsg) {
    if (g_app)
      tds_shell_tray_add(hwnd, g_app->inst);
    return 0;
  }

  switch (msg) {
  case WM_CREATE: {
    g_app = std::make_unique<AppState>();
    g_app->hwnd_main = hwnd;
    g_app->inst = reinterpret_cast<CREATESTRUCTW*>(lparam)->hInstance;
    tds_load_settings(g_app->settings);

    INITCOMMONCONTROLSEX icc{sizeof(icc), ICC_LISTVIEW_CLASSES | ICC_BAR_CLASSES | ICC_STANDARD_CLASSES};
    InitCommonControlsEx(&icc);

    g_app->hwnd_status =
        CreateWindowExW(0, STATUSCLASSNAMEW, nullptr, WS_CHILD | WS_VISIBLE, 0, 0, 0, 0, hwnd, nullptr, g_app->inst, nullptr);
    g_app->hwnd_edit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 0, 0, 0, 0,
                                       hwnd, reinterpret_cast<HMENU>(static_cast<UINT_PTR>(IDC_EDIT_QUERY)), g_app->inst, nullptr);
    g_app->hwnd_list =
        CreateWindowExW(WS_EX_CLIENTEDGE, WC_LISTVIEWW, L"", WS_CHILD | WS_VISIBLE | LVS_REPORT | LVS_OWNERDATA | LVS_SHOWSELALWAYS,
                        0, 0, 0, 0, hwnd, reinterpret_cast<HMENU>(static_cast<UINT_PTR>(IDC_LIST_RESULTS)), g_app->inst, nullptr);
    SendMessageW(g_app->hwnd_list, LVM_SETEXTENDEDLISTVIEWSTYLE, 0, LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);

    LVCOLUMNW col{};
    col.mask = LVCF_TEXT | LVCF_WIDTH;
    col.cx = 220;
    col.pszText = const_cast<LPWSTR>(L"Name");
    ListView_InsertColumn(g_app->hwnd_list, 0, &col);
    col.cx = 520;
    col.pszText = const_cast<LPWSTR>(L"Path");
    ListView_InsertColumn(g_app->hwnd_list, 1, &col);

    HFONT ui_font = static_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
    SendMessageW(g_app->hwnd_edit, WM_SETFONT, reinterpret_cast<WPARAM>(ui_font), TRUE);
    SendMessageW(g_app->hwnd_list, WM_SETFONT, reinterpret_cast<WPARAM>(ui_font), TRUE);

    tds_shell_tray_add(hwnd, g_app->inst);
    RegisterHotKey(hwnd, HOTKEY_ID_TDS, g_app->settings.hotkey_mod, g_app->settings.hotkey_vk);

    PostMessageW(hwnd, WM_APP_START_INDEX, 0, 0);
    return 0;
  }
  case WM_APP_START_INDEX:
    start_index_build(hwnd);
    return 0;
  case WM_APP_INDEX_DONE: {
    std::unique_ptr<IndexPayload> pl(reinterpret_cast<IndexPayload*>(lparam));
    if (!pl || !g_app)
      return 0;
    g_app->index = std::move(pl->index);
    wchar_t ready[128]{};
    load_string_buf(g_app->inst, IDS_STATUS_READY, ready, 128);
    if (!pl->ok) {
      if (!pl->error.empty())
        set_status_text(pl->error.c_str());
      else
        set_status_text(L"Index failed.");
    } else {
      wchar_t buf[256]{};
      swprintf_s(buf, L"%s %zu files indexed.", ready, g_app->index ? g_app->index->size() : 0);
      set_status_text(buf);
    }
    return 0;
  }
  case WM_APP_SEARCH_DONE: {
    std::unique_ptr<SearchPayload> pl(reinterpret_cast<SearchPayload*>(lparam));
    if (!pl || !g_app)
      return 0;
    g_app->hits = std::move(pl->hits);
    ListView_SetItemCountEx(g_app->hwnd_list, static_cast<int>(g_app->hits.size()), LVSICF_NOINVALIDATEALL);
    InvalidateRect(g_app->hwnd_list, nullptr, FALSE);
    wchar_t buf[128]{};
    swprintf_s(buf, L"%zu matches", g_app->hits.size());
    set_status_text(buf);
    return 0;
  }
  case WM_SIZE:
    if (g_app)
      layout_children(hwnd);
    return 0;
  case WM_GETMINMAXINFO: {
    auto* mmi = reinterpret_cast<MINMAXINFO*>(lparam);
    mmi->ptMinTrackSize.x = 480;
    mmi->ptMinTrackSize.y = 320;
    return 0;
  }
  case WM_COMMAND: {
    const int id = LOWORD(wparam);
    const int code = HIWORD(wparam);
    if (id == IDM_EXIT) {
      DestroyWindow(hwnd);
      return 0;
    }
    if (id == IDC_EDIT_QUERY && code == EN_CHANGE) {
      KillTimer(hwnd, IDT_SEARCH_DEBOUNCE);
      SetTimer(hwnd, IDT_SEARCH_DEBOUNCE, 400, nullptr);
    }
    return 0;
  }
  case WM_TIMER:
    if (wparam == IDT_SEARCH_DEBOUNCE) {
      KillTimer(hwnd, IDT_SEARCH_DEBOUNCE);
      run_search_from_edit(hwnd);
    }
    return 0;
  case WM_NOTIFY: {
    if (!g_app)
      return 0;
    auto* hdr = reinterpret_cast<NMHDR*>(lparam);
    if (hdr->hwndFrom == g_app->hwnd_list) {
      if (hdr->code == LVN_GETDISPINFOW) {
        auto* di = reinterpret_cast<NMLVDISPINFOW*>(lparam);
        if (!g_app || !g_app->index || g_app->hits.empty())
          return 0;
        const int i = di->item.iItem;
        if (i < 0 || static_cast<std::size_t>(i) >= g_app->hits.size())
          return 0;
        const std::size_t rec = g_app->hits[static_cast<std::size_t>(i)];
        thread_local wchar_t buf0[1024];
        thread_local wchar_t buf1[4096];
        if (di->item.mask & LVIF_TEXT) {
          if (di->item.iSubItem == 0) {
            wcsncpy_s(buf0, std::size(buf0), g_app->index->at(rec).inner_file_name.c_str(), _TRUNCATE);
            di->item.pszText = buf0;
          } else if (di->item.iSubItem == 1) {
            std::wstring const p = g_app->index->build_path(rec);
            wcsncpy_s(buf1, std::size(buf1), p.c_str(), _TRUNCATE);
            di->item.pszText = buf1;
          }
        }
        return 0;
      }
      if (hdr->code == NM_DBLCLK) {
        auto* ia = reinterpret_cast<NMITEMACTIVATE*>(lparam);
        const int i = ia->iItem;
        if (i >= 0 && g_app->index && static_cast<std::size_t>(i) < g_app->hits.size()) {
          std::wstring const path = g_app->index->build_path(g_app->hits[static_cast<std::size_t>(i)]);
          ShellExecuteW(hwnd, L"open", path.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
        }
        return 0;
      }
    }
    return 0;
  }
  case WM_HOTKEY:
    if (wparam == HOTKEY_ID_TDS) {
      ShowWindow(hwnd, SW_RESTORE);
      SetForegroundWindow(hwnd);
    }
    return 0;
  case WM_APP_TRAYICON: {
    if (lparam == WM_RBUTTONUP || lparam == WM_CONTEXTMENU) {
      POINT pt{};
      GetCursorPos(&pt);
      SetForegroundWindow(hwnd);
      HMENU menu = CreatePopupMenu();
      AppendMenuW(menu, MF_STRING, IDM_EXIT, L"Exit");
      TrackPopupMenuEx(menu, TPM_RIGHTBUTTON, pt.x, pt.y, hwnd, nullptr);
      DestroyMenu(menu);
    } else if (lparam == WM_LBUTTONDBLCLK) {
      ShowWindow(hwnd, SW_RESTORE);
      SetForegroundWindow(hwnd);
    }
    return 0;
  }
  case WM_DESTROY:
    if (g_app) {
      tds_save_settings(g_app->settings);
      UnregisterHotKey(hwnd, HOTKEY_ID_TDS);
      tds_shell_tray_remove(hwnd);
      g_app->search.cancel();
    }
    g_app.reset();
    PostQuitMessage(0);
    return 0;
  default:
    break;
  }
  return DefWindowProcW(hwnd, msg, wparam, lparam);
}

int APIENTRY wWinMain(HINSTANCE hi, HINSTANCE, LPWSTR, int show) {
  wchar_t title[128]{};
  LoadStringW(hi, IDS_APP_TITLE, title, 128);

  WNDCLASSW wc{};
  wc.lpfnWndProc = main_wnd_proc;
  wc.hInstance = hi;
  wc.lpszClassName = L"TdsNativeMainWnd";
  wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
  wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
  RegisterClassW(&wc);

  HWND hwnd = CreateWindowExW(0, wc.lpszClassName, title, WS_OVERLAPPEDWINDOW | WS_VISIBLE, CW_USEDEFAULT, CW_USEDEFAULT,
                              900, 600, nullptr, nullptr, hi, nullptr);
  if (!hwnd)
    return 1;

  ShowWindow(hwnd, show);
  UpdateWindow(hwnd);

  MSG msg{};
  while (GetMessageW(&msg, nullptr, 0, 0)) {
    TranslateMessage(&msg);
    DispatchMessageW(&msg);
  }
  return static_cast<int>(msg.wParam);
}
