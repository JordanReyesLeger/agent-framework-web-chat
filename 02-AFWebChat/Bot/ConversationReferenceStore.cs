using Microsoft.Agents.Core.Models;
using System.Collections.Concurrent;

namespace AFWebChat.Bot;

/// <summary>
/// Thread-safe in-memory store for conversation references.
/// Used for proactive messaging — stores references when users interact with the bot,
/// then retrieves them to send messages proactively.
/// </summary>
public class ConversationReferenceStore
{
    private readonly ConcurrentDictionary<string, StoredConversation> _conversations = new();

    /// <summary>
    /// Stores or updates a conversation reference from an incoming activity.
    /// </summary>
    public void AddOrUpdate(IActivity activity)
    {
        var reference = new ConversationReference
        {
            Agent = activity.Recipient,
            Conversation = activity.Conversation,
            ServiceUrl = activity.ServiceUrl,
            ChannelId = activity.ChannelId ?? "unknown"
        };

        var key = GetKey(activity);
        _conversations[key] = new StoredConversation
        {
            Key = key,
            Reference = reference,
            UserName = activity.From?.Name ?? "Unknown",
            UserId = activity.From?.Id ?? "",
            ChannelId = activity.ChannelId ?? "unknown",
            LastActivity = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets all stored conversation references.
    /// </summary>
    public IReadOnlyList<StoredConversation> GetAll()
        => _conversations.Values.ToList().AsReadOnly();

    /// <summary>
    /// Gets a specific conversation reference by key.
    /// </summary>
    public StoredConversation? Get(string key)
        => _conversations.TryGetValue(key, out var conv) ? conv : null;

    /// <summary>
    /// Removes a conversation reference.
    /// </summary>
    public bool Remove(string key)
        => _conversations.TryRemove(key, out _);

    private static string GetKey(IActivity activity)
        => activity.Conversation?.Id ?? activity.From?.Id ?? Guid.NewGuid().ToString();
}

public class StoredConversation
{
    public required string Key { get; init; }
    public required ConversationReference Reference { get; init; }
    public required string UserName { get; init; }
    public required string UserId { get; init; }
    public required string ChannelId { get; init; }
    public required DateTime LastActivity { get; init; }
}
