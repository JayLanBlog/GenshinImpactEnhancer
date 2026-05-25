#include "Overlay.h"
#include <stdio.h>

namespace Stella {
namespace Overlay {

static HHOOK g_hHook = nullptr;
static HWND g_hwnd = nullptr;
static HBRUSH g_bgBrush = nullptr;
static HFONT g_font = nullptr;
static const wchar_t* OVERLAY_CLASS = L"StellaOverlayClass";
static volatile bool g_running = false;
static HMODULE g_hModule = nullptr;

static void Log(const wchar_t* msg)
{
    FILE* f = nullptr;
    _wfopen_s(&f, L"C:\\Users\\86178\\AppData\\Local\\GenshinStellaMod\\overlay_debug.log", L"a");
    if (f) { fwprintf_s(f, L"%s\n", msg); fclose(f); }
}

LRESULT CALLBACK OverlayWndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);
        FillRect(hdc, &rc, g_bgBrush);
        SetBkMode(hdc, TRANSPARENT);
        SelectObject(hdc, g_font);

        SetTextColor(hdc, RGB(0, 255, 80));
        TextOutW(hdc, 15, 12, L"Genshin Stella Mod", 19);

        SetTextColor(hdc, RGB(255, 255, 255));
        wchar_t buf[128];
        SYSTEMTIME st; GetLocalTime(&st);
        swprintf_s(buf, L"Time: %02d:%02d:%02d | PEB: Hidden", st.wHour, st.wMinute, st.wSecond);
        TextOutW(hdc, 15, 42, buf, (int)wcslen(buf));

        SetTextColor(hdc, RGB(0, 255, 80));
        TextOutW(hdc, 15, 74, L"DLL: Injected | PEB: Hidden", 27);

        SetTextColor(hdc, RGB(255, 200, 0));
        TextOutW(hdc, 15, 110, L"Debug panel active!", 19);

        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_ERASEBKGND: return 1;
    case WM_DESTROY: PostQuitMessage(0); return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// Window + message pump + keyboard hook all in ONE thread
static DWORD WINAPI WindowThread(LPVOID)
{
    Log(L"WindowThread: starting...");

    // Install global keyboard hook FIRST (before window creation)
    // WH_KEYBOARD_LL catches all key presses system-wide
    HHOOK hHook = SetWindowsHookExW(WH_KEYBOARD_LL, [](int code, WPARAM wp, LPARAM lp) -> LRESULT {
        if (code == HC_ACTION && wp == WM_KEYDOWN) {
            if (((KBDLLHOOKSTRUCT*)lp)->vkCode == VK_HOME) {
                Toggle();
            }
        }
        return CallNextHookEx(nullptr, code, wp, lp);
    }, nullptr, 0);
    g_hHook = hHook;
    if (hHook) Log(L"WindowThread: keyboard hook installed OK");
    else Log(L"WindowThread: keyboard hook FAILED");
    CreateDirectoryW(L"C:\\Users\\86178\\AppData\\Local\\GenshinStellaMod", nullptr);

    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = OverlayWndProc;
    wc.hInstance = g_hModule;
    wc.lpszClassName = OVERLAY_CLASS;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.hbrBackground = (HBRUSH)GetStockObject(BLACK_BRUSH);
    RegisterClassExW(&wc);

    g_hwnd = CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST,
        OVERLAY_CLASS, L"Stella Mod Debug",
        WS_POPUP | WS_VISIBLE,
        10, 50, 380, 180,
        nullptr, nullptr, g_hModule, nullptr);

    if (!g_hwnd) { Log(L"WindowThread: CreateWindowEx FAILED"); return 1; }
    Log(L"WindowThread: window created");

    SetLayeredWindowAttributes(g_hwnd, RGB(10, 10, 30), 220, LWA_ALPHA | LWA_COLORKEY);

    g_font = CreateFontW(20, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, FIXED_PITCH | FF_MODERN, L"Consolas");
    g_bgBrush = CreateSolidBrush(RGB(10, 10, 30));

    ShowWindow(g_hwnd, SW_SHOW);
    UpdateWindow(g_hwnd);
    {
        wchar_t buf[256];
        swprintf_s(buf, L"WindowThread: HWND=0x%p title='Stella Mod Debug'", g_hwnd);
        Log(buf);
    }

    // Write HWND to registry so test tool can verify
    HKEY hKey;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, L"Software\\StellaMod", 0, nullptr, REG_OPTION_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr) == ERROR_SUCCESS)
    {
        DWORD dwHwnd = (DWORD)(DWORD_PTR)g_hwnd;
        RegSetValueExW(hKey, L"OverlayHWND", 0, REG_DWORD, (BYTE*)&dwHwnd, sizeof(dwHwnd));
        RegCloseKey(hKey);
        Log(L"HWND written to registry");
    }

    // Message pump (on the window's thread)
    g_running = true;
    MSG msg;
    while (g_running && GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    Log(L"WindowThread: pump ended");
    if (g_hHook) UnhookWindowsHookEx(g_hHook);
    if (g_font) DeleteObject(g_font);
    if (g_bgBrush) DeleteObject(g_bgBrush);
    if (g_hwnd) DestroyWindow(g_hwnd);
    return 0;
}

bool CreateAndShow(HMODULE hDllModule)
{
    g_hModule = hDllModule;
    HANDLE h = CreateThread(nullptr, 0, WindowThread, nullptr, 0, nullptr);
    if (h) { CloseHandle(h); Log(L"DllMain: thread started"); return true; }
    Log(L"DllMain: thread FAILED");
    return false;
}

static volatile LONG g_toggleCount = 0;

void Toggle()
{
    if (!g_hwnd) return;
    ShowWindow(g_hwnd, IsWindowVisible(g_hwnd) ? SW_HIDE : SW_SHOW);
    // Write toggle count to registry for test verification
    LONG count = InterlockedIncrement(&g_toggleCount);
    HKEY hKey;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, L"Software\\StellaMod", 0, nullptr, REG_OPTION_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr) == ERROR_SUCCESS)
    {
        DWORD dw = (DWORD)count;
        RegSetValueExW(hKey, L"ToggleCount", 0, REG_DWORD, (BYTE*)&dw, sizeof(dw));
        RegCloseKey(hKey);
    }
}

bool IsVisible() { return g_hwnd && IsWindowVisible(g_hwnd); }

void Shutdown()
{
    g_running = false;
    if (g_hwnd) PostMessageW(g_hwnd, WM_QUIT, 0, 0);
    Log(L"Shutdown");
}

} // namespace Overlay
} // namespace Stella
