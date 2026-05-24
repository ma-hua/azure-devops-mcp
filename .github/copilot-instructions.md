# Azure DevOps PR Review MCP — Copilot Instructions

This repository is a **Model Context Protocol (MCP) server** written in C# / .NET 8.
Its sole purpose is to provide a low-context, high-precision toolset for Copilot-driven Azure DevOps pull request review conversations.

---

## Architecture

```
Copilot Chat
    └── MCP JSON-RPC over stdio
            └── McpServer                (Protocol/McpServer.cs)
                    └── PrToolService    (Tools/PrToolService.cs)
                            ├── InputResolver       (Resolution/InputResolver.cs)
                            ├── AzureDevOpsClient   (Ado/AzureDevOpsClient.cs)
                            └── UnifiedDiff         (Utils/UnifiedDiff.cs)
```

### Runtime layers

| Layer | File | Responsibility |
|---|---|---|
| Protocol | `Protocol/McpServer.cs` | MCP JSON-RPC lifecycle over stdio (`initialize`, `tools/list`, `tools/call`) |
| Transport | `Protocol/StdioMessageIO.cs` | stdio framing; tolerates both CRLF and LF header separators |
| Tool orchestration | `Tools/PrToolService.cs` | Tool schemas, routing, and low-context response shaping |
| Resolution | `Resolution/InputResolver.cs` | Resolves user input to an exact PR target |
| ADO adapter | `Ado/AzureDevOpsClient.cs` | Azure DevOps REST calls, PAT auth, error mapping |
| Diff utility | `Utils/UnifiedDiff.cs` | Bounded unified diff for a single file |
| Configuration | `Configuration/AdoOptions.cs` | Typed env-var options with range-clamped defaults |

---

## Technology Stack

- **Language**: C#  
- **Runtime**: .NET 8  
- **Transport**: MCP JSON-RPC over stdio  
- **Auth**: Azure DevOps PAT (Basic auth, PAT as password)  
- **API**: Azure DevOps REST API (default api-version `7.1-preview`)  

---

## Design Principles

### Low-context by default
- Return summary first, details on demand.
- Use bounded lists for candidates, threads, and changed files.
- Truncate diff payloads; API error bodies capped at 500 chars (`TruncateErrorBody`) to prevent large HTML or JSON error pages flooding LLM context.

### Resolve first, execute second
Every tool that needs a PR target follows: **resolve → verify uniqueness → execute**.  
- Read tools may return candidates when ambiguous.  
- Write tools must block with actionable guidance when ambiguous.

### Narrow and composable tools
Each tool performs exactly one action. This reduces schema bloat and improves model behavior.

### Structured output contract
Every tool response includes:
- `content` — brief human-readable text  
- `structuredContent` — machine-consumable payload  
- `isError` — explicit boolean  

---

## Tool Reference

### Discovery / input

| Tool | Required params | Description |
|---|---|---|
| `resolve_pr_input` | *(none required)* | Resolves PR from URL, ID, repo+ID, or natural-language hint |
| `fuzzy_find_repository` | `repositoryQuery` | Fuzzy-matches repository names inside org/project |
| `search_pull_requests` | `repository` | Lists PRs by repository and status (`active`/`completed`/`abandoned`) |

**`resolve_pr_input` URL formats supported:**
- `https://dev.azure.com/{org}/{project}/_git/{repo}/pullrequest/{id}`
- `https://{org}.visualstudio.com/{project}/_git/{repo}/pullrequest/{id}` (legacy domain)
- PR ID only (bounded cross-repo scan up to `AZDO_MAX_REPOSITORY_SCAN`)
- Natural-language string containing a PR number

### Read

| Tool | Required params | Description |
|---|---|---|
| `get_pr_summary` | `repository`, `pullRequestId` | Lightweight PR metadata + bounded changed file list |
| `get_pr_file_diff` | `repository`, `pullRequestId`, `path` | Single-file unified diff (`contextLines` 0–5, output bounded by `AZDO_MAX_DIFF_CHARS`) |
| `list_pr_threads_summary` | `repository`, `pullRequestId` | Thread list with preview truncation; `status` = `active` or `all` |
| `get_pr_thread_details` | `repository`, `pullRequestId`, `threadId` | Full details for one thread |

