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

    public IReadOnlyList<object> GetToolDefinitions()
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
                        top = new { type = "integer", minimum = 1, maximum = 50 }
                    }
                }
            },
            new
            {
                name = "get_pr_summary",
                description = "Get high-level pull request summary without heavy diff details.",
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
                name = "get_pr_file_diff",
                description = "Get a targeted unified diff for a single file in a PR.",
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
                        contextLines = new { type = "integer", minimum = 0, maximum = 5 }
                    }
                }
            },
            new
            {
                name = "list_pr_threads_summary",
                description = "Get PR thread summary with small previews.",
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
                        status = new { type = "string", @enum = new[] { "active", "all" } }
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
                "list_pr_threads_summary" => await ListThreadsSummaryAsync(args, cancellationToken),
                "get_pr_thread_details" => await GetThreadDetailsAsync(args, cancellationToken),
                "create_pr_comment" => await CreateCommentAsync(args, cancellationToken),
                "vote_pr" => await VotePrAsync(args, cancellationToken),
                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };
        }
        catch (HttpRequestException ex)
        {
            var status = ex.StatusCode.HasValue ? $"HTTP {(int)ex.StatusCode.Value}" : "HTTP error";
            return Failure($"ADO API request failed: {status}", new { toolName });
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message, new { toolName });
        }
        catch (Exception)
        {
            return Failure("An unexpected error occurred.", new { toolName });
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
        {
            return Failure(resolved.Message, new
            {
                isResolved = false,
                candidates = Array.Empty<object>()
            });
        }

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

        EnsureScope(org, project);

        var prs = await _adoClient.SearchPullRequestsAsync(org, project, repository, status, top, cancellationToken);
        var items = prs.Select(pr => new
        {
            id = pr.Id,
            title = pr.Title,
            author = pr.Author,
            pr.Status,
            createdAt = pr.CreatedAt,
            repository = pr.Repository
        });

        return Success($"Found {prs.Count} pull requests in {repository}.", new { org, project, repository, items });
    }

    private async Task<object> GetPrSummaryAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var org = target.Organization;
        var project = target.Project;
        var repository = target.Repository;
        var pullRequestId = target.PullRequestId;

        EnsureScope(org, project);

        var pr = await _adoClient.GetPullRequestAsync(org, project, repository, pullRequestId, cancellationToken);

        // Use the iterations/changes endpoint (the /pullRequests/{id}/changes route does not exist and returns 404)
        var iterationId = await _adoClient.GetLatestIterationIdAsync(org, project, repository, pullRequestId, cancellationToken);
        var changedFiles = new List<string>();
        string? changedFilesNote = null;
        if (iterationId > 0)
        {
            var changes = await _adoClient.GetPullRequestIterationChangesAsync(org, project, repository, pullRequestId, iterationId, cancellationToken);
            changedFiles = ExtractChangedFiles(changes).ToList();
        }
        else
        {
            changedFilesNote = "No iterations found; changed file list is unavailable.";
        }

        var summary = new
        {
            id = pr.GetProperty("pullRequestId").GetInt32(),
            title = pr.GetProperty("title").GetString(),
            status = pr.GetProperty("status").GetString(),
            createdBy = pr.GetProperty("createdBy").GetProperty("displayName").GetString(),
            createdAt = pr.GetProperty("creationDate").GetDateTimeOffset(),
            sourceRef = pr.GetProperty("sourceRefName").GetString(),
            targetRef = pr.GetProperty("targetRefName").GetString(),
            description = TrimText(pr.TryGetProperty("description", out var desc) ? desc.GetString() : string.Empty, 1200),
            changedFileCount = changedFiles.Count,
            changedFiles,
            changedFilesNote
        };

        return Success($"Loaded summary for PR #{pullRequestId} with {changedFiles.Count} changed files.", summary);
    }

    private async Task<object> GetPrFileDiffAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var path = GetRequiredString(args, "path");
        var contextLines = Math.Clamp(GetInt(args, "contextLines") ?? 3, 0, 5);

        var pr = await _adoClient.GetPullRequestAsync(target.Organization, target.Project, target.Repository, target.PullRequestId, cancellationToken);
        var sourceCommit = pr.GetProperty("lastMergeSourceCommit").GetProperty("commitId").GetString();
        var targetCommit = pr.GetProperty("lastMergeTargetCommit").GetProperty("commitId").GetString();

        if (string.IsNullOrWhiteSpace(sourceCommit) || string.IsNullOrWhiteSpace(targetCommit))
        {
            return Failure("Pull request commit info is missing for diff generation.", new { path });
        }

        var before = await _adoClient.TryGetFileContentAtCommitAsync(
            target.Organization,
            target.Project,
            target.Repository,
            path,
            targetCommit,
            cancellationToken);

        var after = await _adoClient.TryGetFileContentAtCommitAsync(
            target.Organization,
            target.Project,
            target.Repository,
            path,
            sourceCommit,
            cancellationToken);

        if (!before.Found && !after.Found)
        {
            return Failure("File path not found in PR source/target commits.", new { path });
        }

        var diff = UnifiedDiff.Build(before.Content, after.Content, contextLines, _options.MaxDiffChars, out var truncated);
        return Success("Generated targeted file diff.", new
        {
            org = target.Organization,
            project = target.Project,
            repository = target.Repository,
            pullRequestId = target.PullRequestId,
            path,
            contextLines,
            truncated,
            diff
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

        EnsureScope(org, project);

        var payload = await _adoClient.GetPullRequestThreadsAsync(org, project, repository, pullRequestId, cancellationToken);
        var threads = payload.GetProperty("value")
            .EnumerateArray()
            .Where(thread => status == "all" || (thread.TryGetProperty("status", out var s) && s.GetString() == "active"))
            .Take(_options.MaxThreads)
            .Select(thread =>
            {
                var comments = thread.GetProperty("comments").EnumerateArray().ToList();
                var first = comments.FirstOrDefault();
                var preview = first.ValueKind == JsonValueKind.Undefined
                    ? string.Empty
                    : TrimText(first.GetProperty("content").GetString(), 120);

                var path = thread.TryGetProperty("threadContext", out var ctx) &&
                           ctx.TryGetProperty("filePath", out var filePath)
                    ? filePath.GetString()
                    : null;

                return new
                {
                    threadId = thread.GetProperty("id").GetInt32(),
                    threadStatus = thread.GetProperty("status").GetString(),
                    filePath = path,
                    commentCount = comments.Count,
                    preview
                };
            })
            .ToList();

        return Success($"Loaded {threads.Count} thread summaries.", new { pullRequestId, threads });
    }

    private async Task<object> GetThreadDetailsAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(args, cancellationToken);
        var threadId = GetRequiredInt(args, "threadId");

        var payload = await _adoClient.GetPullRequestThreadsAsync(target.Organization, target.Project, target.Repository, target.PullRequestId, cancellationToken);
        var thread = payload.GetProperty("value")
            .EnumerateArray()
            .FirstOrDefault(t => t.TryGetProperty("id", out var id) && id.GetInt32() == threadId);

        if (thread.ValueKind == JsonValueKind.Undefined)
        {
            return Failure("Thread not found in pull request.", new { threadId });
        }

        var comments = thread.GetProperty("comments")
            .EnumerateArray()
            .Select(comment => new
            {
                id = comment.GetProperty("id").GetInt32(),
                author = comment.GetProperty("author").GetProperty("displayName").GetString(),
                publishedAt = comment.TryGetProperty("publishedDate", out var publishedDate)
                    ? publishedDate.GetDateTimeOffset()
                    : (DateTimeOffset?)null,
                content = comment.GetProperty("content").GetString() ?? string.Empty
            })
            .ToList();

        var path = thread.TryGetProperty("threadContext", out var ctx) && ctx.TryGetProperty("filePath", out var fp)
            ? fp.GetString()
            : null;

        return Success("Loaded thread details.", new
        {
            threadId,
            status = thread.GetProperty("status").GetString(),
            filePath = path,
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

    private static IEnumerable<string> ExtractChangedFiles(JsonElement changes)
    {
        // The iterations/changes API response uses "changeEntries" (not "changes")
        if (!changes.TryGetProperty("changeEntries", out var changedFiles) || changedFiles.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var changed in changedFiles.EnumerateArray())
        {
            if (!changed.TryGetProperty("item", out var item) || !item.TryGetProperty("path", out var path))
            {
                continue;
            }

            var value = path.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
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
}
