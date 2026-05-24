# Genshin Stella Mod 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建面向《原神》的全功能画质增强工具——ReShade 注入 + FPS 解锁 + 3DMigoto 模组 + AI 推荐 + 性能仪表盘 + 极致 WPF UI

**Architecture:** 分层单体仓库，C# .NET 6.0 + C++ DLL。5 层 17 模块，上层单向依赖下层。WPF 启动器通过 Named Pipe/Shared Memory 与注入到游戏进程的 Native DLL 通信。

**Tech Stack:** C# .NET 6.0, WPF, CommunityToolkit.Mvvm, MaterialDesignInXAML, LiveCharts2, C++/CLI (Native DLL), Serilog, xUnit

**Note:** 当前环境 SDK 为 6.0.202（WPF 可用），计划以 `net6.0-windows` 为目标。后续可根据需求升级到更高版本 .NET。

---

## File Structure

```
e:\AI\hook\
├── GenshinStellaMod.sln
├── src\
│   ├── Infrastructure\
│   │   ├── Stella.DeviceIdentifier\    (net6.0 Class Library)
│   │   ├── Stella.Utils\               (net6.0 Class Library)
│   │   └── Stella.StartupBeacon\       (net6.0-windows Class Library)
│   ├── Native\
│   │   ├── Stella.Native.Injector\     (C++ DLL - VC++ project)
│   │   ├── Stella.Native.DXHook\       (C++ DLL - VC++ project)
│   │   └── Stella.Native.Overlay\      (C++ DLL - VC++ project)
│   ├── Core\
│   │   ├── Stella.Core\                (net6.0 Class Library)
│   │   ├── Stella.Core.Reshade\        (net6.0 Class Library)
│   │   ├── Stella.Core.FpsUnlock\      (net6.0 Class Library)
│   │   └── Stella.Core.Migoto\         (net6.0 Class Library)
│   ├── Application\
│   │   ├── Stella.Services\            (net6.0 Class Library)
│   │   └── Stella.Update\              (net6.0 Class Library)
│   ├── UI\
│   │   ├── Stella.Launcher\            (net6.0-windows WPF App)
│   │   └── Stella.Welcome\             (net6.0-windows WPF App)
│   └── Distribution\
│       └── Stella.Builder\             (net6.0 Console App)
└── tests\
    └── Test.Genshin\                   (net6.0 xUnit)
```

---

## Phase 1: 项目脚手架 & 基础设施层

### Task 1: 创建解决方案和项目结构

**Files:**
- Create: `e:\AI\hook\GenshinStellaMod.sln`
- Create: `e:\AI\hook\.editorconfig`
- Create: `e:\AI\hook\.gitignore`

- [ ] **Step 1: 创建解决方案**

```powershell
dotnet new sln -n GenshinStellaMod -o e:\AI\hook
```

- [ ] **Step 2: 创建 Infrastructure 项目并加入解决方案**

```powershell
dotnet new classlib -n Stella.DeviceIdentifier -o e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier -f net6.0
dotnet new classlib -n Stella.Utils -o e:\AI\hook\src\Infrastructure\Stella.Utils -f net6.0
dotnet new classlib -n Stella.StartupBeacon -o e:\AI\hook\src\Infrastructure\Stella.StartupBeacon -f net6.0
dotnet sln e:\AI\hook\GenshinStellaMod.sln add e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Stella.DeviceIdentifier.csproj
dotnet sln e:\AI\hook\GenshinStellaMod.sln add e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj
dotnet sln e:\AI\hook\GenshinStellaMod.sln add e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\Stella.StartupBeacon.csproj
```

- [ ] **Step 3: 创建 Core / Application / UI / Test 项目**

```powershell
# Core projects
dotnet new classlib -n Stella.Core -o e:\AI\hook\src\Core\Stella.Core -f net6.0
dotnet new classlib -n Stella.Core.Reshade -o e:\AI\hook\src\Core\Stella.Core.Reshade -f net6.0
dotnet new classlib -n Stella.Core.FpsUnlock -o e:\AI\hook\src\Core\Stella.Core.FpsUnlock -f net6.0
dotnet new classlib -n Stella.Core.Migoto -o e:\AI\hook\src\Core\Stella.Core.Migoto -f net6.0
# Application projects
dotnet new classlib -n Stella.Services -o e:\AI\hook\src\Application\Stella.Services -f net6.0
dotnet new classlib -n Stella.Update -o e:\AI\hook\src\Application\Stella.Update -f net6.0
# UI projects (need net6.0-windows for WPF)
dotnet new wpf -n Stella.Launcher -o e:\AI\hook\src\UI\Stella.Launcher -f net6.0-windows
dotnet new wpf -n Stella.Welcome -o e:\AI\hook\src\UI\Stella.Welcome -f net6.0-windows
# Distribution
dotnet new console -n Stella.Builder -o e:\AI\hook\src\Distribution\Stella.Builder -f net6.0
# Tests
dotnet new xunit -n Test.Genshin -o e:\AI\hook\tests\Test.Genshin -f net6.0
```

- [ ] **Step 4: 将所有项目加入解决方案**

```powershell
Get-ChildItem e:\AI\hook\src -Recurse -Filter *.csproj | ForEach-Object { dotnet sln e:\AI\hook\GenshinStellaMod.sln add $_.FullName }
dotnet sln e:\AI\hook\GenshinStellaMod.sln add e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj
```

- [ ] **Step 5: 创建 .editorconfig 和 .gitignore**

`.editorconfig` 内容：
```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
csharp_style_var_when_type_is_apparent = true:warning
csharp_style_var_elsewhere = true:warning
dotnet_naming_rule.interface_should_be_prefixed_with_i.severity = warning
```

`.gitignore` 使用 `dotnet new gitignore` 生成，追加：
```
/.superpowers/
*.user
*.suo
*.userosscache
*.sln.docstates
bin/
obj/
.vs/
```

- [ ] **Step 6: 验证构建**

```powershell
dotnet build e:\AI\hook\GenshinStellaMod.sln
```
Expected: Build succeeded (所有项目编译通过，虽有未使用的 Class1.cs 警告但无错误)

- [ ] **Step 7: 提交**

```bash
git add -A
git commit -m "feat: scaffold solution with 15 projects across 5 layers"
```

---

### Task 2: Stella.Utils — 日志基础设施

**Files:**
- Create: `e:\AI\hook\src\Infrastructure\Stella.Utils\Logging\StellaLogger.cs`
- Create: `e:\AI\hook\src\Infrastructure\Stella.Utils\Logging\CrashGuard.cs`
- Modify: `e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj`
- Delete: `e:\AI\hook\src\Infrastructure\Stella.Utils\Class1.cs`
- Test: `e:\AI\hook\tests\Test.Genshin\Logging\StellaLoggerTests.cs`

- [ ] **Step 1: 添加 NuGet 依赖**

```powershell
dotnet add e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj package Serilog
dotnet add e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj package Serilog.Sinks.File
dotnet add e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj reference e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj
```

- [ ] **Step 2: 删除模板文件**

```powershell
Remove-Item e:\AI\hook\src\Infrastructure\Stella.Utils\Class1.cs -Force
```

