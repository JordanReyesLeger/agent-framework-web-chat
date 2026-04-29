namespace AFWebChat.Models;

public record SessionInfo(
    string Id,
    string AgentName,
    DateTime CreatedAt,
    int MessageCount);

public record WorkflowInfo(
    string Name,
    string Description,
    string[] Agents,
    string Pattern);

/// <summary>
/// Represents a built-in AF multi-agent orchestration (Sequential, Concurrent, GroupChat, Handoff).
/// </summary>
public record OrchestrationInfo(
    string Name,
    string Description,
    string[] Agents,
    string OrchestrationPattern);

public record DocumentInfo(
    string Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAt,
    string Status);

public record ApprovalRequest(
    string RequestId,
    string ToolName,
    Dictionary<string, object?> Arguments);

public record ApprovalResponse(
    string RequestId,
    bool Approved);
