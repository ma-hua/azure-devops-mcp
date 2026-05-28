## Plan: Optimize PR Review MCP Contract

**TL;DR:** Refactor the server from an ADO data relay into a typed review semantic layer — structured diff hunks with explicit line roles, file snapshots, typed errors, cursor-based truncation, and one aggregate bundle tool. Implement in 6 phases with Phase 1 as the foundation.

---

### Phase 1 — Typed Error + Truncation as First-Class Citizen
*Foundation — all other phases depend on this*

**1a. Unified error envelope** — modify `PrToolService.cs` + `McpModels.cs`
- Add `McpErrorDetail` record: `{ code, message, retryable, upstreamStatus, source, diagnosticsId }`
- Error codes: `ADO_HTTP_404`, `ADO_HTTP_401`, `ADO_HTTP_403`, `ADO_HTTP_429`, `ADO_HTTP_5xx`, `INVALID_INPUT`, `UNEXPECTED`
- Retryability rule: 429/5xx = true; 4xx = false; UNEXPECTED = true
- `diagnosticsId` = `Guid.NewGuid()` per call; `source` = method name string passed from catch site
- Refactor `Failure()` helper and `ExecuteAsync` exception classifier to produce typed codes

**1b. Strict empty vs error** — bug fix
- `list_pr_threads_summary`: ADO returns null threads → `threads: []`, not Failure
- `get_pr_thread_details`: same for null comments

**1c. Truncation envelope** — applied everywhere a `.Take()` currently happens silently
Fields: `isTruncated: bool`, `returnedCount: int`, `totalCount: int?`, `nextCursor: string?`

| Response | Cursor format |
|---|---|
| `list_pr_threads_summary` | `"offset:20"` |
| `search_pull_requests` | `"offset:10"` |
| `get_pr_summary` changedFiles | `"skip:200"` (ADO supports `$skip`) |
| `get_pr_file_diff` | `"hunk:3"` (re-fetch, skip first N complete hunks) |

New config: `AZDO_MAX_SNAPSHOT_LINES` (default 300, clamped 50–2000) in `Configuration/AdoOptions.cs`

---

### Phase 2 — Structured Diff with Line-Level Semantics
*Parallel with Phase 4; depends on Phase 1 patterns*

**2a. Refactor `UnifiedDiff.cs`** — return `DiffResult` instead of `string`
- `DiffResult { RawPatch: string, Hunks: IReadOnlyList<DiffHunk> }`
- `DiffHunk { OldStart, OldCount, NewStart, NewCount, Lines }`
- `DiffLine { Kind (Added|Removed|Context), Content (text only, no `+`/`-`/` ` prefix), OldLineNumber: int?, NewLineNumber: int? }`
- Track `oldLine`/`newLine` counters as the LCS walk progresses; assign to each emitted line

**2b. Surface commit IDs + iteration ID** — thread through from `AzureDevOpsClient.cs` `GetPullRequestAsync` (commit IDs already fetched, just not returned)

**2c. Comment anchors on each diff line** — enriched in `PrToolService.cs`
Each `DiffLine` gets a `commentAnchor { filePath, side: "left"|"right", line, iterationId, sourceCommitId, targetCommitId }`:
- `removed` lines → `side: "left"`, `line = oldLineNumber`
- `added` / `context` lines → `side: "right"`, `line = newLineNumber`

**2d. New `get_pr_file_diff` response:**
`rawPatch`, `hunks[]`, `iterationId`, `sourceCommitId`, `targetCommitId`, `isTruncated`, `returnedHunkCount`, `totalHunkCount`, `nextCursor`

Cursor param: new optional `cursor` input; response re-fetches full diff and skips first N complete hunks.

---

### Phase 3 — Before/After File Snapshots
*Depends on Phase 1; needs commit IDs from Phase 2b*

