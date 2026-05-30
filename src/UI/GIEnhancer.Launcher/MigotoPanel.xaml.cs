using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using GIEnhancer.Core.Migoto;

namespace GIEnhancer.Launcher;

public partial class MigotoPanel : Window
{
    public MigotoPanel(MigotoEngine? engine = null, string? gameDir = null)
    {
        InitializeComponent();
        LoadData(engine, gameDir);
    }

    private void LoadData(MigotoEngine? engine, string? gameDir)
    {
        if (engine != null)
        {
            TxtInitStatus.Text = engine.IsInitialized ? "✅ 已初始化" : "❌ 未初始化";
            TxtInitStatus.Foreground = engine.IsInitialized ? Brushes.LimeGreen : Brushes.OrangeRed;
            TxtDeployStatus.Text = engine.IsDeployed ? "✅ 已部署" : "❌ 未部署";
            TxtDeployStatus.Foreground = engine.IsDeployed ? Brushes.LimeGreen : Brushes.OrangeRed;
            TxtModCount.Text = engine.LoadedModCount.ToString();
            TxtToolDir.Text = engine.ToolDir ?? "—";
            gameDir ??= engine.GameDir;

            if (engine.Config != null)
            {
                TxtHunting.Text = engine.Config.HuntingEnabled ? "✅ 开启" : "❌ 关闭";
                TxtHunting.Foreground = engine.Config.HuntingEnabled ? Brushes.LimeGreen : Brushes.OrangeRed;
            }
        }

        TxtGameDir.Text = gameDir ?? "—";

        if (!string.IsNullOrEmpty(gameDir))
        {
            CheckFile("TxtD3d11", Path.Combine(gameDir, "d3d11.dll"));
            CheckFile("TxtD3dcompiler", Path.Combine(gameDir, "d3dcompiler_47.dll"));
            CheckFile("TxtNvapi", Path.Combine(gameDir, "nvapi64.dll"));
            LoadAntiDetection(gameDir);
            LoadHotkeys(gameDir);
            LoadMods(gameDir);
        }
    }

    private void CheckFile(string txtName, string path)
    {
        var txt = FindName(txtName) as System.Windows.Controls.TextBlock;
        if (txt == null) return;
        txt.Text = File.Exists(path) ? "✅ 存在" : "❌ 不存在";
        txt.Foreground = File.Exists(path) ? Brushes.LimeGreen : Brushes.OrangeRed;
    }

    private void LoadAntiDetection(string gameDir)
    {
        string iniPath = Path.Combine(gameDir, "d3dx.ini");
        if (!File.Exists(iniPath))
        {
            TxtAntiDet.Text = "❌ d3dx.ini 不存在";
            TxtAntiDet.Foreground = Brushes.OrangeRed;
            return;
        }

        string loadLib = ReadIniValue("System", "load_library_redirect", iniPath);
        string checkFg = ReadIniValue("System", "check_foreground_window", iniPath);
        string logCalls = ReadIniValue("Logging", "calls", iniPath);
        string logInput = ReadIniValue("Logging", "input", iniPath);
        string logDebug = ReadIniValue("Logging", "debug", iniPath);

        bool loadLibOk = loadLib == "0";
        bool checkFgOk = checkFg == "0";
        bool logOk = logCalls == "0" && logInput == "0" && logDebug == "0";
        bool allOk = loadLibOk && checkFgOk && logOk;

        TxtAntiDet.Text = allOk ? "✅ 安全" : "⚠️ 有风险";
        TxtAntiDet.Foreground = allOk ? Brushes.LimeGreen : Brushes.OrangeRed;

        TxtLoadLib.Text = loadLibOk ? "✅ 0 (禁用)" : $"⚠️ {loadLib}";
        TxtLoadLib.Foreground = loadLibOk ? Brushes.LimeGreen : Brushes.OrangeRed;
        TxtCheckFg.Text = checkFgOk ? "✅ 0 (禁用)" : $"⚠️ {checkFg}";
        TxtCheckFg.Foreground = checkFgOk ? Brushes.LimeGreen : Brushes.OrangeRed;
        TxtLogging.Text = logOk ? "✅ 全部关闭" : $"⚠️ calls={logCalls} input={logInput} debug={logDebug}";
        TxtLogging.Foreground = logOk ? Brushes.LimeGreen : Brushes.OrangeRed;
    }

    private void LoadHotkeys(string gameDir)
    {
        string iniPath = Path.Combine(gameDir, "d3dx.ini");
        if (!File.Exists(iniPath))
        {
            LstHotkeys.Items.Add(new { Section = "—", Function = "(d3dx.ini 不存在)", KeyName = "—" });
            return;
        }

        var hotkeys = new List<dynamic>();

        // Hunting section hotkeys
        var huntingNames = new (string Key, string Func)[]
        {
            ("hunting", "Hunting 模式开关"),
            ("next_pixelshader", "下一个像素着色器"),
            ("previous_pixelshader", "上一个像素着色器"),
            ("mark_pixelshader", "标记像素着色器"),
            ("next_vertexshader", "下一个顶点着色器"),
            ("previous_vertexshader", "上一个顶点着色器"),
            ("take_screenshot", "截图"),
            ("tune_up", "调高参数"),
            ("tune_down", "调低参数"),
        };

        foreach (var (key, func) in huntingNames)
        {
            string val = ReadIniValue("Hunting", key, iniPath);
            if (!string.IsNullOrEmpty(val))
                hotkeys.Add(new { Section = "[Hunting]", Function = func, KeyName = VkToName(val) });
        }

        // Global hotkeys
        string toggleKey = ReadIniValue("KeyToggleMods", "Key", iniPath);
        if (!string.IsNullOrEmpty(toggleKey))
            hotkeys.Add(new { Section = "[KeyToggleMods]", Function = "Mod 开关切换", KeyName = VkToName(toggleKey) });

        string reloadKey = ReadIniValue("KeyReloadMods", "Key", iniPath);
        if (!string.IsNullOrEmpty(reloadKey))
            hotkeys.Add(new { Section = "[KeyReloadMods]", Function = "Mod 重载", KeyName = VkToName(reloadKey) });

        if (hotkeys.Count == 0)
            hotkeys.Add(new { Section = "—", Function = "(无热键配置)", KeyName = "—" });

        LstHotkeys.ItemsSource = hotkeys;
    }

    private void LoadMods(string gameDir)
    {
        string modsDir = Path.Combine(gameDir, "Mods");
        if (!Directory.Exists(modsDir))
        {
            LstMods.Items.Add(new { Name = "(Mods 目录不存在)", FileCount = "0" });
            return;
        }

        var mods = new List<dynamic>();
        foreach (var dir in Directory.GetDirectories(modsDir))
        {
            string name = Path.GetFileName(dir);
            int fileCount = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
            mods.Add(new { Name = name, FileCount = fileCount.ToString() });
        }

        if (mods.Count == 0)
            mods.Add(new { Name = "(空目录)", FileCount = "0" });

        LstMods.ItemsSource = mods;
    }

    private static string VkToName(string vk)
    {
        if (!int.TryParse(vk, out int code)) return vk;
        return code switch
        {
            0x24 => "Home", 0x2D => "Insert", 0x2E => "Delete",
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
            0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
            0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter",
            0x1B => "Escape", 0x20 => "Space", 0x2C => "PrintScreen",
            0x13 => "Pause", 0x14 => "CapsLock", 0x91 => "ScrollLock",
            _ => $"0x{code:X2}"
        };
    }

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

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
