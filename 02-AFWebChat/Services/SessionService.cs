using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFWebChat.Models;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Azure.Cosmos;

namespace AFWebChat.Services;

/// <summary>
/// Session store with an in-memory cache backed by Cosmos DB (when configured) so
/// conversations survive app restarts/scale-outs. Falls back to in-memory only if
/// CosmosDB:AccountEndpoint is not set — Cosmos failures are logged, never thrown,
/// so chat keeps working even if persistence is degraded.
/// </summary>
public class SessionService
{
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private readonly ILogger<SessionService> _logger;
    private readonly Container? _container;

    public SessionService(IConfiguration config, ILogger<SessionService> logger)
    {
        _logger = logger;

        var accountEndpoint = config["CosmosDB:AccountEndpoint"];
        var databaseName = config["CosmosDB:DatabaseName"] ?? "af-webchat";
        var containerName = config["CosmosDB:ContainerName"] ?? "sessions";

        if (string.IsNullOrEmpty(accountEndpoint))
        {
            _logger.LogWarning("CosmosDB:AccountEndpoint not configured — sessions are in-memory only and will not survive a restart.");
            return;
        }

        var clientOptions = new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
        };

        var client = new CosmosClient(accountEndpoint, new DefaultAzureCredential(), clientOptions);
        _container = client.GetContainer(databaseName, containerName);
        _logger.LogInformation("Cosmos DB session persistence enabled ({Database}/{Container})", databaseName, containerName);
    }

    public async Task<AgentSession> GetOrCreateSessionAsync(string sessionId, AIAgent agent)
    {
        if (_sessions.TryGetValue(sessionId, out var data))
        {
            _logger.LogDebug("Restoring session {SessionId} from memory", sessionId);
            return await agent.DeserializeSessionAsync(data.SerializedState);
        }

        var fromCosmos = await TryLoadFromCosmosAsync(sessionId);
        if (fromCosmos is not null)
        {
            _sessions.TryAdd(sessionId, fromCosmos);
            _logger.LogInformation("Restored session {SessionId} from Cosmos DB", sessionId);
            return await agent.DeserializeSessionAsync(fromCosmos.SerializedState);
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
        _logger.LogDebug("Saved session {SessionId} in memory", sessionId);

        await TrySaveToCosmosAsync(sessionId, data);
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
        var removed = _sessions.TryRemove(sessionId, out _);
        _ = TryDeleteFromCosmosAsync(sessionId);
        return removed;
    }

    private async Task<SessionData?> TryLoadFromCosmosAsync(string sessionId)
    {
        if (_container is null) return null;

        try
        {
            var response = await _container.ReadItemAsync<SessionDocument>(sessionId, new PartitionKey(sessionId));
            var doc = response.Resource;
            return new SessionData
            {
                AgentName = doc.AgentName,
                SerializedState = JsonDocument.Parse(doc.SerializedState).RootElement,
                CreatedAt = doc.CreatedAt,
                LastActivityAt = doc.LastActivityAt,
                MessageCount = doc.MessageCount
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read session {SessionId} from Cosmos DB; continuing without it", sessionId);
            return null;
        }
    }

    private async Task TrySaveToCosmosAsync(string sessionId, SessionData data)
    {
        if (_container is null) return;

        try
        {
            var doc = new SessionDocument
            {
                Id = sessionId,
                SessionId = sessionId,
                AgentName = data.AgentName,
                SerializedState = data.SerializedState.GetRawText(),
                CreatedAt = data.CreatedAt,
                LastActivityAt = data.LastActivityAt,
                MessageCount = data.MessageCount
            };
            await _container.UpsertItemAsync(doc, new PartitionKey(sessionId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist session {SessionId} to Cosmos DB", sessionId);
        }
    }

    private async Task TryDeleteFromCosmosAsync(string sessionId)
    {
        if (_container is null) return;

        try
        {
            await _container.DeleteItemAsync<SessionDocument>(sessionId, new PartitionKey(sessionId));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // already gone
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete session {SessionId} from Cosmos DB", sessionId);
        }
    }

    private class SessionData
    {
        public string AgentName { get; set; } = "";
        public JsonElement SerializedState { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public int MessageCount { get; set; }
    }

    private class SessionDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        public string SessionId { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string SerializedState { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public int MessageCount { get; set; }
    }
}
