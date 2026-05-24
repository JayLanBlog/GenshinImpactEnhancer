using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

Console.WriteLine("=== Genshin Stella Mod: 注入测试 ===\n");

// 1. 找 DLL 路径
var dllPath = Path.GetFullPath(
    @"e:\AI\hook\src\Native\Stella.Native.Injector\x64\Release\Stella.Native.Injector.dll");

if (!File.Exists(dllPath))
{
    Console.WriteLine($"❌ DLL 未找到: {dllPath}");
    return 1;
}
Console.WriteLine($"✅ DLL 已找到: {dllPath}");

// 2. 以挂起模式启动 Notepad
Console.WriteLine("\n📌 正在以挂起模式启动 Notepad...");

var si = new STARTUPINFO();
si.cb = (uint)Marshal.SizeOf<STARTUPINFO>();
var pi = new PROCESS_INFORMATION();

bool created = Win32.CreateProcessW(
    null, "notepad.exe",
    IntPtr.Zero, IntPtr.Zero, false,
    Win32.CREATE_SUSPENDED,
    IntPtr.Zero, null,
    ref si, out pi);

if (!created)
{
    Console.WriteLine($"❌ 创建进程失败: {Marshal.GetLastWin32Error()}");
    return 1;
}
Console.WriteLine($"✅ Notepad 已挂起启动 (PID: {pi.dwProcessId})");

// 3. 打开进程句柄
IntPtr hProcess = Win32.OpenProcess(Win32.PROCESS_ALL_ACCESS, false, pi.dwProcessId);
if (hProcess == IntPtr.Zero)
{
    Console.WriteLine($"❌ OpenProcess 失败: {Marshal.GetLastWin32Error()}");
    Win32.ResumeThread(pi.hThread);
    return 1;
}
Console.WriteLine("✅ 已获取进程句柄");

// 4. 分配内存并写入 DLL 路径
byte[] dllPathBytes = Encoding.Unicode.GetBytes(dllPath + '\0');
uint pathSize = (uint)dllPathBytes.Length;
IntPtr remoteMem = Win32.VirtualAllocEx(
    hProcess, IntPtr.Zero, pathSize,
    Win32.MEM_COMMIT | Win32.MEM_RESERVE,
    Win32.PAGE_READWRITE);

if (remoteMem == IntPtr.Zero)
{
    Console.WriteLine($"❌ VirtualAllocEx 失败: {Marshal.GetLastWin32Error()}");
    Win32.CloseHandle(hProcess);
    Win32.ResumeThread(pi.hThread);
    return 1;
}

Win32.WriteProcessMemory(hProcess, remoteMem, dllPathBytes, pathSize, out _);
Console.WriteLine("✅ DLL 路径已写入目标进程内存");

// 5. CreateRemoteThread → LoadLibraryW
IntPtr kernel32 = Win32.GetModuleHandle("kernel32.dll");
IntPtr loadLibAddr = Win32.GetProcAddress(kernel32, "LoadLibraryW");
Console.WriteLine($"📍 LoadLibraryW 地址: 0x{loadLibAddr.ToInt64():X}");

IntPtr hRemoteThread = Win32.CreateRemoteThread(
    hProcess, IntPtr.Zero, 0, loadLibAddr, remoteMem, 0, IntPtr.Zero);

if (hRemoteThread == IntPtr.Zero)
{
    Console.WriteLine($"❌ CreateRemoteThread 失败: {Marshal.GetLastWin32Error()}");
    Win32.VirtualFreeEx(hProcess, remoteMem, 0, Win32.MEM_RELEASE);
    Win32.CloseHandle(hProcess);
    Win32.ResumeThread(pi.hThread);
    return 1;
}
Console.WriteLine("✅ 远程线程已创建");

// 6. 等待注入完成
Console.WriteLine("⏳ 等待 DLL 加载...");
Win32.WaitForSingleObject(hRemoteThread, 10000);
Win32.GetExitCodeThread(hRemoteThread, out uint exitCode);
Win32.CloseHandle(hRemoteThread);

if (exitCode == 0)
    Console.WriteLine("❌ DLL 注入失败 (LoadLibrary 返回 NULL)");
