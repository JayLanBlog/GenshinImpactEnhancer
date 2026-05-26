#include "ReShadeConfig.h"
#include <Windows.h>
#include <stdio.h>
#include <Psapi.h>

namespace GIEnhancer {
namespace ReShadeConfig {

static ConfigData g_config;

static std::wstring GetIniValue(const std::wstring& section, const std::wstring& key, const std::wstring& filePath)
{
    std::wstring result;
    wchar_t buf[4096] = {};
    DWORD len = GetPrivateProfileStringW(section.c_str(), key.c_str(), L"", buf, 4096, filePath.c_str());
    if (len > 0) result = buf;
    return result;
}

static int GetIniInt(const std::wstring& section, const std::wstring& key, const std::wstring& filePath)
{
    return GetPrivateProfileIntW(section.c_str(), key.c_str(), 0, filePath.c_str());
}

bool Load(const wchar_t* gameDir)
{
    g_config = {};
    g_config.gameDir = gameDir;
    g_config.iniLoaded = false;

    std::wstring iniPath = std::wstring(gameDir) + L"\\ReShade.ini";
    if (GetFileAttributesW(iniPath.c_str()) == INVALID_FILE_ATTRIBUTES)
        return false;

    g_config.effectSearchPaths = GetIniValue(L"GENERAL", L"EffectSearchPaths", iniPath);
    g_config.textureSearchPaths = GetIniValue(L"GENERAL", L"TextureSearchPaths", iniPath);
    g_config.presetPath = GetIniValue(L"GENERAL", L"PresetPath", iniPath);
    g_config.intermediateCachePath = GetIniValue(L"GENERAL", L"IntermediateCachePath", iniPath);
    g_config.keyOverlay = GetIniInt(L"INPUT", L"KeyOverlay", iniPath);
    g_config.keyEffects = GetIniInt(L"INPUT", L"KeyEffects", iniPath);
    g_config.keyReload = GetIniInt(L"INPUT", L"KeyReload", iniPath);
    g_config.configVersion = GetIniValue(L"STELLA", L"ConfigVersion", iniPath);

    g_config.shaderCount = CountShaders();
    g_config.rtlbaseLoaded = DetectRtlbase();
    g_config.iniLoaded = true;
    return true;
}

bool DetectRtlbase()
{
    HMODULE hMods[1024];
    DWORD cbNeeded;
    HANDLE hProc = GetCurrentProcess();

    if (EnumProcessModules(hProc, hMods, sizeof(hMods), &cbNeeded))
    {
        for (DWORD i = 0; i < (cbNeeded / sizeof(HMODULE)); i++)
        {
            wchar_t name[MAX_PATH] = {};
            GetModuleFileNameExW(hProc, hMods[i], name, MAX_PATH);
            if (wcsstr(name, L"rtlbase"))
                return true;
        }
    }
    return false;
}

int CountShaders()
{
    if (g_config.effectSearchPaths.empty()) return 0;

    int count = 0;
    std::wstring searchPath = g_config.effectSearchPaths + L"\\*.fx";
    WIN32_FIND_DATAW fd;
    HANDLE hFind = FindFirstFileW(searchPath.c_str(), &fd);
    if (hFind != INVALID_HANDLE_VALUE)
    {
        do { count++; } while (FindNextFileW(hFind, &fd));
        FindClose(hFind);
    }
    return count;
}

const ConfigData& GetConfig() { return g_config; }

} // namespace ReShadeConfig
} // namespace GIEnhancer
