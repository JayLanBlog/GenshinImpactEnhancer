@echo off
chcp 65001 >nul
title GIEnhancer - 自动编译部署
cd /d E:\AI\hook

echo ============================================
echo   GIEnhancer - 自动编译 &amp; 部署
echo ============================================
echo.

:: ── Step 0: 修复 dotnet 环境 ──
echo [0/5] 检查 dotnet 环境...
set DOTNET="C:\Program Files\dotnet\dotnet.exe"
%DOTNET% --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo   ⚠️ dotnet 异常，尝试修复...
    set PATH=%PATH%;C:\Program Files\dotnet
    call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=amd64 >nul 2>&1
    %DOTNET% --version >nul 2>&1
    if %ERRORLEVEL% NEQ 0 (
        echo   ❌ dotnet 修复失败，请重启电脑后重试
        pause
        exit /b 1
    )
)
for /f "tokens=*" %%i in ('%DOTNET% --version') do set DOTNET_VER=%%i
echo   ✅ dotnet %DOTNET_VER%

:: ── Step 1: 编译 Native C++ DLL ──
echo.
echo [1/5] 编译 Native C++ DLL...
set MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
%MSBUILD% src\Native\GIEnhancer.Native.Injector\GIEnhancer.Native.Injector.vcxproj /p:Configuration=Release /p:Platform=x64 /t:Rebuild /v:minimal
if %ERRORLEVEL% NEQ 0 (
    echo   ❌ Native DLL 编译失败
    pause & exit /b 1
)
copy /Y "src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll" "dist\" >nul
echo   ✅ Native DLL → dist\

:: ── Step 2: 清理旧构建 ──
echo.
echo [2/5] 清理旧构建...
rmdir /s /q "src\UI\GIEnhancer.Launcher\bin" 2>nul
rmdir /s /q "src\UI\GIEnhancer.Launcher\obj" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core\bin" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core\obj" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core.Reshade\bin" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core.Reshade\obj" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core.Migoto\bin" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core.Migoto\obj" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core.FpsUnlock\bin" 2>nul
rmdir /s /q "src\Core\GIEnhancer.Core.FpsUnlock\obj" 2>nul
rmdir /s /q "src\Application\GIEnhancer.Services\bin" 2>nul
rmdir /s /q "src\Application\GIEnhancer.Services\obj" 2>nul
rmdir /s /q "src\Infrastructure\GIEnhancer.DeviceIdentifier\bin" 2>nul
rmdir /s /q "src\Infrastructure\GIEnhancer.DeviceIdentifier\obj" 2>nul
rmdir /s /q "src\Infrastructure\GIEnhancer.Utils\bin" 2>nul
rmdir /s /q "src\Infrastructure\GIEnhancer.Utils\obj" 2>nul
rmdir /s /q "src\Application\GIEnhancer.Update\bin" 2>nul
rmdir /s /q "src\Application\GIEnhancer.Update\obj" 2>nul
echo   ✅ 清理完成

:: ── Step 3: 恢复 NuGet 包 ──
echo.
echo [3/5] 恢复 NuGet 包...
%DOTNET% restore src\UI\GIEnhancer.Launcher\GIEnhancer.Launcher.csproj --force
if %ERRORLEVEL% NEQ 0 (
    echo   ❌ restore 失败
    pause & exit /b 1
)
echo   ✅ NuGet 恢复完成

:: ── Step 4: 编译 Launcher ──
echo.
echo [4/5] 编译 Launcher (新版: CREATE_SUSPENDED + 3模块注入)...
%DOTNET% build src\UI\GIEnhancer.Launcher\GIEnhancer.Launcher.csproj -c Release --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo   ❌ 编译失败
    pause & exit /b 1
)
echo   ✅ Launcher 编译完成

:: ── Step 5: 复制到 dist ──
echo.
echo [5/5] 复制文件到 dist\ ...
set SRC=src\UI\GIEnhancer.Launcher\bin\Release\net6.0-windows
copy /Y "%SRC%\GIEnhancer.Launcher.exe" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Launcher.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Launcher.runtimeconfig.json" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Launcher.deps.json" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Launcher.pdb" "dist\" >nul 2>nul
copy /Y "%SRC%\GIEnhancer.Core.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Core.pdb" "dist\" >nul 2>nul
copy /Y "%SRC%\GIEnhancer.Core.Reshade.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Core.Migoto.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Core.FpsUnlock.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Services.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.DeviceIdentifier.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Utils.dll" "dist\" >nul
copy /Y "%SRC%\GIEnhancer.Update.dll" "dist\" >nul
copy /Y "%SRC%\Serilog*.dll" "dist\" >nul 2>nul
copy /Y "%SRC%\System.Management.dll" "dist\" >nul 2>nul
echo   ✅ 所有文件已复制到 dist\

echo.
echo ============================================
echo   ✅ 编译完成！
echo ============================================
echo.
echo   dist\ 目录:
dir /b dist\*.exe dist\*.dll 2>nul
echo.
echo   运行: dist\启动增强.bat (三注入模式)
echo     或: dist\GIEnhancer.Launcher.exe (启动器GUI)
echo.
pause
