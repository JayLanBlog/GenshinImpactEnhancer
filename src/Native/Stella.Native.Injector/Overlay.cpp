#include "Overlay.h"
#include "ReShadeConfig.h"
#include <stdio.h>

namespace Stella {
namespace Overlay {

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
    case WM_HOTKEY:
        // F12 hotkey -> toggle Stella panel
        if (wp == 1) Toggle();
        return 0;

    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);
        FillRect(hdc, &rc, g_bgBrush);
        SetBkMode(hdc, TRANSPARENT);
        SelectObject(hdc, g_font);

        const auto& cfg = ReShadeConfig::GetConfig();
        int y = 12;

        SetTextColor(hdc, RGB(0, 255, 80));
        TextOutW(hdc, 15, y, L"Genshin Stella Mod v1.2", 22); y += 30;

        SetTextColor(hdc, RGB(255, 255, 255));
        wchar_t buf[256];
        SYSTEMTIME st; GetLocalTime(&st);
        swprintf_s(buf, L"PEB: Hidden | %02d:%02d:%02d", st.wHour, st.wMinute, st.wSecond);
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 26;

        SetTextColor(hdc, RGB(80, 80, 100));
        TextOutW(hdc, 15, y, L"--------------------------------", 32); y += 22;

        SetTextColor(hdc, RGB(0, 200, 255));
        TextOutW(hdc, 15, y, L"ReShade", 8); y += 24;

        SetTextColor(hdc, RGB(255, 255, 255));
        swprintf_s(buf, L"  rtlbase.dll: %s", cfg.rtlbaseLoaded ? L"LOADED" : L"NOT FOUND");
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        swprintf_s(buf, L"  ReShade.ini: %s", cfg.iniLoaded ? L"LOADED" : L"NOT FOUND");
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        SetTextColor(hdc, RGB(0, 255, 80));
        swprintf_s(buf, L"  Shaders: %d effects loaded", cfg.shaderCount);
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        if (!cfg.presetPath.empty()) {
            SetTextColor(hdc, RGB(200, 200, 200));
            auto pos = cfg.presetPath.rfind(L'\\');
            std::wstring presetName = pos != std::wstring::npos ? cfg.presetPath.substr(pos + 1) : cfg.presetPath;
            swprintf_s(buf, L"  Preset: %s", presetName.c_str());
            TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;
        }

        y += 4;
        SetTextColor(hdc, RGB(255, 200, 0));
        TextOutW(hdc, 15, y, L"Hotkeys:", 9); y += 20;
        SetTextColor(hdc, RGB(200, 200, 200));
        TextOutW(hdc, 15, y, L"  HOME  - ReShade shader panel", 29); y += 20;
        TextOutW(hdc, 15, y, L"  F11   - Stella info panel", 26);

        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_ERASEBKGND: return 1;
    case WM_DESTROY: PostQuitMessage(0); return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

static DWORD WINAPI WindowThread(LPVOID)
{
    Log(L"WindowThread: starting (RegisterHotKey, no LL hook)...");

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
        WS_POPUP,
        10, 50, 420, 340,
        nullptr, nullptr, g_hModule, nullptr);

    if (!g_hwnd) { Log(L"WindowThread: CreateWindowEx FAILED"); return 1; }
    Log(L"WindowThread: window created");

    // Register F11 hotkey (F12 often taken by other apps)
    if (RegisterHotKey(g_hwnd, 1, 0, VK_F11))
        Log(L"WindowThread: F11 hotkey registered OK");
    else
        Log(L"WindowThread: F11 hotkey FAILED");

    SetLayeredWindowAttributes(g_hwnd, RGB(8, 8, 28), 230, LWA_ALPHA | LWA_COLORKEY);

    g_font = CreateFontW(17, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        CLEARTYPE_QUALITY, FIXED_PITCH | FF_MODERN, L"Consolas");
    g_bgBrush = CreateSolidBrush(RGB(8, 8, 28));

    {
        wchar_t buf[256];
        swprintf_s(buf, L"WindowThread: HWND=0x%p (F12=Stella, HOME=ReShade, no LL hook)", g_hwnd);
        Log(buf);
    }

    // Registry
    HKEY hKey;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, L"Software\\StellaMod", 0, nullptr, REG_OPTION_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr) == ERROR_SUCCESS)
    {
        DWORD dwHwnd = (DWORD)(DWORD_PTR)g_hwnd;
        RegSetValueExW(hKey, L"OverlayHWND", 0, REG_DWORD, (BYTE*)&dwHwnd, sizeof(dwHwnd));
        RegCloseKey(hKey);
    }

    g_running = true;
    MSG msg;
    while (g_running && GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    Log(L"WindowThread: pump ended");
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
    bool visible = IsWindowVisible(g_hwnd);
    ShowWindow(g_hwnd, visible ? SW_HIDE : SW_SHOW);
    if (!visible) InvalidateRect(g_hwnd, nullptr, TRUE);

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
