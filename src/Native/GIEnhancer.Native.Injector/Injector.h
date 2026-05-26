#pragma once
#include <Windows.h>
#include <string>

namespace GIEnhancer {
namespace Native {

class Injector {
public:
    static BOOL CreateSuspendedProcess(
        const std::wstring& exePath,
        const std::wstring& workingDir,
        HANDLE* outProcessHandle,
        HANDLE* outThreadHandle,
        DWORD* outProcessId
    );

    static BOOL InjectDll(HANDLE processHandle, const std::wstring& dllPath);

    static BOOL ResumeProcess(HANDLE threadHandle);

    static BOOL EjectDll(HANDLE processHandle, const std::wstring& dllName);

    static BOOL IsDllLoaded(DWORD processId, const std::wstring& dllName);

private:
    static std::wstring GetLastErrorAsString();
};

}
}
