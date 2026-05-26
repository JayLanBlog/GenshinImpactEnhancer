using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Principal;

// Self-elevate if not admin
if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
{
    var p = new ProcessStartInfo(System.Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
    try { Process.Start(p); } catch { }
    return;
}

var logDir = @"C:\Users\86178\AppData\Local\GenshinImpactEnhancer";
Directory.CreateDirectory(logDir);
var logFile = Path.Combine(logDir, "injection.log");
var lns = new List<string>();
void L(string s) { try { Console.WriteLine(s); } catch { } lns.Add(s); }
void Flush() { try { File.WriteAllLines(logFile, lns); } catch { } }

try {
L("══════════════════════════════════════════════════");
L("  Genshin Impact Enhancer + ReShade Injection (Resident)");
L("  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
L("══════════════════════════════════════════════════");

var gd = @"D:\TestGame\HoYoPlay\games\Genshin Impact game";
var exe = Path.Combine(gd, "GenshinImpact.exe");
var rlog = Path.Combine(gd, "ReShade.log");
var reshadeDll = @"D:\LaunchGame\data\dependencies\reshade\ReShade64.dll";
var enhancerDll = Path.GetFullPath(@"e:\AI\hook\src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll");

if (!File.Exists(reshadeDll)) { L("MISSING: " + reshadeDll); Flush(); return; }
if (!File.Exists(enhancerDll)) { L("MISSING: " + enhancerDll); Flush(); return; }
if (!File.Exists(exe)) { L("MISSING: " + exe); Flush(); return; }

// Clean old ReShade.log for fresh detection
if (File.Exists(rlog)) File.Delete(rlog);

// ── P/Invoke ──
bool InjectDll(IntPtr hp, string dll)
{
    var pathBytes = Encoding.Unicode.GetBytes(dll + '\0');
    uint sz = (uint)pathBytes.Length;

    // Allocate memory in target process for DLL path
    var remoteMem = VirtualAllocEx(hp, IntPtr.Zero, sz, 0x3000 /* MEM_COMMIT | MEM_RESERVE */, 0x04 /* PAGE_READWRITE */);
    if (remoteMem == IntPtr.Zero)
    {
        L("  VirtualAllocEx FAIL: " + Marshal.GetLastWin32Error());
        return false;
    }

    if (!WriteProcessMemory(hp, remoteMem, pathBytes, sz, out _))
    {
        L("  WriteProcessMemory FAIL: " + Marshal.GetLastWin32Error());
        VirtualFreeEx(hp, remoteMem, 0, 0x8000);
        return false;
    }

    var loadLibAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
    if (loadLibAddr == IntPtr.Zero)
    {
        L("  GetProcAddress(LoadLibraryW) FAIL");
        VirtualFreeEx(hp, remoteMem, 0, 0x8000);
        return false;
    }

    // Create remote thread to call LoadLibraryW with our DLL path
    var ht = CreateRemoteThread(hp, IntPtr.Zero, 0, loadLibAddr, remoteMem, 0, IntPtr.Zero);
    if (ht == IntPtr.Zero)
    {
        L("  CreateRemoteThread FAIL: " + Marshal.GetLastWin32Error());
        VirtualFreeEx(hp, remoteMem, 0, 0x8000);
        return false;
    }

    // Wait for LoadLibraryW to complete (max 15s)
    WaitForSingleObject(ht, 15000);
    GetExitCodeThread(ht, out uint exitCode);
    CloseHandle(ht);
    VirtualFreeEx(hp, remoteMem, 0, 0x8000 /* MEM_RELEASE */);

    return exitCode != 0;
}

// ── Step 1: Launch game SUSPENDED ──
L("[1] Launching game (CREATE_SUSPENDED)...");
var si = new STARTUPINFO();
si.cb = (uint)Marshal.SizeOf<STARTUPINFO>();
var pi = new PROCESS_INFORMATION();

if (!CreateProcessW(exe, null, IntPtr.Zero, IntPtr.Zero, false, 0x4 /* CREATE_SUSPENDED */, IntPtr.Zero, gd, ref si, out pi))
{
    L("FAIL CreateProcess: " + Marshal.GetLastWin32Error());
    Flush();
    return;
}
L("  PID=" + pi.dwProcessId + " TID=" + pi.dwThreadId);

// ── Step 2: Open process handle (keep alive for monitoring) ──
var hp = OpenProcess(0x1F0FFF /* PROCESS_ALL_ACCESS */, false, pi.dwProcessId);
if (hp == IntPtr.Zero)
{
    L("FAIL OpenProcess: " + Marshal.GetLastWin32Error());
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    Flush();
    return;
}

// ── Step 3: Inject ReShade64.dll BEFORE resuming ──
L("[2] Injecting ReShade64.dll...");
bool reshadeOk = InjectDll(hp, reshadeDll);
L(reshadeOk ? "  ReShade64.dll: OK" : "  ReShade64.dll: FAIL");
Thread.Sleep(500);

// ── Step 4: Inject Enhancer DLL BEFORE resuming ──
L("[3] Injecting GIEnhancer.Native.Injector.dll...");
bool enhancerOk = InjectDll(hp, enhancerDll);
L(enhancerOk ? "  Enhancer DLL: OK" : "  Enhancer DLL: FAIL");
Thread.Sleep(500);

// ── Step 5: Verify modules loaded ──
var mods = new IntPtr[4096];
EnumProcessModulesEx(hp, mods, (uint)(mods.Length * IntPtr.Size), out uint nmod, 0x03);
int modCount = (int)(nmod / (uint)IntPtr.Size);
bool enhancerVis = false, reshadeVis = false;
for (int i = 0; i < modCount; i++)
{
    var sb = new StringBuilder(512);
    GetModuleFileNameEx(hp, mods[i], sb, 512);
    var nm = sb.ToString();
    if (nm.Contains("GIEnhancer.Native")) enhancerVis = true;
    if (nm.Contains("ReShade") || nm.Contains("rtlbase")) reshadeVis = true;
}
L($"[4] Module check: Enhancer={(enhancerVis ? "YES" : "NO")} ReShade={(reshadeVis ? "YES" : "NO")} ({modCount} modules)");

// ── Step 6: Resume game main thread ──
L("[5] Resuming game main thread...");
uint prevSuspendCount = ResumeThread(pi.hThread);
L($"  ResumeThread returned: {prevSuspendCount}");
Flush();

// Close the thread handle (no longer needed), but KEEP process handle
CloseHandle(pi.hThread);

// ── Step 7: Wait for ReShade initialization ──
L("[6] Waiting for ReShade initialization...");
bool reshadeReady = false;
for (int i = 1; i <= 60; i++) // wait up to 60s
{
    Thread.Sleep(1000);

    // Check if game is still alive
    try
    {
        using var proc = Process.GetProcessById(pi.dwProcessId);
        if (proc.HasExited)
        {
            L($"  Game exited at {i}s (exit code: {proc.ExitCode})");
            Flush();
            return;
        }
    }
    catch
    {
        L($"  Game process gone at {i}s");
        Flush();
        return;
    }

    // Check ReShade.log
    if (File.Exists(rlog))
    {
        try
        {
            using var fs = new FileStream(rlog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var txt = sr.ReadToEnd();
            if (txt.Length > 200 && txt.Contains("Initialized"))
            {
                reshadeReady = true;
                L($"  ReShade initialized at {i}s");
                break;
            }
        }
        catch { }
    }

    if (i % 10 == 0)
    {
        L($"  ... waiting {i}s");
        Flush();
    }
}

if (!reshadeReady)
{
    L("  WARNING: ReShade did not initialize within 60s (game may still work)");
}

// ── Step 8: Read ReShade.ini for overlay key ──
int vkOverlay = 0x24; // Default: Home
var ini = Path.Combine(gd, "ReShade.ini");
if (File.Exists(ini))
{
    var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(ini), @"KeyOverlay=(\d+),(\d+),(\d+),(\d+)");
    if (m.Success) vkOverlay = int.Parse(m.Groups[1].Value);
}
L($"[7] ReShade overlay key: VK_{vkOverlay} ({(vkOverlay == 0x24 ? "Home" : vkOverlay == 0x2D ? "Insert" : "0x" + vkOverlay.ToString("X2"))})");

// ── Step 9: Send Home key to open ReShade panel (after game fully loaded) ──
L("[8] Waiting 12s for game scene to fully load...");
Thread.Sleep(12000);

// Check game still alive before sending key
try
{
    using var proc = Process.GetProcessById(pi.dwProcessId);
    if (proc.HasExited)
    {
        L("  Game exited before Home key could be sent");
        Flush();
        return;
    }
}
catch
{
    L("  Game process gone before Home key");
    Flush();
    return;
}

L("[9] Sending Home key x3 to open ReShade panel...");
for (int i = 0; i < 3; i++)
{
    keybd_event((byte)vkOverlay, 0x47 /* scancode for Home */, 0 /* key down */, UIntPtr.Zero);
    Thread.Sleep(80);
    keybd_event((byte)vkOverlay, 0x47, 2 /* key up */, UIntPtr.Zero);
    Thread.Sleep(2000);
    L($"  Home key press #{i + 1}");
}
Flush();

// ═════════════════════════════════════════════════
//  RESIDENT MODE: Keep running, monitor game status
// ═════════════════════════════════════════════════
L("");
L("══════════════════════════════════════════════════");
L("  RESIDENT MODE ACTIVE");
L("  Injection complete. Monitoring game...");
L("  Press Ctrl+C in this window to stop monitoring");
L("  (Game will continue running independently)");
L("══════════════════════════════════════════════════");
Flush();

// Register Ctrl+C handler for clean exit
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    L("");
    L("Ctrl+C received. Stopping monitor (game keeps running)...");
    Flush();
    Environment.Exit(0);
};

// Long-term stability monitor
int totalSeconds = 0;
int reportInterval = 300; // Report every 5 minutes
var lastReportTime = DateTime.Now;

while (true)
{
    Thread.Sleep(1000);
    totalSeconds++;

    // Check if game is still alive
    bool gameAlive = false;
    try
    {
        using var proc = Process.GetProcessById(pi.dwProcessId);
        gameAlive = !proc.HasExited;
        if (!gameAlive)
        {
            L($"[!] Game exited at {totalSeconds}s");
            Flush();
            break;
        }
    }
    catch
    {
        L($"[!] Game process gone at {totalSeconds}s");
        Flush();
        break;
    }

    // Periodic status report
    if (totalSeconds % reportInterval == 0)
    {
        try
        {
            using var proc = Process.GetProcessById(pi.dwProcessId);
            var memMB = proc.PrivateMemorySize64 / 1024 / 1024;
            var elapsed = TimeSpan.FromSeconds(totalSeconds);
            L($"[HEARTBEAT] {elapsed:hh\\:mm\\:ss} | PID={pi.dwProcessId} | Memory={memMB}MB | Status=RUNNING");
        }
        catch { }

        // Check ReShade.log for errors
        if (File.Exists(rlog))
        {
            try
            {
                using var fs = new FileStream(rlog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var txt = sr.ReadToEnd();
                int errorCount = System.Text.RegularExpressions.Regex.Matches(txt, @"error|fail|crash", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
                if (errorCount > 0)
                    L($"  ReShade.log: {errorCount} potential issues");
            }
            catch { }
        }
        Flush();
    }
}

L("Monitor stopped.");
}
catch (Exception e)
{
    L("EXCEPTION: " + e.Message);
    if (e.StackTrace != null) L("  " + e.StackTrace);
}
finally
{
    Flush();
}

// ═════════════════════════════════════════════════
//  P/Invoke Declarations
// ═════════════════════════════════════════════════

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

[DllImport("kernel32.dll")]
static extern bool CloseHandle(IntPtr hObject);

[DllImport("kernel32.dll")]
static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

[DllImport("kernel32.dll")]
static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

[DllImport("kernel32.dll")]
static extern uint ResumeThread(IntPtr hThread);

[DllImport("kernel32.dll")]
static extern IntPtr GetModuleHandle(string lpModuleName);

[DllImport("kernel32.dll")]
static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

[DllImport("kernel32.dll")]
static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
static extern bool CreateProcessW(string? lpApplicationName, string? lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

[DllImport("psapi.dll", SetLastError = true)]
static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

[DllImport("psapi.dll", SetLastError = true)]
static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

[DllImport("user32.dll")]
static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

struct STARTUPINFO
{
    public uint cb;
    public string lpReserved, lpDesktop, lpTitle;
    public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
    public ushort wShowWindow, cbReserved2;
    public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
}

struct PROCESS_INFORMATION
{
    public IntPtr hProcess, hThread;
    public int dwProcessId, dwThreadId;
}
