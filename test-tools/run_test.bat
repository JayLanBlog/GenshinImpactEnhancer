@echo off
echo Genshin Impact Enhancer - Injection Test
echo.
echo Starting game with ReShade + Enhancer...
e:\AI\hook\test-tools\HomeTestTool\bin\Debug\net6.0\HomeTestTool.exe
if exist "e:\AI\hook\test-tools\test_result.log" (
    echo.
    echo === RESULT ===
    type "e:\AI\hook\test-tools\test_result.log"
) else (
    echo No result log found
)
pause