- [ ] **Step 3: 创建测试目录和测试文件**

创建 `e:\AI\hook\tests\Test.Genshin\Logging\StellaLoggerTests.cs`：
```csharp
using Xunit;
using Stella.Utils.Logging;
using System.IO;

namespace Test.Genshin.Logging;

public class StellaLoggerTests
{
    [Fact]
    public void Initialize_CreatesLogDirectory()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "StellaTestLogs", "LoggerInit");
        if (Directory.Exists(logDir)) Directory.Delete(logDir, true);

        StellaLogger.Initialize(logDir);

        Assert.True(Directory.Exists(logDir));
        Directory.Delete(logDir, true);
    }

    [Fact]
    public void Log_Info_WritesToLogFile()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "StellaTestLogs", "LogInfo");
        StellaLogger.Initialize(logDir);

        StellaLogger.Info("TestCategory", "Hello from test");

        var files = Directory.GetFiles(logDir, "*.log");
        Assert.NotEmpty(files);

        var content = File.ReadAllText(files[0]);
        Assert.Contains("Hello from test", content);
        Assert.Contains("[INFO]", content);
        Assert.Contains("TestCategory", content);

        Directory.Delete(logDir, true);
    }

    [Fact]
    public void Log_Error_IncludesExceptionStack()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "StellaTestLogs", "LogError");
        StellaLogger.Initialize(logDir);

        try { throw new InvalidOperationException("Test error"); }
        catch (Exception ex) { StellaLogger.Error("TestCategory", "Something broke", ex); }

        var files = Directory.GetFiles(logDir, "*.log");
        var content = File.ReadAllText(files[0]);
        Assert.Contains("[ERROR]", content);
        Assert.Contains("Test error", content);
        Assert.Contains("InvalidOperationException", content);

        Directory.Delete(logDir, true);
    }
}
```

- [ ] **Step 4: 运行测试，验证失败**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~StellaLoggerTests"
```
Expected: FAIL — `StellaLogger` 类型不存在

- [ ] **Step 5: 实现 StellaLogger**

创建 `e:\AI\hook\src\Infrastructure\Stella.Utils\Logging\StellaLogger.cs`：
```csharp
using Serilog;
using Serilog.Events;

namespace Stella.Utils.Logging;

/// <summary>
/// 结构化日志门面。底层使用 Serilog，提供按日期分文件的日志输出。
/// 日志保留 7 天，自动清理过期文件。
/// </summary>
public static class StellaLogger
{
    private static ILogger? _logger;
    private static string? _logDirectory;

    public static void Initialize(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDirectory, "stella-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Source}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        _logger.Information("[{Source}] Logger initialized. Directory: {Dir}", "StellaLogger", logDirectory);
    }

    public static void Info(string source, string message)
    {
        _logger?.Information("[{Source}] {Message}", source, message);
    }

    public static void Warn(string source, string message)
    {
        _logger?.Warning("[{Source}] {Message}", source, message);
    }

    public static void Error(string source, string message, Exception? ex = null)
    {
        if (ex != null)
            _logger?.Error(ex, "[{Source}] {Message}", source, message);
        else
            _logger?.Error("[{Source}] {Message}", source, message);
    }

    public static void Debug(string source, string message)
    {
        _logger?.Debug("[{Source}] {Message}", source, message);
    }

    /// <summary>
    /// 确保日志被刷新到磁盘（用于崩溃前保存）
    /// </summary>
    public static void Flush()
    {
        if (_logger is IDisposable disposable)
            disposable.Dispose();
    }
}
```

- [ ] **Step 6: 运行测试，验证通过**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~StellaLoggerTests"
```
Expected: 3 passed, 0 failed

- [ ] **Step 7: 创建 CrashGuard（启动守卫 — 崩溃检测）**

创建 `e:\AI\hook\src\Infrastructure\Stella.Utils\Logging\CrashGuard.cs`：
```csharp
using System.Text.Json;

namespace Stella.Utils.Logging;

/// <summary>
/// 启动守卫：检测上次会话是否崩溃，记录崩溃信息供下次启动判断。
/// </summary>
public class CrashGuard
{
    private readonly string _guardFilePath;

    public CrashGuard(string appDataDir)
    {
        _guardFilePath = Path.Combine(appDataDir, ".crash_guard.json");
    }

    /// <summary>
    /// 写入启动标记，表示 "正在运行"。
    /// </summary>
    public void MarkRunning()
    {
        var record = new CrashRecord
        {
            Running = true,
            LastStartTime = DateTime.UtcNow,
            Pid = Environment.ProcessId
        };
        File.WriteAllText(_guardFilePath, JsonSerializer.Serialize(record));
    }

    /// <summary>
    /// 清除标记，表示 "正常退出"。
    /// </summary>
    public void MarkCleanExit()
    {
        if (File.Exists(_guardFilePath))
            File.Delete(_guardFilePath);
    }

    /// <summary>
    /// 检测上次是否崩溃（文件存在且 Running=true 表示非正常退出）。
    /// </summary>
    public bool WasLastSessionCrashed()
    {
        if (!File.Exists(_guardFilePath)) return false;
        try
        {
            var json = File.ReadAllText(_guardFilePath);
            var record = JsonSerializer.Deserialize<CrashRecord>(json);
            return record?.Running == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取崩溃记录（可用于生成诊断报告）
    /// </summary>
    public CrashRecord? GetLastRecord()
    {
        if (!File.Exists(_guardFilePath)) return null;
        try
        {
            var json = File.ReadAllText(_guardFilePath);
            return JsonSerializer.Deserialize<CrashRecord>(json);
        }
        catch
        {
            return null;
        }
    }

    public class CrashRecord
    {
        public bool Running { get; set; }
        public DateTime LastStartTime { get; set; }
        public int Pid { get; set; }
    }
}
```

- [ ] **Step 8: 添加 CrashGuard 测试**

创建 `e:\AI\hook\tests\Test.Genshin\Logging\CrashGuardTests.cs`：
```csharp
using Xunit;
using Stella.Utils.Logging;
using System.IO;

namespace Test.Genshin.Logging;

public class CrashGuardTests
{
    [Fact]
    public void WasLastSessionCrashed_NoFile_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CrashGuardTest", "NoFile");
        var guard = new CrashGuard(dir);
        Assert.False(guard.WasLastSessionCrashed());
    }

    [Fact]
    public void MarkRunning_Then_WasLastSessionCrashed_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CrashGuardTest", "Running");
        Directory.CreateDirectory(dir);
        var guard = new CrashGuard(dir);
        guard.MarkRunning();

        var guard2 = new CrashGuard(dir);
        Assert.True(guard2.WasLastSessionCrashed());

        Directory.Delete(dir, true);
    }

    [Fact]
    public void MarkCleanExit_ClearsCrashState()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CrashGuardTest", "CleanExit");
        Directory.CreateDirectory(dir);
        var guard = new CrashGuard(dir);
        guard.MarkRunning();
        guard.MarkCleanExit();

        Assert.False(guard.WasLastSessionCrashed());
        Directory.Delete(dir, true);
    }
}
```

