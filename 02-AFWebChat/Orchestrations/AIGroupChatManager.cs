using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AFWebChat.Orchestrations;

/// <summary>
/// Structured response from the AI moderator for each turn decision.
/// </summary>
public sealed class ModeratorDecision
{
    [JsonPropertyName("next_speaker")]
    public string NextSpeaker { get; set; } = "";

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";

    [JsonPropertyName("should_terminate")]
    public bool ShouldTerminate { get; set; }

    [JsonPropertyName("termination_reason")]
    public string? TerminationReason { get; set; }

    [JsonPropertyName("guidance")]
    public string? Guidance { get; set; }
}

/// <summary>
/// Advanced AI-powered GroupChat manager that acts as an intelligent moderator.
/// Uses an LLM to:
/// 1. Select the best next speaker based on expertise and conversation context
/// 2. Decide when the conversation has reached a natural conclusion
/// 3. Provide guidance/context to the next speaker via history augmentation
/// </summary>
public class AIGroupChatManager : GroupChatManager
{
    private readonly IChatClient _chatClient;
    private readonly IReadOnlyList<AIAgent> _agents;
    private ModeratorDecision? _lastDecision;
    private readonly HashSet<string> _agentsWhoSpoke = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastSpeaker;

    public AIGroupChatManager(IReadOnlyList<AIAgent> agents, IChatClient chatClient)
    {
        _chatClient = chatClient;
        _agents = agents;
        MaximumIterationCount = 12;
    }

    protected override async ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var decision = await GetModeratorDecisionAsync(history, cancellationToken);
        _lastDecision = decision;

        // Find the agent by name
        var selected = FindAgent(decision.NextSpeaker);

        if (selected is not null)
        {
            _lastSpeaker = selected.Name;
            _agentsWhoSpoke.Add(selected.Name ?? "");
            return selected;
        }

        // Fallback: pick the agent who has spoken least or not at all
        var fallback = _agents.FirstOrDefault(a => !_agentsWhoSpoke.Contains(a.Name ?? ""))
                       ?? _agents[IterationCount % _agents.Count];
        _lastSpeaker = fallback.Name;
        _agentsWhoSpoke.Add(fallback.Name ?? "");
        return fallback;
    }

    protected override async ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        // Always respect max iterations
        if (IterationCount >= MaximumIterationCount)
            return true;

        // Min 2 iterations before considering termination (let the conversation develop)
        if (IterationCount < 2)
            return false;

        // Use the decision from SelectNextAgentAsync (it runs first each turn)
        if (_lastDecision is not null)
            return _lastDecision.ShouldTerminate;

        return false;
    }

    protected override ValueTask<IEnumerable<ChatMessage>> UpdateHistoryAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        // If the moderator provided guidance for the next speaker, inject it
        if (_lastDecision?.Guidance is { Length: > 0 } guidance)
        {
            var augmented = new List<ChatMessage>(history)
            {
                new(ChatRole.System,
                    $"[Moderator guidance for {_lastDecision.NextSpeaker}]: {guidance}")
            };
            return new(augmented.AsEnumerable());
        }

        return new(history.AsEnumerable());
    }

    protected override void Reset()
    {
        base.Reset();
        _lastDecision = null;
        _agentsWhoSpoke.Clear();
        _lastSpeaker = null;
    }

    private async Task<ModeratorDecision> GetModeratorDecisionAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        // Build agent profiles with descriptions
        var agentProfiles = string.Join("\n", _agents.Select(a =>
        {
            var desc = a.Description ?? a.Name ?? "No description";
            var spoke = _agentsWhoSpoke.Contains(a.Name ?? "") ? " [has spoken]" : " [hasn't spoken yet]";
            return $"  - {a.Name}: {Truncate(desc, 150)}{spoke}";
        }));

        // Build conversation summary (last N messages)
        var recentMessages = history
            .Where(m => !string.IsNullOrEmpty(m.Text))
            .TakeLast(15)
            .Select(m =>
            {
                var author = m.AuthorName ?? m.Role.ToString();
                return $"[{author}]: {Truncate(m.Text, 300)}";
            })
            .ToList();

        var conversationContext = recentMessages.Count > 0
            ? string.Join("\n", recentMessages)
            : "(conversation just started)";

        var userQuery = history.FirstOrDefault(m => m.Role == ChatRole.User)?.Text ?? "(unknown)";

        var prompt = $$"""
            You are an expert conversation moderator managing a multi-agent discussion.

            ## User's Original Question
            {{Truncate(userQuery, 500)}}

            ## Available Agents
            {{agentProfiles}}

            ## Conversation So Far ({{IterationCount}} turns completed)
            {{conversationContext}}

            ## Last Speaker
            {{_lastSpeaker ?? "(none yet)"}}

            ## Your Job
            Analyze the conversation and make a decision. Consider:
            1. Which agent's expertise is MOST needed right now to advance the discussion?
            2. Has the user's question been fully answered from all relevant perspectives?
            3. Are there gaps or angles that haven't been covered yet?
            4. Would another turn add genuine value, or would it be redundant?

            ## Rules
            - Don't pick the same agent twice in a row unless absolutely necessary.
            - Prioritize agents who haven't spoken yet if their expertise is relevant.
            - Terminate if: the question is fully answered, agents are repeating themselves, or all relevant agents have contributed.
            - Provide brief guidance to help the next speaker focus on what's missing.

            ## Respond with ONLY valid JSON (no markdown):
            {"next_speaker": "<agent_name>", "reasoning": "<why this agent>", "should_terminate": false, "termination_reason": null, "guidance": "<what should the next speaker focus on>"}

            If conversation should end:
            {"next_speaker": "", "reasoning": "<summary>", "should_terminate": true, "termination_reason": "<why ending>", "guidance": null}
            """;

        try
        {
            var response = await _chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            var text = response.Text?.Trim() ?? "";

            // Strip markdown fences if present
            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    text = text[(firstNewline + 1)..lastFence].Trim();
            }

            var decision = JsonSerializer.Deserialize<ModeratorDecision>(text);
            if (decision is not null)
                return decision;
        }
        catch
        {
            // Fallback on parse errors
        }

        // Smart fallback: pick an agent who hasn't spoken yet
        var nextFallback = _agents.FirstOrDefault(a => !_agentsWhoSpoke.Contains(a.Name ?? ""))
                           ?? _agents[IterationCount % _agents.Count];

        return new ModeratorDecision
        {
            NextSpeaker = nextFallback.Name ?? "",
            Reasoning = "Fallback selection",
            ShouldTerminate = false
        };
    }

    private AIAgent? FindAgent(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Exact match
        var agent = _agents.FirstOrDefault(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        if (agent is not null) return agent;

        // Partial/contains match
        return _agents.FirstOrDefault(a =>
            a.Name is not null && (
                name.Contains(a.Name, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }

    private static string Truncate(string? text, int maxLen)
        => text is null ? "" : text.Length <= maxLen ? text : text[..maxLen] + "...";
}