**New tool `get_pr_file_content`** — added to `PrToolService.cs`
- Params: `repository`, `pullRequestId`, `path`, `side` (`"before"|"after"|"both"`), optional `startLine`, optional `lineCount`
- Fetches via `TryGetFileContentAtCommitAsync` using target commit (before) / source commit (after)
- Response:
  ```json
  {
    "path": "",
    "iterationId": 0,
    "before": { "commitId": "", "content": "", "totalLineCount": 0, "isTruncated": false, "returnedLineCount": 0, "nextCursor": null },
    "after":  { "commitId": "", "content": "", "totalLineCount": 0, "isTruncated": false, "returnedLineCount": 0, "nextCursor": null }
  }
  ```
- Cursor = `"line:{N}"` (next `startLine`); pagination is local (split on `\n`, slice)

---

### Phase 4 — Enriched get_pr_summary
*Parallel with Phase 2*

Surface currently-discarded ADO data:
- PR level: `isDraft`, `sourceBranch` (strip `refs/heads/`), `targetBranch`, `iterationId`, `sourceCommitId`, `targetCommitId`
- Per changed file: `changeType: "add"|"edit"|"delete"|"rename"`, `originalPath: string?` (renames only)
- Add truncation envelope to changed files list
- Thread summaries: surface `lineNumber: int?`, `side: "left"|"right"|null` from `thread.threadContext.rightFileStart/leftFileStart`

---

### Phase 5 — `get_pr_review_bundle` (Aggregate)
*Depends on Phases 1 + 4*

Single-call aggregate: PR metadata + changed files + thread anchors. Internally fans out 3 ADO calls in parallel via `Task.WhenAll`.

Response combines:
- `pr { id, title, status, isDraft, sourceBranch, targetBranch, iterationId, sourceCommitId, targetCommitId }`
- `changedFiles [{ path, changeType, originalPath }]` + truncation envelope
- `threads [{ threadId, status, filePath, lineNumber, side, commentCount, preview }]` + truncation envelope

---

### Phase 6 — Capability Discovery
*Independent; implement last*

New static tool `get_capabilities` — no params, returns:
```json
{
  "version": "2.0",
  "features": ["structuredDiff", "fileSnapshots", "pagination", "threadAnchors", "reviewBundle"]
}
```

---

### Relevant Files

- `Protocol/McpModels.cs` — add `McpErrorDetail`, truncation mixin records
- `Tools/PrToolService.cs` — refactor `Failure()`/`Success()`, all tool responses, new tools in Phases 3/5/6
- `Ado/AzureDevOpsClient.cs` — thread commit IDs through; support `$skip` on iteration changes
- `Utils/UnifiedDiff.cs` — refactor to return `DiffResult` with structured hunks
- `Configuration/AdoOptions.cs` — add `MaxSnapshotLines`

### Implementation Order

**Phase 1** (foundation) → **Phase 2 + Phase 4** (parallel) → **Phase 3** → **Phase 5** → **Phase 6**

---

### Verification

1. Mixed add/delete hunk: client reads only `kind="added"` lines — no removed code bleeds through
2. Large diff (>MaxDiffChars): `isTruncated=true`, `nextCursor="hunk:3"` → second call returns remaining hunks
3. ADO upstream error: response shape = `{ code: "ADO_HTTP_404", retryable: false, upstreamStatus: 404, diagnosticsId: "..." }`
4. Create comment from anchor: `filePath/side/line/iterationId` round-trips into `create_pr_comment` correctly
5. Zero-thread PR: `threads: []`, `isTruncated: false` — no Failure
6. Renamed file: `changeType: "rename"`, `originalPath` present in summary and bundle
7. Old consumer regression: `rawPatch` still present; `diff` field alias or both field names kept

### Decisions

- `rawPatch` is kept alongside `hunks[]` for backward compatibility; old `diff` field name is aliased or both coexist
- Cursor pagination is re-fetch + skip (no server-side stateful session); acceptable for review workloads
- `isBinary` flag: not available from iteration change entries; omitted from Phase 5 (can be added later via a separate items API call)
- ADO `$skip` for iteration changes: needs runtime verification; if unsupported, `nextCursor` will be null even when `isTruncated=true` (document in response)
