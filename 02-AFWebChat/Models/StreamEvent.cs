namespace AFWebChat.Models;

public record StreamEvent(
    string EventType,
    string? AgentName,
    string? Text,
    object? Data);
