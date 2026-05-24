using System.Text.Json;
using AzureMcp.Server.Tools;

namespace AzureMcp.Server.Protocol;

public sealed class McpServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _serverName;
    private readonly string _serverVersion;
    private readonly PrToolService _toolService;

    public McpServer(string serverName, string serverVersion, PrToolService toolService)
    {
        _serverName = serverName;
        _serverVersion = serverVersion;
        _toolService = toolService;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var io = new StdioMessageIO(Console.OpenStandardInput(), Console.OpenStandardOutput());

        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await io.ReadMessageAsync(cancellationToken);
            if (message is null)
            {
                break;
            }

            JsonRpcRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<JsonRpcRequest>(message, JsonOptions);
            }
            catch (Exception ex)
            {
                var parseError = new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = -32700, Message = "Parse error", Data = ex.Message }
                };
                await io.WriteMessageAsync(JsonSerializer.Serialize(parseError, JsonOptions), cancellationToken);
                continue;
            }

            if (request is null)
            {
                continue;
            }

            if (request.Id is null)
            {
                if (request.Method == "notifications/initialized")
                {
                    continue;
                }

                continue;
            }

            var response = await HandleRequestAsync(request, cancellationToken);
            await io.WriteMessageAsync(JsonSerializer.Serialize(response, JsonOptions), cancellationToken);
        }
    }

    private async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            object? result = request.Method switch
            {
                "initialize" => new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { tools = new { listChanged = false } },
                    serverInfo = new { name = _serverName, version = _serverVersion }
                },
                "tools/list" => new { tools = _toolService.GetToolDefinitions() },
                "tools/call" => await HandleToolCallAsync(request.Params, cancellationToken),
                _ => throw new InvalidOperationException($"Method not found: {request.Method}")
            };

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32603,
                    Message = ex.Message
                }
            };
        }
    }

    private async Task<object> HandleToolCallAsync(JsonElement parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetProperty("name", out var toolNameNode))
        {
            throw new InvalidOperationException("Missing tool name.");
        }

        var toolName = toolNameNode.GetString();
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new InvalidOperationException("Tool name is empty.");
        }

        using var emptyDoc = JsonDocument.Parse("{}");
        var arguments = parameters.TryGetProperty("arguments", out var args)
            ? args
            : emptyDoc.RootElement;

        return await _toolService.ExecuteAsync(toolName, arguments, cancellationToken);
    }
}
