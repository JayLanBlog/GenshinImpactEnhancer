using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

var logPath = @"e:\AI\hook\test-tools\test_result.log";
var lns = new List<string>();
void L(string s) { try { Console.WriteLine(s); } catch { } lns.Add(s); }

try {
L("====================================");
L("  Genshin Stella + ReShade");
L($"  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
L("====================================");

var gameDir = @"D:\TestGame\HoYoPlay\games\Genshin Impact game";
var gameExe = Path.Combine(gameDir, "GenshinImpact.exe");
var stellaDll = Path.GetFullPath(@"e:\AI\hook\src\Native\Stella.Native.Injector\x64\Release\Stella.Native.Injector.dll");
var reshadeDll = Path.Combine(gameDir, "rtlbase.dll");

bool rtlExists = File.Exists(reshadeDll);
L($"ReShade: {(rtlExists?"FOUND":"MISSING")} | Stella: {(File.Exists(stellaDll)?"OK":"MISSING")}");

bool Inject(IntPtr hp, string dll) {
    var b = Encoding.Unicode.GetBytes(dll + '\0'); uint sz = (uint)b.Length;
    var m = VirtualAllocEx(hp, IntPtr.Zero, sz, 0x3000, 0x04);
    WriteProcessMemory(hp, m, b, sz, out _);
    var ll = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
    var ht = CreateRemoteThread(hp, IntPtr.Zero, 0, ll, m, 0, IntPtr.Zero);
    WaitForSingleObject(ht, 15000);
    GetExitCodeThread(ht, out uint ec); CloseHandle(ht);
    VirtualFreeEx(hp, m, 0, 0x8000);
    return ec != 0;
}

L("[1] Start game suspended...");
var si = new STARTUPINFO(); si.cb = (uint)Marshal.SizeOf<STARTUPINFO>();
var pi = new PROCESS_INFORMATION();
CreateProcessW(gameExe, null, IntPtr.Zero, IntPtr.Zero, false, 4, IntPtr.Zero, gameDir, ref si, out pi);
L($"PID={pi.dwProcessId}");
var hp = OpenProcess(0x1F0FFF, false, pi.dwProcessId);

// Order: ReShade first (hooks D3D11), Stella second (detects rtlbase, skips PEB unlink)
if (rtlExists) {
    L("[2a] Inject rtlbase.dll...");
    L(Inject(hp, reshadeDll) ? "  OK" : "  FAIL");
    Thread.Sleep(500);
}
L("[2b] Inject Stella DLL...");
L(Inject(hp, stellaDll) ? "  OK" : "  FAIL");
Thread.Sleep(500);

// Module check
var mods = new IntPtr[4096];
EnumProcessModulesEx(hp, mods, (uint)(mods.Length * IntPtr.Size), out uint n, 0x03);
int cnt = (int)(n / (uint)IntPtr.Size);
bool stellaVis = false, reshadeVis = false;
for (int i = 0; i < cnt; i++) {
    var sb = new StringBuilder(512);
    GetModuleFileNameEx(hp, mods[i], sb, 512);
    var nm = sb.ToString();
    if (nm.Contains("Stella.Native")) stellaVis = true;
    if (nm.Contains("rtlbase")) reshadeVis = true;
}
L($"  Stella: {(stellaVis?"VISIBLE":"HIDDEN")} | rtlbase: {(reshadeVis?"VISIBLE":"HIDDEN")} | {cnt} total");
CloseHandle(hp);

L("[3] Resume game (ReShade hooks D3D11 on load)...");
ResumeThread(pi.hThread);

L("");
L("HOME=ReShade panel | F11=Stella panel | Monitoring 60s...");

int crashTime = -1;
for (int i = 1; i <= 60; i++) {
    Thread.Sleep(1000);
    try { if (Process.GetProcessById(pi.dwProcessId).HasExited) { crashTime = i; break; } }
    catch { crashTime = i; break; }
    if (i % 10 == 0) Console.Write(".");
}
Console.WriteLine();
L(crashTime > 0 ? $"CRASH after {crashTime}s" : "STABLE 60s");

Thread.Sleep(2000);
try { Process.GetProcessById(pi.dwProcessId).Kill(); } catch { }
L(crashTime > 0 ? "RESULT: CRASH" : "RESULT: SUCCESS");
} catch (Exception e) { L("EX: " + e); }
finally { File.WriteAllLines(logPath, lns); }

[DllImport("kernel32")] static extern IntPtr OpenProcess(uint a,bool b,int c);
[DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
[DllImport("kernel32",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,uint s,uint t,uint f);
[DllImport("kernel32",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr p,IntPtr a,byte[] b,uint s,out UIntPtr w);
[DllImport("kernel32",SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr p,IntPtr a,uint s,IntPtr f,IntPtr x,uint c,IntPtr t);
[DllImport("kernel32")] static extern uint WaitForSingleObject(IntPtr h,uint ms);
[DllImport("kernel32")] static extern uint ResumeThread(IntPtr h);
[DllImport("kernel32")] static extern IntPtr GetModuleHandle(string n);
[DllImport("kernel32")] static extern IntPtr GetProcAddress(IntPtr m,string n);
[DllImport("kernel32")] static extern bool GetExitCodeThread(IntPtr h,out uint c);
[DllImport("kernel32")] static extern bool VirtualFreeEx(IntPtr p,IntPtr a,uint s,uint t);
[DllImport("kernel32",SetLastError=true,CharSet=CharSet.Auto)] static extern bool CreateProcessW(string? an,string? cl,IntPtr pa,IntPtr ta,bool ih,uint cf,IntPtr e,string? cd,ref STARTUPINFO si,out PROCESS_INFORMATION pi);
[DllImport("psapi",SetLastError=true)] static extern bool EnumProcessModulesEx(IntPtr p,[Out]IntPtr[] m,uint c,out uint n,uint f);
[DllImport("psapi",SetLastError=true)] static extern uint GetModuleFileNameEx(IntPtr p,IntPtr m,StringBuilder n,uint s);
struct STARTUPINFO { public uint cb; public string r,d,t; public uint x,y,xs,ys,xc,yc,fa,fl; public ushort sw,cb2; public IntPtr r2,hi,ho,he; }
struct PROCESS_INFORMATION { public IntPtr hProcess,hThread; public int dwProcessId,dwThreadId; }
