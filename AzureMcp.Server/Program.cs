using AzureMcp.Server.Ado;
using AzureMcp.Server.Configuration;
using AzureMcp.Server.Protocol;
using AzureMcp.Server.Tools;

try
{
    var options = AdoOptions.FromEnvironment();
    var httpClient = new HttpClient();
    var adoClient = new AzureDevOpsClient(httpClient, options);
    var toolService = new PrToolService(adoClient, options);
    var server = new McpServer("azure-devops-pr-review", "0.1.0", toolService);

    await server.RunAsync();
}
catch (Exception ex)
{
    // Write startup failures to stderr only — never to stdout which carries MCP messages.
    await Console.Error.WriteLineAsync($"[AzureMcp] Fatal startup error: {ex}");
    Environment.Exit(1);
}
