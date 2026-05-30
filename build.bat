@echo off
echo ============================================
echo  GIEnhancer Build Script
echo ============================================
echo.

set DOTNET=C:\Program Files\dotnet\dotnet.exe
set ROOT=E:\AI\hook
set DIST=%ROOT%\dist

echo [1/4] Native C++ DLL...
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" "%ROOT%\src\Native\GIEnhancer.Native.Injector\GIEnhancer.Native.Injector.vcxproj" /p:Configuration=Release /p:Platform=x64 /t:Build /v:minimal
if %ERRORLEVEL% NEQ 0 (echo ERROR: Native build failed & pause & exit /b 1)
copy /Y "%ROOT%\src\Native\GIEnhancer.Native.Injector\x64\Release\GIEnhancer.Native.Injector.dll" "%DIST%\" >nul
echo   Native DLL OK

echo [2/4] Restoring NuGet packages...
%DOTNET% restore "%ROOT%\src\UI\GIEnhancer.Launcher\GIEnhancer.Launcher.csproj" --force
if %ERRORLEVEL% NEQ 0 (echo ERROR: Restore failed & pause & exit /b 1)

echo [3/4] Building Launcher...
rmdir /s /q "%ROOT%\src\UI\GIEnhancer.Launcher\bin" 2>nul
rmdir /s /q "%ROOT%\src\UI\GIEnhancer.Launcher\obj" 2>nul
%DOTNET% build "%ROOT%\src\UI\GIEnhancer.Launcher\GIEnhancer.Launcher.csproj" -c Release --no-restore
if %ERRORLEVEL% NEQ 0 (echo ERROR: Build failed & pause & exit /b 1)

echo [4/4] Copying to dist...
set SRC=%ROOT%\src\UI\GIEnhancer.Launcher\bin\Release\net6.0-windows
copy /Y "%SRC%\GIEnhancer.Launcher.exe" "%DIST%\" >nul
copy /Y "%SRC%\GIEnhancer.Launcher.dll" "%DIST%\" >nul
copy /Y "%SRC%\*.dll" "%DIST%\" >nul
copy /Y "%SRC%\*.json" "%DIST%\" >nul
echo   Files copied to %DIST%

echo.
echo ============ BUILD COMPLETE ============
echo Dist files:
dir /b "%DIST%\*.dll" "%DIST%\*.exe"
echo.
pause
