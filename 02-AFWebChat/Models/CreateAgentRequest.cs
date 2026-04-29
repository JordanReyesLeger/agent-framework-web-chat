namespace AFWebChat.Models;

public class CreateAgentRequest
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string Category { get; set; } = "Custom";
    public string Icon { get; set; } = "🤖";
    public string Color { get; set; } = "#0078d4";
    public string Instructions { get; set; } = "You are a helpful AI assistant.";
    public string[] Tools { get; set; } = [];
    public string[] ExamplePrompts { get; set; } = [];
}
