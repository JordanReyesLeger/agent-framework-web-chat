using System.Collections.Concurrent;
using System.Text.Json;
using AFWebChat.Models;
using Microsoft.Agents.AI;

namespace AFWebChat.Services;

public class SessionService
{
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private readonly ILogger<SessionService> _logger;

    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
    }

    public async Task<AgentSession> GetOrCreateSessionAsync(string sessionId, AIAgent agent)
    {
        if (_sessions.TryGetValue(sessionId, out var data))
        {
            _logger.LogDebug("Restoring session {SessionId}", sessionId);
            return await agent.DeserializeSessionAsync(data.SerializedState);
        }

        _logger.LogInformation("Creating new session {SessionId} for agent {AgentName}", sessionId, agent.Name);
        var session = await agent.CreateSessionAsync();
        return session;
    }

    public async Task SaveSessionAsync(string sessionId, AgentSession session, AIAgent agent, string agentName)
    {
        var serialized = await agent.SerializeSessionAsync(session);
        var data = new SessionData
        {
            AgentName = agentName,
            SerializedState = serialized,
            CreatedAt = _sessions.TryGetValue(sessionId, out var existing)
                ? existing.CreatedAt
                : DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            MessageCount = (_sessions.TryGetValue(sessionId, out var prev) ? prev.MessageCount : 0) + 1
        };

        _sessions.AddOrUpdate(sessionId, data, (_, _) => data);
        _logger.LogDebug("Saved session {SessionId}", sessionId);
    }

    public List<SessionInfo> GetAllSessions()
    {
        return _sessions.Select(kv => new SessionInfo(
            kv.Key,
            kv.Value.AgentName,
            kv.Value.CreatedAt,
            kv.Value.MessageCount
        )).ToList();
    }

    public SessionInfo? GetSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var data))
        {
            return new SessionInfo(sessionId, data.AgentName, data.CreatedAt, data.MessageCount);
        }
        return null;
    }

    public bool DeleteSession(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    private class SessionData
    {
        public string AgentName { get; set; } = "";
        public JsonElement SerializedState { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public int MessageCount { get; set; }
    }
}
