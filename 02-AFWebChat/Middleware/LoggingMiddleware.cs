using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Middleware;

public static class LoggingMiddleware
{
    public static async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var logger = GetLogger(innerAgent);
        logger.LogInformation("Agent {AgentName} - RunAsync started", innerAgent.Name);
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
            logger.LogInformation("Agent {AgentName} - RunAsync completed in {ElapsedMs}ms",
                innerAgent.Name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent {AgentName} - RunAsync failed after {ElapsedMs}ms",
                innerAgent.Name, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private static ILogger GetLogger(AIAgent agent)
    {
        // Use a simple console logger as fallback
        return LoggerFactory.Create(b => b.AddConsole()).CreateLogger("AgentMiddleware");
    }
}
