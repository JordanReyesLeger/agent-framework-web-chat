namespace AFWebChat.Models;

public record AgentInfo(
    string Name,
    string Description,
    string Category,
    string Icon,
    string Color,
    string[] Tools,
    string[] ContextProviders,
    string[] ExamplePrompts,
    bool SupportsStreaming,
    bool SupportsStructuredOutput);
