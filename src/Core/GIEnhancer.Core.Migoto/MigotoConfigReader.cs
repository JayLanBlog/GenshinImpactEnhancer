using System.IO;
using System.Runtime.InteropServices;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Core.Migoto;

/// <summary>
/// 3DMigoto d3dx.ini 配置读取器。
/// 解析 [Hunting] 区段的热键绑定。
/// </summary>
public class MigotoConfigReader
{
    /// <summary>
    /// 3DMigoto 需要部署的 DLL 文件列表
    /// </summary>
    public static readonly string[] RequiredDlls = { "d3d11.dll", "d3dcompiler_47.dll", "nvapi64.dll" };

    /// <summary>
    /// d3dx.ini 中 [Hunting] 区段的热键键名列表
    /// </summary>
    public static readonly string[] HuntingHotkeyNames =
    {
        "next_pixelshader", "previous_pixelshader", "mark_pixelshader",
        "next_vertexshader", "previous_vertexshader", "mark_vertexshader",
        "next_indexbuffer", "previous_indexbuffer", "mark_indexbuffer",
        "next_rendertarget", "previous_rendertarget", "mark_rendertarget",
        "take_screenshot",
        "tune_up", "tune_down",
        "hunting",
    };

    /// <summary>
    /// d3dx.ini 中全局热键区段（非 Hunting）的键名与区段映射
    /// 用于扩展热键冲突检测范围
    /// </summary>
    public static readonly (string Section, string Key)[] GlobalHotkeyEntries =
    {
        ("KeyToggleMods", "Key"),
        ("KeyReloadMods", "Key"),
    };

    /// <summary>
    /// 热键绑定结果：键名 → 虚拟键码
    /// </summary>
    public Dictionary<string, int> Hotkeys { get; private set; } = new();

    /// <summary>
    /// d3dx.ini 是否成功加载
    /// </summary>
    public bool IniLoaded { get; private set; }

    /// <summary>
    /// Hunting 模式是否启用 (hunting=1 或 hunting=2)
    /// </summary>
    public bool HuntingEnabled { get; private set; }

    /// <summary>
    /// 从游戏目录读取 d3dx.ini
    /// </summary>
    public bool Load(string gameDir)
    {
        Hotkeys.Clear();
        IniLoaded = false;
        HuntingEnabled = false;

        string iniPath = Path.Combine(gameDir, "d3dx.ini");
        if (!File.Exists(iniPath))
        {
            StellaLogger.Info("MigotoConfig", $"d3dx.ini not found at {iniPath}");
            return false;
        }

        // 使用 kernel32 的 GetPrivateProfileString 读取 INI
        foreach (var keyName in HuntingHotkeyNames)
        {
            string value = ReadIniValue("Hunting", keyName, iniPath);
            if (!string.IsNullOrEmpty(value))
            {
                int vk = MigotoVKeyMap.Parse(value);
                Hotkeys[keyName] = vk;
            }
        }

        // 读取全局热键（非 Hunting 区段），如 KeyToggleMods、KeyReloadMods
        foreach (var (section, key) in GlobalHotkeyEntries)
        {
            string value = ReadIniValue(section, key, iniPath);
            if (!string.IsNullOrEmpty(value))
            {
                int vk = MigotoVKeyMap.Parse(value);
                if (vk > 0)
                    Hotkeys[section] = vk;
            }
        }

        // 检查 hunting 模式
        string huntingVal = ReadIniValue("Hunting", "hunting", iniPath);
        if (!string.IsNullOrEmpty(huntingVal) && int.TryParse(huntingVal, out int hVal))
            HuntingEnabled = hVal > 0;

        IniLoaded = true;
        StellaLogger.Info("MigotoConfig", $"Loaded {Hotkeys.Count} hotkey bindings, hunting={(HuntingEnabled ? "ON" : "OFF")}");
        return true;
    }

    /// <summary>
    /// 检测工具目录下是否存在 3DMigoto 的所有必需 DLL
    /// </summary>
    public static bool DetectDlls(string toolDir)
    {
        string migotoDir = Path.Combine(toolDir, "3dmigoto");
        if (!Directory.Exists(migotoDir))
        {
            StellaLogger.Info("MigotoConfig", $"3dmigoto directory not found: {migotoDir}");
            return false;
        }

        bool allPresent = true;
        foreach (var dll in RequiredDlls)
        {
            string path = Path.Combine(migotoDir, dll);
            bool exists = File.Exists(path);
            StellaLogger.Info("MigotoConfig", $"  {dll}: {(exists ? "FOUND" : "MISSING")}");
            if (!exists) allPresent = false;
        }

        return allPresent;
    }

