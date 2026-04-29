using System.Text.Json;
using AFWebChat.Models;

namespace AFWebChat.Services;

public class StreamEventService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string FormatSSE(StreamEvent evt)
    {
        var data = JsonSerializer.Serialize(evt, JsonOptions);
        return $"event: {evt.EventType}\ndata: {data}\n\n";
    }

    public static StreamEvent AgentToken(string agentName, string text)
        => new("agent-token", agentName, text, null);

    public static StreamEvent ToolCall(string agentName, string toolName, object? args)
        => new("tool-call", agentName, null, new { toolName, args });

    public static StreamEvent ToolResult(string agentName, string toolName, object? result)
        => new("tool-result", agentName, null, new { toolName, result });

    public static StreamEvent ToolApproval(string agentName, string requestId, string toolName, object? args)
        => new("tool-approval", agentName, null, new { requestId, toolName, args });

    public static StreamEvent AgentStart(string agentName)
        => new("agent-start", agentName, null, null);

    public static StreamEvent AgentComplete(string agentName)
        => new("agent-complete", agentName, null, null);

    public static StreamEvent WorkflowStep(string executorName, string status)
        => new("workflow-step", null, null, new { executorName, status });

    public static StreamEvent WorkflowOutput(string result)
        => new("workflow-output", null, result, null);

    public static StreamEvent Error(string message)
        => new("error", null, message, null);

    public static StreamEvent Done()
        => new("done", null, null, null);
}
