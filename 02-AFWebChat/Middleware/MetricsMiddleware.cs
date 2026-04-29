using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Middleware;

public static class MetricsMiddleware
{
    private static readonly ConcurrentDictionary<string, AgentMetrics> _metrics = new();

    public static async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var metrics = _metrics.GetOrAdd(innerAgent.Name ?? "unknown", _ => new AgentMetrics());
        metrics.InvocationCount++;

        try
        {
            var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
            metrics.LastLatencyMs = sw.ElapsedMilliseconds;
            metrics.TotalLatencyMs += sw.ElapsedMilliseconds;
            return response;
        }
        catch
        {
            metrics.ErrorCount++;
            throw;
        }
    }

    public static IReadOnlyDictionary<string, AgentMetrics> GetAllMetrics() => _metrics;
}

public class AgentMetrics
{
    public long InvocationCount { get; set; }
    public long ErrorCount { get; set; }
    public long LastLatencyMs { get; set; }
    public long TotalLatencyMs { get; set; }
    public double AverageLatencyMs => InvocationCount > 0 ? (double)TotalLatencyMs / InvocationCount : 0;
}
