#include "AntiCheat.h"

namespace GIEnhancer {
namespace AntiCheat {

LDR_DATA_TABLE_ENTRY_EX* FindLdrEntry(HMODULE hModule)
{
    PEB_EX* peb = GetPEB();
    if (!peb) return nullptr;

    PEB_LDR_DATA_EX* ldr = peb->Ldr;
    if (!ldr) return nullptr;

    LIST_ENTRY* head = &ldr->InMemoryOrderModuleList;
    LIST_ENTRY* entry = head->Flink;

    while (entry && entry != head)
    {
        auto* ldrEntry = CONTAINING_RECORD(entry, LDR_DATA_TABLE_ENTRY_EX, InMemoryOrderLinks);
        if (ldrEntry->DllBase == hModule)
            return ldrEntry;
        entry = entry->Flink;
    }
    return nullptr;
}

bool UnlinkModuleFromPEB(HMODULE hModule)
{
    auto* entry = FindLdrEntry(hModule);
    if (!entry) return false;

    // 从 InMemoryOrderModuleList 摘除
    LIST_ENTRY* links = &entry->InMemoryOrderLinks;
    LIST_ENTRY* prev = links->Blink;
    LIST_ENTRY* next = links->Flink;
    if (prev) prev->Flink = next;
    if (next) next->Blink = prev;

    // 从 InLoadOrderModuleList 摘除
    LIST_ENTRY* loadOrder = &entry->InLoadOrderLinks;
    LIST_ENTRY* loPrev = loadOrder->Blink;
    LIST_ENTRY* loNext = loadOrder->Flink;
    if (loPrev) loPrev->Flink = loNext;
    if (loNext) loNext->Blink = loPrev;

    // 从 InInitializationOrderModuleList 摘除
    LIST_ENTRY* initOrder = &entry->InInitializationOrderLinks;
    LIST_ENTRY* ioPrev = initOrder->Blink;
    LIST_ENTRY* ioNext = initOrder->Flink;
    if (ioPrev) ioPrev->Flink = ioNext;
    if (ioNext) ioNext->Blink = ioPrev;

    return true;
}

} // namespace AntiCheat
} // namespace GIEnhancer
