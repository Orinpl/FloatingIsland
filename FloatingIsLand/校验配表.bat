@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === 只校验不落盘（提交前 / 改表后自查）===
echo.
dotnet run --project Tools\TableTool -- check
echo.
pause
