#include "MigotoConfig.h"
#include <Windows.h>
#include <stdio.h>

namespace GIEnhancer {
namespace MigotoConfig {

static MigotoData g_migoto;

static std::wstring GetIniValue(const std::wstring& section, const std::wstring& key, const std::wstring& filePath)
{
    wchar_t buf[4096] = {};
    DWORD len = GetPrivateProfileStringW(section.c_str(), key.c_str(), L"", buf, 4096, filePath.c_str());
    if (len > 0) return buf;
    return L"";
}

static int GetIniInt(const std::wstring& section, const std::wstring& key, const std::wstring& filePath)
{
    return GetPrivateProfileIntW(section.c_str(), key.c_str(), 0, filePath.c_str());
}

// Parse 3DMigoto virtual key string to VK code
static int ParseVKey(const std::wstring& value)
{
    if (value.empty()) return 0;

    // Hex format "0x24"
    if (value.size() > 2 && value[0] == L'0' && (value[1] == L'x' || value[1] == L'X'))
    {
        try { return std::stoi(value, nullptr, 16); }
        catch (...) {}
    }

    // Single character A-Z, 0-9
    if (value.size() == 1)
    {
        wchar_t c = towupper(value[0]);
        if (c >= L'A' && c <= L'Z') return (int)c;
        if (c >= L'0' && c <= L'9') return (int)c;
    }

    // Named keys
    struct VKeyEntry { const wchar_t* name; int vk; };
    static const VKeyEntry entries[] = {
        {L"HOME", 0x24}, {L"END", 0x23}, {L"INSERT", 0x2D}, {L"DELETE", 0x2E},
        {L"F1", 0x70}, {L"F2", 0x71}, {L"F3", 0x72}, {L"F4", 0x73},
        {L"F5", 0x74}, {L"F6", 0x75}, {L"F7", 0x76}, {L"F8", 0x77},
        {L"F9", 0x78}, {L"F10", 0x79}, {L"F11", 0x7A}, {L"F12", 0x7B},
        {L"F13", 0x7C}, {L"F14", 0x7D}, {L"F15", 0x7E}, {L"F16", 0x7F},
        {L"F17", 0x80}, {L"F18", 0x81}, {L"F19", 0x82}, {L"F20", 0x83},
        {L"F21", 0x84}, {L"F22", 0x85}, {L"F23", 0x86}, {L"F24", 0x87},
        {L"PAGEUP", 0x21}, {L"PGUP", 0x21}, {L"PAGEDOWN", 0x22}, {L"PGDN", 0x22},
        {L"PRINTSCREEN", 0x2C}, {L"SNAPSHOT", 0x2C}, {L"PRNT SCR", 0x2C}, {L"PRNTSCRN", 0x2C},
        {L"ESCAPE", 0x1B}, {L"ESC", 0x1B}, {L"TAB", 0x09}, {L"SPACE", 0x20},
        {L"RETURN", 0x0D}, {L"ENTER", 0x0D}, {L"BACKSPACE", 0x08}, {L"BACK", 0x08},
        {L"SCROLL", 0x91}, {L"SCROLLLOCK", 0x91}, {L"NUMLOCK", 0x90},
        {L"PAUSE", 0x13}, {L"BREAK", 0x13},
        {L"NUMPAD0", 0x60}, {L"NUMPAD1", 0x61}, {L"NUMPAD2", 0x62},
        {L"NUMPAD3", 0x63}, {L"NUMPAD4", 0x64}, {L"NUMPAD5", 0x65},
        {L"NUMPAD6", 0x66}, {L"NUMPAD7", 0x67}, {L"NUMPAD8", 0x68},
        {L"NUMPAD9", 0x69},
        {L"MULTIPLY", 0x6A}, {L"ADD", 0x6B}, {L"SUBTRACT", 0x6D},
        {L"DIVIDE", 0x6F}, {L"DECIMAL", 0x6E},
        // Legacy friendly names
        {L"NUM 0", 0x60}, {L"NUM 1", 0x61}, {L"NUM 2", 0x62},
        {L"NUM 3", 0x63}, {L"NUM 4", 0x64}, {L"NUM 5", 0x65},
        {L"NUM 6", 0x66}, {L"NUM 7", 0x67}, {L"NUM 8", 0x68},
        {L"NUM 9", 0x69},
    };

    std::wstring upper = value;
    for (auto& c : upper) c = towupper(c);

    // VK_ prefix
    if (upper.size() > 3 && upper.substr(0, 3) == L"VK_")
    {
        std::wstring name = upper.substr(3);
        for (const auto& e : entries)
            if (name == e.name) return e.vk;
    }

    for (const auto& e : entries)
        if (upper == e.name) return e.vk;

    return 0;
}

bool Load(const wchar_t* gameDir)
{
    g_migoto = {};
    g_migoto.gameDir = gameDir;
    g_migoto.iniLoaded = false;

    // Check if d3dx.ini exists
    std::wstring iniPath = std::wstring(gameDir) + L"\\d3dx.ini";
    g_migoto.d3dxIniPresent = (GetFileAttributesW(iniPath.c_str()) != INVALID_FILE_ATTRIBUTES);

    // Check if d3d11.dll exists (3DMigoto proxy)
    std::wstring d3d11Path = std::wstring(gameDir) + L"\\d3d11.dll";
    g_migoto.d3d11Present = (GetFileAttributesW(d3d11Path.c_str()) != INVALID_FILE_ATTRIBUTES);

    if (!g_migoto.d3dxIniPresent)
        return false;

    // Read hunting mode
    g_migoto.huntingMode = GetIniInt(L"Hunting", L"hunting", iniPath);
    g_migoto.huntingEnabled = (g_migoto.huntingMode > 0);

    // Read hotkey bindings from [Hunting] section
    const wchar_t* hotkeyNames[] = {
        L"next_pixelshader", L"previous_pixelshader", L"mark_pixelshader",
        L"next_vertexshader", L"previous_vertexshader", L"mark_vertexshader",
        L"next_indexbuffer", L"previous_indexbuffer", L"mark_indexbuffer",
        L"next_rendertarget", L"previous_rendertarget", L"mark_rendertarget",
        L"take_screenshot", L"tune_up", L"tune_down",
    };

    for (const auto& name : hotkeyNames)
    {
        std::wstring val = GetIniValue(L"Hunting", name, iniPath);
        if (!val.empty())
        {
            int vk = ParseVKey(val);
            if (vk > 0)
                g_migoto.hotkeys[name] = vk;
        }
    }

    g_migoto.iniLoaded = true;
    return true;
}

const MigotoData& GetConfig() { return g_migoto; }

} // namespace MigotoConfig
} // namespace GIEnhancer