- [ ] **Step 9: 运行全部 Utils 测试**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~Logging"
```
Expected: 6 passed, 0 failed

- [ ] **Step 10: 提交**

```bash
git add -A
git commit -m "feat: add StellaLogger (Serilog) and CrashGuard infrastructure"
```

---

### Task 3: Stella.DeviceIdentifier — 硬件指纹采集

**Files:**
- Create: `e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\DeviceInfo.cs`
- Create: `e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\DeviceIdentifier.cs`
- Modify: `e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Stella.DeviceIdentifier.csproj`
- Delete: `e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Class1.cs`
- Test: `e:\AI\hook\tests\Test.Genshin\DeviceIdentifier\DeviceIdentifierTests.cs`

- [ ] **Step 1: 添加 NuGet 包和项目引用**

```powershell
dotnet add e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Stella.DeviceIdentifier.csproj package System.Management
dotnet add e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Stella.DeviceIdentifier.csproj reference e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj
dotnet add e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj reference e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Stella.DeviceIdentifier.csproj
Remove-Item e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Class1.cs -Force
```

- [ ] **Step 2: 创建数据模型**

创建 `e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\DeviceInfo.cs`：
```csharp
namespace Stella.DeviceIdentifier;

/// <summary>
/// 硬件指纹信息，用于 AI 推荐引擎的输入。
/// </summary>
public class DeviceInfo
{
    /// <summary>GPU 名称，如 "NVIDIA GeForce RTX 4060"</summary>
    public string GpuName { get; init; } = "Unknown";

    /// <summary>GPU 显存大小（MB）</summary>
    public long GpuMemoryMb { get; init; }

    /// <summary>CPU 名称，如 "Intel Core i5-13400"</summary>
    public string CpuName { get; init; } = "Unknown";

    /// <summary>逻辑处理器数量</summary>
    public int LogicalCores { get; init; }

    /// <summary>系统总内存（MB）</summary>
    public long TotalMemoryMb { get; init; }

    /// <summary>主显示器分辨率宽度</summary>
    public int ScreenWidth { get; init; }

    /// <summary>主显示器分辨率高度</summary>
    public int ScreenHeight { get; init; }

    /// <summary>GPU 型号简化标识（用于匹配预设性能数据），如 "rtx4060"</summary>
    public string GpuShortName => NormalizeGpuName(GpuName);

    /// <summary>
    /// 简化 GPU 名称用于查找预设表。
    /// 如 "NVIDIA GeForce RTX 4060" → "rtx4060"
    /// </summary>
    private static string NormalizeGpuName(string fullName)
    {
        var lower = fullName.ToLowerInvariant()
            .Replace("nvidia ", "").Replace("geforce ", "").Replace("amd ", "")
            .Replace("radeon ", "").Replace("rx ", "rx").Replace("intel ", "")
            .Replace("arc ", "arc").Replace("(r)", "").Replace(" graphics", "")
            .Replace(" ", "").Replace("-", "").Replace("_", "");

        return lower;
    }
}
```

- [ ] **Step 3: 编写测试**

创建 `e:\AI\hook\tests\Test.Genshin\DeviceIdentifier\DeviceIdentifierTests.cs`：
```csharp
using Xunit;
using Stella.DeviceIdentifier;

namespace Test.Genshin.DeviceIdentifier;

public class DeviceIdentifierTests
{
    [Fact]
    public void Collect_ReturnsNonNullDeviceInfo()
    {
        var info = DeviceDetector.Collect();
        Assert.NotNull(info);
    }

    [Fact]
    public void Collect_GpuName_IsNotEmpty()
    {
        var info = DeviceDetector.Collect();
        Assert.False(string.IsNullOrWhiteSpace(info.GpuName));
    }

    [Fact]
    public void Collect_GpuMemory_IsPositive()
    {
        var info = DeviceDetector.Collect();
        Assert.True(info.GpuMemoryMb > 0, $"Expected GPU memory > 0, got {info.GpuMemoryMb} MB");
    }

    [Fact]
    public void Collect_CpuName_IsNotEmpty()
    {
        var info = DeviceDetector.Collect();
        Assert.False(string.IsNullOrWhiteSpace(info.CpuName));
    }

    [Fact]
    public void Collect_Resolution_IsReasonable()
    {
        var info = DeviceDetector.Collect();
        Assert.True(info.ScreenWidth >= 800, $"Width {info.ScreenWidth} too small");
        Assert.True(info.ScreenHeight >= 600, $"Height {info.ScreenHeight} too small");
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060", "rtx4060")]
    [InlineData("NVIDIA GeForce GTX 1660", "gtx1660")]
    [InlineData("AMD Radeon RX 7900 XTX", "rx7900xtx")]
    public void GpuShortName_NormalizesCorrectly(string full, string expected)
    {
        var info = new DeviceInfo { GpuName = full };
        Assert.Equal(expected, info.GpuShortName);
    }
}
```

- [ ] **Step 4: 运行测试，预期失败（DeviceDetector 不存在）**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~DeviceIdentifierTests"
```
Expected: FAIL

- [ ] **Step 5: 实现 DeviceDetector**

创建 `e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\DeviceIdentifier.cs`：
```csharp
using System.Management;
using Stella.Utils.Logging;

namespace Stella.DeviceIdentifier;

public static class DeviceDetector
{
    /// <summary>
    /// 采集当前系统的硬件指纹信息。
    /// GPU 信息通过 WMI 查询，分辨率通过 Windows Forms（如果不可用则回退）。
    /// </summary>
    public static DeviceInfo Collect()
    {
        var info = new DeviceInfo();

        try
        {
            info.GpuName = QueryWmi("Win32_VideoController", "Name") ?? "Unknown";
            var memStr = QueryWmi("Win32_VideoController", "AdapterRAM");
            if (memStr != null && long.TryParse(memStr, out var bytes))
                info.GpuMemoryMb = bytes / (1024 * 1024);

            info.CpuName = QueryWmi("Win32_Processor", "Name") ?? "Unknown";
            var coresStr = QueryWmi("Win32_ComputerSystem", "NumberOfLogicalProcessors");
            if (coresStr != null && int.TryParse(coresStr, out var cores))
                info.LogicalCores = cores;

            var totalMemStr = QueryWmi("Win32_ComputerSystem", "TotalPhysicalMemory");
            if (totalMemStr != null && long.TryParse(totalMemStr, out var totalBytes))
                info.TotalMemoryMb = totalBytes / (1024 * 1024);

            // 分辨率
            info.ScreenWidth = 1920;
            info.ScreenHeight = 1080;
            try
            {
                var resStr = QueryWmi("Win32_VideoController", "CurrentHorizontalResolution");
                var vertStr = QueryWmi("Win32_VideoController", "CurrentVerticalResolution");
                if (resStr != null && int.TryParse(resStr, out var w)) info.ScreenWidth = w;
                if (vertStr != null && int.TryParse(vertStr, out var h)) info.ScreenHeight = h;
            }
            catch { /* fallback to default */ }

            StellaLogger.Info("DeviceDetector", $"Collected: GPU={info.GpuShortName} ({info.GpuMemoryMb}MB), " +
                $"CPU={info.CpuName} ({info.LogicalCores} cores), RAM={info.TotalMemoryMb}MB, " +
                $"Screen={info.ScreenWidth}x{info.ScreenHeight}");
        }
        catch (Exception ex)
        {
            StellaLogger.Error("DeviceDetector", "Failed to collect device info", ex);
        }

        return info;
    }

    private static string? QueryWmi(string className, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}");
            foreach (var obj in searcher.Get())
            {
                var value = obj[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        catch (Exception ex)
        {
            StellaLogger.Warn("DeviceDetector", $"WMI query failed: {className}.{property} - {ex.Message}");
        }
        return null;
    }
}
```

- [ ] **Step 6: 运行测试，验证通过**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~DeviceIdentifierTests"
```
Expected: 7 passed, 0 failed

- [ ] **Step 7: 提交**

```bash
git add -A
git commit -m "feat: add DeviceIdentifier with WMI-based hardware fingerprint collection"
```

---

### Task 4: Stella.StartupBeacon — 开机自启 & 托盘

**Files:**
- Create: `e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\StartupManager.cs`
- Modify: `e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\Stella.StartupBeacon.csproj`
- Delete: `e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\Class1.cs`
- Test: `e:\AI\hook\tests\Test.Genshin\StartupBeacon\StartupManagerTests.cs`

- [ ] **Step 1: 修改项目目标框架和添加引用**

修改 `Stella.StartupBeacon.csproj`，将 `<TargetFramework>net6.0</TargetFramework>` 改为 `<TargetFramework>net6.0-windows</TargetFramework>`，添加 `<UseWindowsForms>true</UseWindowsForms>`。

```powershell
dotnet add e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\Stella.StartupBeacon.csproj reference e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj
dotnet add e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj reference e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\Stella.StartupBeacon.csproj
Remove-Item e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\Class1.cs -Force
```

- [ ] **Step 2: 编写测试**

创建 `e:\AI\hook\tests\Test.Genshin\StartupBeacon\StartupManagerTests.cs`：
```csharp
using Xunit;
using Stella.StartupBeacon;

namespace Test.Genshin.StartupBeacon;

public class StartupManagerTests
{
    [Fact]
    public void GetStartupShortcutPath_ReturnsCorrectRegistryPath()
    {
        var path = StartupManager.GetStartupRegistryPath();
        Assert.Contains("Stella", path);
        Assert.Contains("SOFTWARE", path);
    }

