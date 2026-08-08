@echo off
chcp 65001 >nul
cd /d "%~dp0"
set MCP_SERVER_SRC=%LOCALAPPDATA%\UnityMCP\server-9.3.0-fixed
echo === 启动 MCP for Unity 服务端（hub）：http://localhost:8080/mcp ===
echo.
echo 源：%MCP_SERVER_SRC%
echo.
echo 说明：多个 Unity 工程共用这一个 hub，不要重复起；关掉本窗口 = 停服务。
echo       如果 Unity 里已经在 MCP For Unity ^> Advanced Settings ^> Server Path/URL
echo       填了同一个路径，那边的 Start Local HTTP Server 按钮也能直接用，本脚本只是免开 Unity 的备用入口。
echo.
if not exist "%MCP_SERVER_SRC%\pyproject.toml" (
  echo [错误] 找不到本地服务端源：%MCP_SERVER_SRC%
  echo        PyPI 上的 mcpforunityserver==9.3.0 依赖已失效，必须用这份打过补丁的本地源。
  echo        重建方法见下方说明，或让 AI 重新生成。
  echo.
  echo        补丁内容：pyproject.toml 的 dependencies 加 "pydantic-settings^>=2.0"，
  echo        并把 "mcp^>=1.16.0" 改成 "mcp^>=1.16.0,^<2"。
  echo        原因：fastmcp 2.14.1 的 wheel 漏声明 pydantic-settings；
  echo              mcp 2.0.0 把 McpError 改名成 MCPError，fastmcp 2.14.1 仍用旧名。
  echo.
  pause
  exit /b 1
)
uvx --no-cache --refresh --from "%MCP_SERVER_SRC%" mcp-for-unity --transport http --http-url http://localhost:8080 --project-scoped-tools
echo.
echo [MCP 服务已退出]
pause
