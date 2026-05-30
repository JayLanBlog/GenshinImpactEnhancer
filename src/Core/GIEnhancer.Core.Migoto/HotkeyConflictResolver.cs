using System.IO;
using System.Runtime.InteropServices;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Core.Migoto;

/// <summary>
/// 热键冲突条目
/// </summary>
public class HotkeyConflict
{
    /// <summary>冲突的虚拟键码</summary>
    public int VirtualKey { get; set; }

    /// <summary>键的可读名称（如 "HOME", "PRINTSCREEN"）</summary>
    public string KeyName { get; set; } = "";

    /// <summary>使用此键的 ReShade 功能列表</summary>
    public List<string> ReShadeUsages { get; set; } = new();

    /// <summary>使用此键的 3DMigoto 功能列表</summary>
    public List<string> MigotoUsages { get; set; } = new();

    /// <summary>用户选择：修改哪一方 ("reshade" / "migoto" / "both")</summary>
    public string ModifyTarget { get; set; } = "migoto";

    /// <summary>用户选择：替换为哪个键</summary>
    public int ReplaceWithVk { get; set; }
}

/// <summary>
/// 热键冲突检测与解决器。
/// 比较 ReShade 和 3DMigoto 的热键配置，检测并解决冲突。
/// </summary>
public class HotkeyConflictResolver
{
    /// <summary>
    /// ReShade [INPUT] 区段的热键键名 → 功能描述
    /// </summary>
    private static readonly Dictionary<string, string> ReShadeKeyFunctions = new()
    {
        { "KeyOverlay", "ReShade 菜单切换" },
        { "KeyEffects", "效果开关" },
        { "KeyReload", "重载着色器" },
    };

    /// <summary>
    /// 3DMigoto 热键键名 → 功能描述（覆盖 Hunting 和全局热键）
    /// </summary>
    private static readonly Dictionary<string, string> MigotoKeyFunctions = new()
    {
        // [Hunting] 区段
        { "next_pixelshader", "下一个像素着色器" },
        { "previous_pixelshader", "上一个像素着色器" },
        { "mark_pixelshader", "标记像素着色器" },
        { "next_vertexshader", "下一个顶点着色器" },
        { "previous_vertexshader", "上一个顶点着色器" },
        { "mark_vertexshader", "标记顶点着色器" },
        { "next_indexbuffer", "下一个索引缓冲" },
        { "previous_indexbuffer", "上一个索引缓冲" },
        { "mark_indexbuffer", "标记索引缓冲" },
        { "next_rendertarget", "下一个渲染目标" },
        { "previous_rendertarget", "上一个渲染目标" },
        { "mark_rendertarget", "标记渲染目标" },
        { "take_screenshot", "截图" },
        { "tune_up", "参数调大" },
        { "tune_down", "参数调小" },
        // 全局热键区段
        { "KeyToggleMods", "Mod 开关切换" },
        { "KeyReloadMods", "Mod 重载" },
    };

    /// <summary>
    /// 建议的替代键列表（用于冲突解决）
    /// </summary>
    public static readonly (int Vk, string Name)[] SuggestedReplacementKeys =
    {
        (0x7B, "F12"), (0x2D, "INSERT"), (0x2E, "DELETE"),
        (0x23, "END"), (0x21, "PAGEUP"), (0x22, "PAGEDOWN"),
        (0x0D, "ENTER"), (0x09, "TAB"),
    };

    /// <summary>
    /// 检测 ReShade 和 3DMigoto 之间的热键冲突
    /// </summary>
    public List<HotkeyConflict> DetectConflicts(
        Dictionary<string, int> reshadeKeys,
        Dictionary<string, int> migotoKeys)
    {
        var conflicts = new List<HotkeyConflict>();

        // 收集 ReShade 使用的所有虚拟键码
        var reshadeVkMap = new Dictionary<int, List<string>>();
        foreach (var kv in reshadeKeys)
        {
            if (kv.Value > 0)
            {
                if (!reshadeVkMap.ContainsKey(kv.Value))
                    reshadeVkMap[kv.Value] = new List<string>();
                reshadeVkMap[kv.Value].Add(kv.Key);
            }
        }

        // 收集 3DMigoto 使用的所有虚拟键码
        var migotoVkMap = new Dictionary<int, List<string>>();
        foreach (var kv in migotoKeys)
        {
            if (kv.Value > 0)
            {
                if (!migotoVkMap.ContainsKey(kv.Value))
                    migotoVkMap[kv.Value] = new List<string>();
                migotoVkMap[kv.Value].Add(kv.Key);
            }
        }

        // 找出交集
        foreach (var vk in reshadeVkMap.Keys)
        {
            if (migotoVkMap.TryGetValue(vk, out var migotoUsages))
            {
                var reshadeUsages = reshadeVkMap[vk];
                var conflict = new HotkeyConflict
                {
                    VirtualKey = vk,
                    KeyName = MigotoVKeyMap.VkToName(vk),
                };

                foreach (var key in reshadeUsages)
                    if (ReShadeKeyFunctions.TryGetValue(key, out var desc))
                        conflict.ReShadeUsages.Add($"{desc} ({key})");

                foreach (var key in migotoUsages)
                    if (MigotoKeyFunctions.TryGetValue(key, out var desc))
                        conflict.MigotoUsages.Add($"{desc} ({key})");

                // 默认修改 3DMigoto 侧，选择一个安全的替代键
                conflict.ModifyTarget = "migoto";
                conflict.ReplaceWithVk = FindSafeReplacement(vk, reshadeVkMap, migotoVkMap);

                conflicts.Add(conflict);
                StellaLogger.Warn("HotkeyConflict",
                    $"Conflict: {conflict.KeyName} used by ReShade [{string.Join(", ", conflict.ReShadeUsages)}] " +
                    $"and 3DMigoto [{string.Join(", ", conflict.MigotoUsages)}]");
            }
        }

        return conflicts;
    }

