#include <Windows.h>
#include "AntiCheat.h"
#include "Overlay.h"
#include "ReShadeConfig.h"
#include <stdio.h>

static void Log(const wchar_t* msg)
{
    FILE* f = nullptr;
    _wfopen_s(&f, L"C:\\Users\\86178\\AppData\\Local\\GenshinStellaMod\\overlay_debug.log", L"a");
    if (f) { fwprintf_s(f, L"%s\n", msg); fclose(f); }
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved)
{
    switch (reason) {
    case DLL_PROCESS_ATTACH:
    {
        DisableThreadLibraryCalls(hModule);

        // Load ReShade config first
        wchar_t gameDir[MAX_PATH];
        GetCurrentDirectoryW(MAX_PATH, gameDir);
        bool reshadeIni = Stella::ReShadeConfig::Load(gameDir);
        Log(reshadeIni ? L"DllMain: ReShade.ini loaded" : L"DllMain: ReShade.ini not found");

        // Detect rtlbase
        bool rtlLoaded = Stella::ReShadeConfig::DetectRtlbase();
        Log(rtlLoaded ? L"DllMain: rtlbase.dll detected" : L"DllMain: rtlbase.dll NOT in process");

        if (rtlLoaded) {
            // SKIP PEB unlinking when rtlbase is present.
            // Unlinking can corrupt LDR list that rtlbase's D3D11 hooks depend on.
            // ReShade's own anti-detection (d3dx.ini) handles this layer.
            Log(L"DllMain: rtlbase present - skipping PEB unlinking (safety)");
        } else {
            Stella::AntiCheat::UnlinkModuleFromPEB(hModule);
            Log(L"DllMain: PEB module unlinked");
        }

        Stella::Overlay::CreateAndShow(hModule);
        Log(L"DllMain: overlay thread started");
        break;
    }
    case DLL_PROCESS_DETACH:
    {
        Stella::Overlay::Shutdown();
        break;
    }
    }
    return TRUE;
}
