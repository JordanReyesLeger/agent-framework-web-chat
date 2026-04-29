using AFWebChat.Models;
using Microsoft.Agents.AI;

namespace AFWebChat.Agents;

public class AgentDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string Icon { get; init; }
    public string Color { get; init; } = "#0078d4";
    public string[] Tools { get; init; } = [];
    public string[] ContextProviders { get; init; } = [];
    public string[] ExamplePrompts { get; init; } = ["What can you help me with?", "Tell me about your capabilities", "Give me an example of what you do"];
    public bool SupportsStreaming { get; init; } = true;
    public bool SupportsStructuredOutput { get; init; } = false;
    public required Func<IServiceProvider, AIAgent> Factory { get; init; }

    public AgentInfo ToAgentInfo() => new(
        Name, Description, Category, Icon, Color,
        Tools, ContextProviders, ExamplePrompts,
        SupportsStreaming, SupportsStructuredOutput);
}
