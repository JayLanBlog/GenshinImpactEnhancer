# 3DMigoto + ReShade 共存注入 — 设计文档

> 日期：2026-05-28
> 方案：A（配置驱动）

## 目标

在 GIEnhancer 已有 ReShade 注入的基础上，实现 3DMigoto v1.4.9 的同时注入，确保：
1. 3DMigoto 不被米哈游反作弊（rtlbase.dll）识别
2. ReShade 与 3DMigoto 注入顺序不冲突
3. 热键冲突自动检测与解决

## 背景

- 游戏目录 `D:\TestGame\HoYoPlay\games\Genshin Impact game` 已存在 rtlbase.dll
- AntiCheat.cpp 检测到 rtlbase 时跳过 PEB 摘除（dllmain.cpp 第 50 行）
- ReShade 靠 dxgi.dll 代理 + d3dx.ini 自身反检测存活
- 3DMigoto 靠 d3d11.dll 代理，同样需要配置级反检测

## 设计决策

### 1. 反检测：配置驱动（不改内存）

3DMigoto d3dx.ini 已有的反检测配置：
```ini
[System]
load_library_redirect = 0     ; 禁用 LoadLibrary 重定向
check_foreground_window = 0   ; 不检查前台窗口
```

GIEnhancer 部署时自动补全/验证这些配置项。

### 2. 注入顺序：无需处理

- dxgi.dll (ReShade) Hook DXGI 层
- d3d11.dll (3DMigoto) Hook D3D11 层
- 两者 Hook 不同接口，天然兼容
- DXGI 先于 D3D11 加载，顺序正确

### 3. 热键冲突：扩展检测范围

当前 d3dx.ini 没有 [Hunting] 区段（hunting=0），但存在全局热键：
- KeyToggleMods = F3
- KeyReloadMods = F10

需将冲突检测从仅 [Hunting] 扩展到全局热键区段。

## 改动清单

| 文件 | 改动 |
|------|------|
| MigotoConfigReader.cs | DeployToGameDir 增加配置补全；HuntingHotkeyNames 扩展为全局热键 |
| MigotoConfig.cpp | Load 函数扩展读取全局热键 |
| Overlay.cpp | 增加 3DMigoto 反检测配置状态显示 |

## 测试验证

1. 启动 GIEnhancer → 一键启动
2. 验证日志：DLL 部署、配置补全、无热键冲突、注入成功
3. 游戏内：Home 打开 ReShade、F3 切换 3DMigoto Mod、F11 打开 Overlay
