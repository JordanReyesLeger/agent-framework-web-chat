using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Middleware;

public static class AuditMiddleware
{
    private static readonly ConcurrentQueue<AuditEntry> _auditLog = new();
    private const int MaxEntries = 1000;

    public static async ValueTask<object?> FunctionCallMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var entry = new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentName = agent.Name ?? "unknown",
            ToolName = context.Function.Name,
            Arguments = context.Arguments?.ToString(),
            Action = "FunctionCall"
        };

        var result = await next(context, cancellationToken);

        entry.Result = result?.ToString();
        AddEntry(entry);

        return result;
    }

    public static IEnumerable<AuditEntry> GetRecentEntries(int count = 50)
        => _auditLog.TakeLast(count);

    private static void AddEntry(AuditEntry entry)
    {
        _auditLog.Enqueue(entry);
        while (_auditLog.Count > MaxEntries)
            _auditLog.TryDequeue(out _);
    }
}

public class AuditEntry
{
    public DateTime Timestamp { get; set; }
    public string AgentName { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string? Arguments { get; set; }
    public string? Result { get; set; }
    public string Action { get; set; } = "";
}
