using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Mcp;

public static class McpToolsAgent
{
    public const string Name = "McpTools";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente que usa herramientas de servidores MCP (Model Context Protocol) externos.",
        Category = "MCP",
        Icon = "🔌",
        Color = "#2c3e50",
        ExamplePrompts = ["¿Qué herramientas MCP están disponibles?", "Usa una herramienta externa para ayudarme", "Conéctate a un servidor MCP"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var mcpPlugin = sp.GetRequiredService<McpServerPlugin>();

            // Load MCP tools dynamically from configured MCP server
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(mcpPlugin));

            // Also add actual MCP tools if they were initialized
            var mcpTools = mcpPlugin.GetMcpTools();
            if (mcpTools.Count > 0)
            {
                tools.AddRange(mcpTools);
            }

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un agente que puede usar herramientas de servidores MCP externos.
                    Las herramientas disponibles se cargan dinámicamente desde los endpoints de servidores MCP configurados.
                    Usa las herramientas disponibles para ayudar al usuario con sus solicitudes.
                    Si no hay herramientas MCP configuradas, informa al usuario.
                    Puedes usar ListMcpTools para ver las herramientas disponibles y CallMcpTool para invocar una herramienta específica.
                    """,
                tools: tools);
        }
    };
}
