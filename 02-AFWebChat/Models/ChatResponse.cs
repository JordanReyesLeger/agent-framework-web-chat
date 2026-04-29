namespace AFWebChat.Models;

public record ChatResponse(
    string SessionId,
    string AgentName,
    string Text,
    DateTime Timestamp);
