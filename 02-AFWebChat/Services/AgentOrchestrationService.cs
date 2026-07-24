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

        // Feedback inmediato: avisa a la UI que el agente empezó a pensar ANTES de llamar al
        // modelo. Los modelos de razonamiento (gpt-5.1) pueden tardar decenas de segundos en
        // emitir el primer token de resumen; sin esto el usuario no ve nada durante ese tiempo.
        yield return StreamEventService.AgentThinking(request.AgentName);

        await foreach (var update in agent.RunStreamingAsync(request.Message, session))
        {
            // Razonamiento ("thinking"): solo lo emiten modelos de razonamiento (p. ej. gpt-5.1).
            // Si el modelo no razona, Contents no trae TextReasoningContent y no se emite nada (no truena).
            foreach (var content in update.Contents)
            {
                if (content is Microsoft.Extensions.AI.TextReasoningContent reasoning
                    && !string.IsNullOrEmpty(reasoning.Text))
                {
                    yield return StreamEventService.AgentReasoning(request.AgentName, reasoning.Text);
                }
            }

            // Ruta Chat Completions: algunos modelos de razonamiento (DeepSeek-R1, Grok, Qwen3, vLLM)
            // exponen el razonamiento en el campo no estándar `reasoning_content` del delta. El SDK de
            // OpenAI no lo tipa, así que lo extraemos del RawRepresentation. Si el modelo no lo emite,
            // devuelve null y no se emite nada (inofensivo para gpt-4o u otros).
            var chatReasoning = TryGetChatCompletionsReasoning(update.RawRepresentation);
            if (!string.IsNullOrEmpty(chatReasoning))
            {
                yield return StreamEventService.AgentReasoning(request.AgentName, chatReasoning!);
            }

            // Texto final de la respuesta. ToString() concatena solo el TextContent (excluye el reasoning),
            // así que el comportamiento para modelos sin razonamiento queda idéntico al original.
            var text = update.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                hasContent = true;
                yield return StreamEventService.AgentToken(request.AgentName, text!);
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

    /// <summary>
    /// Extrae el razonamiento del campo no estándar <c>reasoning_content</c> que algunos modelos
    /// de razonamiento (DeepSeek-R1, xAI Grok, Qwen3, backends vLLM) devuelven en el delta de la
    /// Chat Completions API. El SDK de OpenAI .NET no expone este campo como propiedad tipada
    /// (ver dotnet/extensions#6208), por lo que serializamos el <c>StreamingChatCompletionUpdate</c>
    /// crudo y leemos <c>choices[0].delta.reasoning_content</c>. Devuelve null si no está presente.
    /// </summary>
    private static string? TryGetChatCompletionsReasoning(object? rawRepresentation)
    {
        var raw = rawRepresentation;
        // El RawRepresentation puede venir envuelto (AgentRunResponseUpdate -> ChatResponseUpdate -> OpenAI update).
        for (int depth = 0; depth < 4 && raw is not null; depth++)
        {
            if (raw is OpenAI.Chat.StreamingChatCompletionUpdate streamingUpdate)
            {
                return ExtractReasoningContent(streamingUpdate);
            }

            if (raw is Microsoft.Extensions.AI.ChatResponseUpdate chatResponseUpdate)
            {
                raw = chatResponseUpdate.RawRepresentation;
                continue;
            }

            break;
        }

        return null;
    }

    private static string? ExtractReasoningContent(OpenAI.Chat.StreamingChatCompletionUpdate streamingUpdate)
    {
        try
        {
            // Serializa el update crudo; los campos desconocidos (como reasoning_content) se conservan
            // en los datos adicionales del delta y se re-emiten al serializar.
            BinaryData json = System.ClientModel.Primitives.ModelReaderWriter.Write(streamingUpdate);
            using var document = System.Text.Json.JsonDocument.Parse(json.ToMemory());

            if (document.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == System.Text.Json.JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("delta", out var delta)
                    && delta.TryGetProperty("reasoning_content", out var reasoning)
                    && reasoning.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var text = reasoning.GetString();
                    return string.IsNullOrEmpty(text) ? null : text;
                }
            }
        }
        catch
        {
            // Si el modelo/SDK no soporta la serialización o el campo, lo ignoramos silenciosamente.
        }

        return null;
    }
}
