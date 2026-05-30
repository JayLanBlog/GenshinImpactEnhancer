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

static volatile LONG g_initialized = 0;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved)
{
    switch (reason) {
    case DLL_PROCESS_ATTACH:
    {
        DisableThreadLibraryCalls(hModule);

        if (InterlockedCompareExchange(&g_initialized, 1, 0) != 0)
            return TRUE;

        Log(L"DllMain: FIRST initialization");

        wchar_t gameDir[MAX_PATH];
        GetCurrentDirectoryW(MAX_PATH, gameDir);

        // Load configs
        GIEnhancer::ReShadeConfig::Load(gameDir);
        GIEnhancer::MigotoConfig::Load(gameDir);

        // Detect rtlbase
        bool rtlLoaded = GIEnhancer::ReShadeConfig::DetectRtlbase();
        Log(rtlLoaded ? L"DllMain: rtlbase.dll detected" : L"DllMain: rtlbase.dll NOT in process");

        // PEB-unlink GIEnhancer (ALWAYS)
        GIEnhancer::AntiCheat::UnlinkModuleFromPEB(hModule);
        Log(L"DllMain: PEB module unlinked (GIEnhancer hidden)");

        // rtlbase (3DMigoto): do NOT PEB-unlink (corrupts D3D hook chain if used as proxy)
        // rtlbase loaded via LoadLibrary = module present, no D3D proxy (no hunting overlay)
        if (rtlLoaded) {
            Log(L"DllMain: rtlbase present - LoadLibrary mode (no D3D proxy/hunting overlay)");
        }

        GIEnhancer::Overlay::CreateAndShow(hModule);
        Log(L"DllMain: overlay thread started");
        break;
    }
    case DLL_PROCESS_DETACH:
    {
        if (g_initialized) {
            GIEnhancer::Overlay::Shutdown();
            Log(L"DllMain: shutdown complete");
        }
        break;
    }
    }
    return TRUE;
}
