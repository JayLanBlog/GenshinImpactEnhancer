using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Stella.DeviceIdentifier;
using Stella.Core;
using Stella.Core.Reshade;
using Stella.Core.FpsUnlock;
using Stella.Core.Migoto;
using Stella.Services;
using Stella.Utils.Logging;

namespace Stella.Launcher;

public partial class MainWindow : Window
{
    private EngineManager? _engineManager;
    private DeviceInfo? _deviceInfo;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await OnLoadedAsync();
    }

    private async Task OnLoadedAsync()
    {
        // 初始化日志
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GenshinStellaMod", "logs");
        StellaLogger.Initialize(logDir);

        // 采集硬件
        _deviceInfo = await Task.Run(() => DeviceDetector.Collect());

        UpdateHardwareDisplay();
        UpdateAiRecommendation();
        UpdateStatus("就绪");
    }

    private void UpdateHardwareDisplay()
    {
        if (_deviceInfo == null) return;

        Dispatcher.Invoke(() =>
        {
            TxtHardware.Text = string.Join("\n",
                $"GPU:   {_deviceInfo.GpuName}",
                $"显存:  {_deviceInfo.GpuMemoryMb} MB",
                $"CPU:   {_deviceInfo.CpuName}",
                $"核心:  {_deviceInfo.LogicalCores}",
                $"内存:  {_deviceInfo.TotalMemoryMb} MB",
                $"分辨率: {_deviceInfo.ScreenWidth}x{_deviceInfo.ScreenHeight}",
                $"GPU标识: {_deviceInfo.GpuShortName}"
            );
        });
    }

    private void UpdateAiRecommendation()
    {
        if (_deviceInfo == null) return;

        var presets = PresetRecommender.GetBuiltInPresets();
        var recommender = new PresetRecommender(presets);
        var results = recommender.Recommend(_deviceInfo, communityData: null);

        Dispatcher.Invoke(() =>
        {
            if (results.Count == 0)
            {
                TxtRecommendation.Text = "⚠️ 没有找到适合你硬件的预设。\n请确认显卡满足最低要求。";
                return;
            }

            var lines = new List<string>();
            var medals = new[] { "★ 推荐", "⚡ 备选", "🎯 备选" };
            for (int i = 0; i < Math.Min(results.Count, 3); i++)
            {
                var r = results[i];
                var medal = i < medals.Length ? medals[i] : "";
                lines.Add($"{medal} {r.DisplayName} — 评分 {r.Score:N0}");
                lines.Add($"   预计 FPS: {r.EstimatedFpsMin}-{r.EstimatedFpsMax} | GPU开销: {r.GpuUtilization}%");
                lines.Add("");
            }
            TxtRecommendation.Text = string.Join("\n", lines);
        });
    }

    private void UpdateEngineStatus(string text)
    {
        Dispatcher.Invoke(() =>
        {
            TxtEngines.Text = text;
        });
    }

    private void UpdateStatus(string text)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = text;
        });
    }

    private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
    {
        BtnLaunch.IsEnabled = false;
        UpdateStatus("正在初始化引擎...");
        UpdateEngineStatus("启动中...");

        try
        {
            var engines = new IEngineModule[]
            {
                new ReshadeEngine(),
                new FpsUnlockEngine { TargetFps = 144 },
                new MigotoEngine()
            };

            _engineManager = new EngineManager(engines);

            var gameDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GenshinStellaMod", "game");

            Directory.CreateDirectory(gameDir);

            var result = await _engineManager.LaunchAllAsync(gameDir);

            if (result)
            {
                UpdateEngineStatus(BuildEngineStatusText());
                UpdateStatus("✅ 引擎启动成功");
                BtnShutdown.IsEnabled = true;
            }
            else
            {
                UpdateEngineStatus("❌ 所有引擎启动失败");
                UpdateStatus("❌ 启动失败");
                BtnLaunch.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            UpdateEngineStatus($"❌ 异常: {ex.Message}");
            UpdateStatus("❌ 启动异常");
            BtnLaunch.IsEnabled = true;
        }
    }

    private async void BtnShutdown_Click(object sender, RoutedEventArgs e)
    {
        BtnShutdown.IsEnabled = false;
        UpdateStatus("正在关闭引擎...");

        try
        {
            if (_engineManager != null)
                await _engineManager.ShutdownAllAsync();

            UpdateEngineStatus("已停止");
            UpdateStatus("就绪");
            BtnLaunch.IsEnabled = true;
        }
        catch (Exception ex)
        {
            UpdateStatus($"关闭异常: {ex.Message}");
            BtnLaunch.IsEnabled = true;
        }
    }

    private string BuildEngineStatusText()
    {
        if (_engineManager == null) return "无引擎";

        var lines = new List<string>();
        foreach (var engine in _engineManager.Engines)
        {
            var icon = engine.IsInitialized ? "✅" : "❌";
            lines.Add($"{icon} {engine.Name}");
        }
        lines.Add("");
        lines.Add($"运行中: {(_engineManager.IsRunning ? "是" : "否")}");
        return string.Join("\n", lines);
    }
}
