---
applyTo: "AzureMcp.Server/Protocol/**"
---

# MCP Protocol Guidelines

## Supported JSON-RPC methods

| Method | Handler |
|---|---|
| `initialize` | Returns server info and capabilities |
| `tools/list` | Returns `PrToolService.GetToolDefinitions()` |
| `tools/call` | Routes to `PrToolService.ExecuteAsync` |

## stdio framing

`StdioMessageIO` reads Content-Length headers then the JSON body.  
The parser tolerates both CRLF (`\r\n`) and LF (`\n`) header separators.  
Do **not** change to strict CRLF-only — this breaks the MCP handshake on some hosts.

## Startup errors

Fatal startup errors must be written to **stderr only**, never stdout.  
stdout is reserved for MCP JSON-RPC messages.  
See `Program.cs` catch block: `Console.Error.WriteLineAsync`.

## Server identity

The server registers as `azure-devops-pr-review` version `0.1.0` (set in `Program.cs`).
