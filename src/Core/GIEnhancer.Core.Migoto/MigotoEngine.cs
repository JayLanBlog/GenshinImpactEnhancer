using System.IO;
using GIEnhancer.Utils.Logging;

namespace GIEnhancer.Core.Migoto;

/// <summary>
/// 3DMigoto 引擎模块。
/// 负责检测、部署 3DMigoto DLL，读取配置，检测热键冲突。
/// </summary>
public class MigotoEngine : IEngineModule
{
    public string Name => "3DMigoto";
    public bool IsInitialized { get; private set; }
    public int LoadedModCount { get; private set; }

    /// <summary>
    /// 3DMigoto 配置读取器
    /// </summary>
    public MigotoConfigReader? Config { get; private set; }

    /// <summary>
    /// 工具目录（包含 tools/3dmigoto/ 子目录）
    /// </summary>
    public string? ToolDir { get; set; }

    /// <summary>
    /// 游戏目录
    /// </summary>
    public string? GameDir { get; private set; }

    /// <summary>
    /// 3DMigoto DLL 是否已部署到游戏目录
    /// </summary>
    public bool IsDeployed { get; private set; }

    public async Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        GameDir = gameDirectory;
        Config = new MigotoConfigReader();

        // 1. 检测 3DMigoto DLL
        string toolDir = ToolDir ?? Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        bool dllsFound = await Task.Run(() => MigotoConfigReader.DetectDlls(toolDir), ct);

        if (!dllsFound)
        {
            StellaLogger.Info(Name, "3DMigoto DLL not found in tools directory — skipping");
            IsInitialized = false;
            return false;
        }

        // 2. 部署到游戏目录
        IsDeployed = await Task.Run(() => MigotoConfigReader.DeployToGameDir(toolDir, gameDirectory), ct);
        if (!IsDeployed)
        {
            StellaLogger.Warn(Name, "Failed to deploy 3DMigoto DLLs to game directory");
            IsInitialized = false;
            return false;
        }

        // 3. 读取 d3dx.ini 配置
        bool configLoaded = await Task.Run(() => Config.Load(gameDirectory), ct);
        if (!configLoaded)
        {
            StellaLogger.Warn(Name, "d3dx.ini not found — 3DMigoto deployed but config missing");
            IsInitialized = true; // DLL 已部署，视为成功
            return true;
        }

        // 4. 检测 Mod 数量
        var modDir = Path.Combine(gameDirectory, "Mods");
        if (Directory.Exists(modDir))
            LoadedModCount = Directory.GetDirectories(modDir).Length;

        IsInitialized = true;
        StellaLogger.Info(Name, $"Initialized — deployed={IsDeployed}, hunting={Config.HuntingEnabled}, mods={LoadedModCount}");
        return true;
    }

    /// <summary>
    /// 获取 3DMigoto 的热键映射（用于冲突检测）
    /// </summary>
    public Dictionary<string, int> GetHotkeys()
    {
        return Config?.Hotkeys ?? new Dictionary<string, int>();
    }

    /// <summary>
    /// 游戏退出后还原备份的 DLL
    /// </summary>
    public async Task RestoreBackupAsync()
    {
        if (GameDir != null && IsDeployed)
        {
            await Task.Run(() => MigotoConfigReader.RestoreBackup(GameDir));
            IsDeployed = false;
        }
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        LoadedModCount = 0;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }
}