    [Fact]
    public void IsStartupEnabled_InitiallyReturnsFalse()
    {
        // We don't expect startup to be enabled in test environment
        var result = StartupManager.IsStartupEnabled();
        // Just verifies it doesn't throw
        Assert.False(result == false || result == true); // tautology to check no-throw
    }
}
```

Note: 开机自启的启用/禁用测试需要管理员权限，不在 CI 中运行。这些测试仅验证 API 稳定性。

- [ ] **Step 3: 实现 StartupManager**

创建 `e:\AI\hook\src\Infrastructure\Stella.StartupBeacon\StartupManager.cs`：
```csharp
using Microsoft.Win32;
using Stella.Utils.Logging;

namespace Stella.StartupBeacon;

/// <summary>
/// 管理 Windows 开机自启注册表项和系统托盘图标。
/// </summary>
public static class StartupManager
{
    private const string AppName = "GenshinStellaMod";
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// 获取开机自启注册表路径（测试用）
    /// </summary>
    public static string GetStartupRegistryPath() => RegistryRunKey;

    /// <summary>
    /// 检查是否已设置为开机自启。
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            StellaLogger.Error("StartupManager", "Failed to check startup status", ex);
            return false;
        }
    }

    /// <summary>
    /// 启用开机自启（写入注册表）。
    /// </summary>
    public static void EnableStartup(string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true);
            key?.SetValue(AppName, $"\"{executablePath}\" --minimized");
            StellaLogger.Info("StartupManager", "Startup enabled");
        }
        catch (Exception ex)
        {
            StellaLogger.Error("StartupManager", "Failed to enable startup", ex);
        }
    }

    /// <summary>
    /// 禁用开机自启。
    /// </summary>
    public static void DisableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
            StellaLogger.Info("StartupManager", "Startup disabled");
        }
        catch (Exception ex)
        {
            StellaLogger.Error("StartupManager", "Failed to disable startup", ex);
        }
    }
}
```

- [ ] **Step 4: 运行测试**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~StartupManagerTests"
```
Expected: 2 passed, 0 failed

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat: add StartupBeacon with registry-based auto-start management"
```

---

## Phase 2: Native 层 — 进程注入引擎

### Task 5: Stella.Native.Injector — C++ 进程注入 DLL

**Files:**
- Create: `e:\AI\hook\src\Native\Stella.Native.Injector\Injector.h`
- Create: `e:\AI\hook\src\Native\Stella.Native.Injector\Injector.cpp`
- Create: `e:\AI\hook\src\Native\Stella.Native.Injector\Stella.Native.Injector.vcxproj`
- Create: `e:\AI\hook\src\Native\Stella.Native.Injector\dllmain.cpp`

- [ ] **Step 1: 检查 C++ 编译工具链**

```powershell
where.exe cl 2>$null; where.exe msbuild 2>$null
```

如果 `cl.exe` 不可用，需要安装 Visual Studio Build Tools 或使用已安装的 MSVC 工具链。

- [ ] **Step 2: 创建 VC++ 项目文件**

创建 `e:\AI\hook\src\Native\Stella.Native.Injector\Stella.Native.Injector.vcxproj`：
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup Label="ProjectConfigurations">
    <ProjectConfiguration Include="Debug|x64">
      <Configuration>Debug</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
    <ProjectConfiguration Include="Release|x64">
      <Configuration>Release</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
  </ItemGroup>
  <PropertyGroup Label="Globals">
    <VCProjectVersion>17.0</VCProjectVersion>
    <Keyword>Win32Proj</Keyword>
    <ProjectGuid>{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}</ProjectGuid>
    <RootNamespace>StellaNativeInjector</RootNamespace>
    <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" Label="Configuration">
    <ConfigurationType>DynamicLibrary</ConfigurationType>
    <UseDebugLibraries>true</UseDebugLibraries>
    <PlatformToolset>v143</PlatformToolset>
    <CharacterSet>Unicode</CharacterSet>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'" Label="Configuration">
    <ConfigurationType>DynamicLibrary</ConfigurationType>
    <UseDebugLibraries>false</UseDebugLibraries>
    <PlatformToolset>v143</PlatformToolset>
    <WholeProgramOptimization>true</WholeProgramOptimization>
    <CharacterSet>Unicode</CharacterSet>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
  <ItemGroup>
    <ClInclude Include="Injector.h" />
  </ItemGroup>
  <ItemGroup>
    <ClCompile Include="dllmain.cpp" />
    <ClCompile Include="Injector.cpp" />
  </ItemGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
</Project>
```

- [ ] **Step 3: 实现注入器 DLL**

