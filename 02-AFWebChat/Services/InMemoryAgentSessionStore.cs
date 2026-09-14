using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;

namespace AFWebChat.Services;

/// <summary>
/// Store en memoria de sesiones para los agentes publicados vía A2A.
/// El hosting A2A lo usa para asociar el <c>contextId</c> del protocolo con una
/// <see cref="AgentSession"/>, de modo que las conversaciones multi-turno mantienen historial.
/// Sin un store registrado el Agent Framework usa <c>NoopAgentSessionStore</c> y cada mensaje
/// A2A empieza una conversación nueva.
/// </summary>
/// <remarks>
/// Los endpoints A2A no requieren autenticación, así que el número de sesiones se acota para
/// evitar crecimiento ilimitado de memoria; al llegar al límite se descarta la menos usada.
/// Para producción multi-instancia conviene sustituirlo por un store distribuido (Redis/Cosmos).
/// </remarks>
public sealed class InMemoryAgentSessionStore : AgentSessionStore
{
    private const int MaxSessions = 1000;

    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);

    public override async ValueTask SaveSessionAsync(
        AIAgent agent, string conversationId, AgentSession session, CancellationToken cancellationToken = default)
    {
        var state = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
        _sessions[conversationId] = new Entry(state, DateTimeOffset.UtcNow);

        if (_sessions.Count > MaxSessions)
        {
            EvictOldest();
        }
    }

    public override async ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent, string conversationId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(conversationId, out var entry))
        {
            _sessions[conversationId] = entry with { LastAccessUtc = DateTimeOffset.UtcNow };
            return await agent.DeserializeSessionAsync(entry.State, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    private void EvictOldest()
    {
        foreach (var key in _sessions.OrderBy(kv => kv.Value.LastAccessUtc)
                                     .Take(_sessions.Count - MaxSessions)
                                     .Select(kv => kv.Key))
        {
            _sessions.TryRemove(key, out _);
        }
    }

    private readonly record struct Entry(JsonElement State, DateTimeOffset LastAccessUtc);
}
