@echo off
chcp 65001 >nul
title GIEnhancer 启动
echo ========================================
echo   GIEnhancer 游戏增强启动器
echo   稳定版: 3模块注入 (rtlbase+GIEnhancer+ReShade)
echo ========================================
echo.
echo 正在清理旧进程...
taskkill /f /im GenshinImpact.exe >nul 2>&1
taskkill /f /im GIEnhancer.Launcher.exe >nul 2>&1
taskkill /f /im HomeTestTool.exe >nul 2>&1
timeout /t 3 /nobreak >nul

echo 启动注入工具...
start "" /B "%~dp0..\test-tools\HomeTestTool\bin\Release\net6.0\HomeTestTool.exe"

echo.
echo ✅ 注入工具已启动
echo ========================================
echo  正在自动: 启动游戏 → 注入3模块 → 验证
echo  游戏启动后:
echo    Home = ReShade 面板
echo    F11  = GIEnhancer 状态
echo    F3   = 3DMigoto 切换
echo ========================================
echo.
pause
