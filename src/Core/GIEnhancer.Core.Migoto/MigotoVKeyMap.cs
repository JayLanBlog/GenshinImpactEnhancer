using System.Runtime.InteropServices;

namespace GIEnhancer.Core.Migoto;

/// <summary>
/// 3DMigoto 虚拟键码映射表。
/// 将 3DMigoto d3dx.ini 中的字符串热键值转换为 Windows 虚拟键码。
/// </summary>
public static class MigotoVKeyMap
{
    private static readonly Dictionary<string, int> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // 功能键
        { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
        { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
        { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
        { "F13", 0x7C }, { "F14", 0x7D }, { "F15", 0x7E }, { "F16", 0x7F },
        { "F17", 0x80 }, { "F18", 0x81 }, { "F19", 0x82 }, { "F20", 0x83 },
        { "F21", 0x84 }, { "F22", 0x85 }, { "F23", 0x86 }, { "F24", 0x87 },

        // 方向键
        { "UP", 0x26 }, { "DOWN", 0x28 }, { "LEFT", 0x25 }, { "RIGHT", 0x27 },
        { "PAGEUP", 0x21 }, { "PGUP", 0x21 }, { "PAGEDOWN", 0x22 }, { "PGDN", 0x22 },

        // 编辑键
        { "HOME", 0x24 }, { "END", 0x23 },
        { "INSERT", 0x2D }, { "INS", 0x2D },
        { "DELETE", 0x2E }, { "DEL", 0x2D },

        // 系统键
        { "ESCAPE", 0x1B }, { "ESC", 0x1B },
        { "TAB", 0x09 }, { "SPACE", 0x20 },
        { "RETURN", 0x0D }, { "ENTER", 0x0D },
        { "BACKSPACE", 0x08 }, { "BACK", 0x08 },

        // Print Screen
        { "PRINTSCREEN", 0x2C }, { "SNAPSHOT", 0x2C },
        { "PRNT SCR", 0x2C }, { "PRNTSCRN", 0x2C },

        // Lock 键
        { "SCROLL", 0x91 }, { "SCROLLLOCK", 0x91 },
        { "NUMLOCK", 0x90 }, { "PAUSE", 0x13 }, { "BREAK", 0x13 },

        // 小键盘
        { "NUMPAD0", 0x60 }, { "NUMPAD1", 0x61 }, { "NUMPAD2", 0x62 },
        { "NUMPAD3", 0x63 }, { "NUMPAD4", 0x64 }, { "NUMPAD5", 0x65 },
        { "NUMPAD6", 0x66 }, { "NUMPAD7", 0x67 }, { "NUMPAD8", 0x68 },
        { "NUMPAD9", 0x69 },
        { "MULTIPLY", 0x6A }, { "ADD", 0x6B }, { "SUBTRACT", 0x6D },
        { "DIVIDE", 0x6F }, { "DECIMAL", 0x6E },

        // 旧版友好名称 (3DMigoto 兼容)
        { "NUM 0", 0x60 }, { "NUM 1", 0x61 }, { "NUM 2", 0x62 },
        { "NUM 3", 0x63 }, { "NUM 4", 0x64 }, { "NUM 5", 0x65 },
        { "NUM 6", 0x66 }, { "NUM 7", 0x67 }, { "NUM 8", 0x68 },
        { "NUM 9", 0x69 },
    };

    /// <summary>
    /// 解析 3DMigoto 热键字符串为虚拟键码。
    /// 支持格式: "HOME", "F3", "Num 2", "0x24", "VK_HOME", "A"
    /// </summary>
    public static int Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        value = value.Trim();

        // 1. 直接查表
        if (Map.TryGetValue(value, out int vk)) return vk;

        // 2. VK_ 前缀
        if (value.StartsWith("VK_", StringComparison.OrdinalIgnoreCase))
        {
            string name = value[3..];
            if (Map.TryGetValue(name, out vk)) return vk;
        }

        // 3. 十六进制格式 "0x24"
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out int hex))
            return hex;

        // 4. 单字符 A-Z, 0-9
        if (value.Length == 1)
        {
            char c = char.ToUpperInvariant(value[0]);
            if (c >= 'A' && c <= 'Z') return c;
            if (c >= '0' && c <= '9') return (int)'0' + (c - '0');
        }

        return 0; // 无法解析
    }

    /// <summary>
    /// 虚拟键码转可读名称
    /// </summary>
    public static string VkToName(int vk)
    {
        if (vk == 0) return "None";
        // 反查表
        foreach (var kv in Map)
        {
            if (kv.Value == vk && !kv.Key.Contains(" ") && !kv.Key.StartsWith("VK_"))
                return kv.Key;
        }
        // 功能键
        if (vk >= 0x70 && vk <= 0x87) return $"F{vk - 0x70 + 1}";
        // 字母
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        // 数字
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        return $"0x{vk:X2}";
    }
}
