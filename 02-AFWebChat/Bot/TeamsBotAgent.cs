using System.Text.Json;
using AFWebChat.Agents;
using AFWebChat.Controllers;
using AFWebChat.Models;
using AFWebChat.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace AFWebChat.Bot;

/// <summary>
/// Bot Framework AgentApplication that bridges Teams/WebChat messages
/// to the existing AF-WebChat orchestration pipeline.
/// Uses Adaptive Cards for rich UI and supports proactive messaging.
/// </summary>
[Agent(name: "AFWebChatBot", description: "AF-WebChat multi-agent bot for Teams and WebChat", version: "1.0")]
public class TeamsBotAgent : AgentApplication
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentRegistry _agentRegistry;
    private readonly ConversationReferenceStore _conversationStore;
    private readonly SessionService _sessionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TeamsBotAgent> _logger;

    private const string DefaultAgentName = "GeneralAssistant";

    // Track processed activity IDs to prevent duplicate processing (Teams retries)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _processedActivities = new();

    public TeamsBotAgent(
        AgentApplicationOptions options,
        IServiceProvider serviceProvider,
        AgentRegistry agentRegistry,
        ConversationReferenceStore conversationStore,
        SessionService sessionService,
        IConfiguration configuration,
        ILogger<TeamsBotAgent> logger) : base(options)
    {
        _serviceProvider = serviceProvider;
        _agentRegistry = agentRegistry;
        _conversationStore = conversationStore;
        _sessionService = sessionService;
        _configuration = configuration;
        _logger = logger;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        // Store conversation reference for proactive messaging (manual store + built-in)
        _conversationStore.AddOrUpdate(turnContext.Activity);

        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                // Send welcome Adaptive Card
                var welcomeCard = AdaptiveCardBuilder.CreateWelcomeCard();
                var reply = MessageFactory.Attachment(welcomeCard);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(
        ITurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        // Store/update conversation reference for proactive messaging
        _conversationStore.AddOrUpdate(turnContext.Activity);

        // Prevent duplicate processing (Teams can retry the same message)
        var activityId = turnContext.Activity.Id;
        if (!string.IsNullOrEmpty(activityId) && !_processedActivities.TryAdd(activityId, 0))
        {
            _logger.LogWarning("Duplicate activity {ActivityId} — skipping", activityId);
            return;
        }

        // Deliver any pending proactive notifications
        await DeliverPendingNotificationsAsync(turnContext, cancellationToken);

        // Check if this is an Adaptive Card submit action
        var text = turnContext.Activity.Text?.Trim();
        if (string.IsNullOrEmpty(text) && turnContext.Activity.Value != null)
        {
            text = ExtractCommandFromCardAction(turnContext.Activity.Value);
        }

        if (string.IsNullOrEmpty(text))
        {
            await turnContext.SendActivityAsync("Please send a text message or use a card button.",
                cancellationToken: cancellationToken);
            return;
        }

        // Handle slash commands
        if (text.StartsWith('/'))
        {
            await HandleCommandAsync(turnContext, text, cancellationToken);
            return;
        }

        // Handle acknowledgements from proactive notifications
        if (text == "ack")
        {
            await turnContext.SendActivityAsync("✅ Notification acknowledged.",
                cancellationToken: cancellationToken);
            return;
        }

        // Determine agent from conversation state or use default
        var agentName = GetAgentForConversation(turnContext) ?? DefaultAgentName;
        var sessionId = GetSessionId(turnContext, agentName);
        var agentDef = _agentRegistry.GetDefinition(agentName);
        var agentIcon = agentDef?.Icon ?? "🤖";

        _logger.LogInformation(
            "Teams message from {User} → Agent: {Agent}, Session: {Session}",
            turnContext.Activity.From?.Name ?? "unknown",
            agentName, sessionId);

        // Send typing indicator
        await turnContext.SendActivitiesAsync(
            [new Activity { Type = ActivityTypes.Typing }],
            cancellationToken);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orchestration = scope.ServiceProvider.GetRequiredService<AgentOrchestrationService>();

            var request = new ChatRequest(
                SessionId: sessionId,
                Message: text,
                AgentName: agentName);

            var responseText = new System.Text.StringBuilder();

            await foreach (var evt in orchestration.RunStreamingAsync(request))
            {
                if (evt.EventType == "agent-token" && !string.IsNullOrEmpty(evt.Text))
                    responseText.Append(evt.Text);
                else if (evt.EventType == "error" && !string.IsNullOrEmpty(evt.Text))
                    responseText.Append($"\n\n⚠️ {evt.Text}");
            }

            var finalText = responseText.ToString();
            if (string.IsNullOrWhiteSpace(finalText))
                finalText = "I processed your message but had no response to generate.";

            // Send response as Adaptive Card for rich formatting (Teams)
            // For M365 Copilot, send plain text only since it doesn't render cards
            var channelId = turnContext.Activity.ChannelId;
            if (channelId == "m365extensions")
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text(finalText), cancellationToken);
            }
            else
            {
                var responseCard = AdaptiveCardBuilder.CreateResponseCard(agentName, agentIcon, finalText);
                var reply = MessageFactory.Attachment(responseCard);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Teams message");
            await turnContext.SendActivityAsync(
                "❌ An error occurred while processing your message. Please try again.",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleCommandAsync(
        ITurnContext turnContext,
        string text,
        CancellationToken cancellationToken)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "/agents":
                var agents = _agentRegistry.GetAllAgentInfos();
                var agentListCard = AdaptiveCardBuilder.CreateAgentListCard(agents);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(agentListCard), cancellationToken);
                break;

            case "/agent":
                if (parts.Length < 2)
                {
                    var currentAgent = GetAgentForConversation(turnContext) ?? DefaultAgentName;
                    var currentDef = _agentRegistry.GetDefinition(currentAgent);
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(
                            AdaptiveCardBuilder.CreateAgentSwitchCard(
                                currentAgent,
                                currentDef?.Icon ?? "🤖",
                                $"Current agent. Use `/agent <name>` to switch.")),
                        cancellationToken);
                    break;
                }

                var agentName = parts[1].Trim();
                if (_agentRegistry.HasAgent(agentName))
                {
                    SetAgentForConversation(turnContext, agentName);
                    var agentInfo = _agentRegistry.GetDefinition(agentName);
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(
                            AdaptiveCardBuilder.CreateAgentSwitchCard(
                                agentName,
                                agentInfo?.Icon ?? "🤖",
                                agentInfo?.Description ?? "")),
                        cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        $"❌ Agent '{agentName}' not found. Use `/agents` to see available agents.",
                        cancellationToken: cancellationToken);
                }
                break;

            case "/help":
                var helpCard = AdaptiveCardBuilder.CreateHelpCard();
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(helpCard), cancellationToken);
                break;

            case "/clear":
                var clearAgent = GetAgentForConversation(turnContext) ?? DefaultAgentName;
                var clearSessionId = GetSessionId(turnContext, clearAgent);
                _sessionService.DeleteSession(clearSessionId);
                await turnContext.SendActivityAsync(
                    $"🗑️ Chat history cleared for **{clearAgent}**. The agent won't remember previous messages.",
                    cancellationToken: cancellationToken);
                break;

            case "/new":
                // Reset agent selection AND clear session
                var prevAgent = GetAgentForConversation(turnContext) ?? DefaultAgentName;
                var prevSessionId = GetSessionId(turnContext, prevAgent);
                _sessionService.DeleteSession(prevSessionId);
                lock (_lock) { _conversationAgents.Remove(turnContext.Activity.Conversation.Id); }
                await turnContext.SendActivityAsync(
                    $"🆕 New conversation started. Agent reset to **{DefaultAgentName}**. History cleared.",
                    cancellationToken: cancellationToken);
                break;

            default:
                await turnContext.SendActivityAsync(
                    $"Unknown command: `{command}`. Use `/help` for available commands.",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Extracts the command string from an Adaptive Card submit action.
    /// </summary>
    private static string? ExtractCommandFromCardAction(object? value)
    {
        if (value is JsonElement jsonElement)
        {
            if (jsonElement.TryGetProperty("command", out var cmdProp))
                return cmdProp.GetString();
        }
        else if (value is not null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("command", out var cmdProp))
                    return cmdProp.GetString();
            }
            catch { }
        }
        return null;
    }

    // ---- Conversation-scoped agent selection (in-memory) ----
    private static readonly Dictionary<string, string> _conversationAgents = new();
    private static readonly object _lock = new();

    private static string GetSessionId(ITurnContext turnContext, string agentName)
        => $"teams-{turnContext.Activity.Conversation.Id}-{agentName}";

    private static string? GetAgentForConversation(ITurnContext turnContext)
    {
        var conversationId = turnContext.Activity.Conversation.Id;
        lock (_lock)
        {
            return _conversationAgents.TryGetValue(conversationId, out var agent) ? agent : null;
        }
    }

    private static void SetAgentForConversation(ITurnContext turnContext, string agentName)
    {
        var conversationId = turnContext.Activity.Conversation.Id;
        lock (_lock)
        {
            _conversationAgents[conversationId] = agentName;
        }
    }

    /// <summary>
    /// Delivers any pending proactive notifications queued via the ProactiveController.
    /// </summary>
    private async Task DeliverPendingNotificationsAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        var conversationKey = turnContext.Activity.Conversation?.Id;
        if (string.IsNullOrEmpty(conversationKey)) return;

        var pending = PendingNotificationStore.DequeueAll(conversationKey);
        foreach (var notification in pending)
        {
            try
            {
                var card = AdaptiveCardBuilder.CreateNotificationCard(
                    notification.Title, notification.Message, notification.Severity);
                var reply = MessageFactory.Attachment(card);
                reply.Text = $"🔔 {notification.Title}: {notification.Message}";
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver pending notification {Id}", notification.Id);
            }
        }
    }

    /// <summary>
    /// Sends a proactive notification to a conversation. Called by ProactiveController.
    /// Uses ChannelAdapter.ContinueConversationAsync for real push.
    /// </summary>
    public async Task SendProactiveNotificationAsync(
        string title, string message, string? severity,
        ConversationReference reference,
        CancellationToken cancellationToken)
    {
        var card = AdaptiveCardBuilder.CreateNotificationCard(title, message, severity);
        var activity = MessageFactory.Attachment(card);

        var adapter = _serviceProvider.GetRequiredService<IChannelAdapter>() as ChannelAdapter
            ?? throw new InvalidOperationException("IChannelAdapter is not a ChannelAdapter");
        var clientId = _configuration["Connections:ServiceConnection:Settings:ClientId"] ?? string.Empty;

        await adapter.ContinueConversationAsync(
            clientId,
            reference,
            async (turnContext, ct) =>
            {
                await turnContext.SendActivityAsync(activity, ct);
            },
            cancellationToken);
    }
}
