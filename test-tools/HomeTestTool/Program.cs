using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

Console.WriteLine("====================================");
Console.WriteLine("  Genshin Stella Mod - Live Injection");
Console.WriteLine("====================================");

var dll = Path.GetFullPath(@"e:\AI\hook\src\Native\Stella.Native.Injector\x64\Release\Stella.Native.Injector.dll");
var exe = @"D:\TestGame\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe";
var dir = @"D:\TestGame\HoYoPlay\games\Genshin Impact game";

// 1. Inject (suspended)
Console.WriteLine("[1/4] Starting game (suspended)...");
var si = new STARTUPINFO(); si.cb = (uint)Marshal.SizeOf<STARTUPINFO>();
var pi = new PROCESS_INFORMATION();
if (!CreateProcessW(exe, null, IntPtr.Zero, IntPtr.Zero, false, 4, IntPtr.Zero, dir, ref si, out pi))
{ Console.WriteLine("FAIL: CreateProcess " + Marshal.GetLastWin32Error()); return; }
Console.WriteLine($"  PID={pi.dwProcessId}");

var hp = OpenProcess(0x1F0FFF, false, pi.dwProcessId);
var bytes = Encoding.Unicode.GetBytes(dll+'\0'); uint sz = (uint)bytes.Length;
var mem = VirtualAllocEx(hp, IntPtr.Zero, sz, 0x3000, 0x04);
WriteProcessMemory(hp, mem, bytes, sz, out _);
var ll = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
var ht = CreateRemoteThread(hp, IntPtr.Zero, 0, ll, mem, 0, IntPtr.Zero);
WaitForSingleObject(ht, 15000);
GetExitCodeThread(ht, out uint ec); CloseHandle(ht);
VirtualFreeEx(hp, mem, 0, 0x8000);
CloseHandle(hp);

if(ec==0) { Console.WriteLine("FAIL: inject"); TerminateProcess(pi.hProcess,0); return; }
Console.WriteLine($"[2/4] DLL injected  0x{ec:X}");

// 2. PEB hidden check
Console.WriteLine("[3/4] Verifying PEB hide...");
var hp2 = OpenProcess(0x0410, false, pi.dwProcessId); // PROCESS_QUERY_INFORMATION | PROCESS_VM_READ
var mods = new IntPtr[4096];
EnumProcessModulesEx(hp2, mods, (uint)(mods.Length*IntPtr.Size), out uint n, 0x03);
int cnt = (int)(n/(uint)IntPtr.Size); bool vis=false;
for(int i=0;i<cnt;i++){ var sb=new StringBuilder(512); GetModuleFileNameEx(hp2,mods[i],sb,512); if(sb.ToString().Contains("Stella")){vis=true;break;} }
Console.WriteLine($"  PEB: {(vis?"VISIBLE":"HIDDEN")} ({cnt} modules)");
CloseHandle(hp2);

// 3. Resume game
Console.WriteLine("[4/4] Resuming game...");
ResumeThread(pi.hThread);

// 4. Monitor for 30 seconds
Console.WriteLine("");
Console.WriteLine("====================================");
Console.WriteLine("  Game is RUNNING!");
Console.WriteLine("  Panel is HIDDEN by default");
Console.WriteLine("  Press physical HOME key to show panel");
Console.WriteLine("  Monitoring for 30s...");
Console.WriteLine("====================================");

int crashTime = -1;
for (int i = 1; i <= 30; i++)
{
    Thread.Sleep(1000);
    try 
    { 
        var p = Process.GetProcessById(pi.dwProcessId);
        if (p.HasExited) { crashTime = i; break; }
        if (i % 5 == 0) Console.Write("."); 
    }
    catch { crashTime = i; break; }
}
Console.WriteLine();

if (crashTime > 0)
{
    Console.WriteLine($"");
    Console.WriteLine($"*** CRASH DETECTED after {crashTime}s ***");
    Console.WriteLine($"*** Game crashed - investigate immediately! ***");
}
else
{
    Console.WriteLine($"");
    Console.WriteLine($"*** Game stable for 30s - NO CRASH ***");
    Console.WriteLine($"*** Check screen for overlay panel ***");
}

// Keep alive
Console.WriteLine("");
Console.WriteLine("Game is RUNNING. Press ENTER to kill game...");
Console.ReadLine();

try { Process.GetProcessById(pi.dwProcessId).Kill(); Console.WriteLine("Game terminated."); }
catch { Console.WriteLine("Game already exited."); }

Console.WriteLine(crashTime > 0 ? "RESULT: CRASH" : "RESULT: STABLE");

[DllImport("kernel32")] static extern IntPtr OpenProcess(uint a,bool b,int c);
[DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
[DllImport("kernel32")] static extern bool TerminateProcess(IntPtr h,uint c);
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