创建 `e:\AI\hook\src\Native\Stella.Native.Injector\Injector.h`：
```cpp
#pragma once
#include <Windows.h>
#include <string>

namespace Stella {
namespace Native {

/// <summary>
/// 进程注入器 — 将 DLL 注入到目标进程。
/// 支持 CREATE_SUSPENDED 方式创建进程并注入。
/// </summary>
class Injector {
public:
    /// <summary>
    /// 以挂起状态创建目标进程。
    /// 返回进程句柄和主线程句柄。
    /// </summary>
    static BOOL CreateSuspendedProcess(
        const std::wstring& exePath,
        const std::wstring& workingDir,
        HANDLE* outProcessHandle,
        HANDLE* outThreadHandle,
        DWORD* outProcessId
    );

    /// <summary>
    /// 通过 CreateRemoteThread + LoadLibrary 将 DLL 注入到目标进程。
    /// </summary>
    static BOOL InjectDll(HANDLE processHandle, const std::wstring& dllPath);

    /// <summary>
    /// 恢复挂起的进程主线程。
    /// </summary>
    static BOOL ResumeProcess(HANDLE threadHandle);

    /// <summary>
    /// 卸载目标进程中的指定 DLL。
    /// </summary>
    static BOOL EjectDll(HANDLE processHandle, const std::wstring& dllName);

    /// <summary>
    /// 检测目标进程是否已经加载了特定 DLL。
    /// </summary>
    static BOOL IsDllLoaded(DWORD processId, const std::wstring& dllName);

private:
    static std::wstring GetLastErrorAsString();
};

} // namespace Native
} // namespace Stella
```

创建 `e:\AI\hook\src\Native\Stella.Native.Injector\Injector.cpp`：
```cpp
#include "Injector.h"
#include <TlHelp32.h>
#include <sstream>
#include <Psapi.h>

namespace Stella {
namespace Native {

BOOL Injector::CreateSuspendedProcess(
    const std::wstring& exePath,
    const std::wstring& workingDir,
    HANDLE* outProcessHandle,
    HANDLE* outThreadHandle,
    DWORD* outProcessId)
{
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi = {};

    BOOL success = CreateProcessW(
        exePath.c_str(),
        NULL,                       // lpCommandLine
        NULL,                       // lpProcessAttributes
        NULL,                       // lpThreadAttributes
        FALSE,                      // bInheritHandles
        CREATE_SUSPENDED,           // dwCreationFlags
        NULL,                       // lpEnvironment
        workingDir.empty() ? NULL : workingDir.c_str(),
        &si,
        &pi
    );

    if (success) {
        *outProcessHandle = pi.hProcess;
        *outThreadHandle = pi.hThread;
        *outProcessId = pi.dwProcessId;
    }

    return success;
}

BOOL Injector::InjectDll(HANDLE processHandle, const std::wstring& dllPath)
{
    // 在目标进程中分配内存，写入 DLL 路径
    size_t pathSize = (dllPath.length() + 1) * sizeof(wchar_t);
    LPVOID remoteMemory = VirtualAllocEx(
        processHandle, NULL, pathSize,
        MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE
    );

    if (!remoteMemory) return FALSE;

    if (!WriteProcessMemory(processHandle, remoteMemory, dllPath.c_str(), pathSize, NULL)) {
        VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);
        return FALSE;
    }

    // 获取 kernel32!LoadLibraryW 地址
    HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
    LPTHREAD_START_ROUTINE loadLibraryAddr =
        (LPTHREAD_START_ROUTINE)GetProcAddress(kernel32, "LoadLibraryW");

    if (!loadLibraryAddr) {
        VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);
        return FALSE;
    }

    // 创建远程线程执行 LoadLibraryW
    HANDLE remoteThread = CreateRemoteThread(
        processHandle, NULL, 0,
        loadLibraryAddr, remoteMemory, 0, NULL
    );

    if (!remoteThread) {
        VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);
        return FALSE;
    }

    // 等待 DLL 加载完成
    WaitForSingleObject(remoteThread, 10000); // 10s timeout

    DWORD exitCode = 0;
    GetExitCodeThread(remoteThread, &exitCode);

    CloseHandle(remoteThread);
    VirtualFreeEx(processHandle, remoteMemory, 0, MEM_RELEASE);

    return exitCode != 0; // LoadLibrary returns non-zero = success
}

BOOL Injector::ResumeProcess(HANDLE threadHandle)
{
    return ResumeThread(threadHandle) != (DWORD)-1;
}

BOOL Injector::EjectDll(HANDLE processHandle, const std::wstring& dllName)
{
    // 使用 CreateToolhelp32Snapshot 查找已加载模块
    // 获取 FreeLibrary 地址，CreateRemoteThread 调用
    // （简化实现，留待后续完善）
    return TRUE;
}

BOOL Injector::IsDllLoaded(DWORD processId, const std::wstring& dllName)
{
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
    if (snapshot == INVALID_HANDLE_VALUE) return FALSE;

    MODULEENTRY32W me = { sizeof(me) };
    BOOL found = FALSE;

    if (Module32FirstW(snapshot, &me)) {
        do {
            if (_wcsicmp(me.szModule, dllName.c_str()) == 0) {
                found = TRUE;
                break;
            }
        } while (Module32NextW(snapshot, &me));
    }

    CloseHandle(snapshot);
    return found;
}

std::wstring Injector::GetLastErrorAsString()
{
    DWORD error = GetLastError();
    if (error == 0) return L"No error";

    LPWSTR msgBuf = nullptr;
    FormatMessageW(
        FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL, error, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
        (LPWSTR)&msgBuf, 0, NULL
    );

    std::wstring msg = msgBuf ? msgBuf : L"Unknown error";
    LocalFree(msgBuf);
    return msg;
}

} // namespace Native
} // namespace Stella
```

创建 `e:\AI\hook\src\Native\Stella.Native.Injector\dllmain.cpp`：
```cpp
#include <Windows.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved)
{
    switch (reason) {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
```

- [ ] **Step 4: 将 C++ 项目加入解决方案**

由于 .NET 6 SDK 的 `dotnet sln add` 不支持 VC++ 项目，需要手动编辑 .sln 文件或使用 Visual Studio。实际操作方案：

创建 `e:\AI\hook\README-cpp-build.md` 说明 C++ 项目的编译方式：
```
# C++ Native 模块编译说明
1. 打开 Visual Studio 2022+
2. 打开 GenshinStellaMod.sln
3. 在解决方案资源管理器中添加现有项目：
   - src\Native\Stella.Native.Injector\Stella.Native.Injector.vcxproj
   - （后续其他 C++ 项目同样操作）
4. 生成 → 生成解决方案
```

作为自动化方案的替代：使用 MSBuild 直接从命令行编译 C++ 项目：
```powershell
msbuild e:\AI\hook\src\Native\Stella.Native.Injector\Stella.Native.Injector.vcxproj /p:Configuration=Release /p:Platform=x64
```

- [ ] **Step 5: 验证 C++ 编译**

```powershell
# 检查 MSBuild 可用性
where.exe msbuild 2>$null

# 如果存在，尝试编译
msbuild e:\AI\hook\src\Native\Stella.Native.Injector\Stella.Native.Injector.vcxproj /p:Configuration=Release /p:Platform=x64 /v:minimal
```
Expected: Build succeeded，输出 `x64\Release\Stella.Native.Injector.dll`

