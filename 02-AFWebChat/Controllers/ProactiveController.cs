using AFWebChat.Bot;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AFWebChat.Controllers;

/// <summary>
/// API controller for proactive messaging / notifications management.
/// Stores conversation references from bot interactions and provides endpoints
/// to send proactive notifications to connected users.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProactiveController : ControllerBase
{
    private readonly ConversationReferenceStore _conversationStore;
    private readonly ILogger<ProactiveController> _logger;

    // In-memory notification log for demo purposes
    private static readonly List<NotificationLog> _notificationHistory = [];
    private static readonly object _historyLock = new();

    public ProactiveController(
        ConversationReferenceStore conversationStore,
        ILogger<ProactiveController> logger)
    {
        _conversationStore = conversationStore;
        _logger = logger;
    }

    /// <summary>
    /// Lists all stored conversation references.
    /// </summary>
    [HttpGet("conversations")]
    public ActionResult<IReadOnlyList<ConversationSummary>> GetConversations()
    {
        var conversations = _conversationStore.GetAll()
            .Select(c => new ConversationSummary(
                c.Key,
                c.UserName,
                c.ChannelId,
                c.LastActivity))
            .ToList();

        return Ok(conversations);
    }

    /// <summary>
    /// Sends a proactive notification to a specific conversation.
    /// Uses ContinueConversationAsync for real push — no need for the user to write first.
    /// </summary>
    [HttpPost("notify")]
    public async Task<IActionResult> SendNotification(
        [FromBody] NotificationRequest request,
        [FromServices] TeamsBotAgent botAgent)
    {
        if (string.IsNullOrEmpty(request.ConversationKey) && !request.SendToAll)
            return BadRequest("ConversationKey is required when SendToAll is false.");

        if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.Message))
            return BadRequest("Title and Message are required.");

        if (request.Message.Length > 4096)
            return BadRequest("Message too long. Maximum 4096 characters.");

        var targets = request.SendToAll
            ? _conversationStore.GetAll()
            : _conversationStore.Get(request.ConversationKey!) is { } conv
                ? new[] { conv }
                : Array.Empty<StoredConversation>();

        if (!targets.Any())
            return NotFound("No matching conversations found.");

        var results = new List<NotificationResult>();

        foreach (var target in targets)
        {
            try
            {
                // Use ContinueConversationAsync for real push — message arrives immediately
                await botAgent.SendProactiveNotificationAsync(
                    request.Title, request.Message, request.Severity ?? "info",
                    target.Reference, CancellationToken.None);

                results.Add(new NotificationResult(target.Key, target.UserName, true, null));
                _logger.LogInformation("Proactive notification PUSHED to {User}: {Title}",
                    target.UserName, request.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push notification to {Key}", target.Key);
                results.Add(new NotificationResult(target.Key, target.UserName, false, ex.Message));
            }
        }

        // Log to history
        lock (_historyLock)
        {
            _notificationHistory.Insert(0, new NotificationLog(
                request.Title, request.Message, request.Severity ?? "info",
                DateTime.UtcNow, results.Count(r => r.Success), results.Count(r => !r.Success)));

            // Keep only last 50 entries
            if (_notificationHistory.Count > 50)
                _notificationHistory.RemoveRange(50, _notificationHistory.Count - 50);
        }

        return Ok(new
        {
            Sent = results.Count(r => r.Success),
            Failed = results.Count(r => !r.Success),
            Results = results
        });
    }

    /// <summary>
    /// Sends a simple text notification to all conversations (real push).
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast(
        [FromBody] BroadcastRequest request,
        [FromServices] TeamsBotAgent botAgent)
    {
        if (string.IsNullOrEmpty(request.Message))
            return BadRequest("Message is required.");

        if (request.Message.Length > 4096)
            return BadRequest("Message too long. Maximum 4096 characters.");

        var conversations = _conversationStore.GetAll();
        if (conversations.Count == 0)
            return NotFound("No conversations stored. Users must first interact with the bot.");

        int sent = 0, failed = 0;

        foreach (var conv in conversations)
        {
            try
            {
                await botAgent.SendProactiveNotificationAsync(
                    "Broadcast", request.Message, "info",
                    conv.Reference, CancellationToken.None);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast to {Key}", conv.Key);
                failed++;
            }
        }

        return Ok(new { Sent = sent, Failed = failed, Total = conversations.Count });
    }

    /// <summary>
    /// Gets pending notifications for a conversation (polled by the bot on each turn).
    /// </summary>
    [HttpGet("pending/{conversationKey}")]
    public ActionResult<IReadOnlyList<PendingNotification>> GetPending(string conversationKey)
    {
        var pending = PendingNotificationStore.DequeueAll(conversationKey);
        return Ok(pending);
    }

    /// <summary>
    /// Gets notification history.
    /// </summary>
    [HttpGet("history")]
    public ActionResult<IReadOnlyList<NotificationLog>> GetHistory()
    {
        lock (_historyLock)
        {
            return Ok(_notificationHistory.ToList());
        }
    }
}

// ---- Pending notification store ----
/// <summary>
/// Thread-safe in-memory store for pending proactive notifications.
/// Notifications are queued here and dequeued by the bot on the next turn,
/// or by the proactive endpoint when configured.
/// </summary>
public static class PendingNotificationStore
{
    private static readonly Dictionary<string, Queue<PendingNotification>> _queues = new();
    private static readonly object _lock = new();

    public static void Enqueue(PendingNotification notification)
    {
        lock (_lock)
        {
            if (!_queues.TryGetValue(notification.ConversationKey, out var queue))
            {
                queue = new Queue<PendingNotification>();
                _queues[notification.ConversationKey] = queue;
            }
            queue.Enqueue(notification);
        }
    }

    public static List<PendingNotification> DequeueAll(string conversationKey)
    {
        lock (_lock)
        {
            if (_queues.TryGetValue(conversationKey, out var queue) && queue.Count > 0)
            {
                var results = queue.ToList();
                queue.Clear();
                return results;
            }
            return [];
        }
    }
}

// ---- Models ----

public record NotificationRequest(
    string? ConversationKey,
    string Title,
    string Message,
    string? Severity = null,
    bool SendToAll = false);

public record BroadcastRequest(string Message);

public record ConversationSummary(
    string Key,
    string UserName,
    string ChannelId,
    DateTime LastActivity);

public record NotificationResult(
    string ConversationKey,
    string UserName,
    bool Success,
    string? Error);

public record NotificationLog(
    string Title,
    string Message,
    string Severity,
    DateTime Timestamp,
    int Sent,
    int Failed);

public class PendingNotification
{
    public required string Id { get; init; }
    public required string ConversationKey { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? Severity { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required ConversationReference Reference { get; init; }
}
