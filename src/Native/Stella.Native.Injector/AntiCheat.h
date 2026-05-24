#pragma once
#include <Windows.h>

//
// Genshin Stella Mod — 反作弊绕过模块
//
// 原理：
//   1. CREATE_SUSPENDED 注入 → DLL 在反作弊初始化前加载
//   2. PEB 模块脱链 → 从 LDR 链表中移除，EnumProcessModules 等 API 不可见
//   3. 仅 Hook 渲染层 (DXGI Present/ResizeBuffers) → 不碰游戏逻辑/内存
//

// ── 未公开的 Windows 内部结构体 ──

typedef struct _UNICODE_STRING_EX {
    USHORT Length;
    USHORT MaximumLength;
    PWSTR  Buffer;
} UNICODE_STRING_EX;

typedef struct _LDR_DATA_TABLE_ENTRY_EX {
    LIST_ENTRY InLoadOrderLinks;
    LIST_ENTRY InMemoryOrderLinks;
    LIST_ENTRY InInitializationOrderLinks;
    PVOID DllBase;
    PVOID EntryPoint;
    ULONG SizeOfImage;
    UNICODE_STRING_EX FullDllName;
    UNICODE_STRING_EX BaseDllName;
} LDR_DATA_TABLE_ENTRY_EX;

typedef struct _PEB_LDR_DATA_EX {
    ULONG Length;
    BOOLEAN Initialized;
    PVOID SsHandle;
    LIST_ENTRY InLoadOrderModuleList;
    LIST_ENTRY InMemoryOrderModuleList;
    LIST_ENTRY InInitializationOrderModuleList;
} PEB_LDR_DATA_EX;

typedef struct _PEB_EX {
    BOOLEAN InheritedAddressSpace;
    BOOLEAN ReadImageFileExecOptions;
    BOOLEAN BeingDebugged;
    BOOLEAN SpareBool;
    PVOID Mutant;
    PVOID ImageBaseAddress;
    PEB_LDR_DATA_EX* Ldr;
} PEB_EX;

namespace Stella {
namespace AntiCheat {

/// <summary>
/// 从 PEB 的模块链表中摘除当前模块。
/// DLL 仍正常工作，但不再被进程模块枚举 API 发现。
/// </summary>
bool UnlinkModuleFromPEB(HMODULE hModule);

/// <summary>
/// 获取 PEB 地址（x64）
/// </summary>
inline PEB_EX* GetPEB()
{
    return reinterpret_cast<PEB_EX*>(__readgsqword(0x60));
}

/// <summary>
/// 通过遍历 PEB LDR 链表找到指定模块的 LDR_DATA_TABLE_ENTRY。
/// </summary>
LDR_DATA_TABLE_ENTRY_EX* FindLdrEntry(HMODULE hModule);

} // namespace AntiCheat
} // namespace Stella
