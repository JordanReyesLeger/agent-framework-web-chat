using AFWebChat.Agents;
using AFWebChat.Models;
using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AFWebChat.Orchestrations;

public class OrchestrationFactory
{
    private readonly AgentRegistry _registry;
    private readonly ChatClientFactory _chatClientFactory;
    private readonly ILogger<OrchestrationFactory> _logger;

    // ── Orchestration catalog ──────────────────────────────────────────────
    private static readonly Dictionary<string, OrchestrationInfo> _orchestrations
        = new(StringComparer.OrdinalIgnoreCase)
    {
        // ===== PROCESOS DE NEGOCIO =====

        ["PlanDeProyecto"] = new("PlanDeProyecto",
            "📋 Plan de Proyecto: Analista de Negocio → Estimador de Costos → Planificador. Desde la idea hasta el plan ejecutable con tiempos y presupuesto.",
            ["AnalistaDeNegocio", "EstimadorDeCostos", "PlanificadorDeProyecto"],
            "Sequential"),

        ["ReporteEjecutivo"] = new("ReporteEjecutivo",
            "📊 Reporte Ejecutivo: Analista de Datos → Redactor Ejecutivo → Diseñador de Presentación. Transforma datos crudos en una presentación para directivos.",
            ["AnalistaDeDatos", "RedactorEjecutivo", "DiseñadorDePresentacion"],
            "Sequential"),

        ["PropuestaComercial"] = new("PropuestaComercial",
            "🎯 Propuesta Comercial: Consultor de Ventas → Especialista en Solución → Generador de Propuesta. De la oportunidad comercial a la cotización formal.",
            ["ConsultorDeVentas", "EspecialistaEnSolucion", "GeneradorDePropuesta"],
            "Sequential"),

        // ===== COLABORACIÓN EN EQUIPO =====

        ["EquipoDesarrollo"] = new("EquipoDesarrollo",
            "💬 Junta de Equipo (Round-Robin): Desarrollador, Arquitecto, Project Manager y DBA discuten un requerimiento técnico como en una junta real.",
            ["Desarrollador", "Arquitecto", "ProjectManager", "DBA"],
            "GroupChat"),

        ["EquipoDesarrolloAI"] = new("EquipoDesarrolloAI",
            "🧠 Junta de Equipo (Moderador IA): La IA decide quién habla según el contexto — simula una junta donde el moderador da la palabra al experto adecuado.",
            ["Desarrollador", "Arquitecto", "ProjectManager", "DBA"],
            "GroupChatAI"),

        // ===== GESTIÓN DE CORREOS =====

        ["GestionDeCorreos"] = new("GestionDeCorreos",
            "📧 Gestión de Correos (Sequential): Busca correos relacionados → evalúa urgencia → redacta respuesta. Pipeline secuencial con contexto acumulativo.",
            ["BuscadorDeCorreos", "EvaluadorDeUrgencia", "RedactorDeRespuesta"],
            "Sequential"),

        ["GestionDeCorreosAI"] = new("GestionDeCorreosAI",
            "🧠 Gestión de Correos (Moderador IA): Un moderador IA dirige la conversación entre Evaluador, Buscador y Redactor — decide quién habla según el contexto.",
            ["EvaluadorDeUrgencia", "BuscadorDeCorreos", "RedactorDeRespuesta"],
            "GroupChatAI"),

        ["GestionDeCorreosHandoff"] = new("GestionDeCorreosHandoff",
            "🔀 Gestión de Correos (Handoff): El Evaluador actúa como triaje y delega al Buscador o Redactor según la necesidad — delegación inteligente.",
            ["EvaluadorDeUrgencia", "BuscadorDeCorreos", "RedactorDeRespuesta"],
            "Handoff"),

        // ===== MARKETING DE PRODUCTO =====

        ["MarketingDeProducto"] = new("MarketingDeProducto",
            "🛍️ Marketing de Producto (Sequential): Analiza características de un producto → genera texto publicitario impactante. De la idea al anuncio.",
            ["AnalistaDeProducto", "RedactorPublicitario"],
            "Sequential"),

        ["MarketingDeProductoAI"] = new("MarketingDeProductoAI",
            "🧠 Marketing de Producto (Moderador IA): La IA modera la colaboración entre Analista de Producto y Redactor Publicitario para crear la mejor campaña.",
            ["AnalistaDeProducto", "RedactorPublicitario"],
            "GroupChatAI"),

        ["MarketingDeProductoHandoff"] = new("MarketingDeProductoHandoff",
            "🔀 Marketing de Producto (Handoff): El Analista de Producto triaja y delega al Redactor Publicitario cuando tiene las características listas.",
            ["AnalistaDeProducto", "RedactorPublicitario"],
            "Handoff")
    };

