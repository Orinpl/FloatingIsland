#!/bin/bash
# 用 curl 与 MCP for Unity hub (Streamable HTTP) 通信的最小客户端。
# 用法:
#   unity_mcp.sh tools/list
#   unity_mcp.sh tools/call '{"name":"read_console","arguments":{"action":"get","types":["error"],"count":10}}'
#   unity_mcp.sh resources/read '{"uri":"mcpforunity://instances"}'
# 每次调用独立会话：initialize → initialized → 目标请求，输出 SSE data 里的 JSON。
set -u
URL="http://127.0.0.1:8081/mcp"
METHOD="$1"
PARAMS="${2:-{\}}"

HDR=$(mktemp)
curl -s -m 20 -X POST "$URL" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -D "$HDR" -o /dev/null \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"claude-cli","version":"1.0"}}}'
SID=$(grep -i '^mcp-session-id:' "$HDR" | tr -d '\r' | awk '{print $2}')
rm -f "$HDR"
[ -z "$SID" ] && { echo '{"error":"no session id from initialize"}'; exit 1; }

curl -s -m 10 -X POST "$URL" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -H "mcp-session-id: $SID" -o /dev/null \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized"}'

REQ=$(printf '{"jsonrpc":"2.0","id":2,"method":"%s","params":%s}' "$METHOD" "$PARAMS")
# 业务调用超时放宽（menu 执行/资产导入可能要等）
curl -s -m 600 -X POST "$URL" \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -H "mcp-session-id: $SID" \
  -d "$REQ" | sed -n 's/^data: //p'
