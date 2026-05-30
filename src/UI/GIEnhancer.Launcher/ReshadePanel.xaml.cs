using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using GIEnhancer.Core.Reshade;

namespace GIEnhancer.Launcher;

public partial class ReshadePanel : Window
{
    public ReshadePanel(ReshadeEngine? engine = null, string? gameDir = null)
    {
        InitializeComponent();
        LoadData(engine, gameDir);
    }

    private void LoadData(ReshadeEngine? engine, string? gameDir)
    {
        if (engine != null)
        {
            TxtInitStatus.Text = engine.IsInitialized ? "✅ 已初始化" : "❌ 未初始化";
            TxtInitStatus.Foreground = engine.IsInitialized
                ? System.Windows.Media.Brushes.LimeGreen
                : System.Windows.Media.Brushes.OrangeRed;
            TxtDeployStatus.Text = engine.IsDeployed ? "✅ 已部署" : "❌ 未部署";
            TxtDeployStatus.Foreground = engine.IsDeployed
                ? System.Windows.Media.Brushes.LimeGreen
                : System.Windows.Media.Brushes.OrangeRed;
            TxtActivePreset.Text = engine.ActivePreset ?? "无";
            TxtPresetDir.Text = engine.PresetDirectory ?? "—";
            TxtToolDir.Text = engine.ToolDir ?? "—";
            gameDir ??= engine.GameDir;
        }

        TxtGameDir.Text = gameDir ?? "—";

        if (!string.IsNullOrEmpty(gameDir))
        {
            CheckFile("TxtDxgi", Path.Combine(gameDir, "dxgi.dll"));
            CheckFile("TxtReShade64", Path.Combine(gameDir, "ReShade64.dll"));
            CheckFile("TxtReShadeIni", Path.Combine(gameDir, "ReShade.ini"));
            LoadHotkeys(gameDir);
        }
    }

    private void CheckFile(string txtName, string path)
    {
        var txt = this.FindName(txtName) as System.Windows.Controls.TextBlock;
        if (txt == null) return;
        if (File.Exists(path))
        {
            txt.Text = "✅ 存在";
            txt.Foreground = System.Windows.Media.Brushes.LimeGreen;
        }
        else
        {
            txt.Text = "❌ 不存在";
            txt.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    private void LoadHotkeys(string gameDir)
    {
        string iniPath = Path.Combine(gameDir, "ReShade.ini");
        if (!File.Exists(iniPath))
        {
            LstHotkeys.Items.Add(new { Function = "(ReShade.ini 不存在)", KeyName = "—", VkCode = "—" });
            return;
        }

        var hotkeys = new List<dynamic>();
        string[] keyNames = { "KeyOverlay", "KeyEffects", "KeyReload", "KeyScreenshot", "KeyPerformanceMode" };
        string[] functions = { "打开/关闭叠加层", "切换效果", "重载着色器", "截图", "性能模式" };

        for (int i = 0; i < keyNames.Length; i++)
        {
            string val = ReadIniValue("INPUT", keyNames[i], iniPath);
            if (!string.IsNullOrEmpty(val))
            {
                string keyName = VkCodeToName(val);
                hotkeys.Add(new { Function = functions[i], KeyName = keyName, VkCode = val });
            }
        }

        if (hotkeys.Count == 0)
            hotkeys.Add(new { Function = "(无热键配置)", KeyName = "—", VkCode = "—" });

        LstHotkeys.ItemsSource = hotkeys;
    }

    private static string VkCodeToName(string vk)
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
