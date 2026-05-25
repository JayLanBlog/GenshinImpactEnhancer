using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

var logPath = @"e:\AI\hook\test-tools\home_test_result.txt";
var lns = new List<string>();
void L(string s) { try { Console.WriteLine(s); } catch { } lns.Add(s); }

try
{
    L("=== Home Key Auto Test ===");
    var dll = Path.GetFullPath(@"e:\AI\hook\src\Native\Stella.Native.Injector\x64\Release\Stella.Native.Injector.dll");
    var exe = @"D:\TestGame\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe";
    var dir = @"D:\TestGame\HoYoPlay\games\Genshin Impact game";

    // 1. Inject
    var si = new STARTUPINFO(); si.cb = (uint)Marshal.SizeOf<STARTUPINFO>();
    var pi = new PROCESS_INFORMATION();
    if (!CreateProcessW(exe, null, IntPtr.Zero, IntPtr.Zero, false, 4, IntPtr.Zero, dir, ref si, out pi))
    { L("FAIL CreateProcess: " + Marshal.GetLastWin32Error()); File.WriteAllLines(logPath, lns); return; }
    L("PID=" + pi.dwProcessId);

    var hp = OpenProcess(0x1F0FFF, false, pi.dwProcessId);
    var bytes = Encoding.Unicode.GetBytes(dll + '\0'); uint sz = (uint)bytes.Length;
    var mem = VirtualAllocEx(hp, IntPtr.Zero, sz, 0x3000, 0x04);
    WriteProcessMemory(hp, mem, bytes, sz, out _);
    var ll = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
    var ht = CreateRemoteThread(hp, IntPtr.Zero, 0, ll, mem, 0, IntPtr.Zero);
    WaitForSingleObject(ht, 15000);
    GetExitCodeThread(ht, out uint ec); CloseHandle(ht);
    VirtualFreeEx(hp, mem, 0, 0x8000);
    CloseHandle(hp);

    if (ec == 0) { L("FAIL inject"); TerminateProcess(pi.hProcess, 0); File.WriteAllLines(logPath, lns); return; }
    L("OK inject 0x" + ec.ToString("X"));

    // 2. Resume
    ResumeThread(pi.hThread);
    L("Game running...");

    // 3. Wait for game window + DLL init
    L("Waiting 8s for DLL init...");
    Thread.Sleep(8000);

    // 4. Find game window and focus it
    IntPtr gameHwnd = IntPtr.Zero;
    for (int i = 0; i < 20; i++)
    {
        gameHwnd = FindWindowExW(IntPtr.Zero, gameHwnd, null, null);
        var sb = new StringBuilder(256);
        GetWindowTextW(gameHwnd, sb, 256);
        if (sb.ToString().Contains("原神") || sb.ToString().Contains("Genshin"))
            break;
        Thread.Sleep(200);
    }

    // 5. Check overlay - window should be VISIBLE on startup
    //    DLL creates overlay + keyboard hook in WindowThread
    IntPtr regHwnd = IntPtr.Zero;
    try { var key = Registry.CurrentUser.OpenSubKey("Software\\StellaMod"); if (key != null) { var v = key.GetValue("OverlayHWND"); if (v != null) regHwnd = (IntPtr)(int)v; key.Close(); } } catch { }
    bool created = regHwnd != IntPtr.Zero;

    if (created) {
        L("*** OVERLAY CREATED - HWND=0x" + regHwnd.ToInt64().ToString("X") + " ***");
        L("*** Panel should be VISIBLE on screen! ***");
        L("*** Press physical HOME key to toggle ***");
    } else {
        L("*** OVERLAY NOT CREATED - FAIL ***");
    }

    // 6. Try pressing Home (for automated verification - may not work cross-process)
    if (gameHwnd != IntPtr.Zero)
        SetForegroundWindow(gameHwnd);
    Thread.Sleep(300);

    L("Sending HOME key (cross-process - may not trigger due to UIPI)...");
    keybd_event(0x24, 0, 0, UIntPtr.Zero);
    Thread.Sleep(200);
    keybd_event(0x24, 0, 2, UIntPtr.Zero);
    Thread.Sleep(800);

    // Check toggle count
    int toggleAfter = -1;
    try { var key = Registry.CurrentUser.OpenSubKey("Software\\StellaMod"); if (key != null) { var tv = key.GetValue("ToggleCount"); if (tv != null) toggleAfter = (int)tv; key.Close(); } } catch { }
    L("ToggleCount after HOME: " + toggleAfter + " " + (toggleAfter > 0 ? "OK" : "(cross-process input blocked - normal)"));

    // Kill game
    L(created ? "=== PASS ===" : "=== FAIL ===");
    Thread.Sleep(1000);
    TerminateProcess(pi.hProcess, 0);
    L("Game terminated");
}
catch (Exception ex) { L("EX: " + ex.Message); }
finally { File.WriteAllLines(logPath, lns); }

[DllImport("kernel32")] static extern IntPtr OpenProcess(uint a, bool b, int c);
[DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
[DllImport("kernel32")] static extern bool TerminateProcess(IntPtr h, uint c);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, uint s, uint t, uint f);
[DllImport("kernel32", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, uint s, out UIntPtr w);
[DllImport("kernel32", SetLastError = true)] static extern IntPtr CreateRemoteThread(IntPtr p, IntPtr a, uint s, IntPtr f, IntPtr x, uint c, IntPtr t);
[DllImport("kernel32")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
[DllImport("kernel32")] static extern uint ResumeThread(IntPtr h);
[DllImport("kernel32")] static extern IntPtr GetModuleHandle(string n);
[DllImport("kernel32")] static extern IntPtr GetProcAddress(IntPtr m, string n);
[DllImport("kernel32")] static extern bool GetExitCodeThread(IntPtr h, out uint c);
[DllImport("kernel32")] static extern bool VirtualFreeEx(IntPtr p, IntPtr a, uint s, uint t);
[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)] static extern bool CreateProcessW(string? an, string? cl, IntPtr pa, IntPtr ta, bool ih, uint cf, IntPtr e, string? cd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
[DllImport("user32")] static extern IntPtr FindWindowW(string? c, string? t);
[DllImport("user32")] static extern IntPtr FindWindowExW(IntPtr p, IntPtr a, string? c, string? t);
[DllImport("user32")] static extern int GetWindowTextW(IntPtr hWnd, StringBuilder t, int nMaxCount);
[DllImport("user32")] static extern bool EnumWindows(IntPtr lpEnumFunc, IntPtr lParam);
[DllImport("user32")] static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32")] static extern bool IsWindow(IntPtr h);
[DllImport("user32")] static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32")] static extern void keybd_event(byte vk, byte sc, uint fl, UIntPtr ex);
[DllImport("user32")] static extern uint SendInput(uint n, INPUT[] p, int cb);
struct INPUT { public uint type; public KEYBDINPUT ki; }
struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
struct STARTUPINFO { public uint cb; public string r, d, t; public uint x, y, xs, ys, xc, yc, fa, fl; public ushort sw, cb2; public IntPtr r2, hi, ho, he; }
struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
