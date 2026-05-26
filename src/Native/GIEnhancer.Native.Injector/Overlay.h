#pragma once
#include <Windows.h>

namespace GIEnhancer {
namespace Overlay {

bool CreateAndShow(HMODULE hDllModule);
void Toggle();
void Shutdown();

} // namespace Overlay
} // namespace GIEnhancer