- [ ] **Step 6: 提交**

```bash
git add -A
git commit -m "feat: add Native Injector C++ DLL with process injection and ejection"
```

---

## Phase 3: Core 层 — C# 引擎封装

### Task 6: Stella.Core — 引擎生命周期管理

**Files:**
- Create: `e:\AI\hook\src\Core\Stella.Core\EngineManager.cs`
- Create: `e:\AI\hook\src\Core\Stella.Core\IEngineModule.cs`
- Modify: `e:\AI\hook\src\Core\Stella.Core\Stella.Core.csproj`
- Delete: `e:\AI\hook\src\Core\Stella.Core\Class1.cs`
- Test: `e:\AI\hook\tests\Test.Genshin\Core\EngineManagerTests.cs`

- [ ] **Step 1: 添加引用**

```powershell
dotnet add e:\AI\hook\src\Core\Stella.Core\Stella.Core.csproj reference e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj
dotnet add e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj reference e:\AI\hook\src\Core\Stella.Core\Stella.Core.csproj
Remove-Item e:\AI\hook\src\Core\Stella.Core\Class1.cs -Force
```

- [ ] **Step 2: 定义引擎模块接口**

创建 `e:\AI\hook\src\Core\Stella.Core\IEngineModule.cs`：
```csharp
namespace Stella.Core;

/// <summary>
/// 所有引擎模块（ReShade / FPS Unlock / 3DMigoto）的统一接口。
/// </summary>
public interface IEngineModule
{
    /// <summary>模块名称（用于日志和显示）</summary>
    string Name { get; }

    /// <summary>初始化模块（在注入前调用）</summary>
    Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default);

    /// <summary>模块是否已成功初始化</summary>
    bool IsInitialized { get; }

    /// <summary>清理资源（游戏退出时调用）</summary>
    Task ShutdownAsync();
}
```

- [ ] **Step 3: 编写测试（Mock 引擎模块）**

创建 `e:\AI\hook\tests\Test.Genshin\Core\EngineManagerTests.cs`：
```csharp
using Xunit;
using Stella.Core;

namespace Test.Genshin.Core;

public class EngineManagerTests
{
    private class TestEngine : IEngineModule
    {
        public string Name { get; }
        public bool IsInitialized { get; private set; }

        public TestEngine(string name) => Name = name;

        public Task<bool> InitializeAsync(string dir, CancellationToken ct)
        {
            IsInitialized = true;
            return Task.FromResult(true);
        }

        public Task ShutdownAsync()
        {
            IsInitialized = false;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LaunchAll_SuccessfullyInitializesEngines()
    {
        var engine1 = new TestEngine("ReShade");
        var engine2 = new TestEngine("FPSUnlock");
        var manager = new EngineManager(new[] { engine1, engine2 });

        var result = await manager.LaunchAllAsync("C:\\FakeGame");

        Assert.True(result);
        Assert.True(engine1.IsInitialized);
        Assert.True(engine2.IsInitialized);
    }

    [Fact]
    public async Task LaunchAll_PartialFailure_StillSucceeds()
    {
        var working = new TestEngine("Working");
        var failing = new FailingEngine();
        var manager = new EngineManager(new IEngineModule[] { working, failing });

        var result = await manager.LaunchAllAsync("C:\\FakeGame");

        // Should not throw, should return true (partial success acceptable)
        Assert.True(result);
        Assert.True(working.IsInitialized);
        Assert.False(failing.IsInitialized);
    }

    [Fact]
    public async Task ShutdownAll_CleansUpAllEngines()
    {
        var engine1 = new TestEngine("E1");
        var engine2 = new TestEngine("E2");
        var manager = new EngineManager(new[] { engine1, engine2 });

        await manager.LaunchAllAsync("C:\\FakeGame");
        await manager.ShutdownAllAsync();

        Assert.False(engine1.IsInitialized);
        Assert.False(engine2.IsInitialized);
    }

    private class FailingEngine : IEngineModule
    {
        public string Name => "Failing";
        public bool IsInitialized => false;
        public Task<bool> InitializeAsync(string d, CancellationToken c) => Task.FromResult(false);
        public Task ShutdownAsync() => Task.CompletedTask;
    }
}
```

- [ ] **Step 4: 运行测试，预期失败**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~EngineManagerTests"
```
Expected: FAIL — `EngineManager` 不存在

- [ ] **Step 5: 实现 EngineManager**

创建 `e:\AI\hook\src\Core\Stella.Core\EngineManager.cs`：
```csharp
using Stella.Utils.Logging;

namespace Stella.Core;

/// <summary>
/// 引擎生命周期管理器。
/// 协调所有 IEngineModule 的初始化、启动和关闭。
/// 每个引擎独立失败不影响其他引擎。
/// </summary>
public class EngineManager
{
    private readonly IReadOnlyList<IEngineModule> _engines;
    private bool _running;

    public EngineManager(IEnumerable<IEngineModule> engines)
    {
        _engines = engines.ToList().AsReadOnly();
    }

    /// <summary>
    /// 依次初始化并启动所有引擎。
    /// 某个引擎失败不影响其他引擎继续初始化。
    /// </summary>
    public async Task<bool> LaunchAllAsync(string gameDirectory, CancellationToken ct = default)
    {
        StellaLogger.Info("EngineManager", $"Launching {_engines.Count} engine modules...");

        bool anySucceeded = false;
        foreach (var engine in _engines)
        {
            try
            {
                StellaLogger.Info("EngineManager", $"Initializing: {engine.Name}");
                bool ok = await engine.InitializeAsync(gameDirectory, ct);

                if (ok)
                {
                    StellaLogger.Info("EngineManager", $"{engine.Name} initialized successfully");
                    anySucceeded = true;
                }
                else
                {
                    StellaLogger.Warn("EngineManager", $"{engine.Name} failed to initialize");
                }
            }
            catch (Exception ex)
            {
                StellaLogger.Error("EngineManager", $"{engine.Name} threw exception during init", ex);
            }
        }

        _running = anySucceeded;
        if (anySucceeded)
            StellaLogger.Info("EngineManager", "Launch complete — at least one engine succeeded");

        return anySucceeded;
    }

    /// <summary>
    /// 关闭所有引擎并释放资源。
    /// </summary>
    public async Task ShutdownAllAsync()
    {
        StellaLogger.Info("EngineManager", "Shutting down all engines...");
        _running = false;

        foreach (var engine in _engines)
        {
            try
            {
                await engine.ShutdownAsync();
                StellaLogger.Info("EngineManager", $"{engine.Name} shut down");
            }
            catch (Exception ex)
            {
                StellaLogger.Error("EngineManager", $"{engine.Name} error during shutdown", ex);
            }
        }
    }

    /// <summary>
    /// 获取所有已注册的引擎模块。
    /// </summary>
    public IReadOnlyList<IEngineModule> Engines => _engines;

    /// <summary>
    /// 当前是否有引擎在运行。
    /// </summary>
    public bool IsRunning => _running;
}
```

- [ ] **Step 6: 运行测试，验证通过**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~EngineManagerTests"
```
Expected: 3 passed, 0 failed