else
    Console.WriteLine($"✅ DLL 注入成功! LoadLibrary 返回: 0x{exitCode:X}");

// 7. 验证 DLL 在目标进程中
Console.WriteLine("\n🔍 枚举目标进程模块...");
var modules = new IntPtr[2048];
Win32.EnumProcessModulesEx(hProcess, modules,
    (uint)(modules.Length * IntPtr.Size), out uint needed, 0x03);
int moduleCount = (int)(needed / (uint)IntPtr.Size);
bool dllFound = false;
Console.WriteLine($"  共 {moduleCount} 个模块，正在搜索 Stella.Native.Injector...");
for (int i = 0; i < moduleCount; i++)
{
    var sb = new StringBuilder(512);
    Win32.GetModuleFileNameEx(hProcess, modules[i], sb, 512);
    var name = sb.ToString();
    var shortName = Path.GetFileName(name);
    if (shortName.Contains("Stella", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Stella", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  ✅ 找到: {shortName}");
        Console.WriteLine($"     完整路径: {name}");
        dllFound = true;
        break;
    }
}

if (!dllFound)
    Console.WriteLine("  ⚠️ 未在模块列表中找到注入的 DLL");

// 8. 清理
Console.WriteLine("\n🧹 清理...");
Win32.VirtualFreeEx(hProcess, remoteMem, 0, Win32.MEM_RELEASE);
Win32.CloseHandle(hProcess);
Win32.ResumeThread(pi.hThread);
Console.WriteLine("✅ Notepad 进程已恢复");
Thread.Sleep(500);

try
{
    var p = Process.GetProcessById(pi.dwProcessId);
    p.Kill();
    Console.WriteLine("✅ Notepad 已关闭");
}
catch { }

Console.WriteLine($"\n=== 注入测试完成: {(dllFound ? "✅ 成功" : "⚠️ 部分成功")} ===");
return dllFound ? 0 : 1;


// ── Win32 P/Invoke ──
static class Win32
{
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool b, int c);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, uint s, uint t, uint f);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, uint s, out UIntPtr w);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateRemoteThread(IntPtr p, IntPtr a, uint s, IntPtr f, IntPtr x, uint c, IntPtr t);
    [DllImport("kernel32.dll")] public static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] public static extern uint ResumeThread(IntPtr h);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string n);
    [DllImport("kernel32.dll")] public static extern IntPtr GetProcAddress(IntPtr m, string n);
    [DllImport("kernel32.dll")] public static extern bool GetExitCodeThread(IntPtr h, out uint c);
    [DllImport("kernel32.dll")] public static extern bool VirtualFreeEx(IntPtr p, IntPtr a, uint s, uint t);
    [DllImport("psapi.dll", SetLastError = true)] public static extern bool EnumProcessModulesEx(IntPtr p, [Out] IntPtr[] m, uint c, out uint n, uint f);
    [DllImport("psapi.dll", SetLastError = true)] public static extern uint GetModuleFileNameEx(IntPtr p, IntPtr m, StringBuilder n, uint s);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool CreateProcessW(string? an, string? cl, IntPtr pa, IntPtr ta, bool ih, uint cf, IntPtr e, string? cd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    public const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
    public const uint MEM_COMMIT = 0x1000;
    public const uint MEM_RESERVE = 0x2000;
    public const uint MEM_RELEASE = 0x8000;
    public const uint PAGE_READWRITE = 0x04;
    public const uint CREATE_SUSPENDED = 0x00000004;
}

[StructLayout(LayoutKind.Sequential)]
struct STARTUPINFO
{
    public uint cb;
    public string lpReserved; public string lpDesktop; public string lpTitle;
    public uint dwX; public uint dwY; public uint dwXSize; public uint dwYSize;
    public uint dwXCountChars; public uint dwYCountChars;
    public uint dwFillAttribute; public uint dwFlags;
    public ushort wShowWindow; public ushort cbReserved2;
    public IntPtr lpReserved2; public IntPtr hStdInput;
    public IntPtr hStdOutput; public IntPtr hStdError;
}

[StructLayout(LayoutKind.Sequential)]
struct PROCESS_INFORMATION
{
    public IntPtr hProcess; public IntPtr hThread;
    public int dwProcessId; public int dwThreadId;
}
