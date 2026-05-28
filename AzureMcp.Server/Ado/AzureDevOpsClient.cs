using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AzureMcp.Server.Configuration;

namespace AzureMcp.Server.Ado;

public sealed class AzureDevOpsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AdoOptions _options;

    public AzureDevOpsClient(HttpClient httpClient, AdoOptions options)
    {
        _httpClient = httpClient;
        _options = options;

        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_options.PersonalAccessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    public async Task<IReadOnlyList<RepositoryInfo>> ListRepositoriesAsync(string organization, string project, CancellationToken cancellationToken)
    {
        var url = BuildUrl(organization, project, "_apis/git/repositories");
        var payload = await GetJsonAsync(url, cancellationToken);
        return payload.RootElement
            .GetProperty("value")
            .EnumerateArray()
            .Select(static r => new RepositoryInfo(
                r.GetProperty("id").GetString() ?? string.Empty,
                r.GetProperty("name").GetString() ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<PullRequestInfo>> SearchPullRequestsAsync(
        string organization,
        string project,
        string repository,
        string status,
        int top,
        int skip,
        CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var query = $"searchCriteria.status={Uri.EscapeDataString(status)}&$top={top}&$skip={skip}";
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullrequests", query);

        var payload = await GetJsonAsync(url, cancellationToken);
        return payload.RootElement
            .GetProperty("value")
            .EnumerateArray()
            .Select(static pr => new PullRequestInfo(
                pr.GetProperty("pullRequestId").GetInt32(),
                pr.GetProperty("title").GetString() ?? string.Empty,
                pr.GetProperty("status").GetString() ?? string.Empty,
                pr.GetProperty("creationDate").GetDateTimeOffset(),
                pr.GetProperty("createdBy").GetProperty("displayName").GetString() ?? string.Empty,
                pr.GetProperty("repository").GetProperty("name").GetString() ?? string.Empty))
            .ToList();
    }

    public async Task<JsonElement> GetPullRequestAsync(string organization, string project, string repository, int pullRequestId, CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullRequests/{pullRequestId}");
        var payload = await GetJsonAsync(url, cancellationToken);
        return payload.RootElement.Clone();
    }

    /// <summary>Gets the latest iteration ID for a pull request. Returns -1 if no iterations exist.</summary>
    public async Task<int> GetLatestIterationIdAsync(string organization, string project, string repository, int pullRequestId, CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullRequests/{pullRequestId}/iterations");
        var payload = await GetJsonAsync(url, cancellationToken);
        var value = payload.RootElement.GetProperty("value");
        var length = value.GetArrayLength();
        if (length == 0) return -1;
        // Iterations are ordered oldest→newest; the last one is the latest
        return value[length - 1].GetProperty("id").GetInt32();
    }

    /// <summary>Gets the changed files for a specific PR iteration. Response has a <c>changeEntries</c> array.</summary>
    public async Task<JsonElement> GetPullRequestIterationChangesAsync(
        string organization,
        string project,
        string repository,
        int pullRequestId,
        int iterationId,
        int skip,
        CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var query = $"$top={_options.MaxChanges}&$skip={skip}";
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullRequests/{pullRequestId}/iterations/{iterationId}/changes", query);
        var payload = await GetJsonAsync(url, cancellationToken);
        return payload.RootElement.Clone();
    }

    public async Task<bool> TryGetPullRequestAsync(string organization, string project, string repository, int pullRequestId, CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullRequests/{pullRequestId}");

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if ((int)response.StatusCode == 404)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Azure DevOps API failed ({(int)response.StatusCode}): {ExtractAdoErrorMessage(json)}");
    }

    public async Task<string> GetFileContentAtCommitAsync(
        string organization,
        string project,
        string repository,
        string path,
        string commitId,
        CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var query =
            $"path={Uri.EscapeDataString(path)}&versionDescriptor.versionType=commit&versionDescriptor.version={Uri.EscapeDataString(commitId)}&$format=text";
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/items", query);

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure DevOps API failed ({(int)response.StatusCode}): {ExtractAdoErrorMessage(text)}");
        }

        return text;
    }

    public async Task<(bool Found, string Content)> TryGetFileContentAtCommitAsync(
        string organization,
        string project,
        string repository,
        string path,
        string commitId,
        CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var query =
            $"path={Uri.EscapeDataString(path)}&versionDescriptor.versionType=commit&versionDescriptor.version={Uri.EscapeDataString(commitId)}&$format=text";
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/items", query);

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return (true, text);
        }

        if ((int)response.StatusCode == 404)
        {
            return (false, string.Empty);
        }

        throw new InvalidOperationException($"Azure DevOps API failed ({(int)response.StatusCode}): {ExtractAdoErrorMessage(text)}");
    }

    public async Task<JsonElement> GetPullRequestThreadsAsync(string organization, string project, string repository, int pullRequestId, CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullRequests/{pullRequestId}/threads");
        var payload = await GetJsonAsync(url, cancellationToken);
        return payload.RootElement.Clone();
    }

    public async Task<JsonElement> CreateThreadAsync(string organization, string project, string repository, int pullRequestId, object body, CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullRequests/{pullRequestId}/threads");
        return await SendJsonAsync(HttpMethod.Post, url, body, cancellationToken);
    }

    public async Task<JsonElement> VoteAsync(string organization, string project, string repository, int pullRequestId, string reviewerId, short vote, CancellationToken cancellationToken)
    {
        var repoSegment = Uri.EscapeDataString(repository);
        var reviewerSegment = Uri.EscapeDataString(reviewerId);
        var url = BuildUrl(organization, project, $"_apis/git/repositories/{repoSegment}/pullRequests/{pullRequestId}/reviewers/{reviewerSegment}");
        return await SendJsonAsync(HttpMethod.Put, url, new { vote }, cancellationToken);
    }

    public async Task<string> GetCurrentUserIdAsync(string organization, CancellationToken cancellationToken)
    {
        var url = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/_apis/connectionData?connectOptions=1&lastChangeId=-1&lastChangeId64=-1&api-version={_options.ApiVersion}";
        var payload = await GetJsonAsync(url, cancellationToken);
        return payload.RootElement.GetProperty("authenticatedUser").GetProperty("id").GetString() ?? string.Empty;
    }

    private string BuildUrl(string organization, string project, string path, string? query = null)
    {
        var escapedOrg = Uri.EscapeDataString(organization);
        var escapedProject = Uri.EscapeDataString(project);
        var qp = string.IsNullOrWhiteSpace(query) ? string.Empty : $"&{query}";
        return $"https://dev.azure.com/{escapedOrg}/{escapedProject}/{path}?api-version={_options.ApiVersion}{qp}";
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure DevOps API failed ({(int)response.StatusCode}): {ExtractAdoErrorMessage(json)}");
        }

        return JsonDocument.Parse(json);
    }

    private async Task<JsonElement> SendJsonAsync(HttpMethod method, string url, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure DevOps API failed ({(int)response.StatusCode}): {ExtractAdoErrorMessage(json)}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>Trims API error bodies to 500 chars to prevent HTML pages or large JSON from flooding LLM context.</summary>
    private static string ExtractAdoErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? body;
        }
        catch { }
        const int max = 300;
        return body.Length <= max ? body : body[..max] + "… [truncated]";
    }
}

public sealed record RepositoryInfo(string Id, string Name);

public sealed record PullRequestInfo(
    int Id,
    string Title,
    string Status,
    DateTimeOffset CreatedAt,
    string Author,
    string Repository);