    /// <summary>
    /// 找到一个不与任何现有热键冲突的替代键
    /// </summary>
    private int FindSafeReplacement(int conflictVk,
        Dictionary<int, List<string>> reshadeKeys,
        Dictionary<int, List<string>> migotoKeys)
    {
        // 收集所有已使用的键
        var usedKeys = new HashSet<int>(reshadeKeys.Keys);
        foreach (var vk in migotoKeys.Keys) usedKeys.Add(vk);
        usedKeys.Add(0x7A); // F11 - Enhancer 自身使用

        foreach (var (vk, name) in SuggestedReplacementKeys)
        {
            if (vk != conflictVk && !usedKeys.Contains(vk))
                return vk;
        }

        // 兜底：F12
        return 0x7B;
    }

    /// <summary>
    /// 将冲突解决方案应用到 INI 文件
    /// </summary>
    public bool ApplyResolution(HotkeyConflict conflict, string gameDir)
    {
        if (conflict.ReplaceWithVk == 0) return false;

        string replaceName = MigotoVKeyMap.VkToName(conflict.ReplaceWithVk);

        if (conflict.ModifyTarget == "migoto" || conflict.ModifyTarget == "both")
        {
            string iniPath = Path.Combine(gameDir, "d3dx.ini");
            if (!File.Exists(iniPath))
            {
                StellaLogger.Warn("HotkeyConflict", $"d3dx.ini not found, cannot modify Migoto hotkeys");
                return false;
            }

            // 找出 3DMigoto 中使用冲突键的所有键名
            foreach (var usage in conflict.MigotoUsages)
            {
                // usage 格式: "截图 (take_screenshot)" 或 "Mod 开关切换 (KeyToggleMods)"
                int parenIdx = usage.IndexOf('(');
                if (parenIdx >= 0)
                {
                    string keyName = usage[(parenIdx + 1)..].TrimEnd(')');
                    // 判断该键属于哪个区段：全局热键用自身名称作为区段，Hunting 热键统一用 Hunting
                    string section = MigotoConfigReader.GlobalHotkeyEntries
                        .Any(e => e.Section == keyName) ? keyName : "Hunting";
                    WriteIniValue(section, "Key", replaceName, iniPath);
                    StellaLogger.Info("HotkeyConflict",
                        $"Modified d3dx.ini [{section}] Key = {replaceName} (was {conflict.KeyName})");
                }
            }
        }

        if (conflict.ModifyTarget == "reshade" || conflict.ModifyTarget == "both")
        {
            string iniPath = Path.Combine(gameDir, "ReShade.ini");
            if (!File.Exists(iniPath))
            {
                StellaLogger.Warn("HotkeyConflict", $"ReShade.ini not found, cannot modify ReShade hotkeys");
                return false;
            }

            foreach (var usage in conflict.ReShadeUsages)
            {
                int parenIdx = usage.IndexOf('(');
                if (parenIdx >= 0)
                {
                    string keyName = usage[(parenIdx + 1)..].TrimEnd(')');
                    WriteIniValue("INPUT", keyName, conflict.ReplaceWithVk.ToString(), iniPath);
                    StellaLogger.Info("HotkeyConflict",
                        $"Modified ReShade.ini [{keyName}] = {conflict.ReplaceWithVk} (was {conflict.VirtualKey})");
                }
            }
        }

        return true;
    }

    // ── INI 写入（P/Invoke）──
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WritePrivateProfileStringW(
        string lpAppName, string lpKeyName, string lpString, string lpFileName);

    private static void WriteIniValue(string section, string key, string value, string filePath)
    {
        WritePrivateProfileStringW(section, key, value, filePath);
    }
}
