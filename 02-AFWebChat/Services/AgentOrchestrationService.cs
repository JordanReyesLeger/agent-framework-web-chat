using AFWebChat.Agents;
using AFWebChat.Models;
using AFWebChat.Orchestrations;
using AFWebChat.Workflows;
using Microsoft.Agents.AI;

namespace AFWebChat.Services;

public class AgentOrchestrationService
{
    private readonly AgentRegistry _registry;
    private readonly SessionService _sessionService;
    private readonly WorkflowFactory _workflowFactory;
    private readonly OrchestrationFactory _orchestrationFactory;
    private readonly ILogger<AgentOrchestrationService> _logger;

    public AgentOrchestrationService(
        AgentRegistry registry,
        SessionService sessionService,
        WorkflowFactory workflowFactory,
        OrchestrationFactory orchestrationFactory,
        ILogger<AgentOrchestrationService> logger)
    {
        _registry = registry;
        _sessionService = sessionService;
        _workflowFactory = workflowFactory;
        _orchestrationFactory = orchestrationFactory;
        _logger = logger;
    }

    public async IAsyncEnumerable<StreamEvent> RunStreamingAsync(ChatRequest request)
    {
        // Route to custom ad-hoc workflow/orchestration if CustomAgents are specified
        if (request.CustomAgents is { Length: > 0 } && !string.IsNullOrEmpty(request.CustomPattern))
        {
            // Determine if the custom pattern is an orchestration or a workflow
            var isOrchestration = request.CustomPattern is "Sequential" or "Concurrent" or "GroupChat" or "GroupChatAI" or "Handoff";

            if (isOrchestration)
            {
                var customOrch = new OrchestrationInfo(
                    "Custom",
                    "Custom orchestration built from the UI",
                    request.CustomAgents,
                    request.CustomPattern);
                _logger.LogInformation("Routing to custom orchestration ({Pattern}) with agents: {Agents}",
                    request.CustomPattern, string.Join(", ", request.CustomAgents));
                await foreach (var evt in _orchestrationFactory.ExecuteOrchestrationAsync(customOrch, request))
                    yield return evt;
            }
            else
            {
                var customWf = new WorkflowInfo(
                    "Custom",
                    "Custom workflow built from the UI",
                    request.CustomAgents,
                    request.CustomPattern);
                _logger.LogInformation("Routing to custom workflow ({Pattern}) with agents: {Agents}",
                    request.CustomPattern, string.Join(", ", request.CustomAgents));
                await foreach (var evt in _workflowFactory.ExecuteWorkflowAsync(customWf, request))
                    yield return evt;
            }
            yield break;
        }

        // Route to named orchestration if OrchestrationName is specified
        if (!string.IsNullOrEmpty(request.OrchestrationName))
        {
            _logger.LogInformation("Routing to orchestration: {OrchestrationName}", request.OrchestrationName);
            await foreach (var evt in _orchestrationFactory.ExecuteOrchestrationAsync(request.OrchestrationName, request))
                yield return evt;
            yield break;
        }

        // Route to named workflow if WorkflowName is specified
        if (!string.IsNullOrEmpty(request.WorkflowName))
        {
            _logger.LogInformation("Routing to workflow: {WorkflowName}", request.WorkflowName);
            await foreach (var evt in _workflowFactory.ExecuteWorkflowAsync(request.WorkflowName, request))
                yield return evt;
            yield break;
        }

        _logger.LogInformation("Processing request for agent: {AgentName}, session: {SessionId}",
            request.AgentName, request.SessionId);

        AIAgent agent;
        string? agentError = null;
        try
        {
            agent = _registry.GetAgent(request.AgentName);
        }
        catch (KeyNotFoundException)
        {
            agentError = $"Agent '{request.AgentName}' not found.";
            agent = null!;
        }

        if (agentError is not null)
        {
            yield return StreamEventService.Error(agentError);
            yield return StreamEventService.Done();
            yield break;
        }

        AgentSession session;
        try
        {
            session = await _sessionService.GetOrCreateSessionAsync(request.SessionId, agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/restoring session");
            session = await agent.CreateSessionAsync();
        }

        bool hasContent = false;

        await foreach (var update in agent.RunStreamingAsync(request.Message, session))
        {
            if (!string.IsNullOrEmpty(update.ToString()))
            {
                hasContent = true;
                yield return StreamEventService.AgentToken(request.AgentName, update.ToString()!);
            }
        }

        if (!hasContent)
        {
            yield return StreamEventService.AgentToken(request.AgentName, "I received your message but had no response to generate.");
        }

        yield return StreamEventService.AgentComplete(request.AgentName);

        // Save session
        try
        {
            await _sessionService.SaveSessionAsync(request.SessionId, session, agent, request.AgentName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error saving session {SessionId}", request.SessionId);
        }

        yield return StreamEventService.Done();
    }

    public async Task<ChatResponse> RunAsync(ChatRequest request)
    {
        var agent = _registry.GetAgent(request.AgentName);
        var session = await _sessionService.GetOrCreateSessionAsync(request.SessionId, agent);

        var response = await agent.RunAsync(request.Message, session);

        await _sessionService.SaveSessionAsync(request.SessionId, session, agent, request.AgentName);

        return new ChatResponse(
            request.SessionId,
            request.AgentName,
            response.ToString() ?? "",
            DateTime.UtcNow);
    }
}
