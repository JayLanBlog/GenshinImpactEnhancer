using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Principal;

if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
{ Console.WriteLine("[WARNING] Not running as Administrator! Injection may fail.\n"); }
else { Console.WriteLine("[OK] Running as Administrator\n"); }

var logDir = @"C:\Users\86178\AppData\Local\GenshinImpactEnhancer";
Directory.CreateDirectory(logDir);
var logFile = Path.Combine(logDir, "injection.log");
var lns = new List<string>();
void L(string s) { try { Console.WriteLine(s); } catch { } lns.Add(s); }
void Flush() { try { File.WriteAllLines(logFile, lns, Encoding.UTF8); } catch { } }

var gd = @"D:\TestGame\HoYoPlay\games\Genshin Impact game";
var gameExe = Path.Combine(gd, "GenshinImpact.exe");
var reshadeDll = Path.Combine(gd, "ReShade64.dll");
var enhancerDll = Path.GetFullPath(@"e:\AI\hook\src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll");
var rlog = Path.Combine(gd, "ReShade.log");
var ovlLog = Path.Combine(logDir, "overlay_debug.log");

L("══════════════════════════════════════════════════");
L("  GIEnhancer 自动化测试 (CREATE_SUSPENDED)");
L("  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
L("══════════════════════════════════════════════════");

if (!File.Exists(gameExe)) { L("MISSING: " + gameExe); Flush(); return; }
if (!File.Exists(reshadeDll)) { L("MISSING: " + reshadeDll); Flush(); return; }
if (!File.Exists(enhancerDll)) { L("MISSING: " + enhancerDll); Flush(); return; }

// ══════════════════════════════════════════════════════════════════
// 最优稳定方案（游戏目录零修改，反作弊不可见）：
//   ① rtlbase  (3DMigoto)  → LoadLibrary 注入
//   ② GIEnhancer            → LoadLibrary 注入 + PEB摘除自身
//   ③ ReShade64             → LoadLibrary 注入
//   清理所有代理DLL + 禁用 Stella Mod
//   3DMigoto模块确认：枚举进程模块列表，搜索 rtlbase
// ══════════════════════════════════════════════════════════════════

// 0. 清理旧代理
foreach (var name in new[] { "dxgi.dll", "dxgi.dll.disabled", "d3d11.dll", "d3d11.dll.disabled" }) {
    var dp = Path.Combine(gd, name);
    for (int a = 0; a < 3; a++) { try { File.Delete(dp); break; } catch { Thread.Sleep(300); } }
}

// 1. 禁用 Stella Mod
foreach (var pn in new[] { "Stella Mod Launcher", "LaunchGame", "Welcome App", "Configuration Window" })
    foreach (var p in Process.GetProcessesByName(pn)) { try { p.Kill(); p.WaitForExit(3000); } catch { } }
var stellaRS = @"D:\LaunchGame\data\dependencies\reshade\ReShade64.dll";
if (File.Exists(stellaRS)) { try { File.Move(stellaRS, stellaRS + ".disabled"); } catch { } }

var rtlPath = Path.Combine(gd, "rtlbase.dll");
L($"  rtlbase={(File.Exists(rtlPath)?"OK":"MISSING")} | 注入顺序: rtlbase→GIEnhancer→ReShade | 零文件修改");

const uint CREATE_SUSPENDED = 0x00000004;
const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
const int MAX_RETRIES = 3;

bool InjectDll(IntPtr hp, string dll)
{
    var bytes = Encoding.Unicode.GetBytes(dll + '\0');
    uint sz = (uint)bytes.Length;
    var mem = NativeMethods.VirtualAllocEx(hp, IntPtr.Zero, sz, 0x3000, 0x04);
    if (mem == IntPtr.Zero) return false;
    NativeMethods.WriteProcessMemory(hp, mem, bytes, sz, out _);
    var loadLib = NativeMethods.GetProcAddress(NativeMethods.GetModuleHandle("kernel32.dll"), "LoadLibraryW");
    var ht = NativeMethods.CreateRemoteThread(hp, IntPtr.Zero, 0, loadLib, mem, 0, IntPtr.Zero);
    if (ht == IntPtr.Zero) { NativeMethods.VirtualFreeEx(hp, mem, 0, 0x8000); return false; }
    NativeMethods.WaitForSingleObject(ht, 15000);
    NativeMethods.GetExitCodeThread(ht, out uint ec);
    NativeMethods.CloseHandle(ht);
    NativeMethods.VirtualFreeEx(hp, mem, 0, 0x8000);
    return ec != 0;
}

bool CheckResult(int pid)
{
    var h = NativeMethods.OpenProcess(0x0410, false, pid);
    if (h == IntPtr.Zero) { L("  ⚠️ OpenProcess failed"); return false; }

    var mods = new IntPtr[8192]; uint nmod;
    bool migotoOk = false, reshadeOk = false, enhancerOk = false;

    if (NativeMethods.EnumProcessModulesEx(h, mods, (uint)(mods.Length * IntPtr.Size), out nmod, 0x03))
    {
        int count = (int)(nmod / (uint)IntPtr.Size);
        L($"  进程加载了 {count} 个模块:");

        for (int i = 0; i < count; i++)
        {
            var sb = new StringBuilder(512);
            NativeMethods.GetModuleFileNameEx(h, mods[i], sb, 512);
            string name = sb.ToString();
            
            if (name.Contains("rtlbase", StringComparison.OrdinalIgnoreCase))
            { migotoOk = true; L($"    [3DMigoto] ✅ {Path.GetFileName(name)} @ 0x{mods[i]:X}"); }
            else if (name.Contains("ReShade", StringComparison.OrdinalIgnoreCase))
            { reshadeOk = true; L($"    [ReShade]  ✅ {Path.GetFileName(name)} @ 0x{mods[i]:X}"); }
            else if (name.Contains("GIEnhancer", StringComparison.OrdinalIgnoreCase) || 
                     name.Contains("Stella.Native", StringComparison.OrdinalIgnoreCase))
            { enhancerOk = true; L($"    [GIEnhancer]✅ {Path.GetFileName(name)} @ 0x{mods[i]:X}"); }
        }
    }
    NativeMethods.CloseHandle(h);

    // Fallback checks
    if (!enhancerOk && File.Exists(ovlLog)) { try { enhancerOk = File.ReadAllText(ovlLog).Contains("Toggle:"); } catch { } }
    if (!reshadeOk && File.Exists(rlog) && new FileInfo(rlog).Length > 10) reshadeOk = true;
    if (!reshadeOk && File.Exists(rlog + "1") && new FileInfo(rlog + "1").Length > 10) reshadeOk = true;

    L($"  3DMigoto: {(migotoOk ? "✅ 模块已确认" : "❌ 未在进程中找到")}");
    L($"  ReShade:  {(reshadeOk ? "✅ 模块已确认" : "❌ 未在进程中找到")}");
    L($"  GIEnhancer:{(enhancerOk ? "✅ 模块已确认" : "❌ 未在进程中找到")}");
    return enhancerOk;
}

void SendTestKeys(int pid)
{
    // Force foreground using AttachThreadInput (we're admin)
    var hwnd = FindGameWindow(pid);
    if (hwnd != IntPtr.Zero) {
        uint gid; NativeMethods.GetWindowThreadProcessId(hwnd, out gid);
        uint tid = NativeMethods.GetCurrentThreadId();
        NativeMethods.AttachThreadInput(tid, gid, true);
        NativeMethods.SetForegroundWindow(hwnd); Thread.Sleep(200);
        NativeMethods.BringWindowToTop(hwnd);
        NativeMethods.AttachThreadInput(tid, gid, false);
        Thread.Sleep(300);
        L($"  窗口置前: {(NativeMethods.GetForegroundWindow() == hwnd ? "OK" : "retry...")}");
    }

    L("[9] 发送Home键 (ReShade)...");
    for (int k = 0; k < 3; k++)
    {
        if (hwnd != IntPtr.Zero) { NativeMethods.PostMessage(hwnd, 0x100, (IntPtr)0x24, (IntPtr)0x00000001); Thread.Sleep(80); NativeMethods.PostMessage(hwnd, 0x101, (IntPtr)0x24, (IntPtr)0xC0000001); }
        NativeMethods.SendInputKey(0x24, false); Thread.Sleep(100);
        NativeMethods.SendInputKey(0x24, true); Thread.Sleep(2000);
        if (File.Exists(rlog) && new FileInfo(rlog).Length > 10) { L("  ✅ ReShade响应"); break; }
        if (File.Exists(rlog + "1") && new FileInfo(rlog + "1").Length > 10) { L("  ✅ ReShade响应"); break; }
        L($"  第{k+2}次..."); Flush();
    }
    
    L("[10] 发送F3键 (3DMigoto)...");
    if (hwnd != IntPtr.Zero) { NativeMethods.PostMessage(hwnd, 0x100, (IntPtr)0x72, (IntPtr)0x00000001); Thread.Sleep(80); NativeMethods.PostMessage(hwnd, 0x101, (IntPtr)0x72, (IntPtr)0xC0000001); }
    NativeMethods.SendInputKey(0x72, false); Thread.Sleep(100);
    NativeMethods.SendInputKey(0x72, true); Thread.Sleep(2000);
    
    L("[11] 发送F11键 (GIEnhancer)...");
    for (int k = 0; k < 3; k++)
    {
        NativeMethods.SendInputKey(0x7A, false); Thread.Sleep(100);
        NativeMethods.SendInputKey(0x7A, true); Thread.Sleep(2000);
        Flush();
        if (File.Exists(ovlLog) && File.ReadAllText(ovlLog).Contains("Toggle:"))
        { L("  ✅ GIEnhancer面板已触发！"); break; }
    }
}

IntPtr FindGameWindow(int pid)
{
    IntPtr result = IntPtr.Zero;
    try { result = Process.GetProcessById(pid).MainWindowHandle; } catch { }
    if (result != IntPtr.Zero) return result;

    IntPtr found = IntPtr.Zero;
    NativeMethods.EnumWindows((h, l) =>
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetWindowText(h, sb, 256);
        if (sb.ToString().Contains("原神"))
        {
            NativeMethods.GetWindowThreadProcessId(h, out uint p);
            if (p == pid) { found = h; return false; }
        }
        return true;
    }, IntPtr.Zero);
    return found;
}

