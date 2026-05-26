#pragma once
#include <string>
#include <vector>

namespace GIEnhancer {
namespace ReShadeConfig {

struct ConfigData {
    // [GENERAL]
    std::wstring effectSearchPaths;    // E:\GameData\ReShade\Shaders\Effects
    std::wstring textureSearchPaths;   // E:\GameData\ReShade\Shaders\Textures
    std::wstring presetPath;           // current preset
    std::wstring intermediateCachePath;// E:\GameData\ReShade\Cache

    // [INPUT]
    int keyOverlay;   // Home=36
    int keyEffects;   // toggle effects
    int keyReload;    // reload shaders

    // [STELLA]
    std::wstring configVersion;

    // Status
    int shaderCount;
    int textureCount;
    bool iniLoaded;
    bool rtlbaseLoaded;
    std::wstring gameDir;
};

// Load ReShade.ini from game directory
bool Load(const wchar_t* gameDir);

// Detect rtlbase.dll in process
bool DetectRtlbase();

// Get config data
const ConfigData& GetConfig();

// Count shaders in effect search paths
int CountShaders();

} // namespace ReShadeConfig
} // namespace GIEnhancer
