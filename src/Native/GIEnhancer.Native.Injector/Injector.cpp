#include "Injector.h"
#include <TlHelp32.h>
#include <Psapi.h>

namespace GIEnhancer {
namespace Native {

BOOL Injector::CreateSuspendedProcess(
    const std::wstring& exePath,
    const std::wstring& workingDir,
    HANDLE* outProcessHandle,
    HANDLE* outThreadHandle,
    DWORD* outProcessId)
{
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi = {};

    BOOL success = CreateProcessW(
        exePath.c_str(),
        NULL,
        NULL,
        NULL,
        FALSE,
        CREATE_SUSPENDED,
        NULL,
        workingDir.empty() ? NULL : workingDir.c_str(),
        &si,
        &pi
    );

    if (success) {
        *outProcessHandle = pi.hProcess;
        *outThreadHandle = pi.hThread;
        *outProcessId = pi.dwProcessId;
    }
    return success;
}

BOOL Injector::InjectDll(HANDLE processHandle, const std::wstring& dllPath)
{
    size_t pathSize = (dllPath.length() + 1) * sizeof(wchar_t);
    LPVOID remoteMemory = VirtualAllocEx(
        processHandle, NULL, pathSize,
        MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE
    );
    if (!remoteMemory) return FALSE;

    if (!WriteProcessMemory(processHandle, remoteMemory, dllPath.c_str(), pathSize, NULL)) {
        VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);
        return FALSE;
    }

    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    LPTHREAD_START_ROUTINE loadLibraryAddr =
        (LPTHREAD_START_ROUTINE)GetProcAddress(kernel32, "LoadLibraryW");
    if (!loadLibraryAddr) {
        VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);
        return FALSE;
    }

    HANDLE remoteThread = CreateRemoteThread(
        processHandle, NULL, 0,
        loadLibraryAddr, remoteMemory, 0, NULL
    );
    if (!remoteThread) {
        VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);
        return FALSE;
    }

    WaitForSingleObject(remoteThread, 10000);
    DWORD exitCode = 0;
    GetExitCodeThread(remoteThread, &exitCode);
    CloseHandle(remoteThread);
    VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);
    return exitCode != 0;
}

BOOL Injector::ResumeProcess(HANDLE threadHandle)
{
    return ResumeThread(threadHandle) != (DWORD)-1;
}

BOOL Injector::EjectDll(HANDLE processHandle, const std::wstring& dllName)
{
    return TRUE;
}

BOOL Injector::IsDllLoaded(DWORD processId, const std::wstring& dllName)
{
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
    if (snapshot == INVALID_HANDLE_VALUE) return FALSE;

    MODULEENTRY32W me = { sizeof(me) };
    BOOL found = FALSE;
    if (Module32FirstW(snapshot, &me)) {
        do {
            if (_wcsicmp(me.szModule, dllName.c_str()) == 0) {
                found = TRUE;
                break;
            }
        } while (Module32NextW(snapshot, &me));
    }
    CloseHandle(snapshot);
    return found;
}

std::wstring Injector::GetLastErrorAsString()
{
    DWORD error = GetLastError();
    if (error == 0) return L"No error";
    LPWSTR msgBuf = nullptr;
    FormatMessageW(
        FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL, error, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
        (LPWSTR)&msgBuf, 0, NULL
    );
    std::wstring msg = msgBuf ? msgBuf : L"Unknown error";
    LocalFree(msgBuf);
    return msg;
}

}
}