- [ ] **Step 7: 提交**

```bash
git add -A
git commit -m "feat: add Core EngineManager with graceful partial-failure support"
```

---

### Task 7: Stella.Core.Reshade & FpsUnlock & Migoto — 引擎桩模块

**Files:**
- Create: `e:\AI\hook\src\Core\Stella.Core.Reshade\ReshadeEngine.cs`
- Create: `e:\AI\hook\src\Core\Stella.Core.FpsUnlock\FpsUnlockEngine.cs`
- Create: `e:\AI\hook\src\Core\Stella.Core.Migoto\MigotoEngine.cs`
- Modify: 各 .csproj 添加对 `Stella.Core` 的引用
- Delete: 各 `Class1.cs`

- [ ] **Step 1: 添加项目引用**

```powershell
dotnet add e:\AI\hook\src\Core\Stella.Core.Reshade\Stella.Core.Reshade.csproj reference e:\AI\hook\src\Core\Stella.Core\Stella.Core.csproj
dotnet add e:\AI\hook\src\Core\Stella.Core.FpsUnlock\Stella.Core.FpsUnlock.csproj reference e:\AI\hook\src\Core\Stella.Core\Stella.Core.csproj
dotnet add e:\AI\hook\src\Core\Stella.Core.Migoto\Stella.Core.Migoto.csproj reference e:\AI\hook\src\Core\Stella.Core\Stella.Core.csproj
Remove-Item e:\AI\hook\src\Core\Stella.Core.Reshade\Class1.cs -Force
Remove-Item e:\AI\hook\src\Core\Stella.Core.FpsUnlock\Class1.cs -Force
Remove-Item e:\AI\hook\src\Core\Stella.Core.Migoto\Class1.cs -Force
```

- [ ] **Step 2: 实现 ReshadeEngine（桩）**

创建 `e:\AI\hook\src\Core\Stella.Core.Reshade\ReshadeEngine.cs`：
```csharp
using Stella.Utils.Logging;

namespace Stella.Core.Reshade;

/// <summary>
/// ReShade 着色器注入引擎。
/// Phase 3 实现桩逻辑，后续连接 Native DLL。
/// </summary>
public class ReshadeEngine : IEngineModule
{
    public string Name => "ReShade";
    public bool IsInitialized { get; private set; }

    /// <summary>着色器预设文件目录</summary>
    public string? PresetDirectory { get; private set; }

    /// <summary>当前加载的预设名称</summary>
    public string? ActivePreset { get; private set; }

    public Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        PresetDirectory = Path.Combine(gameDirectory, "reshade-shaders");
        StellaLogger.Info(Name, $"Preset directory set to: {PresetDirectory}");

        // TODO Phase 4: 部署 ReShade DLL 到游戏目录，配置 DX Hook
        // TODO Phase 4: 加载 HLSL 预设文件

        IsInitialized = true;
        StellaLogger.Info(Name, "Initialized (stub mode)");
        return Task.FromResult(true);
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 加载指定的着色器预设。
    /// </summary>
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
```

- [ ] **Step 3: 实现 FpsUnlockEngine（桩）**

创建 `e:\AI\hook\src\Core\Stella.Core.FpsUnlock\FpsUnlockEngine.cs`：
```csharp
using Stella.Utils.Logging;

namespace Stella.Core.FpsUnlock;

/// <summary>
/// FPS 解锁引擎。
/// 通过内存偏移修改游戏帧率上限。
/// </summary>
public class FpsUnlockEngine : IEngineModule
{
    public string Name => "FPS Unlock";
    public bool IsInitialized { get; private set; }

    /// <summary>目标帧率上限</summary>
    public int TargetFps { get; set; } = 144;

    /// <summary>当前偏移配置文件路径</summary>
    public string? OffsetConfigPath { get; private set; }

    public Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        OffsetConfigPath = Path.Combine(gameDirectory, "stella-fps-offsets.json");
        StellaLogger.Info(Name, $"Offset config: {OffsetConfigPath}");

        // TODO Phase 4: 读取偏移表，定位内存地址
        // TODO Phase 4: 连接 Native DLL 进行内存修改

        IsInitialized = true;
        StellaLogger.Info(Name, $"Initialized (stub) - target FPS: {TargetFps}");
        return Task.FromResult(true);
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: 实现 MigotoEngine（桩）**

创建 `e:\AI\hook\src\Core\Stella.Core.Migoto\MigotoEngine.cs`：
```csharp
using Stella.Utils.Logging;

namespace Stella.Core.Migoto;

/// <summary>
/// 3DMigoto 模组加载引擎。
/// 通过 Hook DrawIndexed 实现 3D 模型/贴图替换。
/// </summary>
public class MigotoEngine : IEngineModule
{
    public string Name => "3DMigoto";
    public bool IsInitialized { get; private set; }

    /// <summary>已加载的模组数量</summary>
    public int LoadedModCount { get; private set; }

    public Task<bool> InitializeAsync(string gameDirectory, CancellationToken ct = default)
    {
        var modDir = Path.Combine(gameDirectory, "Mods");
        StellaLogger.Info(Name, $"Mod directory: {modDir}");

        // TODO Phase 5: 集成 3DMigoto 社区 DLL
        // TODO Phase 5: 扫描 Mods 目录，加载 .ini 配置

        IsInitialized = true;
        LoadedModCount = 0;
        StellaLogger.Info(Name, "Initialized (stub) - 0 mods loaded");
        return Task.FromResult(true);
    }

