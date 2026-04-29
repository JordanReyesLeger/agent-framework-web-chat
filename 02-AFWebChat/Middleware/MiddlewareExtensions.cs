using Microsoft.Agents.AI;

namespace AFWebChat.Middleware;

public static class MiddlewareExtensions
{
    public static AIAgent WithFullMiddleware(this AIAgent agent, ILogger logger)
    {
        return agent.AsBuilder()
            .Use(LoggingMiddleware.RunAsync, null)
            .Use(MetricsMiddleware.RunAsync, null)
            .Use(AuditMiddleware.FunctionCallMiddleware)
            .Build();
    }
}
