#pragma once
#include <Windows.h>

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

namespace GIEnhancer {
namespace AntiCheat {

bool UnlinkModuleFromPEB(HMODULE hModule);

inline PEB_EX* GetPEB()
{
    return reinterpret_cast<PEB_EX*>(__readgsqword(0x60));
}

LDR_DATA_TABLE_ENTRY_EX* FindLdrEntry(HMODULE hModule);

} // namespace AntiCheat
} // namespace GIEnhancer
