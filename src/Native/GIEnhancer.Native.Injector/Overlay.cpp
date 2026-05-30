#include "Overlay.h"
#include "ReShadeConfig.h"
#include "MigotoConfig.h"
#include <stdio.h>

namespace GIEnhancer {
namespace Overlay {

static HWND g_hwndMsg = nullptr;   // hidden message-only window (for WM_HOTKEY)
static HWND g_hwndOverlay = nullptr; // the actual overlay panel (created/destroyed on demand)
static HBRUSH g_bgBrush = nullptr;
static HFONT g_font = nullptr;
static volatile bool g_running = false;
static HMODULE g_hModule = nullptr;

static void Log(const wchar_t* msg)
{
    FILE* f = nullptr;
    _wfopen_s(&f, L"C:\\Users\\86178\\AppData\\Local\\GenshinImpactEnhancer\\overlay_debug.log", L"a");
    if (f) { fwprintf_s(f, L"%s\n", msg); fclose(f); }
}

// Helper: VK code to short name
static const wchar_t* VkName(int vk)
{
    switch (vk) {
    case 0x24: return L"HOME";
    case 0x23: return L"END";
    case 0x2D: return L"INSERT";
    case 0x2E: return L"DELETE";
    case 0x2C: return L"PRTSC";
    case 0x21: return L"PGUP";
    case 0x22: return L"PGDN";
    case 0x0D: return L"ENTER";
    case 0x09: return L"TAB";
    case 0x1B: return L"ESC";
    case 0x20: return L"SPACE";
    default:
        if (vk >= 0x70 && vk <= 0x87) {
            static wchar_t buf[8];
            swprintf_s(buf, L"F%d", vk - 0x70 + 1);
            return buf;
        }
        return L"?";
    }
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

        const auto& cfg = ReShadeConfig::GetConfig();
        const auto& mcfg = MigotoConfig::GetConfig();
        int y = 12;

        // Title
        SetTextColor(hdc, RGB(0, 255, 80));
        TextOutW(hdc, 15, y, L"Genshin Impact Enhancer v1.2", 29); y += 30;

        // PEB status
        SetTextColor(hdc, RGB(255, 255, 255));
        wchar_t buf[256];
        SYSTEMTIME st; GetLocalTime(&st);
        swprintf_s(buf, L"PEB: %s | %02d:%02d:%02d",
            cfg.rtlbaseLoaded ? L"Skipped (ReShade)" : L"Hidden",
            st.wHour, st.wMinute, st.wSecond);
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 26;

        // Separator
        SetTextColor(hdc, RGB(80, 80, 100));
        TextOutW(hdc, 15, y, L"--------------------------------", 32); y += 22;

        // ReShade section
        SetTextColor(hdc, RGB(0, 200, 255));
        TextOutW(hdc, 15, y, L"ReShade", 8); y += 24;

        SetTextColor(hdc, RGB(255, 255, 255));
        swprintf_s(buf, L"  rtlbase.dll: %s", cfg.rtlbaseLoaded ? L"LOADED" : L"NOT FOUND");
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        swprintf_s(buf, L"  ReShade.ini: %s", cfg.iniLoaded ? L"LOADED" : L"NOT FOUND");
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        SetTextColor(hdc, RGB(0, 255, 80));
        swprintf_s(buf, L"  Shaders: %d effects", cfg.shaderCount);
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        if (!cfg.presetPath.empty()) {
            SetTextColor(hdc, RGB(200, 200, 200));
            auto pos = cfg.presetPath.rfind(L'\\');
            std::wstring pn = pos != std::wstring::npos ? cfg.presetPath.substr(pos + 1) : cfg.presetPath;
            swprintf_s(buf, L"  Preset: %s", pn.c_str());
            TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;
        }

        y += 4;

        // 3DMigoto section
        SetTextColor(hdc, RGB(255, 165, 0)); // Orange
        TextOutW(hdc, 15, y, L"3DMigoto", 9); y += 24;

        SetTextColor(hdc, RGB(255, 255, 255));
        swprintf_s(buf, L"  d3d11.dll: %s", mcfg.d3d11Present ? L"DEPLOYED" : L"NOT FOUND");
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        swprintf_s(buf, L"  d3dx.ini: %s", mcfg.d3dxIniPresent ? L"LOADED" : L"NOT FOUND");
        TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

        if (mcfg.iniLoaded) {
            swprintf_s(buf, L"  Hunting: %s", mcfg.huntingEnabled ? L"ENABLED" : L"DISABLED");
            TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

            // Anti-detection config status
            SetTextColor(hdc, mcfg.loadLibraryRedirect && mcfg.checkForegroundWindow ? RGB(0, 255, 80) : RGB(255, 80, 80));
            swprintf_s(buf, L"  Anti-Det: %s",
                (mcfg.loadLibraryRedirect && mcfg.checkForegroundWindow) ? L"OK" : L"WARNING");
            TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

            SetTextColor(hdc, RGB(200, 200, 200));
            swprintf_s(buf, L"    load_lib_redirect=%s fg_check=%s",
                mcfg.loadLibraryRedirect ? L"OFF" : L"ON",
                mcfg.checkForegroundWindow ? L"OFF" : L"ON");
            TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;

            // Show global hotkeys
            SetTextColor(hdc, RGB(255, 255, 255));
            auto itToggle = mcfg.hotkeys.find(L"KeyToggleMods");
            if (itToggle != mcfg.hotkeys.end()) {
                swprintf_s(buf, L"  ToggleMods: %s (0x%02X)", VkName(itToggle->second), itToggle->second);
                TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;
            }
            auto itReload = mcfg.hotkeys.find(L"KeyReloadMods");
            if (itReload != mcfg.hotkeys.end()) {
                swprintf_s(buf, L"  ReloadMods: %s (0x%02X)", VkName(itReload->second), itReload->second);
                TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;
            }

            // Show screenshot hotkey if configured
            auto it = mcfg.hotkeys.find(L"take_screenshot");
            if (it != mcfg.hotkeys.end()) {
                swprintf_s(buf, L"  Screenshot key: %s (0x%02X)", VkName(it->second), it->second);
                TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;
            }
        }

        y += 4;

        // Separator
        SetTextColor(hdc, RGB(80, 80, 100));
        TextOutW(hdc, 15, y, L"--------------------------------", 32); y += 22;

        // Hotkeys section
        SetTextColor(hdc, RGB(255, 200, 0));
        TextOutW(hdc, 15, y, L"Hotkeys:", 9); y += 20;
        SetTextColor(hdc, RGB(200, 200, 200));
        TextOutW(hdc, 15, y, L"  HOME  - ReShade panel", 22); y += 20;
        TextOutW(hdc, 15, y, L"  F11   - Enhancer panel (toggle)", 32); y += 20;

        // Show 3DMigoto screenshot key if different from defaults
        if (mcfg.iniLoaded) {
            auto it = mcfg.hotkeys.find(L"take_screenshot");
            if (it != mcfg.hotkeys.end() && it->second != 0x2C) { // Not PrintScreen
                swprintf_s(buf, L"  %s   - 3DMigoto screenshot", VkName(it->second));
                TextOutW(hdc, 15, y, buf, (int)wcslen(buf)); y += 20;
            }
        }

        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_ERASEBKGND: return 1;
    case WM_DESTROY: return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// Hotkey window proc (message-only, never visible)
LRESULT CALLBACK HotkeyWndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    if (msg == WM_HOTKEY && wp == 1)
    {
        Toggle();
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

static DWORD WINAPI WindowThread(LPVOID)
{
    Log(L"Thread: starting (message-only hotkey window)...");

    // Register a hidden message-only window just for WM_HOTKEY
    // HWND_MESSAGE = never rendered, zero GPU interference
    WNDCLASSEXW wcHotkey = {};
    wcHotkey.cbSize = sizeof(wcHotkey);
    wcHotkey.lpfnWndProc = HotkeyWndProc;
    wcHotkey.hInstance = g_hModule;
    wcHotkey.lpszClassName = L"GIEnhancerHotkeyClass";
    RegisterClassExW(&wcHotkey);

    g_hwndMsg = CreateWindowExW(0, L"GIEnhancerHotkeyClass", L"", 0,
        0, 0, 0, 0, HWND_MESSAGE, nullptr, g_hModule, nullptr);

    if (!g_hwndMsg) { Log(L"Thread: msg-only window FAILED"); return 1; }

    if (RegisterHotKey(g_hwndMsg, 1, 0, VK_F11))
        Log(L"Thread: F11 hotkey OK (message-only window)");
    else
        Log(L"Thread: F11 hotkey FAILED");

    Log(L"Thread: running (no overlay window - zero GPU overhead)");

    g_running = true;
    MSG msg;
    while (g_running && GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    Log(L"Thread: ended");
    // Cleanup overlay if still shown
    if (g_hwndOverlay) { DestroyWindow(g_hwndOverlay); g_hwndOverlay = nullptr; }
    if (g_font) { DeleteObject(g_font); g_font = nullptr; }
    if (g_bgBrush) { DeleteObject(g_bgBrush); g_bgBrush = nullptr; }
    if (g_hwndMsg) { DestroyWindow(g_hwndMsg); g_hwndMsg = nullptr; }
    return 0;
}

bool CreateAndShow(HMODULE hDllModule)
{
    g_hModule = hDllModule;
    HANDLE h = CreateThread(nullptr, 0, WindowThread, nullptr, 0, nullptr);
    if (h) { CloseHandle(h); Log(L"DllMain: hotkey thread started"); return true; }
    Log(L"DllMain: thread FAILED");
    return false;
}

static volatile LONG g_toggleCount = 0;
static HFONT g_overlayFont = nullptr;

void Toggle()
{
    if (g_hwndOverlay)
    {
        // DESTROY the overlay window completely
        DestroyWindow(g_hwndOverlay);
        g_hwndOverlay = nullptr;
        Log(L"Toggle: overlay DESTROYED (zero GPU impact)");
    }
    else
    {
        // CREATE overlay window on demand
        // Register the class if first time
        WNDCLASSEXW wc = {};
        wc.cbSize = sizeof(wc);
        wc.lpfnWndProc = OverlayWndProc;
        wc.hInstance = g_hModule;
        wc.lpszClassName = L"GIEnhancerOverlayClass";
        wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        wc.style = CS_HREDRAW | CS_VREDRAW;
        wc.hbrBackground = (HBRUSH)GetStockObject(BLACK_BRUSH);
        RegisterClassExW(&wc);

        g_hwndOverlay = CreateWindowExW(
            WS_EX_LAYERED | WS_EX_TOPMOST,
            L"GIEnhancerOverlayClass", L"Genshin Impact Enhancer",
            WS_POPUP,
            10, 50, 420, 520,  // Increased height for anti-detection + global hotkeys
            nullptr, nullptr, g_hModule, nullptr);

        if (!g_hwndOverlay) { Log(L"Toggle: CreateWindow FAILED"); return; }

        SetLayeredWindowAttributes(g_hwndOverlay, RGB(8, 8, 28), 230, LWA_ALPHA);

        // Create GDI objects on demand
        if (!g_font)
            g_font = CreateFontW(17, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                CLEARTYPE_QUALITY, FIXED_PITCH | FF_MODERN, L"Consolas");
        if (!g_bgBrush)
            g_bgBrush = CreateSolidBrush(RGB(8, 8, 28));

        ShowWindow(g_hwndOverlay, SW_SHOW);
        UpdateWindow(g_hwndOverlay);
        Log(L"Toggle: overlay CREATED");
    }

    LONG count = InterlockedIncrement(&g_toggleCount);
    HKEY hKey;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, L"Software\\GenshinImpactEnhancer", 0, nullptr, REG_OPTION_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr) == ERROR_SUCCESS)
    {
        DWORD dw = (DWORD)count;
        RegSetValueExW(hKey, L"ToggleCount", 0, REG_DWORD, (BYTE*)&dw, sizeof(dw));
        RegCloseKey(hKey);
    }
}

bool IsVisible() { return g_hwndOverlay != nullptr && IsWindowVisible(g_hwndOverlay); }

void Shutdown()
{
    g_running = false;
    if (g_hwndMsg) PostMessageW(g_hwndMsg, WM_QUIT, 0, 0);
    Log(L"Shutdown");
}

} // namespace Overlay
} // namespace GIEnhancer
