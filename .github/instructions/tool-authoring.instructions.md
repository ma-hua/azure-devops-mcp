---
applyTo: "AzureMcp.Server/Tools/**"
---

# Tool Authoring Guidelines

## Tool definition shape

Each tool is declared in `PrToolService.GetToolDefinitions()` as an anonymous object with:
- `name` — snake_case matching the MCP tool name
- `description` — concise single-sentence description for the model
- `inputSchema` — JSON Schema object (`type`, `required[]`, `properties`)

## Execution routing

`PrToolService.ExecuteAsync` routes by tool name via a `switch` expression.  
Every arm calls a private `Async` method and returns `object`.

## Response helpers

Use the private helpers already in `PrToolService`:
- `Success(content, structuredContent)` — wraps a successful result
- `Failure(message, structuredContent)` — sets `isError = true`

## Error handling

The public `ExecuteAsync` catch blocks handle:
- `HttpRequestException` → formatted HTTP status failure
- `InvalidOperationException` → message forwarded as failure
- All others → generic unexpected error failure

Do **not** add additional top-level try/catch in individual tool methods; let exceptions propagate to `ExecuteAsync`.

## Context budget

- Avoid returning raw large lists; apply `_options.MaxXxx` bounds before returning.
- Truncate free-text fields (thread comments, error bodies) before including in responses.
