@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === 转表：Tables\*.xlsx -^> Assets\Resources\Tables\*.json + Assets\Script\Config\Generated\Tables.g.cs ===
echo.
dotnet run --project Tools\TableTool -- convert
echo.
if errorlevel 1 (
  echo [失败] 转表未通过，上面是错误坐标 文件!Sheet!单元格，改完 Excel 再跑一次。
) else (
  echo [成功] 回到 Unity 窗口等它导入完，即可用 Tables.xxx 访问新数据。
)
pause