**`get_pr_summary` changed-file resolution:**
1. `GET /pullRequests/{id}/iterations` — select the last iteration ID  
2. `GET /pullRequests/{id}/iterations/{iterationId}/changes` — read `changeEntries` property  
> The route `/pullRequests/{id}/changes` does **not** exist in ADO and returns 404.

### Write

| Tool | Required params | Description |
|---|---|---|
| `create_pr_comment` | `repository`, `pullRequestId`, `path`, `line`, `content` | Inline comment on a specific file and line |
| `vote_pr` | `repository`, `pullRequestId`, `vote` | Vote on a PR |

**`create_pr_comment` invariants:**
- `filePath` is normalized to always start with `/` (ADO rejects paths without a leading slash).
- `threadContext` uses `rightFileStart={line, offset:1}` and `rightFileEnd={line, offset:2}`. A non-zero-width selection is required; zero-width selections are silently dropped by ADO.

**`vote_pr` valid values:** `approve`, `approve_with_suggestions`, `wait_for_author`, `reject`, `reset`

---

## Typical Copilot Review Flow

```
1. resolve_pr_input
2. get_pr_summary
3. list_pr_threads_summary
4. get_pr_file_diff          ← only for risky files, one at a time
5. get_pr_thread_details     ← only for selected threads, one at a time
6. create_pr_comment / vote_pr
```

**Low-context rules:**
- Never pull full details for all files at once.
- Pull one file diff at a time.
- Pull one thread detail at a time.

---

## Configuration (Environment Variables)

| Variable | Default | Notes |
|---|---|---|
| `AZDO_PAT` | *(required)* | Azure DevOps Personal Access Token |
| `AZDO_DEFAULT_ORG` | *(recommended)* | Default organization |
| `AZDO_DEFAULT_PROJECT` | *(recommended)* | Default project |
| `AZDO_API_VERSION` | `7.1-preview` | ADO REST API version |
| `AZDO_MAX_CANDIDATES` | `10` | Max PR candidates returned (clamped 1–50) |
| `AZDO_MAX_THREADS` | `20` | Max threads returned (clamped 1–100) |
| `AZDO_MAX_CHANGES` | `200` | Max changed files returned (clamped 10–2000) |
| `AZDO_MAX_REPOSITORY_SCAN` | `30` | Max repos scanned for ID-only resolution (clamped 5–200) |
| `AZDO_MAX_DIFF_CHARS` | `8000` | Max diff output characters (clamped 1000–50000) |

Typed in: `AzureMcp.Server/Configuration/AdoOptions.cs`

---

## Key Source Files

| File | Purpose |
|---|---|
| `AzureMcp.Server/Program.cs` | Entry point; constructs and runs `McpServer` |
| `AzureMcp.Server/Protocol/McpServer.cs` | MCP protocol handler |
| `AzureMcp.Server/Protocol/StdioMessageIO.cs` | stdio framing (CRLF/LF tolerant) |
| `AzureMcp.Server/Tools/PrToolService.cs` | Tool definitions and execution routing |
| `AzureMcp.Server/Resolution/InputResolver.cs` | PR input resolution (URL, ID, fuzzy) |
| `AzureMcp.Server/Ado/AzureDevOpsClient.cs` | Azure DevOps REST adapter |
| `AzureMcp.Server/Utils/UnifiedDiff.cs` | Bounded unified diff builder |
| `AzureMcp.Server/Configuration/AdoOptions.cs` | Typed env-var configuration |

---

## Non-Goals (MVP)

- Full Azure DevOps domain coverage
- Bulk PR review with full-repo diff hydration
- Work item and pipeline deep integration
- OAuth / Entra ID auth (planned)

---

## Known Design Trade-offs

1. **ID-only resolution** scans a bounded repository subset — may miss PRs if repo count exceeds `AZDO_MAX_REPOSITORY_SCAN`.
2. **Unified diff** is generated from source and target commit file snapshots — not yet enriched with server-side hunk metadata.
3. **Natural-language parsing** is intentionally lightweight — benefits from explicit repository hints.