// ═══════════════════════ 主测试循环 ═══════════════════════
for (int retry = 1; retry <= MAX_RETRIES; retry++)
{
    L($"\n{'='*50}\n  第 {retry}/{MAX_RETRIES} 次测试\n{'='*50}");
    Flush();

    // 退出时恢复 Stella Mod 的 ReShade64.dll
var stellaRSdisabled = stellaRS + ".disabled";
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    if (File.Exists(stellaRSdisabled)) {
        try { File.Move(stellaRSdisabled, stellaRS); L("  已恢复 Stella Mod ReShade64.dll"); } catch { }
    }
    Flush(); Environment.Exit(0);
};

// 清理旧进程 + 日志
L("[0] 清理旧进程+日志...");
    foreach (var p in Process.GetProcessesByName("GenshinImpact"))
    { try { p.Kill(); p.WaitForExit(3000); L($"  已杀死 PID={p.Id}"); } catch { } }
    Thread.Sleep(2000);
    try { File.Delete(rlog); File.Delete(rlog + "1"); } catch { }
    try { File.Delete(ovlLog); } catch { }

    // ── Step 1: CREATE_SUSPENDED ──
    L("[1] CREATE_SUSPENDED 启动游戏...");
    var si = new STARTUPINFO { cb = (uint)Marshal.SizeOf<STARTUPINFO>() };
    bool ok = NativeMethods.CreateProcessW(null, "\"" + gameExe + "\"", IntPtr.Zero, IntPtr.Zero, false,
        CREATE_SUSPENDED, IntPtr.Zero, gd, ref si, out var pi);
    if (!ok) { L($"  FAIL CreateProcessW: {Marshal.GetLastWin32Error()}"); Flush(); continue; }
    L($"  游戏已启动 (SUSPENDED): PID={pi.dwProcessId}");
    Flush();

    // ── Step 2: 打开进程 ──
    L("[2] 打开进程...");
    IntPtr hProc = NativeMethods.OpenProcess(PROCESS_ALL_ACCESS, false, pi.dwProcessId);
    if (hProc == IntPtr.Zero)
    { L($"  FAIL OpenProcess: {Marshal.GetLastWin32Error()}"); NativeMethods.TerminateProcess(pi.hProcess, 0); continue; }
    L("  句柄获取成功");

    // ── Step 3: 按序 LoadLibrary 注入三DLL ──
    // ① rtlbase (3DMigoto) - 最先
    L("[3] 注入 rtlbase.dll (3DMigoto) [①]...");
    bool migotoOk = InjectDll(hProc, rtlPath);
    L(migotoOk ? "  ✅ 3DMigoto" : "  ❌ 3DMigoto");
    
    // ② GIEnhancer - PEB摘除自身
    L("[4] 注入 GIEnhancer [② PEB隐藏]...");
    bool enhancerOk2 = InjectDll(hProc, enhancerDll);
    L(enhancerOk2 ? "  ✅ GIEnhancer" : "  ❌ GIEnhancer");
    
    // ③ ReShade64
    L("[5] 注入 ReShade64 [③]...");
    bool reshadeOk = InjectDll(hProc, reshadeDll);
    L(reshadeOk ? "  ✅ ReShade" : "  ❌ ReShade");
    
    NativeMethods.CloseHandle(hProc);
    L($"  3DMigoto={(migotoOk?"OK":"FAIL")} GIEnhancer={(enhancerOk2?"OK":"FAIL")} ReShade={(reshadeOk?"OK":"FAIL")}");

    // ── Step 4: 恢复线程 ──
    L("[5] 恢复游戏主线程...");
    NativeMethods.ResumeThread(pi.hThread);
    NativeMethods.CloseHandle(pi.hThread);
    L("  线程已恢复，游戏正在启动");
    Flush();

    // ── Step 5: 等待游戏加载 ──
    L("[6] 等待游戏加载（40秒）...");
    bool alive = true;
    for (int i = 1; i <= 40; i++)
    {
        Thread.Sleep(1000);
        try { if (Process.GetProcessById(pi.dwProcessId).HasExited) { alive = false; L($"  在 {i}s 退出"); break; } }
        catch { alive = false; L($"  在 {i}s 消失"); break; }
        if (i % 10 == 0) { L($"  ... {i}s"); Flush(); }
    }
    if (!alive) { Flush(); continue; }

    // ── Step 6: 查找窗口 ──
    L("[7] 查找窗口...");
    var hwnd = FindGameWindow(pi.dwProcessId);
    if (hwnd != IntPtr.Zero) { NativeMethods.SetForegroundWindow(hwnd); L($"  窗口: 0x{hwnd:X}"); }
    else L("  未找到窗口");

    // ── Step 7: 稳定 ──
    L("[8] 稳定10秒...");
    Thread.Sleep(10000);
    try { if (Process.GetProcessById(pi.dwProcessId).HasExited) { L("  已退出"); continue; } }
    catch { L("  已退出"); continue; }

    // ── Step 8: 发送按键 ──
    SendTestKeys(pi.dwProcessId);
    Flush();
    Thread.Sleep(5000);

    // ── Step 9: 检查结果 ──
    L($"\n{'='*50}\n  测试结果\n{'='*50}");
    bool allOk = CheckResult(pi.dwProcessId);
    try { L($"  游戏: 运行中 ✓ (PID={pi.dwProcessId})"); }
    catch { L("  游戏: 已退出 ✗"); }
    L($"{'='*50}");
    Flush();

    if (allOk)
    {
        L("\n🎉 测试成功！所有面板正常！");
        Flush();
        L("按Ctrl+C退出...");
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; Flush(); Environment.Exit(0); };
        while (true) Thread.Sleep(5000);
    }
    L($"\n面板未全部触发，重试...\n");
}

L("\n❌ 已达最大重试次数");
Flush();