    public OrchestrationFactory(AgentRegistry registry, ChatClientFactory chatClientFactory, ILogger<OrchestrationFactory> logger)
    {
        _registry = registry;
        _chatClientFactory = chatClientFactory;
        _logger = logger;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public List<OrchestrationInfo> GetAllOrchestrations() => _orchestrations.Values.ToList();
    public OrchestrationInfo? GetOrchestration(string name) => _orchestrations.TryGetValue(name, out var o) ? o : null;
    public bool HasOrchestration(string name) => _orchestrations.ContainsKey(name);

    public async IAsyncEnumerable<StreamEvent> ExecuteOrchestrationAsync(string orchestrationName, ChatRequest request)
    {
        var orch = GetOrchestration(orchestrationName);
        if (orch is null)
        {
            yield return StreamEventService.Error($"Orchestration '{orchestrationName}' not found.");
            yield return StreamEventService.Done();
            yield break;
        }

        await foreach (var evt in ExecuteOrchestrationAsync(orch, request))
            yield return evt;
    }

    public async IAsyncEnumerable<StreamEvent> ExecuteOrchestrationAsync(OrchestrationInfo orch, ChatRequest request)
    {
        _logger.LogInformation("Executing AF orchestration: {Name} ({Pattern})", orch.Name, orch.OrchestrationPattern);

        var executor = orch.OrchestrationPattern switch
        {
            "Sequential" => RunSequentialOrchestrationAsync(orch, request),
            "Concurrent" => RunConcurrentOrchestrationAsync(orch, request),
            "GroupChat" => RunGroupChatOrchestrationAsync(orch, request),
            "GroupChatAI" => RunGroupChatAIOrchestrationAsync(orch, request),
            "Handoff" => RunHandoffOrchestrationAsync(orch, request),
            _ => RunSequentialOrchestrationAsync(orch, request)
        };

        await foreach (var evt in executor)
            yield return evt;
    }

    // ── Sequential Orchestration ───────────────────────────────────────────
    private async IAsyncEnumerable<StreamEvent> RunSequentialOrchestrationAsync(OrchestrationInfo orch, ChatRequest request)
    {
        var agents = ResolveAgents(orch);
        if (agents is null) { yield return StreamEventService.Error("Failed to resolve agents."); yield return StreamEventService.Done(); yield break; }

        var workflow = AgentWorkflowBuilder.BuildSequential(agents);
        var messages = new List<ChatMessage> { new(ChatRole.User, request.Message) };

        await foreach (var evt in RunAndStreamAsync(workflow, messages, orch))
            yield return evt;
    }

    // ── Concurrent Orchestration ───────────────────────────────────────────
    private async IAsyncEnumerable<StreamEvent> RunConcurrentOrchestrationAsync(OrchestrationInfo orch, ChatRequest request)
    {
        var agents = ResolveAgents(orch);
        if (agents is null) { yield return StreamEventService.Error("Failed to resolve agents."); yield return StreamEventService.Done(); yield break; }

        var workflow = AgentWorkflowBuilder.BuildConcurrent(agents);
        var messages = new List<ChatMessage> { new(ChatRole.User, request.Message) };

        await foreach (var evt in RunAndStreamAsync(workflow, messages, orch))
            yield return evt;
    }

    // ── GroupChat Orchestration ────────────────────────────────────────────
    private async IAsyncEnumerable<StreamEvent> RunGroupChatOrchestrationAsync(OrchestrationInfo orch, ChatRequest request)
    {
        var agents = ResolveAgents(orch);
        if (agents is null) { yield return StreamEventService.Error("Failed to resolve agents."); yield return StreamEventService.Done(); yield break; }

        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(participants =>
                new RoundRobinGroupChatManager(participants)
                {
                    MaximumIterationCount = 3
                })
            .AddParticipants(agents.ToArray())
            .Build();

        var messages = new List<ChatMessage> { new(ChatRole.User, request.Message) };

        await foreach (var evt in RunAndStreamAsync(workflow, messages, orch))
            yield return evt;
    }

    // ── GroupChat AI Orchestration (LLM picks next speaker) ────────────────
    private async IAsyncEnumerable<StreamEvent> RunGroupChatAIOrchestrationAsync(OrchestrationInfo orch, ChatRequest request)
    {
        var agents = ResolveAgents(orch);
        if (agents is null) { yield return StreamEventService.Error("Failed to resolve agents."); yield return StreamEventService.Done(); yield break; }

        var chatClient = _chatClientFactory.CreateChatClient();

        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(participants =>
                new AIGroupChatManager(participants, chatClient)
                {
                    MaximumIterationCount = 10
                })
            .AddParticipants(agents.ToArray())
            .Build();

        var messages = new List<ChatMessage> { new(ChatRole.User, request.Message) };

        await foreach (var evt in RunAndStreamAsync(workflow, messages, orch))
            yield return evt;
    }

    // ── Handoff Orchestration ──────────────────────────────────────────────
    private async IAsyncEnumerable<StreamEvent> RunHandoffOrchestrationAsync(OrchestrationInfo orch, ChatRequest request)
    {
        var agents = ResolveAgents(orch);
        if (agents is null || agents.Count < 2) { yield return StreamEventService.Error("Handoff requires at least 2 agents."); yield return StreamEventService.Done(); yield break; }

        // First agent is triage, rest are specialists
        var triageAgent = agents[0];
        var specialists = agents.Skip(1).ToArray();

        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(triageAgent)
            .WithHandoffs(triageAgent, specialists)
            .EmitAgentResponseUpdateEvents(true)
            .Build();

        var messages = new List<ChatMessage> { new(ChatRole.User, request.Message) };

        await foreach (var evt in RunAndStreamAsync(workflow, messages, orch))
            yield return evt;
    }

    // ── Core execution: run AF workflow and convert events to SSE StreamEvents ──
    private async IAsyncEnumerable<StreamEvent> RunAndStreamAsync(
        Workflow workflow,
        List<ChatMessage> messages,
        OrchestrationInfo orch)
    {
        // Emit initial step events
        foreach (var agentName in orch.Agents)
            yield return StreamEventService.WorkflowStep(agentName, "pending");

        string? lastExecutorId = null;

        await using var run = await InProcessExecution.RunStreamingAsync(workflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var wfEvent in run.WatchStreamAsync())
        {
            switch (wfEvent)
            {
                case AgentResponseUpdateEvent responseUpdate:
                    // Track agent transitions — emit agent-start for the frontend
                    if (responseUpdate.ExecutorId != lastExecutorId)
                    {
                        if (lastExecutorId is not null)
                            yield return StreamEventService.WorkflowStep(lastExecutorId, "completed");

                        lastExecutorId = responseUpdate.ExecutorId;
                        yield return StreamEventService.AgentStart(responseUpdate.ExecutorId);
                        yield return StreamEventService.WorkflowStep(responseUpdate.ExecutorId, "running");
                    }

                    // Stream the agent token
                    var text = responseUpdate.Update?.Text;
                    if (!string.IsNullOrEmpty(text))
                        yield return StreamEventService.AgentToken(responseUpdate.ExecutorId, text);
                    break;

                case WorkflowOutputEvent outputEvent:
                    // Mark last executor completed
                    if (lastExecutorId is not null)
                        yield return StreamEventService.WorkflowStep(lastExecutorId, "completed");

                    // Try to extract final messages
                    var finalMessages = outputEvent.As<List<ChatMessage>>();
                    if (finalMessages is not null)
                    {
                        var summary = string.Join("\n",
                            finalMessages.Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text))
                                         .Select(m => m.Text));
                        if (!string.IsNullOrEmpty(summary))
                            yield return StreamEventService.WorkflowOutput(summary);
                    }
                    break;

                case RequestInfoEvent requestInfo:
                    // Human-in-the-loop: tool approval
                    if (requestInfo.Request.TryGetDataAs(out ToolApprovalRequestContent? approvalRequest))
                    {
                        yield return StreamEventService.ToolApproval(
                            lastExecutorId ?? "unknown",
                            requestInfo.Request.RequestId,
                            "tool-call",
                            null);

                        // Auto-approve for now (can be wired to UI later)
                        await run.SendResponseAsync(
                            requestInfo.Request.CreateResponse(
                                approvalRequest.CreateResponse(approved: true)));
                    }
                    break;
            }
        }

        yield return StreamEventService.Done();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private List<AIAgent>? ResolveAgents(OrchestrationInfo orch)
    {
        try
        {
            return orch.Agents.Select(name => _registry.GetAgent(name)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve agents for orchestration {Name}", orch.Name);
            return null;
        }
    }
}
