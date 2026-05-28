using System.Text.Json;
using AzureMcp.Server.Ado;
using AzureMcp.Server.Configuration;
using AzureMcp.Server.Resolution;
using AzureMcp.Server.Utils;

namespace AzureMcp.Server.Tools;

public sealed class PrToolService
{
    private readonly AzureDevOpsClient _adoClient;
    private readonly AdoOptions _options;
    private readonly InputResolver _inputResolver;

    public PrToolService(AzureDevOpsClient adoClient, AdoOptions options)
    {
        _adoClient = adoClient;
        _options = options;
        _inputResolver = new InputResolver(_adoClient, _options);
    }

    public IReadOnlyList<object> GetToolDefinitions() => BuildToolDefinitions();

    private IReadOnlyList<object> BuildToolDefinitions()
    {
        return
        [
            new
            {
                name = "resolve_pr_input",
                description = "Resolve PR input from URL, ID, repo+ID, or natural language hint.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        input = new { type = "string" },
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" }
                    }
                }
            },
            new
            {
                name = "fuzzy_find_repository",
                description = "Fuzzy search repository name in the given org/project.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repositoryQuery" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repositoryQuery = new { type = "string" },
                        top = new { type = "integer", minimum = 1, maximum = 20 }
                    }
                }
            },
            new
            {
                name = "search_pull_requests",
                description = "Search pull requests by repository and status.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        status = new { type = "string", @enum = new[] { "active", "completed", "abandoned" } },
                        top = new { type = "integer", minimum = 1, maximum = 50 },
                        cursor = new { type = "string", description = "Pagination cursor from a previous response (e.g. 'offset:10')." }
                    }
                }
            },
            new
            {
                name = "get_pr_summary",
                description = "Get high-level pull request summary including changed files with change types.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" },
                        cursor = new { type = "string", description = "Pagination cursor for changed files (e.g. 'skip:200')." }
                    }
                }
            },
            new
            {
                name = "get_pr_file_diff",
                description = "Get a unified diff for a single file in a PR with structured hunk and line-level semantics.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId", "path" },
                    properties = new
                    {
                        input = new { type = "string" },
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" },
                        path = new { type = "string" },
                        contextLines = new { type = "integer", minimum = 0, maximum = 5 },
                        cursor = new { type = "string", description = "Pagination cursor for hunk-level pagination (e.g. 'hunk:3')." }
                    }
                }
            },
            new
            {
                name = "get_pr_file_content",
                description = "Get the before and/or after full file content for a changed file in a PR.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId", "path" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" },
                        path = new { type = "string" },
                        side = new { type = "string", @enum = new[] { "before", "after", "both" }, description = "Which version(s) to return. Defaults to 'both'." },
                        startLine = new { type = "integer", minimum = 1, description = "1-based first line to return. Defaults to 1." },
                        lineCount = new { type = "integer", minimum = 1, description = "Maximum lines to return per side." },
                        cursor = new { type = "string", description = "Pagination cursor from a previous response (e.g. 'line:301')." }
                    }
                }
            },
            new
            {
                name = "list_pr_threads_summary",
                description = "Get PR thread summaries with file location, line anchor, and short preview.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" },
                        status = new { type = "string", @enum = new[] { "active", "all" } },
                        cursor = new { type = "string", description = "Pagination cursor from a previous response (e.g. 'offset:20')." }
                    }
                }
            },
            new
            {
                name = "get_pr_thread_details",
                description = "Get full details of one PR thread.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId", "threadId" },
                    properties = new
                    {
                        input = new { type = "string" },
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" },
                        threadId = new { type = "integer" }
                    }
                }
            },
            new
            {
                name = "create_pr_comment",
                description = "Create a PR review comment on a specific file and line.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId", "path", "line", "content" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" },
                        input = new { type = "string" },
                        path = new { type = "string" },
                        line = new { type = "integer" },
                        content = new { type = "string" }
                    }
                }
            },
            new
            {
                name = "vote_pr",
                description = "Vote on a pull request as the authenticated user.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId", "vote" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" },
                        input = new { type = "string" },
                        vote = new
                        {
                            type = "string",
                            @enum = new[] { "approve", "approve_with_suggestions", "wait_for_author", "reject", "reset" }
                        }
                    }
                }
            },
            new
            {
                name = "get_pr_review_bundle",
                description = "Get a single-call review bundle: PR metadata, changed files with change types, and thread anchors.",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "repository", "pullRequestId" },
                    properties = new
                    {
                        org = new { type = "string" },
                        project = new { type = "string" },
                        repository = new { type = "string" },
                        pullRequestId = new { type = "integer" }
                    }
                }
            },
            new
            {
                name = "get_capabilities",
                description = "Discover server version and supported feature flags.",
                inputSchema = new { type = "object", properties = new { } }
            }
        ];
    }

    public async Task<object> ExecuteAsync(string toolName, JsonElement args, CancellationToken cancellationToken)
    {
        try
        {
            return toolName switch
            {
                "resolve_pr_input" => await ResolvePrInputAsync(args, cancellationToken),
                "fuzzy_find_repository" => await FuzzyFindRepositoryAsync(args, cancellationToken),
                "search_pull_requests" => await SearchPullRequestsAsync(args, cancellationToken),
                "get_pr_summary" => await GetPrSummaryAsync(args, cancellationToken),
                "get_pr_file_diff" => await GetPrFileDiffAsync(args, cancellationToken),
                "get_pr_file_content" => await GetPrFileContentAsync(args, cancellationToken),
                "list_pr_threads_summary" => await ListThreadsSummaryAsync(args, cancellationToken),
                "get_pr_thread_details" => await GetThreadDetailsAsync(args, cancellationToken),
                "create_pr_comment" => await CreateCommentAsync(args, cancellationToken),
                "vote_pr" => await VotePrAsync(args, cancellationToken),
                "get_pr_review_bundle" => await GetPrReviewBundleAsync(args, cancellationToken),
                "get_capabilities" => GetCapabilities(),
                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 0;
            var (code, retryable) = statusCode switch
            {
                401 => ("ADO_HTTP_401", false),
                403 => ("ADO_HTTP_403", false),
                404 => ("ADO_HTTP_404", false),
                429 => ("ADO_HTTP_429", true),
                >= 500 => ($"ADO_HTTP_{statusCode}", true),
                _ => ("ADO_HTTP_ERROR", false)
            };
            var msg = statusCode > 0 ? $"ADO API request failed: HTTP {statusCode}." : "ADO API request failed.";
            return Failure(msg, code, retryable, statusCode > 0 ? statusCode : null);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message, "INVALID_INPUT");
        }
        catch (Exception)
        {
            return Failure("An unexpected error occurred.", "UNEXPECTED", retryable: true);
        }
    }

    private async Task<object> ResolvePrInputAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var input = GetString(args, "input") ?? string.Empty;
        var org = GetString(args, "org");
        var project = GetString(args, "project");
        var repository = GetString(args, "repository");
        var pullRequestId = GetInt(args, "pullRequestId");

        var resolved = await _inputResolver.ResolveAsync(input, org, project, repository, pullRequestId, cancellationToken);
        if (resolved.IsError)
            return Failure(resolved.Message, "INVALID_INPUT");

        if (resolved.IsResolved && resolved.Resolved.HasValue)
        {
            var target = resolved.Resolved.Value;
            return Success(resolved.Message, new
            {
                isResolved = true,
                resolved = new
                {
                    org = target.Organization,
                    project = target.Project,
                    repository = target.Repository,
                    pullRequestId = target.PullRequestId
                },
                candidates = Array.Empty<object>()
            });
        }

        return Success(resolved.Message, new
        {
            isResolved = false,
            resolved = (object?)null,
            candidates = resolved.Candidates.Select(c => new
            {
                org = c.Organization,
                project = c.Project,
                repository = c.Repository,
                pullRequestId = c.PullRequestId
            })
        });
    }

    private async Task<object> FuzzyFindRepositoryAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var org = GetString(args, "org") ?? _options.DefaultOrganization;
        var project = GetString(args, "project") ?? _options.DefaultProject;
        var query = GetRequiredString(args, "repositoryQuery");
        var top = Math.Clamp(GetInt(args, "top") ?? _options.MaxCandidates, 1, 20);

        EnsureScope(org, project);

        var repos = await _adoClient.ListRepositoriesAsync(org, project, cancellationToken);
        var ranked = repos
            .Select(r => new
            {
                repository = r,
                score = FuzzyMatch.Score(query, r.Name)
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.repository.Name, StringComparer.OrdinalIgnoreCase)
            .Take(top)
            .ToList();

        return Success(
            $"Found {ranked.Count} repository candidates.",
            new
            {
                org,
                project,
                candidates = ranked.Select(x => new { id = x.repository.Id, name = x.repository.Name, score = x.score })
            });
    }

    private async Task<object> SearchPullRequestsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var org = GetString(args, "org") ?? _options.DefaultOrganization;
        var project = GetString(args, "project") ?? _options.DefaultProject;
        var repository = GetRequiredString(args, "repository");
        var status = GetString(args, "status") ?? "active";
        var top = Math.Clamp(GetInt(args, "top") ?? _options.MaxCandidates, 1, 50);
        var skip = ParseCursorOffset(GetString(args, "cursor"));

        EnsureScope(org, project);

        var prs = await _adoClient.SearchPullRequestsAsync(org, project, repository, status, top, skip, cancellationToken);
        var isTruncated = prs.Count == top;
        var nextCursor = isTruncated ? $"offset:{skip + top}" : null;

        var items = prs.Select(pr => new
        {
            id = pr.Id,
            title = pr.Title,
            author = pr.Author,
            pr.Status,
            createdAt = pr.CreatedAt,
            repository = pr.Repository
        });

        return Success($"Found {prs.Count} pull requests in {repository}.",
            new { org, project, repository, items, returnedCount = prs.Count, isTruncated, nextCursor });
    }

    private async Task<object> GetPrSummaryAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var org = target.Organization;
        var project = target.Project;
        var repository = target.Repository;
        var pullRequestId = target.PullRequestId;
        var fileSkip = ParseCursorSkip(GetString(args, "cursor"));

        EnsureScope(org, project);

        // Fetch PR metadata + iteration ID in parallel
        var prTask = _adoClient.GetPullRequestAsync(org, project, repository, pullRequestId, cancellationToken);
        var iterIdTask = _adoClient.GetLatestIterationIdAsync(org, project, repository, pullRequestId, cancellationToken);
        await Task.WhenAll(prTask, iterIdTask);

        var pr = prTask.Result;
        var iterationId = iterIdTask.Result;

        var fileEntries = new List<(string Path, string ChangeType, string? OriginalPath)>();
        string? changedFilesNote = null;

        if (iterationId > 0)
        {
            // Use the iterations/changes endpoint (the /pullRequests/{id}/changes route does not exist and returns 404)
            var changes = await _adoClient.GetPullRequestIterationChangesAsync(
                org, project, repository, pullRequestId, iterationId, fileSkip, cancellationToken);
            fileEntries = ExtractChangedFileEntries(changes).ToList();
        }
        else
        {
            changedFilesNote = "No iterations found; changed file list is unavailable.";
        }

        var isTruncated = fileEntries.Count == _options.MaxChanges;
        var nextCursor = isTruncated ? $"skip:{fileSkip + _options.MaxChanges}" : null;

        var sourceRef = pr.GetProperty("sourceRefName").GetString() ?? string.Empty;
        var targetRef = pr.GetProperty("targetRefName").GetString() ?? string.Empty;
        var sourceCommitId = pr.TryGetProperty("lastMergeSourceCommit", out var srcC) && srcC.TryGetProperty("commitId", out var srcId)
            ? srcId.GetString() : null;
        var targetCommitId = pr.TryGetProperty("lastMergeTargetCommit", out var tgtC) && tgtC.TryGetProperty("commitId", out var tgtId)
            ? tgtId.GetString() : null;

        var summary = new
        {
            id = pr.GetProperty("pullRequestId").GetInt32(),
            title = pr.GetProperty("title").GetString(),
            status = pr.GetProperty("status").GetString(),
            isDraft = pr.TryGetProperty("isDraft", out var draft) && draft.GetBoolean(),
            createdBy = pr.GetProperty("createdBy").GetProperty("displayName").GetString(),
            createdAt = pr.GetProperty("creationDate").GetDateTimeOffset(),
            sourceBranch = StripRefPrefix(sourceRef),
            targetBranch = StripRefPrefix(targetRef),
            description = TrimText(pr.TryGetProperty("description", out var desc) ? desc.GetString() : string.Empty, 1200),
            iterationId,
            sourceCommitId,
            targetCommitId,
            changedFileCount = fileEntries.Count,
            changedFiles = fileEntries.Select(f => new { path = f.Path, changeType = f.ChangeType, originalPath = f.OriginalPath }),
            filesIsTruncated = isTruncated,
            filesNextCursor = nextCursor,
            changedFilesNote
        };

        return Success($"Loaded summary for PR #{pullRequestId} with {fileEntries.Count} changed files.", summary);
    }

    private async Task<object> GetPrFileDiffAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var path = GetRequiredString(args, "path");
        var contextLines = Math.Clamp(GetInt(args, "contextLines") ?? 3, 0, 5);
        var hunkOffset = ParseCursorHunk(GetString(args, "cursor"));

        var prTask = _adoClient.GetPullRequestAsync(target.Organization, target.Project, target.Repository, target.PullRequestId, cancellationToken);
        var iterIdTask = _adoClient.GetLatestIterationIdAsync(target.Organization, target.Project, target.Repository, target.PullRequestId, cancellationToken);
        await Task.WhenAll(prTask, iterIdTask);

        var pr = prTask.Result;
        var iterationId = iterIdTask.Result;
        var sourceCommit = TryGetCommitId(pr, "lastMergeSourceCommit");
        var targetCommit = TryGetCommitId(pr, "lastMergeTargetCommit");

        if (string.IsNullOrWhiteSpace(sourceCommit) || string.IsNullOrWhiteSpace(targetCommit))
        {
            return Success("PR commit metadata is unavailable; returning empty diff.", new
            {
                org = target.Organization,
                project = target.Project,
                repository = target.Repository,
                pullRequestId = target.PullRequestId,
                path,
                contextLines,
                iterationId,
                sourceCommitId = sourceCommit,
                targetCommitId = targetCommit,
                rawPatch = string.Empty,
                hunks = Array.Empty<object>(),
                isTruncated = false,
                returnedHunkCount = 0,
                totalHunkCount = 0,
                nextCursor = (string?)null,
                diffNote = "Missing commit metadata; diff content is unavailable."
            });
        }

        var beforeTask = _adoClient.TryGetFileContentAtCommitAsync(target.Organization, target.Project, target.Repository, path, targetCommit, cancellationToken);
        var afterTask = _adoClient.TryGetFileContentAtCommitAsync(target.Organization, target.Project, target.Repository, path, sourceCommit, cancellationToken);
        await Task.WhenAll(beforeTask, afterTask);

        var (beforeFound, beforeContent) = beforeTask.Result;
        var (afterFound, afterContent) = afterTask.Result;

        if (!beforeFound && !afterFound)
            return Failure("File path not found in PR source/target commits.", "ADO_HTTP_404");

        var diffResult = UnifiedDiff.Build(beforeContent, afterContent, contextLines, _options.MaxDiffChars, hunkOffset, _options.MaxDiffLines);
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;

        var hunks = diffResult.Hunks.Select(hunk => new
        {
            oldStart = hunk.OldStart,
            oldCount = hunk.OldCount,
            newStart = hunk.NewStart,
            newCount = hunk.NewCount,
            lines = hunk.Lines.Select(line => new
            {
                kind = line.Kind.ToString().ToLowerInvariant(),
                content = line.Content,
                oldLineNumber = line.OldLineNumber,
                newLineNumber = line.NewLineNumber,
                commentAnchor = BuildCommentAnchor(normalizedPath, line, iterationId, sourceCommit, targetCommit)
            })
        });

        var returnedHunkCount = diffResult.Hunks.Count;
        var nextCursor = diffResult.IsTruncated ? $"hunk:{hunkOffset + returnedHunkCount}" : null;

        return Success("Generated targeted file diff.", new
        {
            org = target.Organization,
            project = target.Project,
            repository = target.Repository,
            pullRequestId = target.PullRequestId,
            path,
            contextLines,
            iterationId,
            sourceCommitId = sourceCommit,
            targetCommitId = targetCommit,
            rawPatch = diffResult.RawPatch,
            hunks,
            isTruncated = diffResult.IsTruncated,
            returnedHunkCount,
            totalHunkCount = diffResult.TotalHunkCount,
            nextCursor
        });
    }

    private async Task<object> ListThreadsSummaryAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var org = target.Organization;
        var project = target.Project;
        var repository = target.Repository;
        var pullRequestId = target.PullRequestId;
        var status = GetString(args, "status") ?? "active";
        var threadOffset = ParseCursorOffset(GetString(args, "cursor"));

        EnsureScope(org, project);

        var payload = await _adoClient.GetPullRequestThreadsAsync(org, project, repository, pullRequestId, cancellationToken);

        // Guard against null/missing "value" — an empty thread list is not an error
        var allThreads = payload.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Array
            ? val.EnumerateArray().ToList()
            : new List<JsonElement>();

        var filtered = allThreads
            .Where(t => t.ValueKind == JsonValueKind.Object &&
                        (status == "all" || (t.TryGetProperty("status", out var s) && s.GetString() == "active")))
            .ToList();

        var totalCount = filtered.Count;
        var page = filtered.Skip(threadOffset).Take(_options.MaxThreads).ToList();
        var isTruncated = threadOffset + page.Count < totalCount;
        var nextCursor = isTruncated ? $"offset:{threadOffset + page.Count}" : null;

        var threads = page.Select(thread =>
        {
            var comments = thread.TryGetProperty("comments", out var c) && c.ValueKind == JsonValueKind.Array
                ? c.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList() : new List<JsonElement>();
            var first = comments.FirstOrDefault();
            var preview = first.ValueKind == JsonValueKind.Object && first.TryGetProperty("content", out var ct)
                ? TrimText(ct.GetString(), 120) : string.Empty;
            var filePath = thread.TryGetProperty("threadContext", out var ctx) && ctx.ValueKind == JsonValueKind.Object && ctx.TryGetProperty("filePath", out var fp)
                ? fp.GetString() : null;
            var (lineNumber, side) = ExtractThreadLineContext(thread);
            return new
            {
                threadId = thread.GetProperty("id").GetInt32(),
                threadStatus = thread.TryGetProperty("status", out var ts) ? ts.GetString() : null,
                filePath,
                lineNumber,
                side,
                commentCount = comments.Count,
                preview
            };
        }).ToList();

        return Success($"Loaded {threads.Count} thread summaries.", new
        {
            pullRequestId,
            threads,
            returnedCount = threads.Count,
            totalCount,
            isTruncated,
            nextCursor
        });
    }

    private async Task<object> GetThreadDetailsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var threadId = GetRequiredInt(args, "threadId");

        var payload = await _adoClient.GetPullRequestThreadsAsync(target.Organization, target.Project, target.Repository, target.PullRequestId, cancellationToken);
        var thread = payload.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().FirstOrDefault(t => t.TryGetProperty("id", out var id) && id.GetInt32() == threadId)
            : default;

        if (thread.ValueKind == JsonValueKind.Undefined)
            return Failure("Thread not found in pull request.", "ADO_HTTP_404");

        var comments = thread.TryGetProperty("comments", out var c) && c.ValueKind == JsonValueKind.Array
            ? c.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).Select(comment => new
            {
                id = comment.GetProperty("id").GetInt32(),
                author = comment.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? a.GetProperty("displayName").GetString() : null,
                publishedAt = comment.TryGetProperty("publishedDate", out var pd) ? pd.GetDateTimeOffset() : (DateTimeOffset?)null,
                content = comment.TryGetProperty("content", out var ct) ? ct.GetString() ?? string.Empty : string.Empty
            }).ToList()
            : new List<object>().Select(_ => new { id = 0, author = (string?)null, publishedAt = (DateTimeOffset?)null, content = string.Empty }).ToList();

        var filePath = thread.TryGetProperty("threadContext", out var ctx) && ctx.TryGetProperty("filePath", out var fp)
            ? fp.GetString() : null;
        var (lineNumber, side) = ExtractThreadLineContext(thread);

        return Success("Loaded thread details.", new
        {
            threadId,
            status = thread.TryGetProperty("status", out var ts) ? ts.GetString() : null,
            filePath,
            lineNumber,
            side,
            comments
        });
    }

    private async Task<object> CreateCommentAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var org = target.Organization;
        var project = target.Project;
        var repository = target.Repository;
        var pullRequestId = target.PullRequestId;
        var path = GetRequiredString(args, "path");
        var line = GetRequiredInt(args, "line");
        var content = GetRequiredString(args, "content");

        EnsureScope(org, project);

        // ADO requires filePath to start with '/'; normalize it defensively
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;

        var body = new
        {
            comments = new[]
            {
                new
                {
                    parentCommentId = 0,
                    content,
                    commentType = 1
                }
            },
            status = 1,
            threadContext = new
            {
                filePath = normalizedPath,
                // offset is 1-based character position; start=1, end=2 creates a 1-char
                // wide selection on the target line — required by ADO to anchor the comment
                rightFileStart = new { line, offset = 1 },
                rightFileEnd = new { line, offset = 2 }
            }
        };

        var result = await _adoClient.CreateThreadAsync(org, project, repository, pullRequestId, body, cancellationToken);
        return Success("Comment thread created.", new
        {
            pullRequestId,
            threadId = result.GetProperty("id").GetInt32(),
            path,
            line
        });
    }

    private async Task<object> VotePrAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var org = target.Organization;
        var project = target.Project;
        var repository = target.Repository;
        var pullRequestId = target.PullRequestId;
        var voteInput = GetRequiredString(args, "vote");

        EnsureScope(org, project);

        var vote = voteInput switch
        {
            "approve" => (short)10,
            "approve_with_suggestions" => (short)5,
            "wait_for_author" => (short)-5,
            "reject" => (short)-10,
            "reset" => (short)0,
            _ => throw new InvalidOperationException("Invalid vote value.")
        };

        var reviewerId = await _adoClient.GetCurrentUserIdAsync(org, cancellationToken);
        var response = await _adoClient.VoteAsync(org, project, repository, pullRequestId, reviewerId, vote, cancellationToken);

        return Success("Vote submitted.", new
        {
            pullRequestId,
            reviewerId,
            vote = response.GetProperty("vote").GetInt16()
        });
    }

    private async Task<ResolvedPr> ResolveTargetAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var org = GetString(args, "org");
        var project = GetString(args, "project");
        var repository = GetString(args, "repository");
        var pullRequestId = GetInt(args, "pullRequestId");
        var input = GetString(args, "input") ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(repository) && pullRequestId.HasValue)
        {
            var resolvedOrg = string.IsNullOrWhiteSpace(org) ? _options.DefaultOrganization : org;
            var resolvedProject = string.IsNullOrWhiteSpace(project) ? _options.DefaultProject : project;
            EnsureScope(resolvedOrg!, resolvedProject!);
            return new ResolvedPr(resolvedOrg!, resolvedProject!, repository, pullRequestId.Value);
        }

        var resolved = await _inputResolver.ResolveAsync(input, org, project, repository, pullRequestId, cancellationToken);
        if (resolved.IsResolved && resolved.Resolved.HasValue)
        {
            return resolved.Resolved.Value;
        }

        if (resolved.Candidates.Count > 0)
        {
            var candidateRepos = string.Join(", ",
                resolved.Candidates.Take(5).Select(c => $"{c.Repository}#{c.PullRequestId}"));
            throw new InvalidOperationException(
                $"Input resolved to multiple candidates. Specify repository to continue. Candidates: {candidateRepos}");
        }

        throw new InvalidOperationException(resolved.Message);
    }

    private static IEnumerable<(string Path, string ChangeType, string? OriginalPath)> ExtractChangedFileEntries(JsonElement changes)
    {
        // The iterations/changes API response uses "changeEntries" (not "changes")
        if (changes.ValueKind == JsonValueKind.Undefined ||
            !changes.TryGetProperty("changeEntries", out var changedFiles) ||
            changedFiles.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var entry in changedFiles.EnumerateArray())
        {
            if (!entry.TryGetProperty("item", out var item) || !item.TryGetProperty("path", out var pathEl))
                continue;

            var path = pathEl.GetString();
            if (string.IsNullOrWhiteSpace(path)) continue;

            var changeType = entry.TryGetProperty("changeType", out var ct)
                ? (ct.GetString() ?? "edit").ToLowerInvariant() : "edit";

            // ADO may surface rename original path under "sourceServerItem" or "item.originalPath"
            string? originalPath = null;
            if (entry.TryGetProperty("sourceServerItem", out var ssi)) originalPath = ssi.GetString();
            if (originalPath == null && item.TryGetProperty("originalPath", out var op)) originalPath = op.GetString();

            yield return (path, changeType, originalPath);
        }
    }

    private static object Success(string text, object data)
    {
        return new
        {
            content = new[] { new { type = "text", text } },
            structuredContent = data,
            isError = false
        };
    }

    private static object Failure(string text, object data)
    {
        return new
        {
            content = new[] { new { type = "text", text } },
            structuredContent = new { error = text, context = data },
            isError = true
        };
    }

    /// <summary>Returns a typed error envelope. Every failure carries a stable <c>code</c>, a
    /// <c>retryable</c> hint, and a per-call <c>diagnosticsId</c> for tracing.</summary>
    private static object Failure(string message, string code = "INVALID_INPUT", bool retryable = false, int? upstreamStatus = null)
    {
        return new
        {
            content = new[] { new { type = "text", text = message } },
            structuredContent = new
            {
                error = new { code, message, retryable, upstreamStatus, diagnosticsId = Guid.NewGuid().ToString("N") }
            },
            isError = true
        };
    }

    private static string? GetString(JsonElement args, string key)
    {
        if (!args.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.GetString();
    }

    private static string GetRequiredString(JsonElement args, string key)
    {
        var value = GetString(args, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required string argument '{key}'.");
        }

        return value;
    }

    private static int? GetInt(JsonElement args, string key)
    {
        if (!args.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.GetInt32();
    }

    private static int GetRequiredInt(JsonElement args, string key)
    {
        var value = GetInt(args, key);
        if (!value.HasValue)
        {
            throw new InvalidOperationException($"Missing required integer argument '{key}'.");
        }

        return value.Value;
    }

    private static string TrimText(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    private static void EnsureScope(string organization, string project)
    {
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project))
        {
            throw new InvalidOperationException("Organization and project are required. Provide org/project args or set AZDO_DEFAULT_ORG and AZDO_DEFAULT_PROJECT.");
        }
    }

    // ── New tools (Phase 3: file snapshots, Phase 5: review bundle, Phase 6: capabilities) ─────

    private async Task<object> GetPrFileContentAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var path = GetRequiredString(args, "path");
        var side = GetString(args, "side") ?? "both";
        var maxLines = Math.Clamp(GetInt(args, "lineCount") ?? _options.MaxSnapshotLines, 1, _options.MaxSnapshotLines);

        // startLine may come from a cursor or explicit param
        var cursorStr = GetString(args, "cursor");
        int startLine;
        if (cursorStr?.StartsWith("line:", StringComparison.Ordinal) == true &&
            int.TryParse(cursorStr["line:".Length..], out var cursorLine))
            startLine = Math.Max(1, cursorLine);
        else
            startLine = Math.Max(1, GetInt(args, "startLine") ?? 1);

        var prTask = _adoClient.GetPullRequestAsync(target.Organization, target.Project, target.Repository, target.PullRequestId, cancellationToken);
        var iterIdTask = _adoClient.GetLatestIterationIdAsync(target.Organization, target.Project, target.Repository, target.PullRequestId, cancellationToken);
        await Task.WhenAll(prTask, iterIdTask);

        var pr = prTask.Result;
        var iterationId = iterIdTask.Result;
        var sourceCommit = TryGetCommitId(pr, "lastMergeSourceCommit");
        var targetCommit = TryGetCommitId(pr, "lastMergeTargetCommit");
        var missingCommitMetadata = string.IsNullOrWhiteSpace(sourceCommit) || string.IsNullOrWhiteSpace(targetCommit);

        object? before = null;
        object? after = null;

        if ((side is "before" or "both") && !string.IsNullOrWhiteSpace(targetCommit))
        {
            var (found, content) = await _adoClient.TryGetFileContentAtCommitAsync(
                target.Organization, target.Project, target.Repository, path, targetCommit, cancellationToken);
            before = found ? BuildFileSnapshot(content, targetCommit, startLine, maxLines) : null;
        }

        if ((side is "after" or "both") && !string.IsNullOrWhiteSpace(sourceCommit))
        {
            var (found, content) = await _adoClient.TryGetFileContentAtCommitAsync(
                target.Organization, target.Project, target.Repository, path, sourceCommit, cancellationToken);
            after = found ? BuildFileSnapshot(content, sourceCommit, startLine, maxLines) : null;
        }

        if (before == null && after == null)
        {
            if (missingCommitMetadata)
            {
                return Success($"Loaded file content for '{path}' with missing commit metadata.", new
                {
                    repository = target.Repository,
                    pullRequestId = target.PullRequestId,
                    path,
                    iterationId,
                    sourceCommitId = sourceCommit,
                    targetCommitId = targetCommit,
                    before,
                    after,
                    snapshotNote = "Missing commit metadata; file snapshots are unavailable."
                });
            }

            return Failure($"File '{path}' not found in PR commits.", "ADO_HTTP_404");
        }

        return Success($"Loaded file content for '{path}'.", new
        {
            repository = target.Repository,
            pullRequestId = target.PullRequestId,
            path,
            iterationId,
            sourceCommitId = sourceCommit,
            targetCommitId = targetCommit,
            before,
            after
        });
    }

    private async Task<object> GetPrReviewBundleAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var org = target.Organization;
        var project = target.Project;
        var repository = target.Repository;
        var pullRequestId = target.PullRequestId;

        EnsureScope(org, project);

        // Fetch PR metadata + iteration ID in parallel, then changed files + threads in parallel
        var prTask = _adoClient.GetPullRequestAsync(org, project, repository, pullRequestId, cancellationToken);
        var iterIdTask = _adoClient.GetLatestIterationIdAsync(org, project, repository, pullRequestId, cancellationToken);
        await Task.WhenAll(prTask, iterIdTask);

        var pr = prTask.Result;
        var iterationId = iterIdTask.Result;

        var changesTask = iterationId > 0
            ? _adoClient.GetPullRequestIterationChangesAsync(org, project, repository, pullRequestId, iterationId, 0, cancellationToken)
            : Task.FromResult(default(JsonElement));
        var threadsTask = _adoClient.GetPullRequestThreadsAsync(org, project, repository, pullRequestId, cancellationToken);
        await Task.WhenAll(changesTask, threadsTask);

        // Changed files
        var allFiles = iterationId > 0
            ? ExtractChangedFileEntries(changesTask.Result).ToList()
            : new List<(string Path, string ChangeType, string? OriginalPath)>();
        var filesTotal = allFiles.Count;
        var returnedFiles = allFiles.Take(_options.MaxChanges).ToList();
        var filesIsTruncated = returnedFiles.Count < filesTotal;

        // Threads
        var threadsPayload = threadsTask.Result;
        var allThreadElems = threadsPayload.TryGetProperty("value", out var tv) && tv.ValueKind == JsonValueKind.Array
            ? tv.EnumerateArray().ToList() : new List<JsonElement>();
        var threadsTotal = allThreadElems.Count;
        var returnedThreadElems = allThreadElems.Take(_options.MaxThreads).ToList();
        var threadsIsTruncated = returnedThreadElems.Count < threadsTotal;

        // PR metadata
        var sourceRef = pr.GetProperty("sourceRefName").GetString() ?? string.Empty;
        var targetRef = pr.GetProperty("targetRefName").GetString() ?? string.Empty;
        var sourceCommitId = pr.TryGetProperty("lastMergeSourceCommit", out var src) && src.TryGetProperty("commitId", out var srcId)
            ? srcId.GetString() : null;
        var targetCommitId = pr.TryGetProperty("lastMergeTargetCommit", out var tgt) && tgt.TryGetProperty("commitId", out var tgtId)
            ? tgtId.GetString() : null;

        var prMeta = new
        {
            id = pr.GetProperty("pullRequestId").GetInt32(),
            title = pr.GetProperty("title").GetString(),
            status = pr.GetProperty("status").GetString(),
            isDraft = pr.TryGetProperty("isDraft", out var isDraft) && isDraft.GetBoolean(),
            sourceBranch = StripRefPrefix(sourceRef),
            targetBranch = StripRefPrefix(targetRef),
            createdBy = pr.GetProperty("createdBy").GetProperty("displayName").GetString(),
            iterationId,
            sourceCommitId,
            targetCommitId
        };

        var changedFiles = returnedFiles.Select(f => new { path = f.Path, changeType = f.ChangeType, originalPath = f.OriginalPath });

        var threads = returnedThreadElems.Where(t => t.ValueKind == JsonValueKind.Object).Select(thread =>
        {
            var comments = thread.TryGetProperty("comments", out var c) && c.ValueKind == JsonValueKind.Array
                ? c.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList() : new List<JsonElement>();
            var first = comments.FirstOrDefault();
            var preview = first.ValueKind == JsonValueKind.Object && first.TryGetProperty("content", out var ct)
                ? TrimText(ct.GetString(), 120) : string.Empty;
            var filePath = thread.TryGetProperty("threadContext", out var ctx) && ctx.ValueKind == JsonValueKind.Object && ctx.TryGetProperty("filePath", out var fp)
                ? fp.GetString() : null;
            var (lineNumber, lineSide) = ExtractThreadLineContext(thread);
            return new
            {
                threadId = thread.GetProperty("id").GetInt32(),
                status = thread.TryGetProperty("status", out var ts) ? ts.GetString() : null,
                filePath,
                lineNumber,
                side = lineSide,
                commentCount = comments.Count,
                preview
            };
        });

        return Success($"Loaded review bundle for PR #{pullRequestId}.", new
        {
            pr = prMeta,
            changedFiles,
            fileCount = filesTotal,
            filesIsTruncated,
            filesNextCursor = filesIsTruncated ? $"skip:{_options.MaxChanges}" : null,
            threads,
            threadCount = threadsTotal,
            threadsIsTruncated,
            threadsNextCursor = threadsIsTruncated ? $"offset:{_options.MaxThreads}" : null
        });
    }

    private static object GetCapabilities()
    {
        return Success("Server capabilities.", new
        {
            version = "2.0",
            features = new[] { "structuredDiff", "fileSnapshots", "pagination", "threadAnchors", "reviewBundle", "typedErrors" },
            tools = new[]
            {
                "resolve_pr_input", "fuzzy_find_repository", "search_pull_requests",
                "get_pr_summary", "get_pr_file_diff", "get_pr_file_content",
                "list_pr_threads_summary", "get_pr_thread_details",
                "create_pr_comment", "vote_pr",
                "get_pr_review_bundle", "get_capabilities"
            }
        });
    }

    // ── Data extraction helpers ───────────────────────────────────────────────

    private static (int? LineNumber, string? Side) ExtractThreadLineContext(JsonElement thread)
    {
        if (!thread.TryGetProperty("threadContext", out var ctx) || ctx.ValueKind == JsonValueKind.Null)
            return (null, null);

        if (ctx.TryGetProperty("rightFileStart", out var rightStart) &&
            rightStart.TryGetProperty("line", out var rightLine) && rightLine.GetInt32() > 0)
            return (rightLine.GetInt32(), "right");

        if (ctx.TryGetProperty("leftFileStart", out var leftStart) &&
            leftStart.TryGetProperty("line", out var leftLine) && leftLine.GetInt32() > 0)
            return (leftLine.GetInt32(), "left");

        return (null, null);
    }

    private static object? BuildCommentAnchor(string filePath, DiffLine line, int iterationId, string? sourceCommitId, string? targetCommitId)
    {
        var (side, lineNum) = line.Kind switch
        {
            DiffLineKind.Removed => ("left", line.OldLineNumber),
            _ => ("right", line.NewLineNumber)
        };
        if (lineNum == null) return null;
        return new { filePath, side, line = lineNum.Value, iterationId, sourceCommitId, targetCommitId };
    }

    private static object BuildFileSnapshot(string content, string? commitId, int startLine, int maxLines)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var totalLineCount = lines.Length;
        var start = Math.Clamp(startLine - 1, 0, totalLineCount);
        var slice = lines.Skip(start).Take(maxLines).ToArray();
        var isTruncated = start + slice.Length < totalLineCount;
        return new
        {
            commitId,
            content = string.Join('\n', slice),
            totalLineCount,
            returnedLineCount = slice.Length,
            isTruncated,
            nextCursor = isTruncated ? $"line:{start + slice.Length + 1}" : null
        };
    }

    // ── Cursor parsing helpers ────────────────────────────────────────────────

    private static int ParseCursorOffset(string? cursor)
    {
        if (cursor?.StartsWith("offset:", StringComparison.Ordinal) == true &&
            int.TryParse(cursor["offset:".Length..], out var v) && v > 0)
            return v;
        return 0;
    }

    private static int ParseCursorSkip(string? cursor)
    {
        if (cursor?.StartsWith("skip:", StringComparison.Ordinal) == true &&
            int.TryParse(cursor["skip:".Length..], out var v) && v > 0)
            return v;
        return 0;
    }

    private static int ParseCursorHunk(string? cursor)
    {
        if (cursor?.StartsWith("hunk:", StringComparison.Ordinal) == true &&
            int.TryParse(cursor["hunk:".Length..], out var v) && v > 0)
            return v;
        return 0;
    }

    private static string StripRefPrefix(string refName)
    {
        const string prefix = "refs/heads/";
        return refName.StartsWith(prefix, StringComparison.Ordinal) ? refName[prefix.Length..] : refName;
    }

    private static string? TryGetCommitId(JsonElement pr, string commitPropertyName)
    {
        if (!pr.TryGetProperty(commitPropertyName, out var commitNode) || commitNode.ValueKind != JsonValueKind.Object)
            return null;
        if (!commitNode.TryGetProperty("commitId", out var commitIdNode) || commitIdNode.ValueKind != JsonValueKind.String)
            return null;

        var value = commitIdNode.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
