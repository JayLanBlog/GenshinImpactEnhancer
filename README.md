# Genshin Impact Enhancer

<p align="center">
  <b>🎮 原神游戏增强工具 · 一站式画面增强、帧率解锁、模组加载平台</b>
</p>

---

## 📖 目录

- [功能架构](#-功能架构)
- [核心功能](#-核心功能)
- [技术架构](#-技术架构)
- [使用教程](#-使用教程)
- [构建与部署](#-构建与部署)
- [项目结构](#-项目结构)
- [配置说明](#-配置说明)

---

## 🏗 功能架构

<p align="center">
  <img src="assets/architecture_diagram.png" alt="Genshin Impact Enhancer Architecture" width="100%">
</p>

<details>
<summary>📋 点击展开文本版架构图</summary>

```
┌──────────────────────────────────────────────────────────────────┐
│                     Genshin Impact Enhancer                      │
│                      桌面启动器 (WPF)                              │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────┐       │
│  │  FPS Unlock │  │   ReShade    │  │    3DMigoto      │       │
│  │   帧率解锁   │  │  画面后处理   │  │   MOD 模组加载    │       │
│  │             │  │              │  │                  │       │
│  │ 解除游戏     │  │ HDR/锐化/   │  │ 支持角色/场景     │       │
│  │ 60 FPS 限制  │  │ 色彩/光晕    │  │ 3D模型替换       │       │
│  │ 最高 144FPS │  │ 86+ 种特效  │  │                  │       │
│  └──────┬──────┘  └──────┬───────┘  └────────┬─────────┘       │
│         │                │                    │                  │
│         └────────────────┼────────────────────┘                  │
│                          │                                       │
│               ┌──────────┴──────────┐                           │
│               │   Engine Manager    │  ← 插件式引擎管理           │
│               │   统一生命周期管理     │                           │
│               └──────────┬──────────┘                           │
│                          │                                       │
├──────────────────────────┼───────────────────────────────────────┤
│                          │                                       │
│     ┌────────────────────┼────────────────────┐                 │
│     │         Application Services            │                 │
│     ├────────────────────┼────────────────────┤                 │
│     │  ConfigManager     │  PresetRecommender │                 │
│     │  通用配置读写        │  智能预设推荐       │                 │
│     │  (JSON 持久化)      │  (硬件匹配+评分)    │                 │
│     └────────────────────┴────────────────────┘                 │
│                          │                                       │
│     ┌────────────────────┼────────────────────┐                 │
│     │            Infrastructure                │                 │
│     ├────────────────────┼────────────────────┤                 │
│     │  DeviceDetector    │  StartupBeacon     │                 │
│     │  硬件信息检测(WMI)  │  开机自启管理       │                 │
│     └────────────────────┴────────────────────┘                 │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│                    Native Injection Layer                        │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │          GIEnhancer.Native.Injector.dll (C++ x64)         │  │
│  │  · DLL 注入 (CreateRemoteThread + LoadLibrary)             │  │
│  │  · PEB 模块隐藏 (反检测)                                     │  │
│  │  · 游戏内叠加层 Overlay (F11 切换)                           │  │
│  │  · ReShade 配置集成面板 (Home 键)                            │  │
│  │  · 热键管理 (RegisterHotKey)                                │  │
│  └──────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

</details>

---

## 🎯 核心功能

### 1. ReShade 画面增强

通过注入 **ReShade64.dll** 到游戏进程，提供实时后处理特效：

| 特性 | 说明 |
|------|------|
| 🎨 特效数量 | **86+** 种自定义 Shader 特效 |
| ⌨️ 控制面板 | **Home 键** 打开/关闭 ReShade 参数调节面板 |
| 📦 预设管理 | 支持导入/切换 `.ini` 格式画面预设 |
| 🚀 性能模式 | 可开关性能模式，平衡画质与帧率 |
| 💾 缓存加速 | 着色器编译缓存，减少游戏启动卡顿 |

**内置预设效果示例：**
- 🎬 **电影级** — HDR + 景深 + 光晕 + 锐化
- ☀️ **明亮** — 锐化 + 色彩增强 + 亮度提升
- 🌿 **写实** — 自然色彩 + 细微锐化 + 轻度景深
- 🎭 **动漫风** — 色彩增强 + 轮廓线 + 去噪

### 2. FPS 帧率解锁

解除原神默认的 **60 FPS** 帧率上限：

| 特性 | 说明 |
|------|------|
| 🔓 解锁上限 | 最高支持 **144 FPS**（可自定义） |
| 📐 偏移配置 | 通过 `stella-fps-offsets.json` 适配不同游戏版本 |

### 3. 3DMigoto MOD 加载

支持加载 3DMigoto 格式的游戏模组：

| 特性 | 说明 |
|------|------|
| 📁 MOD 目录 | 自动扫描 `{游戏目录}\Mods` 下的模组 |
| 🔄 热加载 | 支持运行时加载/卸载 MOD |

### 4. 智能预设推荐

根据你的硬件配置自动推荐最佳画面预设：

| 功能 | 说明 |
|------|------|
| 🔍 硬件检测 | 自动识别 GPU 型号、显存大小、CPU 型号 |
| 📊 性能基准 | 内置多款热门 GPU 的性能数据库 |
| 🧮 加权评分 | 综合性能、画质、稳定性三维度智能推荐 |

### 5. 其他特性

- 🖥️ **游戏内叠加层** — F11 键显示/隐藏状态面板（ReShade 状态、模块信息、当前时间）
- 🛡️ **反检测** — PEB 模块隐藏，降低被反作弊检测的风险
- 🚀 **开机自启** — 可设置随系统启动
- 📝 **日志记录** — 完整运行日志，便于问题排查

---

## 💻 使用教程

### 前置条件

| 要求 | 说明 |
|------|------|
| 操作系统 | Windows 10/11 (64-bit) |
| .NET 运行环境 | .NET 6.0 Desktop Runtime |
| VC++ 运行库 | Visual C++ 2015-2022 Redistributable (x64) |
| 管理员权限 | 注入游戏进程需要管理员权限（程序会自动申请提权） |
| 原神游戏 | 已安装原神客户端 |

### 快速开始

#### 1. 下载与解压

```
GenshinImpactEnhancer/
├── GIEnhancer.Launcher.exe     ← 主程序
├── GIEnhancer.Native.Injector.dll  ← 注入 DLL
└── ...
```

#### 2. 配置 ReShade（可选）

如果要使用画面增强功能，需要准备 ReShade 资源：

1. 下载 ReShade 安装包并提取 `ReShade64.dll`
2. 将 `ReShade64.dll` 放到任意位置（如 `D:\LaunchGame\data\dependencies\reshade\`）
3. 在游戏目录 `D:\TestGame\HoYoPlay\games\Genshin Impact game\` 下放置 `ReShade.ini` 配置文件

> 📌 **ReShade.ini 配置示例：**
> ```ini
> [GENERAL]
> EffectSearchPaths=E:\GameData\ReShade\Shaders\Effects
> TextureSearchPaths=E:\GameData\ReShade\Shaders\Textures
> PresetPath=E:\GameData\ReShade\Presets\MyPreset.ini
> 
> [INPUT]
> KeyOverlay=36,0,0,0    ; Home 键打开面板
> ```

#### 3. 启动程序

双击运行 `GIEnhancer.Launcher.exe`：

```
┌─────────────────────────────────────┐
│     Genshin Impact Enhancer         │
│  ┌───────────────────────────────┐  │
│  │  🖥️ 硬件信息                    │  │
│  │  GPU: NVIDIA GeForce RTX 4060 │  │
│  │  VRAM: 8188 MB               │  │
│  │  CPU: Intel Core i7-13700H   │  │
│  └───────────────────────────────┘  │
│  ┌───────────────────────────────┐  │
│  │  💡 推荐预设: 写实              │  │
│  │  基于你的硬件配置自动推荐        │  │
│  └───────────────────────────────┘  │
│                                     │
│  [ 🚀 启动游戏 ]   [ ⚙️ 设置 ]     │
└─────────────────────────────────────┘
```

#### 4. 启动游戏并注入

点击 **"启动游戏"** 按钮，程序会：

1. 🚀 以挂起模式启动游戏进程
2. 💉 注入 `ReShade64.dll` 和 `GIEnhancer.Native.Injector.dll`
3. ▶️ 恢复游戏主线程
4. ✅ 等待 ReShade 初始化完成
5. ⌨️ 自动发送 Home 键打开 ReShade 面板

#### 5. 游戏中操作

| 按键 | 功能 |
|------|------|
| **Home** | 打开/关闭 ReShade 参数调节面板 |
| **F11** | 显示/隐藏 Enhancer 状态信息面板 |
| **PageUp / PageDown** | 切换 ReShade 预设 |
| **PrintScreen** | ReShade 截图 |

#### 6. 命令行注入（高级用户）

也可以使用测试工具直接注入，不启动 GUI：

```bat
cd test-tools
run_test.bat
```

或者直接运行：

```powershell
.\test-tools\HomeTestTool\bin\Debug\net6.0\HomeTestTool.exe
```

注入成功后会自动进入**驻留模式**，持续监控游戏状态，按 `Ctrl+C` 退出监控（游戏会继续运行）。

---

## 🔧 构建与部署

### 开发环境要求

| 工具 | 版本 | 用途 |
|------|------|------|
| Visual Studio 2022 | 17.0+ Community | C++ 项目编译 |
| .NET SDK | 6.0.x | C# 项目编译 |
| Windows SDK | 10.0+ | Win32 API 支持 |
| MSVC v143 | 14.31+ | C++ 工具链 |

### 从源码构建

#### 1. 克隆项目

```bash
git clone <repo-url>
cd GenshinImpactEnhancer
```

#### 2. 构建 Native C++ DLL

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  src\Native\GIEnhancer.Native.Injector\GIEnhancer.Native.Injector.vcxproj `
  /p:Configuration=Release /p:Platform=x64 /t:Build /m
```

输出：`src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll`

#### 3. 构建 C# 启动器

```powershell
dotnet build src\UI\GIEnhancer.Launcher\GIEnhancer.Launcher.csproj -c Release
```

输出：`src\UI\GIEnhancer.Launcher\bin\Release\net6.0-windows\GIEnhancer.Launcher.exe`

#### 4. 构建测试工具

```powershell
dotnet build test-tools\HomeTestTool\HomeTestTool.csproj -c Debug
```

#### 5. 运行自动化测试

```powershell
dotnet test tests\Test.Genshin\Test.Genshin.csproj
```

### 打包部署

使用 Builder 项目生成发行包：

```powershell
dotnet run --project src\Distribution\GIEnhancer.Builder\GIEnhancer.Builder.csproj
```

### 部署清单

最终部署目录结构：

```
GenshinImpactEnhancer/                    # 发行包根目录
├── GIEnhancer.Launcher.exe              # 主启动器
├── GIEnhancer.Launcher.dll              # 启动器程序集
├── GIEnhancer.Native.Injector.dll       # C++ 注入 DLL (x64)
├── GIEnhancer.Launcher.runtimeconfig.json
├── GIEnhancer.Core.dll                  # 引擎核心
├── GIEnhancer.Core.FpsUnlock.dll        # FPS 解锁模块
├── GIEnhancer.Core.Reshade.dll          # ReShade 模块
├── GIEnhancer.Core.Migoto.dll           # 3DMigoto 模块
├── GIEnhancer.Services.dll              # 业务服务
├── GIEnhancer.Utils.dll                 # 工具库
├── GIEnhancer.DeviceIdentifier.dll      # 设备检测
├── GIEnhancer.StartupBeacon.dll         # 开机自启
└── GIEnhancer.Update.dll                # 更新检查
```

### 安装步骤

1. 解压发行包到任意目录（建议非系统盘，如 `D:\GenshinImpactEnhancer`）
2. 确保已安装 [.NET 6.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)
3. 确保已安装 [VC++ Redistributable x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)
4. 以管理员身份运行 `GIEnhancer.Launcher.exe`
5. 首次运行会自动创建日志目录：`%LocalAppData%\GenshinImpactEnhancer\`

> ⚠️ **注意：** 程序需要管理员权限才能注入游戏进程，首次运行会触发 UAC 弹窗，请点击"是"允许。

---

## 📂 项目结构

```
GenshinImpactEnhancer/
│
├── GenshinImpactEnhancer.sln          # Visual Studio 解决方案
├── .editorconfig                       # 代码风格配置
├── .gitignore                          # Git 忽略规则
├── README.md                           # 本文件
│
├── src/                                # 【主源码】
│   ├── UI/
│   │   ├── GIEnhancer.Launcher/        # WPF 主启动器
│   │   └── GIEnhancer.Welcome/         # 欢迎引导窗口
│   │
│   ├── Core/                           # 引擎核心层
│   │   ├── GIEnhancer.Core/            # 引擎管理器 + IEngineModule 接口
│   │   ├── GIEnhancer.Core.FpsUnlock/  # FPS 解锁模块
│   │   ├── GIEnhancer.Core.Reshade/    # ReShade 后处理模块
│   │   └── GIEnhancer.Core.Migoto/     # 3DMigoto MOD 模块
│   │
│   ├── Application/                    # 应用服务层
│   │   ├── GIEnhancer.Services/        # 配置管理 + 预设推荐
│   │   └── GIEnhancer.Update/          # 版本更新检查
│   │
│   ├── Infrastructure/                 # 基础设施层
│   │   ├── GIEnhancer.Utils/           # 日志记录 + 崩溃保护
│   │   ├── GIEnhancer.DeviceIdentifier/# 硬件信息采集
│   │   └── GIEnhancer.StartupBeacon/   # 开机自启管理
│   │
│   ├── Native/                         # 原生 C++ 层
│   │   └── GIEnhancer.Native.Injector/ # DLL 注入 + Overlay + 反检测
│   │
│   └── Distribution/
│       └── GIEnhancer.Builder/         # 构建打包工具
│
├── test-tools/                         # 【测试工具】
│   ├── HomeTestTool/                   # 注入测试工具（命令行）
│   └── run_test.bat                    # 一键测试脚本
│
└── tests/                              # 【单元测试】
    └── Test.Genshin/                   # xUnit 测试项目
        ├── Core/                       # 引擎管理器测试
        ├── DeviceIdentifier/           # 硬件检测测试
        ├── Logging/                    # 日志测试
        ├── Services/                   # 推荐引擎测试
        ├── StartupBeacon/              # 自启管理测试
        └── Update/                     # 更新检查测试
```

---

## ⚙️ 配置说明

### ReShade.ini

位于游戏目录下，核心配置项：

| 配置节 | 键 | 说明 | 示例 |
|--------|-----|------|------|
| `[GENERAL]` | `EffectSearchPaths` | Shader 特效文件搜索路径 | `E:\GameData\ReShade\Shaders\Effects` |
| `[GENERAL]` | `TextureSearchPaths` | 纹理文件搜索路径 | `E:\GameData\ReShade\Shaders\Textures` |
| `[GENERAL]` | `PresetPath` | 当前使用的预设路径 | `E:\GameData\ReShade\Presets\My.ini` |
| `[GENERAL]` | `IntermediateCachePath` | 着色器缓存路径 | `E:\GameData\ReShade\Cache` |
| `[GENERAL]` | `PerformanceMode` | 性能模式 (0=关, 1=开) | 1 |
| `[INPUT]` | `KeyOverlay` | 面板快捷键 VK 码 | `36,0,0,0` (Home) |
| `[INPUT]` | `KeyEffects` | 特效开关快捷键 | `35,0,0,0` (End) |
| `[INPUT]` | `KeyReload` | 重新加载快捷键 | `0,0,0,0` |
| `[INPUT]` | `ForceShortcutModifiers` | 强制修饰键 | 0 |
| `[INPUT]` | `InputProcessing` | 输入处理模式 | 2 |
| `[OVERLAY]` | `ShowFPS` | 显示帧率 | 1 |
| `[OVERLAY]` | `ShowClock` | 显示时钟 | 1 |
| `[OVERLAY]` | `ShowFrameTime` | 显示帧时间 | 1 |
| `[SCREENSHOT]` | `SavePath` | 截图保存路径 | `E:\GameData\Screenshots` |
| `[SCREENSHOT]` | `FileFormat` | 格式 (0=BMP, 1=PNG, 2=JPG) | 1 |
| `[STELLA]` | `ConfigVersion` | 配置版本标识 | 1.2.1 |

### stella-fps-offsets.json

FPS 解锁偏移配置文件（JSON 格式），包含各游戏版本的内存偏移量。

### 日志路径

所有运行日志存放在：

```
%LocalAppData%\GenshinImpactEnhancer\
├── logs\                        # 启动器日志
├── overlay_debug.log            # C++ Overlay 调试日志
├── injection.log               # 注入流程日志
└── test_result.log             # 测试结果日志
```

---

## 📄 许可证

本项目仅供学习研究使用，请遵守相关游戏用户协议。

---

<p align="center">
  <sub>Made with ❤️ for Genshin Impact players</sub>
</p>
