#include <Windows.h>
#include "AntiCheat.h"
#include "Overlay.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved)
{
    switch (reason) {
    case DLL_PROCESS_ATTACH:
    {
        DisableThreadLibraryCalls(hModule);
        Stella::AntiCheat::UnlinkModuleFromPEB(hModule);
        Stella::Overlay::CreateAndShow(hModule);
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
