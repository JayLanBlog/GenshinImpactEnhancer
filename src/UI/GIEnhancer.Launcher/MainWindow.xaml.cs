using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using GIEnhancer.DeviceIdentifier;
using GIEnhancer.Core;
using GIEnhancer.Core.Reshade;
using GIEnhancer.Core.FpsUnlock;
using GIEnhancer.Core.Migoto;
using GIEnhancer.Services;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Launcher;

public partial class MainWindow : Window
{
    private EngineManager? _engineManager;
    private ReshadeEngine? _reshadeEngine;
    private MigotoEngine? _migotoEngine;
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

    // ── 一键启动（完整流程）──
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

            // 1. 初始化引擎（同时部署 ReShade + 3DMigoto DLL 到游戏目录）
            AppendLog("⚙️ 初始化引擎模块...");
            _reshadeEngine = new ReshadeEngine();
            _migotoEngine = new MigotoEngine();
            var engines = new IEngineModule[] { _reshadeEngine, new FpsUnlockEngine { TargetFps = 144 }, _migotoEngine };
            _engineManager = new EngineManager(engines);
            await _engineManager.LaunchAllAsync(gameDir);
            foreach (var eng in _engineManager.Engines)
                AppendLog($"  {(eng.IsInitialized ? "✅" : "❌")} {eng.Name}");

            // 2. 热键冲突检测（仅在 3DMigoto 和 ReShade 都加载时）
            if (_migotoEngine.IsInitialized)
            {
                AppendLog("🔍 检测热键冲突...");
                var reshadeKeys = ReadReshadeHotkeys(gameDir);
                var migotoKeys = _migotoEngine.GetHotkeys();

                var resolver = new HotkeyConflictResolver();
                var conflicts = resolver.DetectConflicts(reshadeKeys, migotoKeys);

                if (conflicts.Count > 0)
                {
                    AppendLog($"⚠️ 检测到 {conflicts.Count} 个热键冲突！");
                    foreach (var c in conflicts)
                        AppendLog($"  🔑 {c.KeyName}: ReShade [{string.Join(", ", c.ReShadeUsages)}] vs 3DMigoto [{string.Join(", ", c.MigotoUsages)}]");

                    // 弹出冲突解决对话框
                    var dlg = new HotkeyConflictDialog(conflicts);
                    dlg.Owner = this;
                    dlg.ShowDialog();

                    if (!dlg.Applied)
                    {
                        AppendLog("⏹ 用户取消启动");
                        BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true;
                        await _engineManager.ShutdownAllAsync();
                        return;
                    }

                    // 应用用户选择的解决方案
                    foreach (var conflict in conflicts)
                    {
                        resolver.ApplyResolution(conflict, gameDir);
                    }
                    AppendLog("✅ 热键冲突已解决");
                }
                else
                {
                    AppendLog("✅ 无热键冲突");
                }
            }

            // 3. 挂起创建游戏进程
            AppendLog($"🎯 创建进程: {Path.GetFileName(gamePath)}");
            var si = new STARTUPINFO(); si.cb = (uint)Marshal.SizeOf<STARTUPINFO>();
            var pi = new PROCESS_INFORMATION();
            bool created = Win32.CreateProcessW(gamePath, null, IntPtr.Zero, IntPtr.Zero, false,
                Win32.CREATE_SUSPENDED, IntPtr.Zero, gameDir, ref si, out pi);
            if (!created)
            { AppendLog($"❌ CreateProcess 失败: {Marshal.GetLastWin32Error()}"); BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true; return; }
            _gameProcessHandle = pi.hProcess; _gameThreadHandle = pi.hThread; _gamePid = pi.dwProcessId; _gameRunning = true;
            AppendLog($"✅ PID={_gamePid} (挂起)");

            // 4. 注入
            await Task.Run(() => InjectDll(gameDir));

            // 5. 恢复
            Win32.ResumeThread(_gameThreadHandle);
            AppendLog("▶ 进程已恢复");
            AppendLog("✅ 启动完成！");

