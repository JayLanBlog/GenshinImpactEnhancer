# Genshin Stella Mod — 设计规格书

> **日期：** 2026-05-24  
> **参考项目：** [sefinek/Genshin-Impact-ReShade](https://github.com/sefinek/Genshin-Impact-ReShade)  
> **目标：** 全功能复刻 + 差异化创新

---

## 1. 项目概述

### 1.1 项目定位

一个面向《原神》的桌面端画质增强工具，集成 ReShade 着色器注入、FPS 解锁、3DMigoto 模组加载三大能力，通过统一启动器提供一键式体验。

### 1.2 核心差异化

| 差异化方向 | 描述 |
|-----------|------|
| 🤖 AI 智能调参 | 基于硬件指纹 + 社区数据，加权评分算法自动推荐最佳着色器预设 |
| 🎨 极致 UI/UX | WPF + Fluent Design，Acrylic/Mica 效果，实时预览，丝滑动画 |
| 📊 性能仪表盘 | 游戏内 Overlay + 启动器面板，实时 FPS/GPU 温度/着色器开销可视化 |

### 1.3 技术栈

| 层 | 技术 |
|----|------|
| 桌面 UI | WPF (.NET 10)，MVVM (CommunityToolkit.Mvvm) |
| 控件库 | MaterialDesignInXAML + HandyControl |
| 图表 | LiveCharts2 |
| 核心引擎封装 | C# (.NET 10) |
| 原生注入层 | C++ (DLL) |
| 安装器 | WiX Toolset / .NET 发布单文件 |
| 测试 | xUnit + NSubstitute |

---

## 2. 架构设计

### 2.1 总体架构：分层单体仓库 (Layered Monorepo)

单个 .sln 解决方案，按职责分 5 层，上层依赖下层，绝不允许反向。

```
GenshinStellaMod.sln
│
├── 🖥️ Presentation Layer（UI）
│   ├── Stella.Launcher          WPF 主程序
│   └── Stella.Welcome           首次引导
│
├── 🧠 Application Layer（业务逻辑）
│   ├── Stella.Services          AI推荐 · 性能采集 · 场景识别 · 配置管理
│   └── Stella.Update            自动更新检查
│
├── ⚙️ Core Layer（引擎封装）
│   ├── Stella.Core              引擎总入口 · 生命周期管理
│   ├── Stella.Core.Reshade      ReShade DLL 注入 · HLSL 预设解析
│   ├── Stella.Core.FpsUnlock    内存偏移定位 · 帧率修改
│   └── Stella.Core.Migoto       3DMigoto 封装 · 模组加载
│
├── 🧬 Native Layer（原生 C++）
│   ├── Stella.Native.Injector   进程注入器
│   ├── Stella.Native.DXHook     DX11/DX12 Present Hook
│   └── Stella.Native.Overlay    游戏内 Overlay 渲染
│
├── 🛠️ Infrastructure
│   ├── Stella.DeviceIdentifier  硬件指纹采集
│   ├── Stella.Utils             通用工具 · 日志
│   └── Stella.StartupBeacon     开机自启 · 托盘
│
├── 💿 Distribution
│   ├── Stella.Setup             MSI/EXE 安装器
│   └── Stella.Builder           CI/CD 构建脚本
│
└── 🧪 Tests
    └── Test.Genshin             集成测试
```

**依赖关系：**

```
Launcher → Services → Core → Native DLL
                ↓
              Update
                ↓
    DeviceIdentifier · Utils · StartupBeacon (被所有层引用)
```

### 2.2 与原项目差异

| 对比 | 原项目 | 本设计 |
|------|--------|--------|
| 仓库管理 | Git Submodule（多仓库） | 单体仓库（单一 .sln） |
| AI 推荐 | 无 | 新增 Stella.Services（加权评分） |
| 游戏 Overlay | 无 | 新增 Stella.Native.Overlay（性能仪表盘） |
| Core 拆分 | 单一 Stella.Core | 拆为 4 个子模块（入口 + 3 引擎） |

---

## 3. 核心数据流

### 3.1 启动→注入→运行→退出 完整链路

**Phase 1 — 准备：**

```
用户点击"启动" → 硬件指纹采集 → AI 推荐预设 → 用户确认配置
```

**Phase 2 — 注入：**

```
部署 Native DLL → CREATE_SUSPENDED 创建游戏进程
→ CreateRemoteThread(LoadLibrary) 注入 DXHook.dll
→ DX Present() Hook 安装 → ResumeThread 恢复游戏
```

**Phase 3 — 运行时：**

```
游戏进程内：
  ReShade Hook: Present() → HLSL 着色器管线 → 输出帧
  FPS Unlock:   内存偏移写值 → 解除帧率上限
  3DMigoto Hook: DrawIndexed → 模型/贴图替换
  Overlay 渲染:  Direct2D → FPS/温度/预设名显示

IPC 通信：
  Overlay ← Shared Memory(MemoryMappedFile) → WPF 仪表盘
  控制指令 ← Named Pipe → 游戏内 Overlay
```

**Phase 4 — 退出：**

```
游戏退出检测 → 卸载所有 Hook → 释放 DLL → 清理共享内存 → 收集运行数据
```

### 3.2 关键技术决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 注入方式 | CREATE_SUSPENDED + CreateRemoteThread + LoadLibrary | 最经典可靠，不碰游戏文件，兼容性好 |
| IPC 通信 | Named Pipe（指令）+ Shared Memory（数据） | 低延迟指令 + 高吞吐数据，各司其职 |
| 反作弊兼容 | 只 Hook DX Present()，不修改代码段 | 修改图形管线输出端，不碰游戏逻辑层 |
| 游戏更新适配 | 启动时校验版本 → API 拉取新偏移表 → 特征码回退搜索 | 三级适配策略，最大化兼容性 |

---

## 4. WPF UI 设计

### 4.1 技术选型

| 项目 | 选择 |
|------|------|
| MVVM 框架 | CommunityToolkit.Mvvm |
| UI 风格 | Fluent Design · Acrylic · Mica |
| 控件库 | MaterialDesignInXAML + HandyControl |
| 图表 | LiveCharts2 |
| 导航 | TabControl + 自定义过渡动画 |

### 4.2 四大页面

**Tab 1 — 🎮 一键启动：**
- 游戏横幅/Logo
- [▶ 一键启动增强模式] 按钮
- 当前配置预览条（预设名 · FPS 目标 · 已加载 Mod 数量）
- AI 推荐提示栏

**Tab 2 — ⚙️ 着色器预设：**
- 左侧预设列表（电影级 / 明亮 / 写实 / 动漫风 / 用户自定义）
- 右侧预设详情（启用的着色器清单 · 性能开销估算 · [套用] [编辑] [分享] 按钮）

**Tab 3 — 📊 性能仪表盘：**
- 实时数值卡片（FPS · GPU 温度 · CPU 温度 · VRAM 占用）
- FPS 曲线图（LiveCharts2 实时折线图）
- 着色器开销排行（每个着色器的 GPU 开销百分比）

**Tab 4 — 🤖 AI 推荐：**
- 当前硬件信息摘要
- ★ 推荐预设（带评分和预计 FPS 范围）
- 备选方案（排名第 2、3）
- 社区偏好数据展示

### 4.3 游戏内 Overlay

| 属性 | 值 |
|------|-----|
| 实现 | C++ Direct2D 渲染 |
| 位置 | 屏幕左上角 · 半透明 · 可拖拽 |
| 内容 | FPS · GPU 温度 · 当前预设名 · 热键提示 |
| 显隐热键 | Ctrl+Shift+O |
| 性能开销 | < 1ms（仅文字渲染 2-3 行） |

---

## 5. AI 智能推荐引擎

### 5.1 设计理念

着色器预设的性能表现是**可精确测量的确定性问题**，不需要机器学习模型来拟合。使用**加权评分算法**即可达到精准推荐效果。

### 5.2 三大数据输入

**输入 1 — 硬件指纹（本地自动采集）：**
- GPU 型号 / 显存 / 驱动版本
- CPU 型号 / 核心数
- 屏幕分辨率
- 系统内存

**输入 2 — 预设性能数据（本地硬编码 + API 在线更新）：**
- 每个预设在多基准硬件上的性能表现
- JSON 格式存储，首次打包在应用内，后续从 GitHub API 拉取更新

**输入 3 — 社区偏好（匿名上报 + 在线聚合）：**
- 用户选择预设后匿名上报 GPU + 分辨率 + 预设
- 服务端做简单聚合统计
- 需要用户同意才上报

### 5.3 评分算法

```
总分 = W1×性能分 + W2×画质分 + W3×社区分 + W4×稳定性分

W1 = 0.40（性能 — FPS 预估，≥60 为基础，>144 封顶）
W2 = 0.30（画质 — 特效丰富度，HDR/景深/光晕 加权）
W3 = 0.20（社区 — 同配置玩家的选择占比）
W4 = 0.10（稳定性 — 已知 Bug 数 / 驱动兼容度）
```

### 5.4 离线降级

网络不可用时：
- 社区偏好权重置零（W3 = 0）
- 权重重新分配：性能 55% / 画质 35% / 稳定性 10%
- 静默降级，不弹错误提示

---

## 6. 错误处理 & 容灾

### 6.1 五大关键故障场景

| 场景 | 应对策略 |
|------|----------|
| **注入失败** | 降级启动：游戏以纯净模式运行，弹明确提示；FPS Unlock 失败不影响 ReShade |
| **游戏崩溃** | 自动捕获崩溃信息 → 生成诊断报告 → 下次启动提示"安全模式" |
| **性能异常** | 仪表盘实时监控 → FPS 低于基准 50% 持续 5s → Overlay 弹出警告 |
| **游戏更新** | 启动对比版本哈希 → API 拉取新偏移表 → 特征码回退搜索 |
| **网络不可用** | 全离线可用，AI 退化为纯本地评分，静默降级 |

### 6.2 四项全局防护

- **结构化日志：** Serilog + 按日期分文件，保留 7 天
- **启动守卫：** 检测上次崩溃记录，决定是否进入安全模式
- **超时保护：** 注入超过 10 秒 → 取消注入，恢复游戏进程
- **配置回滚：** 修改前自动备份，崩溃后恢复上一版本

---

## 7. 测试策略

### 7.1 四层测试金字塔

```
        ┌─────┐
        │ E2E  │  真实游戏注入 · 完整启动链路（少量，人工 + 社区 Beta）
       ┌┴─────┴┐
       │ 集成   │  Mini DX 模拟器注入 · DX Hook 兼容 · IPC 通信
      ┌┴───────┴┐
      │ 契约    │  偏移表 Schema · 预设配置 Schema · API 响应 Schema
     ┌┴─────────┴┐
     │  单元测试  │  AI 评分算法 · 配置序列化 · 偏移查找器 · ViewModel
     └───────────┘
```

### 7.2 关键测试手段

**Mini DX 目标进程：** 创建一个极简 DirectX 程序模拟游戏环境，无需启动原神即可测试注入流程。编译 < 3s，启动 < 1s，适合 CI。

**偏移验证自动化：** CI 运行中拉取偏移表 JSON → Schema 校验 → 合法范围检查。

**CI 流水线（GitHub Actions）：** push → build → test（单元+集成）→ 偏移表校验 → 生成 Release 包。

### 7.3 明确不测试

| 不测试项 | 原因 |
|----------|------|
| 真实游戏注入（CI） | 需要完整游戏客户端，不可行 |
| 特定 GPU 画质表现 | 没有 GPU 矩阵 CI |
| Native 代码 100% 覆盖 | 底层 Hook 代码覆盖率不切实际 |

真实注入测试靠 Release 前人工冒烟测试 + 社区 Beta 频道反馈。

---

## 8. 约束与风险

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 游戏更新导致 Hook 失效 | 工具完全不可用 | 三级偏移适配策略 + 社区快速响应 |
| 杀毒软件误报 | 用户无法安装 | 代码签名 + 提交样本到 AV 厂商白名单 |
| 反作弊检测升级 | 账号风险 | 仅 Hook 渲染层，不做游戏逻辑修改 |
| 3DMigoto 社区停更 | Mod 加载功能失效 | 保持分模块解耦，不影响 ReShade/FPS |

---

## 9. 版本策略

- **主版本号：** 对齐游戏大版本（例如 v8.x 对应原神 4.x）
- **更新渠道：** GitHub Release API + winget 自动发布
- **增量更新：** 仅下载变化的着色器预设和偏移表（~KB 级），不解压全量包
