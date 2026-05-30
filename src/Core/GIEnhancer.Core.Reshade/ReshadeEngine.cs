using System.IO;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Core.Reshade;

/// <summary>
/// ReShade 引擎模块。
/// 负责检测 ReShade DLL 是否存在并提供注入路径。
/// ReShade64.dll 通过 LoadLibrary 直接注入游戏进程，不需要代理 DLL。
/// </summary>
public class ReshadeEngine : IEngineModule
{
    public string Name => "ReShade";
    public bool IsInitialized { get; private set; }
    public string? PresetDirectory { get; private set; }
    public string? ActivePreset { get; private set; }

    /// <summary>
    /// ReShade 核心引擎 DLL（直接通过 LoadLibrary 注入，不是代理 DLL）
    /// </summary>
    public static readonly string[] RequiredDlls = { "ReShade64.dll" };

    /// <summary>
    /// ReShade64.dll 的完整路径（InitializeAsync 后可用）
    /// </summary>
    public string? DllPath { get; private set; }

    /// <summary>
    /// 工具目录（包含 tools/reshade/ 子目录）
    /// </summary>
    public string? ToolDir { get; set; }

    /// <summary>
    /// 游戏目录
    /// </summary>
    public string? GameDir { get; private set; }

    /// <summary>
    /// ReShade DLL 是否已部署到游戏目录
    /// </summary>
    public bool IsDeployed { get; private set; }

    public async Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        GameDir = gameDirectory;
        PresetDirectory = Path.Combine(gameDirectory, "reshade-shaders");

        // 0. 清理旧的 dinput8.dll 代理（D3D11 游戏不需要，可能会干扰）
        var oldProxy = Path.Combine(gameDirectory, "dinput8.dll");
        if (File.Exists(oldProxy))
        {
            var backupDir = Path.Combine(gameDirectory, "backup_reshade");
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
            File.Copy(oldProxy, Path.Combine(backupDir, "dinput8.dll"), overwrite: true);
            File.Delete(oldProxy);
            StellaLogger.Info(Name, "Removed old dinput8.dll proxy (not needed for D3D11 games)");
        }

        // 1. 查找 ReShade64.dll（按优先级：工具目录 > 游戏目录 > 系统目录）
        string toolDir = ToolDir ?? Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

        // 优先使用 D:\LaunchGame\data\dependencies\reshare\ReShade64.dll (HomeTestTool 验证的路径)
        var candidatePaths = new[]
        {
            @"D:\LaunchGame\data\dependencies\reshade\ReShade64.dll",
            Path.Combine(toolDir, "reshade", "ReShade64.dll"),
            Path.Combine(gameDirectory, "ReShade64.dll"),
        };

        foreach (var p in candidatePaths)
        {
            if (File.Exists(p))
            {
                DllPath = p;
                IsInitialized = true;
                IsDeployed = true;
                StellaLogger.Info(Name, $"ReShade64.dll found: {p}");
                return true;
            }
        }

        StellaLogger.Info(Name, "ReShade64.dll not found in any known location — skipping");
        IsInitialized = false;
        return false;
    }

    /// <summary>
    /// 检测工具目录下是否存在 ReShade 的所有必需 DLL
    /// </summary>
    public static bool DetectDlls(string toolDir)
    {
        string reshadeDir = Path.Combine(toolDir, "reshade");
        if (!Directory.Exists(reshadeDir))
        {
            StellaLogger.Info("ReshadeEngine", $"reshade directory not found: {reshadeDir}");
            return false;
        }

        bool allPresent = true;
        foreach (var dll in RequiredDlls)
        {
            string path = Path.Combine(reshadeDir, dll);
            bool exists = File.Exists(path);
            StellaLogger.Info("ReshadeEngine", $"  {dll}: {(exists ? "FOUND" : "MISSING")}");
            if (!exists) allPresent = false;
        }

        return allPresent;
    }

    /// <summary>
    /// 将 ReShade DLL 复制到游戏目录（备份已有文件）
    /// </summary>
    public static bool DeployToGameDir(string toolDir, string gameDir)
    {
        string reshadeDir = Path.Combine(toolDir, "reshade");
        string backupDir = Path.Combine(gameDir, "backup_reshade");

        try
        {
            // 创建备份目录
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            foreach (var dll in RequiredDlls)
            {
                string src = Path.Combine(reshadeDir, dll);
                string dst = Path.Combine(gameDir, dll);
                string bak = Path.Combine(backupDir, dll);

                if (!File.Exists(src))
                {
                    StellaLogger.Warn("ReshadeEngine", $"Source DLL missing: {dll}");
                    return false;
                }

                // 备份已有文件
                if (File.Exists(dst))
                {
                    File.Copy(dst, bak, overwrite: true);
                    StellaLogger.Info("ReshadeEngine", $"Backed up existing {dll}");
                }

                // 复制 ReShade DLL
                File.Copy(src, dst, overwrite: true);
                StellaLogger.Info("ReshadeEngine", $"Deployed {dll} → game dir");
            }

            return true;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("ReshadeEngine", $"Deploy failed: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// 从游戏目录还原备份的 DLL
    /// </summary>
    public async Task RestoreBackupAsync()
    {
        if (GameDir != null && IsDeployed)
        {
            await Task.Run(() => RestoreBackup(GameDir));
            IsDeployed = false;
        }
    }

    /// <summary>
    /// 静态还原方法
    /// </summary>
    public static bool RestoreBackup(string gameDir)
    {
        string backupDir = Path.Combine(gameDir, "backup_reshade");
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
                    StellaLogger.Info("ReshadeEngine", $"Restored {dll} from backup");
                }
                else
                {
                    // 没有备份说明原来就不存在，删除部署的文件
                    if (File.Exists(dst))
                    {
                        File.Delete(dst);
                        StellaLogger.Info("ReshadeEngine", $"Removed deployed {dll} (no backup)");
                    }
                }
            }
            Directory.Delete(backupDir, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("ReshadeEngine", $"Restore failed: {ex.Message}", ex);
            return false;
        }
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }

    public bool LoadPreset(string presetFilePath)
    {
        if (!File.Exists(presetFilePath))
        {
            StellaLogger.Warn(Name, $"Preset not found: {presetFilePath}");
            return false;
        }
        ActivePreset = Path.GetFileNameWithoutExtension(presetFilePath);
        StellaLogger.Info(Name, $"Preset loaded: {ActivePreset}");
        return true;
    }
}
