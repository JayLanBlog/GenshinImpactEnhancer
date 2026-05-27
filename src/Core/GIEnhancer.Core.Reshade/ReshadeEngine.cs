using System.IO;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Core.Reshade;

/// <summary>
/// ReShade 引擎模块。
/// 负责检测、部署 ReShade 代理 DLL，读取配置，管理预设。
/// </summary>
public class ReshadeEngine : IEngineModule
{
    public string Name => "ReShade";
    public bool IsInitialized { get; private set; }
    public string? PresetDirectory { get; private set; }
    public string? ActivePreset { get; private set; }

    /// <summary>
    /// ReShade 需要部署的文件列表。
    /// dxgi.dll = 代理入口（游戏加载它，它再加载 ReShade64.dll）
    /// ReShade64.dll = 核心着色器引擎（实际功能所在）
    /// </summary>
    public static readonly string[] RequiredDlls = { "dxgi.dll", "ReShade64.dll" };

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

        // 1. 检测 ReShade DLL
        string toolDir = ToolDir ?? Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        bool dllsFound = await Task.Run(() => DetectDlls(toolDir), ct);

        if (!dllsFound)
        {
            // 也检查游戏目录是否已有 ReShade
            bool alreadyInGame = File.Exists(Path.Combine(gameDirectory, "dxgi.dll"));
            if (alreadyInGame && File.Exists(Path.Combine(gameDirectory, "ReShade.ini")))
            {
                StellaLogger.Info(Name, "ReShade already present in game directory");
                IsDeployed = false; // 不是我们部署的，不需要还原
                IsInitialized = true;
                return true;
            }

            StellaLogger.Info(Name, "ReShade DLL not found — skipping");
            IsInitialized = false;
            return false;
        }

        // 2. 部署到游戏目录
        IsDeployed = await Task.Run(() => DeployToGameDir(toolDir, gameDirectory), ct);
        if (!IsDeployed)
        {
            StellaLogger.Warn(Name, "Failed to deploy ReShade DLL to game directory");
            IsInitialized = false;
            return false;
        }

        IsInitialized = true;
        StellaLogger.Info(Name, $"Initialized — deployed={IsDeployed}, presetDir={PresetDirectory}");
        return true;
    }

    /// <summary>
    /// 检测工具目录下是否存在 ReShade 的代理 DLL
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
    /// 将 ReShade 代理 DLL 复制到游戏目录（备份已有文件）
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
