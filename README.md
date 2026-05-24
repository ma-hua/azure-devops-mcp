# Azure DevOps PR Review MCP (C# MVP)

This repository contains a minimal Azure DevOps MCP server focused on PR review workflows with lower context usage.

## Documentation

- Copilot context (architecture, technology, design, features): docs/COPILOT_CONTEXT.md

## Current MVP Scope

- Resolve PR target from URL, PR ID, repo+ID, or natural language hint
- Fuzzy repository lookup in a target org/project
- Pull request search by repository and status
- Lightweight PR summary retrieval
- Targeted single-file diff generation with output truncation
- PR thread summary retrieval (with preview truncation)
- PR single-thread detail retrieval
- Create PR review comment (file + line)
- Vote on PR as current authenticated user

## Why this is context-efficient

- Tools are intentionally small and task-focused
- PR summary returns capped description and changed file list only
- Thread list returns lightweight preview instead of full thread history
- Bounded defaults and max limits for candidates, threads, and changes

## Prerequisites

- .NET SDK 8+
- Azure DevOps PAT with repo read/write permissions

## Environment Variables

Set these before running the server:

- AZDO_PAT (required)
- AZDO_DEFAULT_ORG (recommended)
- AZDO_DEFAULT_PROJECT (recommended)
- AZDO_API_VERSION (optional, default 7.1)
- AZDO_MAX_CANDIDATES (optional, default 10)
- AZDO_MAX_THREADS (optional, default 20)
- AZDO_MAX_CHANGES (optional, default 200)
- AZDO_MAX_REPOSITORY_SCAN (optional, default 30)
- AZDO_MAX_DIFF_CHARS (optional, default 8000)

## Run

```powershell
dotnet run --project AzureMcp.Server/AzureMcp.Server.csproj
```

## Build

```powershell
dotnet build AzureMcp.slnx
```

## MCP Tools (MVP)

- fuzzy_find_repository
- resolve_pr_input
- search_pull_requests
- get_pr_summary
- get_pr_file_diff
- list_pr_threads_summary
- get_pr_thread_details
- create_pr_comment
- vote_pr

## Next Implementation Steps

- Add integration tests against Azure DevOps sandbox
