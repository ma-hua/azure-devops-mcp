---
applyTo: "AzureMcp.Server/Ado/**"
---

# Azure DevOps Client Guidelines

## Auth

All requests use HTTP Basic auth: username is empty string, password is `AZDO_PAT`.  
Do **not** add other auth schemes without updating `AdoOptions` and `AzureDevOpsClient`.

## API version

Default api-version is `7.1-preview`, set in `AdoOptions.ApiVersion`.  
Append `?api-version={options.ApiVersion}` to every REST call unless the endpoint requires a different version.

## Error body truncation

Call `TruncateErrorBody(body)` on all non-success response bodies before throwing or returning.  
Cap is 500 characters. This prevents large HTML 404 pages from flooding LLM context.

## Changed files — correct endpoint

To retrieve changed files for a PR:
1. `GET /pullRequests/{id}/iterations` — take the last element's `id`
2. `GET /pullRequests/{id}/iterations/{iterationId}/changes` — read `changeEntries` array

> The route `/pullRequests/{id}/changes` does **not** exist in ADO and returns 404.  
> The JSON property is `changeEntries`, not `changes`.

## Comment anchoring

`create_pr_comment` posts a thread with `threadContext`:
```json
{
  "filePath": "/{path}",
  "rightFileStart": { "line": N, "lineOffset": 1 },
  "rightFileEnd":   { "line": N, "lineOffset": 2 }
}
```
- `filePath` must start with `/`; normalize before sending.
- Offset range must be non-zero-width; ADO silently drops zero-width selections.
