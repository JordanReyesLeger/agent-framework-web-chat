namespace AFWebChat.Models;

public record ChatRequest(
    string SessionId,
    string Message,
    string AgentName,
    string? WorkflowName = null,
    string? OrchestrationName = null,
    string[]? AttachmentUrls = null,
    string[]? CustomAgents = null,
    string? CustomPattern = null);
