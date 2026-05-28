namespace AzureMcp.Server.Configuration;

public sealed class AdoOptions
{
    public string PersonalAccessToken { get; init; } = string.Empty;
    public string DefaultOrganization { get; init; } = string.Empty;
    public string DefaultProject { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "7.1-preview";
    public int MaxCandidates { get; init; } = 10;
    public int MaxThreads { get; init; } = 20;
    public int MaxChanges { get; init; } = 200;
    public int MaxRepositoryScan { get; init; } = 30;
    public int MaxDiffChars { get; init; } = 8000;
    public int MaxSnapshotLines { get; init; } = 300;

    public static AdoOptions FromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable("AZDO_PAT") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Environment variable AZDO_PAT is not set. Please configure a valid Azure DevOps Personal Access Token.");
        }

        return new AdoOptions
        {
            PersonalAccessToken = token,
            DefaultOrganization = Environment.GetEnvironmentVariable("AZDO_DEFAULT_ORG") ?? string.Empty,
            DefaultProject = Environment.GetEnvironmentVariable("AZDO_DEFAULT_PROJECT") ?? string.Empty,
            ApiVersion = Environment.GetEnvironmentVariable("AZDO_API_VERSION") ?? "7.1-preview",
            MaxCandidates = ParseInt("AZDO_MAX_CANDIDATES", 10, 1, 50),
            MaxThreads = ParseInt("AZDO_MAX_THREADS", 20, 1, 100),
            MaxChanges = ParseInt("AZDO_MAX_CHANGES", 200, 10, 2000),
            MaxRepositoryScan = ParseInt("AZDO_MAX_REPOSITORY_SCAN", 30, 5, 200),
            MaxDiffChars = ParseInt("AZDO_MAX_DIFF_CHARS", 8000, 1000, 50000),
            MaxSnapshotLines = ParseInt("AZDO_MAX_SNAPSHOT_LINES", 300, 50, 2000)
        };
    }

    private static int ParseInt(string key, int defaultValue, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (!int.TryParse(raw, out var value))
        {
            return defaultValue;
        }

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
