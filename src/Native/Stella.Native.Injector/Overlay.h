#pragma once
#include <Windows.h>

namespace Stella {
namespace Overlay {

bool CreateAndShow(HMODULE hDllModule);
void Toggle();
void Shutdown();

} // namespace Overlay
} // namespace Stella
