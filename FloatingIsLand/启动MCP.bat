@echo off
chcp 65001 >nul
cd /d "%~dp0"
set MCP_SERVER_SRC=%LOCALAPPDATA%\UnityMCP\server-9.3.0-fixed
echo === 启动 MCP for Unity 服务端（hub）：http://127.0.0.1:8081/mcp ===
echo.
echo 源：%MCP_SERVER_SRC%
echo.
echo 多个 Unity 工程共用这一个 hub，不要重复起；关掉本窗口 = 停服务。
echo Unity 内 MCP For Unity 窗口的 Start Local HTTP Server 按钮同样可用（已配好本地源覆盖），
echo 本脚本只是免开 Unity 的备用入口。
echo.
if not exist "%MCP_SERVER_SRC%\pyproject.toml" (
  echo [错误] 找不到本地服务端源：%MCP_SERVER_SRC%
  echo 原因：PyPI 上的 mcpforunityserver==9.3.0 依赖已失效，必须用打过补丁的本地源。
  echo 重建：下载 9.3.0 sdist 解压到上述目录，pyproject.toml 的 dependencies 里
  echo       加依赖 pydantic-settings（2.0 及以上），并把 mcp 的版本上限锁到 2 以下。
  echo       背景：fastmcp 2.14.1 的 wheel 漏声明 pydantic-settings；
  echo             mcp 2.0.0 把 McpError 改名 MCPError，fastmcp 2.14.1 仍用旧名。
  echo.
  pause
  exit /b 1
)
uvx --no-cache --refresh --from "%MCP_SERVER_SRC%" mcp-for-unity --transport http --http-url http://127.0.0.1:8081 --project-scoped-tools
echo.
echo [MCP 服务已退出]
pause
