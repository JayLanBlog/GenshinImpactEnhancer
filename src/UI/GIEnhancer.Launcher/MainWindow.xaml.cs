using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using GIEnhancer.DeviceIdentifier;
using GIEnhancer.Services;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Launcher;

public partial class MainWindow : Window
{
    private DeviceInfo? _deviceInfo;
    private IntPtr _gameProcessHandle;
    private IntPtr _gameThreadHandle;
    private int _gamePid;
    private bool _gameRunning;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await OnLoadedAsync();
    }

    private async Task OnLoadedAsync()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GenshinImpactEnhancer", "logs");
        StellaLogger.Initialize(logDir);
        _deviceInfo = await Task.Run(DeviceDetector.Collect);

        Dispatcher.Invoke(() =>
        {
            TxtHardware.Text = string.Join("\n",
                $"GPU:   {_deviceInfo.GpuName}",
                $"显存:  {_deviceInfo.GpuMemoryMb} MB",
                $"CPU:   {_deviceInfo.CpuName}",
                $"内存:  {_deviceInfo.TotalMemoryMb} MB",
                $"分辨率: {_deviceInfo.ScreenWidth}x{_deviceInfo.ScreenHeight}");
            TxtRecommendation.Text = BuildRecommendation();
            UpdateStatus("就绪 — 请设置游戏路径后启动");
        });
    }

    // ── 游戏路径搜索 ──
    private static readonly string[] KnownGamePaths =
    {
        @"D:\TestGame\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe",
        @"D:\TestGame\HoYoPlay\games\Genshin Impact game\YuanShen.exe",
        @"C:\Program Files\Genshin Impact\Genshin Impact Game\YuanShen.exe",
        @"C:\Program Files\Genshin Impact\Genshin Impact Game\GenshinImpact.exe",
        @"D:\Program Files\Genshin Impact\Genshin Impact Game\YuanShen.exe",
        @"D:\Program Files\Genshin Impact\Genshin Impact Game\GenshinImpact.exe",
        @"E:\Genshin Impact\Genshin Impact Game\YuanShen.exe",
        @"E:\Genshin Impact\Genshin Impact Game\GenshinImpact.exe",
        @"D:\Genshin Impact\Genshin Impact Game\YuanShen.exe",
    };

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        BtnSearch.IsEnabled = false; BtnSearch.Content = "搜索中...";
        var found = Task.Run(() =>
        {
            foreach (var p in KnownGamePaths) if (File.Exists(p)) return p;
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Genshin Impact");
                var path = key?.GetValue("DisplayIcon")?.ToString();
                if (path != null && File.Exists(path)) return path;
            }
            catch { }
            foreach (var drive in new[] { "C", "D", "E", "F", "G" })
            {
                var p = $@"{drive}:\Program Files\Genshin Impact\Genshin Impact Game\YuanShen.exe";
                if (File.Exists(p)) return p;
                p = $@"{drive}:\Genshin Impact\Genshin Impact Game\YuanShen.exe";
                if (File.Exists(p)) return p;
            }
            return null;
        }).Result;

        Dispatcher.Invoke(() =>
        {
            if (found != null)
            { TxtGamePath.Text = found; TxtGamePath.Foreground = Brushes.LimeGreen; UpdateStatus($"✅ 找到: {Path.GetFileName(found)}"); }
            else
            { TxtGamePath.Text = "未找到，请手动📂浏览"; TxtGamePath.Foreground = Brushes.OrangeRed; UpdateStatus("⚠️ 未搜索到原神"); }
            BtnSearch.Content = "🔍 搜索"; BtnSearch.IsEnabled = true;
        });
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择原神主程序",
            Filter = "可执行文件|*.exe|所有文件|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        if (dlg.ShowDialog() == true)
        {
            TxtGamePath.Text = dlg.FileName; TxtGamePath.Foreground = Brushes.LimeGreen;
            UpdateStatus($"已选择: {Path.GetFileName(dlg.FileName)}");
        }
    }

    // ── 一键启动 ──
    private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
    {
        var gamePath = TxtGamePath.Text.Trim();
        if (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath))
        { UpdateStatus("❌ 游戏路径无效"); return; }

        BtnLaunch.IsEnabled = false; BtnSearch.IsEnabled = false;
        AppendLog("🚀 开始启动...");

        try
        {
            var gameDir = Path.GetDirectoryName(gamePath) ?? "";

            // ═══════════════════════════════════════════════════════════
            // [0] 清理旧代理 + 杀 Stella Mod
            // ═══════════════════════════════════════════════════════════
            AppendLog("🧹 清理环境...");

            // 删除旧代理文件
            foreach (var name in new[] { "dxgi.dll", "dxgi.dll.disabled", "d3d11.dll", "d3d11.dll.disabled" })
            {
                var dp = Path.Combine(gameDir, name);
                try { if (File.Exists(dp)) File.Delete(dp); } catch { }
            }

            // 杀 Stella Mod 进程（避免双 ReShade）
            foreach (var pn in new[] { "Stella Mod Launcher", "LaunchGame", "Welcome App", "Configuration Window" })
                foreach (var p in Process.GetProcessesByName(pn))
                {
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
                }

            AppendLog("  环境清理完成 ✓");

            // ═══════════════════════════════════════════════════════════
            // [1] CREATE_SUSPENDED 启动游戏
            // ═══════════════════════════════════════════════════════════
            AppendLog($"🎯 CREATE_SUSPENDED 启动: {Path.GetFileName(gamePath)}");
            var si = new STARTUPINFO { cb = (uint)Marshal.SizeOf<STARTUPINFO>() };

            bool ok = Win32.CreateProcessW(null, "\"" + gamePath + "\"", IntPtr.Zero, IntPtr.Zero, false,
                Win32.CREATE_SUSPENDED, IntPtr.Zero, gameDir, ref si, out var pi);
            if (!ok)
            {
                AppendLog($"❌ CreateProcess 失败 (err={Marshal.GetLastWin32Error()})");
                BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true;
                return;
            }
            _gamePid = pi.dwProcessId; _gameProcessHandle = pi.hProcess; _gameThreadHandle = pi.hThread; _gameRunning = true;
            AppendLog($"  游戏已启动 (SUSPENDED): PID={_gamePid}");

            // ═══════════════════════════════════════════════════════════
            // [2] 打开进程句柄
            // ═══════════════════════════════════════════════════════════
            var hProc = Win32.OpenProcess(Win32.PROCESS_ALL_ACCESS, false, _gamePid);
            if (hProc == IntPtr.Zero)
            {
                AppendLog($"❌ OpenProcess 失败 (err={Marshal.GetLastWin32Error()})");
                Win32.TerminateProcess(pi.hProcess, 0);
                BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true;
                return;
            }

            // ═══════════════════════════════════════════════════════════
            // [3] 注入 DLL
            // ═══════════════════════════════════════════════════════════
            AppendLog("💉 注入 DLL...");

            // ① rtlbase.dll (3DMigoto)
            var rtlPath = Path.Combine(gameDir, "rtlbase.dll");
            bool migotoOk = false;
            if (File.Exists(rtlPath))
            {
                migotoOk = InjectDll(hProc, rtlPath);
                AppendLog(migotoOk ? "  ✅ rtlbase (3DMigoto)" : "  ❌ rtlbase 注入失败");
            }
            else
            {
                AppendLog("  ⚠️ rtlbase.dll 未找到，跳过");
            }

            // ② GIEnhancer.Native.Injector.dll
            var nativeDll = FindNativeDll();
            bool enhancerOk = false;
            if (!string.IsNullOrEmpty(nativeDll) && File.Exists(nativeDll))
            {
                enhancerOk = InjectDll(hProc, nativeDll);
                AppendLog(enhancerOk ? "  ✅ GIEnhancer (F11面板)" : "  ❌ GIEnhancer 注入失败");
            }
            else
            {
                AppendLog("  ⚠️ GIEnhancer DLL 未找到");
            }

            // ③ ReShade64.dll
            var reshadeDll = FindReshadeDll(gameDir);
            bool reshadeOk = false;
            if (!string.IsNullOrEmpty(reshadeDll) && File.Exists(reshadeDll))
            {
                reshadeOk = InjectDll(hProc, reshadeDll);
                AppendLog(reshadeOk ? "  ✅ ReShade64" : "  ❌ ReShade 注入失败");
            }
            else
            {
                AppendLog("  ⚠️ ReShade64.dll 未找到");
            }

            Win32.CloseHandle(hProc);

            // ═══════════════════════════════════════════════════════════
            // [4] 恢复游戏（先恢复再验证，避免反作弊检测）
            // ═══════════════════════════════════════════════════════════
            AppendLog("▶ 恢复游戏进程...");
            Win32.ResumeThread(pi.hThread);
            AppendLog("✅ 游戏已启动！");
            AppendLog($"  3DMigoto={(migotoOk ? "✅" : "❌")} GIEnhancer={(enhancerOk ? "✅" : "❌")} ReShade={(reshadeOk ? "✅" : "❌")}");

            UpdateStatus($"✅ 运行中 PID={_gamePid}");
            UpdateDllStatus(enhancerOk ? "✅ 已注入" : "❌ 失败", enhancerOk ? Brushes.LimeGreen : Brushes.Red);
            UpdatePebStatus(enhancerOk ? "✅ 已隐藏" : "—", enhancerOk ? Brushes.LimeGreen : Brushes.Gray);
            BtnShutdown.IsEnabled = true;

            // ═══════════════════════════════════════════════════════════
            // [5] 延迟验证模块（等游戏加载完）
            // ═══════════════════════════════════════════════════════════
            _ = Task.Run(async () =>
            {
                await Task.Delay(30000); // 等30秒游戏加载完
                VerifyModules(_gamePid);
            });

            // ═══════════════════════════════════════════════════════════
            // [6] 监控退出
            // ═══════════════════════════════════════════════════════════
            _ = Task.Run(MonitorGameExit);
        }
        catch (Exception ex)
        {
            AppendLog($"❌ {ex.Message}");
            BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true;
        }
    }

    // ── 模块验证 ──
    private void VerifyModules(int pid)
    {
        bool migotoOk = false, reshadeOk = false, enhancerOk = false;
        try
        {
            var h = Win32.OpenProcess(0x0410, false, pid);
            if (h == IntPtr.Zero) { AppendLog("  ⚠️ 无法打开进程验证模块"); return; }

            var mods = new IntPtr[8192];
            if (Win32.EnumProcessModulesEx(h, mods, (uint)(mods.Length * IntPtr.Size), out uint nmod, 0x03))
            {
                int count = (int)(nmod / (uint)IntPtr.Size);
                AppendLog($"  进程已加载 {count} 个模块");
                for (int i = 0; i < count; i++)
                {
                    var sb = new StringBuilder(512);
                    Win32.GetModuleFileNameEx(h, mods[i], sb, 512);
                    string name = sb.ToString();

                    if (name.Contains("rtlbase", StringComparison.OrdinalIgnoreCase))
                    { migotoOk = true; AppendLog($"    [3DMigoto] ✅ rtlbase.dll @ 0x{mods[i]:X}"); }
                    else if (name.Contains("ReShade", StringComparison.OrdinalIgnoreCase))
                    { reshadeOk = true; AppendLog($"    [ReShade]  ✅ ReShade64.dll @ 0x{mods[i]:X}"); }
                    else if (name.Contains("GIEnhancer", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Stella.Native", StringComparison.OrdinalIgnoreCase))
                    { enhancerOk = true; AppendLog($"    [GIEnhancer]✅ Native Injector @ 0x{mods[i]:X}"); }
                }
            }
            Win32.CloseHandle(h);
        }
        catch { }

        if (!migotoOk) AppendLog("    [3DMigoto] ❌ 未在进程中找到");
        if (!reshadeOk) AppendLog("    [ReShade]  ❌ 未在进程中找到");
        if (!enhancerOk) AppendLog("    [GIEnhancer]❌ 未在进程中找到");
    }

    // ── DLL 注入（LoadLibrary + CreateRemoteThread）──
    private bool InjectDll(IntPtr hp, string dllPath)
    {
        try
        {
            byte[] bytes = Encoding.Unicode.GetBytes(dllPath + '\0');
            uint size = (uint)bytes.Length;
            IntPtr mem = Win32.VirtualAllocEx(hp, IntPtr.Zero, size,
                Win32.MEM_COMMIT | Win32.MEM_RESERVE, Win32.PAGE_READWRITE);
            if (mem == IntPtr.Zero) return false;

            Win32.WriteProcessMemory(hp, mem, bytes, size, out _);
            IntPtr loadLib = Win32.GetProcAddress(Win32.GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            IntPtr hThread = Win32.CreateRemoteThread(hp, IntPtr.Zero, 0, loadLib, mem, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero) { Win32.VirtualFreeEx(hp, mem, 0, Win32.MEM_RELEASE); return false; }

            Win32.WaitForSingleObject(hThread, 15000);
            Win32.GetExitCodeThread(hThread, out uint ec);
            Win32.CloseHandle(hThread);
            Win32.VirtualFreeEx(hp, mem, 0, Win32.MEM_RELEASE);
            return ec != 0;
        }
        catch { return false; }
    }

    // ── 查找 DLL 路径 ──
    private string? FindNativeDll()
    {
        var exeDir = AppContext.BaseDirectory;
        // 1. 同目录
        var dll = Path.Combine(exeDir, "GIEnhancer.Native.Injector.dll");
        if (File.Exists(dll)) return dll;
        // 2. 源码输出目录（开发环境：Launcher bin → 往上5层到 repo root → src/Native/...）
        dll = Path.GetFullPath(Path.Combine(exeDir,
            @"..\..\..\..\..\src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll"));
        if (File.Exists(dll)) { AppendLog($"  Native DLL found at: {dll}"); return dll; }
        // 3. 固定路径
        dll = @"E:\AI\hook\src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll";
        if (File.Exists(dll)) { AppendLog($"  Native DLL found at: {dll}"); return dll; }
        AppendLog($"  Searched: exeDir={exeDir}");
        return null;
    }

    private static string? FindReshadeDll(string gameDir)
    {
        // 1. 游戏目录
        var dll = Path.Combine(gameDir, "ReShade64.dll");
        if (File.Exists(dll)) return dll;
        // 2. tools目录
        var baseDir = AppContext.BaseDirectory;
        dll = Path.Combine(baseDir, "tools", "reshade", "ReShade64.dll");
        if (File.Exists(dll)) return dll;
        return null;
    }

    // ── 监控游戏退出 ──
    private void MonitorGameExit()
    {
        try
        {
            if (_gameProcessHandle != IntPtr.Zero)
                Win32.WaitForSingleObject(_gameProcessHandle, 0xFFFFFFFF);
        }
        catch { }
        finally
        {
            Dispatcher.Invoke(OnGameExit);
        }
    }

    // ── 停止 ──
    private async void BtnShutdown_Click(object sender, RoutedEventArgs e)
    {
        BtnShutdown.IsEnabled = false;
        try { Process.GetProcessById(_gamePid).Kill(); } catch { }
        if (_gameProcessHandle != IntPtr.Zero) { Win32.CloseHandle(_gameProcessHandle); }
        if (_gameThreadHandle != IntPtr.Zero) { Win32.CloseHandle(_gameThreadHandle); }
        _gameProcessHandle = IntPtr.Zero; _gameThreadHandle = IntPtr.Zero;
        _gameRunning = false;
        AppendLog("⏹ 已停止");
        BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true;
        UpdateStatus("就绪"); UpdateDllStatus("待检测", Brushes.Gray); UpdatePebStatus("—", Brushes.Gray);
    }

    private void OnGameExit()
    {
        AppendLog("🛑 游戏已退出");
        // 还原 Stella Mod ReShade64.dll
        var stellaRS = @"D:\LaunchGame\data\dependencies\reshade\ReShade64.dll.disabled";
        if (File.Exists(stellaRS))
        {
            try { File.Move(stellaRS, stellaRS.Replace(".disabled", "")); }
            catch { }
        }
        BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true;
        BtnShutdown.IsEnabled = false; _gameRunning = false;
        UpdateStatus("就绪"); UpdateDllStatus("待检测", Brushes.Gray); UpdatePebStatus("—", Brushes.Gray);
    }

    // ── 面板按钮 ──
    private void BtnReshadePanel_Click(object sender, RoutedEventArgs e)
    {
        var gamePath = TxtGamePath.Text.Trim();
        var gameDir = string.IsNullOrWhiteSpace(gamePath) ? "" : Path.GetDirectoryName(gamePath) ?? "";
        AppendLog("🎨 ReShade 面板");
        AppendLog($"  dinput8.dll: {(File.Exists(Path.Combine(gameDir, "dinput8.dll")) ? "✅" : "❌")}");
        AppendLog($"  ReShade64.dll: {(File.Exists(Path.Combine(gameDir, "ReShade64.dll")) ? "✅" : "❌")}");
        AppendLog($"  ReShade.ini: {(File.Exists(Path.Combine(gameDir, "ReShade.ini")) ? "✅" : "❌")}");
        AppendLog($"  dxgi.dll: {(File.Exists(Path.Combine(gameDir, "dxgi.dll")) ? "⚠️ 需删除" : "✅ 无冲突")}");
    }

    private void BtnMigotoPanel_Click(object sender, RoutedEventArgs e)
    {
        var gamePath = TxtGamePath.Text.Trim();
        var gameDir = string.IsNullOrWhiteSpace(gamePath) ? "" : Path.GetDirectoryName(gamePath) ?? "";
        AppendLog("🔧 3DMigoto 面板");
        AppendLog($"  d3d11.dll: {(File.Exists(Path.Combine(gameDir, "d3d11.dll")) ? "⚠️ 已忽略" : "✅ 无")}");
        AppendLog($"  rtlbase.dll: {(File.Exists(Path.Combine(gameDir, "rtlbase.dll")) ? "✅" : "❌")}");
        AppendLog($"  d3dx.ini: {(File.Exists(Path.Combine(gameDir, "d3dx.ini")) ? "✅" : "❌")}");
        AppendLog($"  注入方式: LoadLibrary (非D3D代理)");
    }

    // ── UI 辅助 ──
    private string BuildRecommendation()
    {
        if (_deviceInfo == null) return "—";
        var results = new PresetRecommender(PresetRecommender.GetBuiltInPresets()).Recommend(_deviceInfo, null);
        if (results.Count == 0) return "⚠️ 没有适合的预设";
        var lines = new List<string>();
        var medals = new[] { "★ 推荐", "⚡ 备选", "🎯 备选" };
        for (int i = 0; i < Math.Min(results.Count, 3); i++)
        {
            lines.Add($"{medals[i]} {results[i].DisplayName} — {results[i].Score:N0}分");
            lines.Add($"  预估 {results[i].EstimatedFpsMin}-{results[i].EstimatedFpsMax} FPS · GPU {results[i].GpuUtilization}%\n");
        }
        return string.Join("\n", lines);
    }

    private static readonly string _logFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GenshinImpactEnhancer", "launcher_inject.log");

    private void AppendLog(string msg)
    {
        StellaLogger.Info("Launcher", msg);
        Dispatcher.Invoke(() =>
        {
            if (TxtLog == null) return;
            try { TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n"); TxtLog.ScrollToEnd(); } catch { }
        });
    }
    private void UpdateStatus(string t) => Dispatcher.Invoke(() => { if (TxtStatus != null) TxtStatus.Text = t; });
    private void UpdateDllStatus(string t, Brush c) => Dispatcher.Invoke(() => { if (TxtDllStatus != null) { TxtDllStatus.Text = t; TxtDllStatus.Foreground = c; } });
    private void UpdatePebStatus(string t, Brush c) => Dispatcher.Invoke(() => { if (TxtPebStatus != null) { TxtPebStatus.Text = t; TxtPebStatus.Foreground = c; } });

    // ═══════════════════════════════════════════════════════════════
    // Win32 P/Invoke
    // ═══════════════════════════════════════════════════════════════
    private static class Win32
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
        [DllImport("kernel32.dll")] public static extern bool TerminateProcess(IntPtr h, uint ec);
        [DllImport("psapi.dll")] public static extern bool EnumProcessModulesEx(IntPtr p, [Out] IntPtr[] m, uint cb, out uint needed, uint flags);
        [DllImport("psapi.dll")] public static extern uint GetModuleFileNameEx(IntPtr p, IntPtr m, [Out] StringBuilder s, uint sz);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CreateProcessW(
            string? lpApp, string lpCmd, IntPtr lpProcAttr, IntPtr lpThreadAttr,
            bool bInherit, uint dwFlags, IntPtr lpEnv, string lpDir,
            ref STARTUPINFO lpSI, out PROCESS_INFORMATION lpPI);
        public const uint PROCESS_ALL_ACCESS = 0x1F0FFF, MEM_COMMIT = 0x1000, MEM_RESERVE = 0x2000, MEM_RELEASE = 0x8000, PAGE_READWRITE = 0x04, CREATE_SUSPENDED = 0x00000004;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STARTUPINFO
    {
        public uint cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public ushort wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
}