    /// <summary>
    /// 将 3DMigoto DLL 复制到游戏目录（备份已有文件）
    /// </summary>
    public static bool DeployToGameDir(string toolDir, string gameDir)
    {
        string migotoDir = Path.Combine(toolDir, "3dmigoto");
        string backupDir = Path.Combine(gameDir, "backup_migoto");

        try
        {
            // 创建备份目录
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            foreach (var dll in RequiredDlls)
            {
                string src = Path.Combine(migotoDir, dll);
                string dst = Path.Combine(gameDir, dll);
                string bak = Path.Combine(backupDir, dll);

                if (!File.Exists(src))
                {
                    StellaLogger.Warn("MigotoConfig", $"Source DLL missing: {dll}");
                    return false;
                }

                // 备份已有文件
                if (File.Exists(dst))
                {
                    File.Copy(dst, bak, overwrite: true);
                    StellaLogger.Info("MigotoConfig", $"Backed up existing {dll}");
                }

                // 复制 3DMigoto DLL
                File.Copy(src, dst, overwrite: true);
                StellaLogger.Info("MigotoConfig", $"Deployed {dll} → game dir");
            }

            // 补全 d3dx.ini 反检测配置
            EnsureAntiDetectionConfig(gameDir);

            return true;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("MigotoConfig", $"Deploy failed: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// 从游戏目录还原备份的 DLL
    /// </summary>
    public static bool RestoreBackup(string gameDir)
    {
        string backupDir = Path.Combine(gameDir, "backup_migoto");
        if (!Directory.Exists(backupDir)) return true;

        try
        {
            foreach (var dll in RequiredDlls)
            {
                string bak = Path.Combine(backupDir, dll);
                string dst = Path.Combine(gameDir, dll);
                if (File.Exists(bak))
                {
                    File.Copy(bak, dst, overwrite: true);
                    StellaLogger.Info("MigotoConfig", $"Restored {dll} from backup");
                }
            }
            Directory.Delete(backupDir, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("MigotoConfig", $"Restore failed: {ex.Message}", ex);
            return false;
        }
    }

    // ── 反检测配置补全 ──

    /// <summary>
    /// 需要确保写入 d3dx.ini 的反检测配置项
    /// 格式：(区段, 键名, 安全值)
    /// </summary>
    private static readonly (string Section, string Key, string Value)[] AntiDetectionSettings =
    {
        ("System", "load_library_redirect", "0"),     // 禁用 LoadLibrary 重定向，减少模块痕迹
        ("System", "check_foreground_window", "0"),   // 不检查前台窗口，避免检测循环
        ("Logging", "calls", "0"),                     // 禁用 API 调用日志
        ("Logging", "input", "0"),                     // 禁用输入日志
        ("Logging", "debug", "0"),                     // 禁用调试日志
    };

    /// <summary>
    /// 确保 d3dx.ini 包含正确的反检测配置项。
    /// 仅在配置项缺失或值不正确时写入，避免覆盖用户自定义设置。
    /// </summary>
    public static void EnsureAntiDetectionConfig(string gameDir)
    {
        string iniPath = Path.Combine(gameDir, "d3dx.ini");
        if (!File.Exists(iniPath))
        {
            StellaLogger.Warn("MigotoConfig", "d3dx.ini not found, cannot ensure anti-detection config");
            return;
        }

        int patched = 0;
        foreach (var (section, key, safeValue) in AntiDetectionSettings)
        {
            string current = ReadIniValue(section, key, iniPath);
            if (current != safeValue)
            {
                WriteIniValue(section, key, safeValue, iniPath);
                patched++;
                StellaLogger.Info("MigotoConfig", $"Anti-detection: [{section}] {key} = {safeValue} (was '{current}')");
            }
        }

        if (patched > 0)
            StellaLogger.Info("MigotoConfig", $"Patched {patched} anti-detection settings in d3dx.ini");
        else
            StellaLogger.Info("MigotoConfig", "Anti-detection config already correct");
    }

    // ── INI 读写（P/Invoke）──
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetPrivateProfileStringW(
        string lpAppName, string lpKeyName, string lpDefault,
        [Out] char[] lpReturnedString, uint nSize, string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WritePrivateProfileStringW(
        string lpAppName, string lpKeyName, string lpString, string lpFileName);

    private static string ReadIniValue(string section, string key, string filePath)
    {
        char[] buf = new char[4096];
        uint len = GetPrivateProfileStringW(section, key, "", buf, (uint)buf.Length, filePath);
        return len > 0 ? new string(buf, 0, (int)len) : "";
    }

    private static void WriteIniValue(string section, string key, string value, string filePath)
    {
        WritePrivateProfileStringW(section, key, value, filePath);
    }
}
