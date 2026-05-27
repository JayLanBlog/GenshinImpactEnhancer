#include <Windows.h>
#include "AntiCheat.h"
#include "Overlay.h"
#include "ReShadeConfig.h"
#include "MigotoConfig.h"
#include <stdio.h>

static void Log(const wchar_t* msg)
{
    FILE* f = nullptr;
    _wfopen_s(&f, L"C:\\Users\\86178\\AppData\\Local\\GenshinImpactEnhancer\\overlay_debug.log", L"a");
    if (f) { fwprintf_s(f, L"%s\n", msg); fclose(f); }
}

// Global flag to prevent multiple DllMain initializations.
// Unity may load/unload the DLL multiple times via LoadLibrary/FreeLibrary.
// Only the FIRST DLL_PROCESS_ATTACH should perform initialization.
static volatile LONG g_initialized = 0;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved)
{
    switch (reason) {
    case DLL_PROCESS_ATTACH:
    {
        DisableThreadLibraryCalls(hModule);

        // Prevent re-initialization if already loaded
        if (InterlockedCompareExchange(&g_initialized, 1, 0) != 0)
        {
            Log(L"DllMain: SKIP - already initialized (Unity reload)");
            return TRUE;
        }

        Log(L"DllMain: FIRST initialization");

        // Load ReShade config first
        wchar_t gameDir[MAX_PATH];
        GetCurrentDirectoryW(MAX_PATH, gameDir);
        bool reshadeIni = GIEnhancer::ReShadeConfig::Load(gameDir);
        Log(reshadeIni ? L"DllMain: ReShade.ini loaded" : L"DllMain: ReShade.ini not found");

        // Load 3DMigoto config
        bool migotoIni = GIEnhancer::MigotoConfig::Load(gameDir);
        Log(migotoIni ? L"DllMain: d3dx.ini loaded" : L"DllMain: d3dx.ini not found");

        // Detect rtlbase
        bool rtlLoaded = GIEnhancer::ReShadeConfig::DetectRtlbase();
        Log(rtlLoaded ? L"DllMain: rtlbase.dll detected" : L"DllMain: rtlbase.dll NOT in process");

        if (rtlLoaded) {
            // SKIP PEB unlinking when rtlbase is present.
            // Unlinking can corrupt LDR list that rtlbase's D3D11 hooks depend on.
            // ReShade's own anti-detection (d3dx.ini) handles this layer.
            Log(L"DllMain: rtlbase present - skipping PEB unlinking (safety)");
        } else {
            GIEnhancer::AntiCheat::UnlinkModuleFromPEB(hModule);
            Log(L"DllMain: PEB module unlinked");
        }

        GIEnhancer::Overlay::CreateAndShow(hModule);
        Log(L"DllMain: overlay thread started");
        break;
    }
    case DLL_PROCESS_DETACH:
    {
        // Only shutdown if we were the one who initialized
        if (g_initialized)
        {
            GIEnhancer::Overlay::Shutdown();
            Log(L"DllMain: shutdown complete");
        }
        break;
    }
    }
    return TRUE;
}