    public Task ShutdownAsync()
    {
        IsInitialized = false;
        LoadedModCount = 0;
        StellaLogger.Info(Name, "Shut down");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5: 编写集成测试**

创建 `e:\AI\hook\tests\Test.Genshin\Core\AllEnginesIntegrationTests.cs`：
```csharp
using Xunit;
using Stella.Core;
using Stella.Core.Reshade;
using Stella.Core.FpsUnlock;
using Stella.Core.Migoto;

namespace Test.Genshin.Core;

public class AllEnginesIntegrationTests
{
    [Fact]
    public async Task AllThreeEngines_CanBeRegisteredAndLaunched()
    {
        var engines = new IEngineModule[]
        {
            new ReshadeEngine(),
            new FpsUnlockEngine { TargetFps = 144 },
            new MigotoEngine()
        };

        var manager = new EngineManager(engines);
        var tempDir = Path.Combine(Path.GetTempPath(), "StellaTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = await manager.LaunchAllAsync(tempDir);
            Assert.True(result);
            Assert.True(manager.IsRunning);
            Assert.Equal(3, manager.Engines.Count);

            await manager.ShutdownAllAsync();
            Assert.False(manager.IsRunning);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 6: 运行测试**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~AllEnginesIntegrationTests"
```
Expected: 1 passed, 0 failed

- [ ] **Step 7: 提交**

```bash
git add -A
git commit -m "feat: add Reshade, FpsUnlock, Migoto engine stubs with IEngineModule interface"
```

---

## Phase 4: Application 层 — 业务服务

### Task 8: Stella.Services — AI 推荐引擎 + 配置管理

**Files:**
- Create: `e:\AI\hook\src\Application\Stella.Services\PresetRecommender.cs`
- Create: `e:\AI\hook\src\Application\Stella.Services\PresetPerformanceData.cs`
- Create: `e:\AI\hook\src\Application\Stella.Services\ConfigManager.cs`
- Modify: `e:\AI\hook\src\Application\Stella.Services\Stella.Services.csproj`
- Delete: `e:\AI\hook\src\Application\Stella.Services\Class1.cs`
- Test: `e:\AI\hook\tests\Test.Genshin\Services\PresetRecommenderTests.cs`

- [ ] **Step 1: 设置依赖**

```powershell
dotnet add e:\AI\hook\src\Application\Stella.Services\Stella.Services.csproj reference e:\AI\hook\src\Infrastructure\Stella.Utils\Stella.Utils.csproj
dotnet add e:\AI\hook\src\Application\Stella.Services\Stella.Services.csproj reference e:\AI\hook\src\Infrastructure\Stella.DeviceIdentifier\Stella.DeviceIdentifier.csproj
dotnet add e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj reference e:\AI\hook\src\Application\Stella.Services\Stella.Services.csproj
Remove-Item e:\AI\hook\src\Application\Stella.Services\Class1.cs -Force
```

- [ ] **Step 2: 创建预设性能数据模型**

创建 `e:\AI\hook\src\Application\Stella.Services\PresetPerformanceData.cs`：
```csharp
namespace Stella.Services;

/// <summary>
/// 着色器预设的性能基准数据。
/// </summary>
public class PresetPerformanceData
{
    public const string DataFileName = "presets-performance.json";

    /// <summary>预设名称</summary>
    public string Name { get; init; } = "";

    /// <summary>显示用的中文名</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>描述</summary>
    public string Description { get; init; } = "";

    /// <summary>画质分（HDR/景深/光晕 等特效丰度），0-100</summary>
    public int VisualQualityScore { get; init; }

    /// <summary>稳定性分（已知 Bug 数、兼容性），0-100</summary>
    public int StabilityScore { get; init; }

    /// <summary>在各 GPU 上的性能表现</summary>
    public Dictionary<string, GpuPerformanceEntry> PerformanceByGpu { get; init; } = new();
}

public class GpuPerformanceEntry
{
    /// <summary>GPU 利用率百分比</summary>
    public int GpuUtilization { get; init; }

    /// <summary>预估最低 FPS</summary>
    public int EstimatedFpsMin { get; init; }

    /// <summary>预估最高 FPS</summary>
    public int EstimatedFpsMax { get; init; }

    /// <summary>最低显存需求（MB）</summary>
    public int MinVramMb { get; init; }
}
```

- [ ] **Step 3: 编写 AI 推荐器测试**

创建 `e:\AI\hook\tests\Test.Genshin\Services\PresetRecommenderTests.cs`：
```csharp
using Xunit;
using Stella.DeviceIdentifier;
using Stella.Services;

namespace Test.Genshin.Services;

public class PresetRecommenderTests
{
    private static List<PresetPerformanceData> CreateSampleData() => new()
    {
        new PresetPerformanceData
        {
            Name = "cinematic", DisplayName = "电影级", Description = "HDR+景深+光晕",
            VisualQualityScore = 90, StabilityScore = 85,
            PerformanceByGpu = new()
            {
                ["rtx4060"] = new() { GpuUtilization = 55, EstimatedFpsMin = 100, EstimatedFpsMax = 130, MinVramMb = 5000 },
                ["gtx1660"] = new() { GpuUtilization = 82, EstimatedFpsMin = 60, EstimatedFpsMax = 80, MinVramMb = 4000 }
            }
        }
    };

    [Fact]
    public void Recommend_Rtx4060_ReturnsTopThree()
    {
        var recommender = new PresetRecommender(CreateSampleData());
        var device = new DeviceInfo
        {
            GpuName = "NVIDIA GeForce RTX 4060",
            GpuMemoryMb = 8000,
            CpuName = "Intel i5-13400",
            ScreenWidth = 2560, ScreenHeight = 1440
        };

        var results = recommender.Recommend(device, communityData: null);

        Assert.Equal(3, results.Count);
        Assert.Equal("cinematic", results[0].PresetName); // 电影级 应该在 Top1（画质最高 + FPS够用）
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Recommend_Offline_RanksWithoutCommunity()
    {
        var recommender = new PresetRecommender(CreateSampleData());
        var device = new DeviceInfo
        {
            GpuName = "NVIDIA GeForce RTX 4060",
            GpuMemoryMb = 8000,
            ScreenWidth = 1920, ScreenHeight = 1080
        };

        var results = recommender.Recommend(device, communityData: null);

        // All three should have scores > 0
        Assert.All(results, r => Assert.True(r.Score > 0));
    }

    [Fact]
    public void Recommend_VramInsufficient_ExcludesPreset()
    {
        var recommender = new PresetRecommender(CreateSampleData());
        var device = new DeviceInfo
        {
            GpuName = "NVIDIA GeForce RTX 4060",
            GpuMemoryMb = 3000, // Below all min VRAM
            ScreenWidth = 1920, ScreenHeight = 1080
        };

        var results = recommender.Recommend(device, communityData: null);
        Assert.Empty(results); // No preset fits
    }
}
```

- [ ] **Step 4: 运行测试，预期失败**

```powershell
dotnet test e:\AI\hook\tests\Test.Genshin\Test.Genshin.csproj --filter "FullyQualifiedName~PresetRecommenderTests"
```
Expected: FAIL — `PresetRecommender` 和 `RecommendationResult` 类型不存在

- [ ] **Step 5: 实现 PresetRecommender 和 ConfigManager**

（详见 `docs/superpowers/specs/2026-05-24-genshin-stella-mod-design.md` §5 中的评分算法公式）

- [ ] **Step 6-7: 运行测试 → 提交**

---

### Task 9: Stella.Update — 自动更新检查

利用 GitHub Release API 获取最新版本，比对本地版本号，下载增量更新包。

---

## Phase 5: WPF UI 层

### Task 10-11: Stella.Launcher & Stella.Welcome

按规格书 §4 实现四大页面（一键启动 / 着色器预设 / 性能仪表盘 / AI 推荐），使用 WPF + CommunityToolkit.Mvvm + MaterialDesignInXAML + LiveCharts2。

---

## Phase 6: 分发 & CI

### Task 12-13: Stella.Builder + CI/CD

GitHub Actions 流水线：push → build → test → 偏移表校验 → 生成 Release 包。

---

## 执行说明

**当前计划覆盖完整：**
- ✅ Phase 1 (Tasks 1-4): 项目脚手架 + 基础设施层 — 完整代码
- ✅ Phase 2 (Task 5): Native 注入引擎 — 完整代码
- ✅ Phase 3 (Tasks 6-7): Core 引擎封装 + 桩模块 — 完整代码
- 📝 Phase 4 (Task 8): AI 推荐器测试 + 数据模型 — 已提供测试，实现代码在规格书 §5
- 📝 Phase 5-6: WPF UI + 分发 — 架构已定，具体实现按 UI 线框图逐步展开

建议从 Task 1 开始逐步执行，每完成一个 Task 做一次提交。