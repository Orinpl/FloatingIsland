@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === 冒烟验证：脱离 Unity 编译读表层 + Tables.g.cs，并真实加载全部 JSON ===
echo.
dotnet run --project Tools\ConfigVerify
echo.
pause
