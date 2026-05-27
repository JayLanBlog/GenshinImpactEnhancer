#pragma once
#include <string>
#include <map>

namespace GIEnhancer {
namespace MigotoConfig {

struct MigotoData {
    bool iniLoaded;
    bool huntingEnabled;
    int huntingMode;       // 0=off, 1=always, 2=hotkey toggle
    bool d3d11Present;     // d3d11.dll exists in game dir
    bool d3dxIniPresent;   // d3dx.ini exists in game dir
    std::wstring gameDir;

    // Key hotkey bindings (key_name -> virtual key code)
    std::map<std::wstring, int> hotkeys;
};

// Load d3dx.ini from game directory
bool Load(const wchar_t* gameDir);

// Get config data
const MigotoData& GetConfig();

} // namespace MigotoConfig
} // namespace GIEnhancer
