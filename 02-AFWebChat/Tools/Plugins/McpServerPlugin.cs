using ModelContextProtocol.Client;
using System.ComponentModel;
using System.Text.Json;

namespace AFWebChat.Tools.Plugins;

/// <summary>
/// Plugin that connects to and uses tools from an MCP (Model Context Protocol) server.
/// Supports Stdio transport (for local servers) and HTTP transport (for remote servers).
/// </summary>
public class McpServerPlugin
{
    private readonly ILogger<McpServerPlugin> _logger;
    private readonly IConfiguration _configuration;
    private McpClient? _mcpClient;
    private IList<McpClientTool>? _mcpTools;

    public McpServerPlugin(IConfiguration configuration, ILogger<McpServerPlugin> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the MCP client connection.
    /// </summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var mcpServerName = _configuration["McpServer:Name"];
            var mcpServerLocation = _configuration["McpServer:Location"];
            var mcpServerCommand = _configuration["McpServer:Command"];
            var mcpServerArguments = _configuration["McpServer:Arguments"];

            if (string.IsNullOrEmpty(mcpServerLocation) && string.IsNullOrEmpty(mcpServerCommand))
            {
                _logger.LogWarning("MCP Server not configured. MCP functionality will not be available.");
                return false;
            }

            _logger.LogInformation("Connecting to MCP Server: {Name}", mcpServerName ?? "MCP Server");

            // Try Stdio transport first (for local servers)
            if (!string.IsNullOrEmpty(mcpServerCommand))
            {
                var transportOptions = new StdioClientTransportOptions
                {
                    Command = mcpServerCommand,
                    Arguments = string.IsNullOrEmpty(mcpServerArguments)
                        ? Array.Empty<string>()
                        : mcpServerArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                    Name = mcpServerName ?? "MCP Server"
                };

                // Add GitHub token as environment variable if configured
                var gitHubToken = _configuration["McpServer:GitHubToken"];
                if (!string.IsNullOrEmpty(gitHubToken))
                {
                    transportOptions.EnvironmentVariables = new Dictionary<string, string>
                    {
                        ["GITHUB_PERSONAL_ACCESS_TOKEN"] = gitHubToken
                    };
                    _logger.LogInformation("GitHub token configured for MCP Server");
                }

                var transport = new StdioClientTransport(transportOptions);
                _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            }
            else if (!string.IsNullOrEmpty(mcpServerLocation))
            {
                // Try HTTP transport (for remote servers)
                var transport = new HttpClientTransport(new()
                {
                    Endpoint = new Uri(mcpServerLocation),
                    Name = mcpServerName ?? "MCP Server"
                });

                _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            }

            if (_mcpClient == null)
            {
                _logger.LogWarning("Failed to create MCP client");
                return false;
            }

            // List and cache available tools from the MCP server
            _mcpTools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Successfully connected to MCP Server. Available tools: {ToolCount}", _mcpTools.Count);

            foreach (var tool in _mcpTools)
            {
                _logger.LogInformation("MCP tool available: {ToolName}", tool.Name);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MCP Server connection");
            return false;
        }
    }

    /// <summary>
    /// Gets all available MCP tools as AITool instances for use with Microsoft.Extensions.AI agents.
    /// McpClientTool implements AITool, so they can be used directly.
    /// </summary>
    public IList<McpClientTool> GetMcpTools()
    {
        return _mcpTools ?? [];
    }

    [Description("Lists all available tools from the MCP server")]
    public async Task<string> ListMcpTools(CancellationToken cancellationToken = default)
    {
        if (_mcpClient == null)
        {
            return "MCP Server is not connected. Please ensure the MCP server is configured and accessible.";
        }

        try
        {
            var tools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            if (tools.Count == 0)
            {
                return "No tools are available from the MCP server.";
            }

            var response = $"Available MCP tools ({tools.Count}):\n\n";
            for (int i = 0; i < tools.Count; i++)
            {
                response += $"{i + 1}. **{tools[i].Name}**\n   Description: {tools[i].Description}\n\n";
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing MCP tools");
            return $"Error listing MCP tools: {ex.Message}";
        }
    }

    [Description("Calls a specific tool from the MCP server by name with JSON parameters")]
    public async Task<string> CallMcpTool(
        [Description("The name of the MCP tool to call")] string toolName,
        [Description("JSON string with the parameters for the tool")] string parametersJson = "{}",
        CancellationToken cancellationToken = default)
    {
        if (_mcpClient == null)
        {
            return "MCP Server is not connected. Please ensure the MCP server is configured and accessible.";
        }

        try
        {
            _logger.LogInformation("Calling MCP tool: {ToolName} with params: {Params}", toolName, parametersJson);

            var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(parametersJson)
                ?? new Dictionary<string, object?>();

            var result = await _mcpClient.CallToolAsync(toolName, args, cancellationToken: cancellationToken);

            var responseText = string.Empty;
            foreach (var content in result.Content)
            {
                if (content is ModelContextProtocol.Protocol.TextContentBlock textBlock)
                    responseText += textBlock.Text;
            }

            if (string.IsNullOrEmpty(responseText))
            {
                return "The MCP tool returned no text content.";
            }

            _logger.LogInformation("MCP tool {ToolName} executed successfully", toolName);
            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool: {ToolName}", toolName);
            return $"Error calling MCP tool '{toolName}': {ex.Message}";
        }
    }

    /// <summary>
    /// Cleans up the MCP client connection.
    /// </summary>
    public void Cleanup()
    {
        _mcpClient = null;
        _mcpTools = null;
        _logger.LogInformation("MCP Server connection cleaned up");
    }
}
