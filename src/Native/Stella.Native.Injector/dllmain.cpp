#include <Windows.h>
#include "AntiCheat.h"

HMODULE g_hModule = nullptr;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved)
{
    switch (reason) {
    case DLL_PROCESS_ATTACH:
    {
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);

        // ============================================
        //  反作弊绕过：PEB 模块隐藏
        //  在 DLL 加载后立即从 LDR 链表摘除自身
        //  之后 EnumProcessModules / CreateToolhelp32Snapshot
        //  都发现不了本 DLL
        // ============================================
        Stella::AntiCheat::UnlinkModuleFromPEB(hModule);

        // TODO: 后续在此处初始化 DXGI Hook
        // - Hook IDXGISwapChain::Present  → 插入 ReShade 着色器管线
        // - Hook IDXGISwapChain::ResizeBuffers → 处理窗口大小变化
        // - 渲染 Overlay (FPS 计数器 / 性能数据)

        break;
    }
    case DLL_PROCESS_DETACH:
    {
        // TODO: 卸载 DXGI Hook，释放资源
        break;
    }
    }
    return TRUE;
}