            UpdateStatus($"✅ 运行中 PID={_gamePid}");
            BtnShutdown.IsEnabled = true;

            // 6. 监控退出
            _ = Task.Run(() => { try { Process.GetProcessById(_gamePid).WaitForExit(); } catch { } finally { Dispatcher.Invoke(OnGameExit); } });
        }
        catch (Exception ex) { AppendLog($"❌ {ex.Message}"); BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true; }
    }

    /// <summary>
    /// 读取 ReShade.ini 的热键配置（用于冲突检测）
    /// </summary>
    private Dictionary<string, int> ReadReshadeHotkeys(string gameDir)
    {
        var keys = new Dictionary<string, int>();
        string iniPath = Path.Combine(gameDir, "ReShade.ini");
        if (!File.Exists(iniPath)) return keys;

        string[] keyNames = { "KeyOverlay", "KeyEffects", "KeyReload" };
        foreach (var name in keyNames)
        {
            string val = ReadIniValue("INPUT", name, iniPath);
            if (!string.IsNullOrEmpty(val) && int.TryParse(val, out int vk))
                keys[name] = vk;
        }
        return keys;
    }

    private void InjectDll(string gameDir)
    {
        var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        var dllPath = Path.Combine(exeDir, "GIEnhancer.Native.Injector.dll");
        if (!File.Exists(dllPath))
            dllPath = Path.GetFullPath(Path.Combine(exeDir, @"..\..\..\..\..\src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll"));
        if (!File.Exists(dllPath))
        { AppendLog("⚠️ Native DLL 未找到，游戏纯净模式运行"); UpdateDllStatus("未找到", Brushes.Orange); return; }

        AppendLog($"💉 注入: {Path.GetFileName(dllPath)}"); UpdateDllStatus("注入中...", Brushes.Gold);
        IntPtr hProcess = Win32.OpenProcess(Win32.PROCESS_ALL_ACCESS, false, _gamePid);
        if (hProcess == IntPtr.Zero) { AppendLog("❌ OpenProcess 失败"); UpdateDllStatus("权限不足", Brushes.Red); return; }

        try
        {
            byte[] bytes = Encoding.Unicode.GetBytes(dllPath + '\0'); uint size = (uint)bytes.Length;
            IntPtr mem = Win32.VirtualAllocEx(hProcess, IntPtr.Zero, size, Win32.MEM_COMMIT | Win32.MEM_RESERVE, Win32.PAGE_READWRITE);
            if (mem == IntPtr.Zero) { AppendLog("❌ VirtualAllocEx 失败"); return; }
            Win32.WriteProcessMemory(hProcess, mem, bytes, size, out _);
            IntPtr loadLib = Win32.GetProcAddress(Win32.GetModuleHandle("kernel32.dll"), "LoadLibraryW");
            IntPtr hThread = Win32.CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLib, mem, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero) { Win32.VirtualFreeEx(hProcess, mem, 0, Win32.MEM_RELEASE); AppendLog("❌ CreateRemoteThread 失败"); return; }
            Win32.WaitForSingleObject(hThread, 10000);
            Win32.GetExitCodeThread(hThread, out uint ec);
            Win32.CloseHandle(hThread);
            Win32.VirtualFreeEx(hProcess, mem, 0, Win32.MEM_RELEASE);
            if (ec == 0) { AppendLog("❌ LoadLibrary 返回 NULL"); UpdateDllStatus("失败", Brushes.Red); UpdatePebStatus("—", Brushes.Gray); }
            else
            {
                AppendLog($"✅ 注入成功 (0x{ec:X})");
                UpdateDllStatus("✅ 已注入", Brushes.LimeGreen);
                // PEB 隐藏：DLL 在 DllMain 中自动从模块链表摘除
                AppendLog("🛡️ PEB 模块隐藏：DLL 已从 LDR 链表摘除");
                AppendLog("   → EnumProcessModules / CreateToolhelp32Snapshot 不可见");
                UpdatePebStatus("✅ 已隐藏", Brushes.LimeGreen);
            }
        }
        finally { Win32.CloseHandle(hProcess); }
    }

    // ── 停止 ──
    private async void BtnShutdown_Click(object sender, RoutedEventArgs e)
    {
        BtnShutdown.IsEnabled = false;
        if (_engineManager != null) await _engineManager.ShutdownAllAsync();
        // 还原 ReShade + 3DMigoto 备份
        if (_reshadeEngine != null) await _reshadeEngine.RestoreBackupAsync();
        if (_migotoEngine != null) await _migotoEngine.RestoreBackupAsync();
        try { Process.GetProcessById(_gamePid).Kill(); } catch { }
        if (_gameProcessHandle != IntPtr.Zero) { Win32.CloseHandle(_gameProcessHandle); if (_gameThreadHandle != IntPtr.Zero) Win32.CloseHandle(_gameThreadHandle); }
        _gameRunning = false; AppendLog("⏹ 已停止");
        BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true; UpdateStatus("就绪"); UpdateDllStatus("待检测", Brushes.Gray); UpdatePebStatus("—", Brushes.Gray);
    }

    private void OnGameExit()
    {
        AppendLog("🛑 游戏已退出");
        // 还原 ReShade + 3DMigoto 备份
        if (_reshadeEngine != null)
        {
            _ = _reshadeEngine.RestoreBackupAsync();
            AppendLog("📦 ReShade DLL 备份已还原");
        }
        if (_migotoEngine != null)
        {
            _ = _migotoEngine.RestoreBackupAsync();
            AppendLog("📦 3DMigoto DLL 备份已还原");
        }
        BtnLaunch.IsEnabled = true; BtnSearch.IsEnabled = true;
        BtnShutdown.IsEnabled = false; _gameRunning = false; UpdateStatus("就绪"); UpdateDllStatus("待检测", Brushes.Gray); UpdatePebStatus("—", Brushes.Gray);
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
        { lines.Add($"{medals[i]} {results[i].DisplayName} — {results[i].Score:N0}分"); lines.Add($"  预估 {results[i].EstimatedFpsMin}-{results[i].EstimatedFpsMax} FPS · GPU {results[i].GpuUtilization}%\n"); }
        return string.Join("\n", lines);
    }

    private void AppendLog(string msg) => Dispatcher.Invoke(() => { TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n"); TxtLog.ScrollToEnd(); });
    private void UpdateStatus(string t) => Dispatcher.Invoke(() => TxtStatus.Text = t);
    private void UpdateDllStatus(string t, Brush c) => Dispatcher.Invoke(() => { TxtDllStatus.Text = t; TxtDllStatus.Foreground = c; });
    private void UpdatePebStatus(string t, Brush c) => Dispatcher.Invoke(() => { TxtPebStatus.Text = t; TxtPebStatus.Foreground = c; });

    // ── Win32 P/Invoke ──
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
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CreateProcessW(string? an, string? cl, IntPtr pa, IntPtr ta, bool ih, uint cf, IntPtr e, string? cd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        public const uint PROCESS_ALL_ACCESS = 0x1F0FFF, MEM_COMMIT = 0x1000, MEM_RESERVE = 0x2000, MEM_RELEASE = 0x8000, PAGE_READWRITE = 0x04, CREATE_SUSPENDED = 0x00000004;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STARTUPINFO { public uint cb; public string lpReserved, lpDesktop, lpTitle; public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public ushort wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    // ── INI 读取 ──
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetPrivateProfileStringW(
        string lpAppName, string lpKeyName, string lpDefault,
        [Out] char[] lpReturnedString, uint nSize, string lpFileName);

    private static string ReadIniValue(string section, string key, string filePath)
    {
        char[] buf = new char[4096];
        uint len = GetPrivateProfileStringW(section, key, "", buf, (uint)buf.Length, filePath);
        return len > 0 ? new string(buf, 0, (int)len) : "";
    }
}
