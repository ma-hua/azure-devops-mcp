using System.Text.RegularExpressions;
using AzureMcp.Server.Ado;
using AzureMcp.Server.Configuration;
using AzureMcp.Server.Utils;

namespace AzureMcp.Server.Resolution;

public sealed class InputResolver
{
    private static readonly Regex PrUrlRegex = new(
        "https://dev\\.azure\\.com/(?<org>[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/]+)/pullrequest/(?<id>\\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Legacy visualstudio.com domain: https://{org}.visualstudio.com/{project}/_git/{repo}/pullrequest/{id}
    private static readonly Regex PrUrlLegacyRegex = new(
        "https://(?<org>[^\\.]+)\\.visualstudio\\.com/(?<project>[^/]+)/_git/(?<repo>[^/]+)/pullrequest/(?<id>\\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PrIdRegex = new("(?:pr|pull\\s*request)?\\s*#?(?<id>\\d{1,9})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AzureDevOpsClient _adoClient;
    private readonly AdoOptions _options;

    public InputResolver(AzureDevOpsClient adoClient, AdoOptions options)
    {
        _adoClient = adoClient;
        _options = options;
    }

    public async Task<ResolveResult> ResolveAsync(
        string input,
        string? org,
        string? project,
        string? repository,
        int? pullRequestId,
        CancellationToken cancellationToken)
    {
        var normalizedInput = input?.Trim() ?? string.Empty;
        var contextOrg = string.IsNullOrWhiteSpace(org) ? _options.DefaultOrganization : org;
        var contextProject = string.IsNullOrWhiteSpace(project) ? _options.DefaultProject : project;

        if (TryParsePrUrl(normalizedInput, out var parsedFromUrl))
        {
            return ResolveResult.FromResolved(parsedFromUrl with
            {
                Organization = string.IsNullOrWhiteSpace(org) ? parsedFromUrl.Organization : org!,
                Project = string.IsNullOrWhiteSpace(project) ? parsedFromUrl.Project : project!
            }, "Resolved from pull request URL.");
        }

        var idFromInput = pullRequestId ?? TryExtractPullRequestId(normalizedInput);
        var repoFromInput = repository ?? TryExtractRepositoryHint(normalizedInput);

        if (!string.IsNullOrWhiteSpace(repoFromInput) && idFromInput.HasValue)
        {
            if (string.IsNullOrWhiteSpace(contextOrg) || string.IsNullOrWhiteSpace(contextProject))
            {
                return ResolveResult.Failed("Organization and project are required for PR ID + repository resolution.");
            }

                return ResolveResult.FromResolved(
                new ResolvedPr(contextOrg!, contextProject!, repoFromInput!, idFromInput.Value),
                "Resolved from repository hint and pull request ID.");
        }

        if (idFromInput.HasValue)
        {
            if (string.IsNullOrWhiteSpace(contextOrg) || string.IsNullOrWhiteSpace(contextProject))
            {
                return ResolveResult.Failed("Organization and project are required for pull request ID resolution.");
            }

            var candidates = await FindByIdAcrossRepositoriesAsync(contextOrg!, contextProject!, idFromInput.Value, cancellationToken);
            return ToResolvedOrCandidates(candidates, "Resolved from pull request ID lookup.");
        }

        if (!string.IsNullOrWhiteSpace(repoFromInput))
        {
            if (string.IsNullOrWhiteSpace(contextOrg) || string.IsNullOrWhiteSpace(contextProject))
            {
                return ResolveResult.Failed("Organization and project are required for repository-only resolution.");
            }

            var repositories = await _adoClient.ListRepositoriesAsync(contextOrg!, contextProject!, cancellationToken);
            var ranked = repositories
                .Select(r => new { Repo = r, Score = FuzzyMatch.Score(repoFromInput, r.Name) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(_options.MaxCandidates)
                .Select(x => new ResolvedPr(contextOrg!, contextProject!, x.Repo.Name, 0))
                .ToList();

            if (ranked.Count == 0)
            {
                return ResolveResult.Failed("No repositories matched the provided hint.");
            }

            return ResolveResult.FromCandidates(ranked, "Repository hint resolved to candidates. Pull request ID is still required.");
        }

        return ResolveResult.Failed("Could not resolve input. Provide PR URL, PR ID, or repository hint.");
    }

    private async Task<IReadOnlyList<ResolvedPr>> FindByIdAcrossRepositoriesAsync(
        string organization,
        string project,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var repositories = await _adoClient.ListRepositoriesAsync(organization, project, cancellationToken);
        var scoredRepos = repositories
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(_options.MaxRepositoryScan)
            .ToList();

        var tasks = scoredRepos.Select(async repo =>
        {
            var found = await _adoClient.TryGetPullRequestAsync(organization, project, repo.Name, pullRequestId, cancellationToken);
            return found ? (ResolvedPr?)new ResolvedPr(organization, project, repo.Name, pullRequestId) : null;
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r.HasValue).Select(r => r!.Value).ToList();
    }

    private static ResolveResult ToResolvedOrCandidates(IReadOnlyList<ResolvedPr> candidates, string resolvedMessage)
    {
        return candidates.Count switch
        {
            0 => ResolveResult.Failed("No matching pull request found in scanned repositories."),
            1 => ResolveResult.FromResolved(candidates[0], resolvedMessage),
            _ => ResolveResult.FromCandidates(candidates, "Multiple repositories contain the same pull request ID. Specify repository.")
        };
    }

    private static bool TryParsePrUrl(string input, out ResolvedPr result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var match = PrUrlRegex.Match(input);
        if (!match.Success)
        {
            match = PrUrlLegacyRegex.Match(input);
        }

        if (!match.Success)
        {
            return false;
        }

        var org = Uri.UnescapeDataString(match.Groups["org"].Value);
        var project = Uri.UnescapeDataString(match.Groups["project"].Value);
        var repo = Uri.UnescapeDataString(match.Groups["repo"].Value);
        var id = int.Parse(match.Groups["id"].Value);

        result = new ResolvedPr(org, project, repo, id);
        return true;
    }

    private static int? TryExtractPullRequestId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = PrIdRegex.Match(input);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups["id"].Value, out var id) ? id : null;
    }

    private static string? TryExtractRepositoryHint(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var marker = "repo";
        var idx = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var tail = input[(idx + marker.Length)..].Trim().Trim(':', '-', ' ');
        if (string.IsNullOrWhiteSpace(tail))
        {
            return null;
        }

        var firstToken = tail.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstToken) ? null : firstToken;
    }
}

public readonly record struct ResolvedPr(string Organization, string Project, string Repository, int PullRequestId);

public sealed class ResolveResult
{
    public bool IsResolved { get; private init; }
    public bool IsError { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public ResolvedPr? Resolved { get; private init; }
    public IReadOnlyList<ResolvedPr> Candidates { get; private init; } = Array.Empty<ResolvedPr>();

    public static ResolveResult FromResolved(ResolvedPr resolved, string message) => new()
    {
        IsResolved = true,
        Message = message,
        Resolved = resolved
    };

    public static ResolveResult FromCandidates(IReadOnlyList<ResolvedPr> candidates, string message) => new()
    {
        IsResolved = false,
        IsError = false,
        Message = message,
        Candidates = candidates
    };

    public static ResolveResult Failed(string message) => new()
    {
        IsResolved = false,
        IsError = true,
        Message = message
    };
}
