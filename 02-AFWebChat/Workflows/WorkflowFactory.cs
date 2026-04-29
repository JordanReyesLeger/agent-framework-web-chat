using System.Text.Json;
using System.Text.Json.Serialization;
using AFWebChat.Agents;
using AFWebChat.Models;
using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AFWebChat.Workflows;

/// <summary>
/// Result of the classifier agent for conditional routing.
/// </summary>
public sealed class ClassificationResult
{
    [JsonPropertyName("selected_index")]
    public int SelectedIndex { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Builds and executes AF workflows using WorkflowBuilder, AgentWorkflowBuilder,
/// and InProcessExecution. Supports Iterative, Conditional (Switch), and FanOut patterns.
/// All workflows are built dynamically from the agent registry.
/// </summary>
public class WorkflowFactory
{
    private readonly AgentRegistry _registry;
    private readonly ILogger<WorkflowFactory> _logger;

    // ── Workflow catalog ───────────────────────────────────────────────────
    private static readonly Dictionary<string, WorkflowInfo> _workflows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RedaccionIterativa"] = new("RedaccionIterativa",
            "✍️ Redacción con Revisión Iterativa: El asistente redacta contenido, el Narrador de Datos evalúa calidad y da feedback. Se itera hasta obtener un resultado aprobado.",
            ["GeneralAssistant", "DataStoryteller"],
            "Iterative"),

        ["EnrutamientoInteligente"] = new("EnrutamientoInteligente",
            "🔀 Enrutamiento Inteligente: Un clasificador analiza la solicitud y la dirige al especialista correcto — búsqueda en documentos (RAG), consulta SQL, o respuesta general.",
            ["GeneralAssistant", "RAGAgent", "DatabaseQuery", "AgenteSQLAzure"],
            "Conditional"),

        ["AnalisisParalelo"] = new("AnalisisParalelo",
            "⚡ Análisis NLP en Paralelo: Sentimiento y entidades se analizan simultáneamente. El sintetizador integra todo en un reporte unificado.",
            ["SentimentAnalyzer", "EntityExtractor", "GeneralAssistant"],
            "FanOut")
    };

    public WorkflowFactory(AgentRegistry registry, ILogger<WorkflowFactory> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public List<WorkflowInfo> GetAllWorkflows() => _workflows.Values.ToList();
    public WorkflowInfo? GetWorkflow(string name) => _workflows.TryGetValue(name, out var wf) ? wf : null;
    public bool HasWorkflow(string name) => _workflows.ContainsKey(name);

    public async IAsyncEnumerable<StreamEvent> ExecuteWorkflowAsync(string workflowName, ChatRequest request)
    {
        var wf = GetWorkflow(workflowName);
        if (wf is null)
        {
            yield return StreamEventService.Error($"Workflow '{workflowName}' not found.");
            yield return StreamEventService.Done();
            yield break;
        }

        await foreach (var evt in ExecuteWorkflowAsync(wf, request))
            yield return evt;
    }

    public async IAsyncEnumerable<StreamEvent> ExecuteWorkflowAsync(WorkflowInfo wf, ChatRequest request)
    {
        _logger.LogInformation("Executing AF workflow: {Name} ({Pattern})", wf.Name, wf.Pattern);

        var runner = wf.Pattern switch
        {
            "Iterative" => RunIterativeWorkflowAsync(wf, request),
            "Conditional" => RunConditionalWorkflowAsync(wf, request),
            "FanOut" => RunFanOutWorkflowAsync(wf, request),
            _ => RunIterativeWorkflowAsync(wf, request)
        };

        await foreach (var evt in runner)
            yield return evt;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Iterative Workflow — Writer ↔ Reviewer loop with max iterations
    // ══════════════════════════════════════════════════════════════════════
    private async IAsyncEnumerable<StreamEvent> RunIterativeWorkflowAsync(WorkflowInfo wf, ChatRequest request)
    {
        var agents = ResolveAgents(wf);
        if (agents is null || agents.Count < 2)
        {
            yield return StreamEventService.Error("Iterative workflow requires at least 2 agents.");
            yield return StreamEventService.Done();
            yield break;
        }

        var writerAgent = agents[0];
        var reviewerAgent = agents[1];
        var writerName = wf.Agents[0];
        var reviewerName = wf.Agents[1];
        const int maxIterations = 3;
        var currentDraft = "";

        foreach (var agentName in wf.Agents)
            yield return StreamEventService.WorkflowStep(agentName, "pending");

        for (int i = 1; i <= maxIterations; i++)
        {
            // ── Writer step ──
            yield return StreamEventService.AgentStart(writerName);
            yield return StreamEventService.WorkflowStep(writerName, "running");

            var writerPrompt = i == 1
                ? request.Message
                : $"Revise this content based on feedback.\n\nOriginal request: {request.Message}\n\nCurrent draft:\n{currentDraft}\n\nPlease improve the content.";

            var writerResponse = await writerAgent.RunAsync(writerPrompt);
            currentDraft = writerResponse.ToString() ?? "";
            yield return StreamEventService.AgentToken(writerName, currentDraft);
            yield return StreamEventService.WorkflowStep(writerName, "completed");

            // ── Reviewer step ──
            yield return StreamEventService.AgentStart(reviewerName);
            yield return StreamEventService.WorkflowStep(reviewerName, "running");

            var reviewPrompt = $"Review this content. If ready, respond with APPROVED. Otherwise give specific feedback:\n\n{currentDraft}";
            var reviewResponse = await reviewerAgent.RunAsync(reviewPrompt);
            var reviewText = reviewResponse.ToString() ?? "";
            yield return StreamEventService.AgentToken(reviewerName, reviewText);
            yield return StreamEventService.WorkflowStep(reviewerName, "completed");

            var approved = reviewText.Contains("APPROVED", StringComparison.OrdinalIgnoreCase);

            if (approved || i == maxIterations)
            {
                var label = approved
                    ? $"Workflow completed after {i} iteration(s) — approved."
                    : $"Workflow completed after {i} iteration(s) — max reached.";
                yield return StreamEventService.WorkflowOutput(label + "\n\n" + currentDraft);
                break;
            }
        }

        yield return StreamEventService.Done();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Conditional Workflow — Classifier → Switch → Specialist
    // First agent classifies, specialist handles the request.
    // ══════════════════════════════════════════════════════════════════════
    private async IAsyncEnumerable<StreamEvent> RunConditionalWorkflowAsync(WorkflowInfo wf, ChatRequest request)
    {
        var agents = ResolveAgents(wf);
        if (agents is null || agents.Count < 2)
        {
            yield return StreamEventService.Error("Conditional workflow requires at least 2 agents.");
            yield return StreamEventService.Done();
            yield break;
        }

        var classifierAgent = agents[0];
        var classifierName = wf.Agents[0];
        var specialistNames = wf.Agents.Skip(1).ToArray();
        var specialistAgents = agents.Skip(1).ToArray();

        foreach (var agentName in wf.Agents)
            yield return StreamEventService.WorkflowStep(agentName, "pending");

        // ── Classification step ──
        yield return StreamEventService.AgentStart(classifierName);
        yield return StreamEventService.WorkflowStep(classifierName, "running");

        var specialistList = string.Join("\n", specialistNames.Select((name, i) => $"  {i}: {name}"));
        var classifyPrompt = $"Analyze this request and decide which specialist should handle it.\n\nSpecialists:\n{specialistList}\n\nRequest: {request.Message}\n\nRespond ONLY with JSON: {{\"selected_index\": <number>, \"reason\": \"<reason>\"}}";

        var classifyResponse = await classifierAgent.RunAsync(classifyPrompt);
        var classifyText = classifyResponse.ToString() ?? "";
        yield return StreamEventService.AgentToken(classifierName, classifyText);
        yield return StreamEventService.WorkflowStep(classifierName, "completed");

        // Parse classification
        int selectedIndex = 0;
        try
        {
            var result = JsonSerializer.Deserialize<ClassificationResult>(classifyText);
            if (result is not null && result.SelectedIndex >= 0 && result.SelectedIndex < specialistNames.Length)
                selectedIndex = result.SelectedIndex;
        }
        catch { /* default to 0 */ }

        // ── Specialist step ──
        var selectedName = specialistNames[selectedIndex];
        var selectedAgent = specialistAgents[selectedIndex];

        yield return StreamEventService.AgentStart(selectedName);
        yield return StreamEventService.WorkflowStep(selectedName, "running");

        var specialistResponse = await selectedAgent.RunAsync(request.Message);
        var specialistText = specialistResponse.ToString() ?? "";
        yield return StreamEventService.AgentToken(selectedName, specialistText);
        yield return StreamEventService.WorkflowStep(selectedName, "completed");

        // Mark unselected specialists as skipped
        for (int i = 0; i < specialistNames.Length; i++)
        {
            if (i != selectedIndex)
                yield return StreamEventService.WorkflowStep(specialistNames[i], "completed");
        }

        yield return StreamEventService.WorkflowOutput(specialistText);
        yield return StreamEventService.Done();
    }

    // ══════════════════════════════════════════════════════════════════════
    // FanOut Workflow — Parallel agents → Synthesizer
    // Uses AgentWorkflowBuilder.BuildConcurrent for true parallel execution,
    // then runs the synthesizer agent on the aggregated results.
    // ══════════════════════════════════════════════════════════════════════
    private async IAsyncEnumerable<StreamEvent> RunFanOutWorkflowAsync(WorkflowInfo wf, ChatRequest request)
    {
        var agents = ResolveAgents(wf);
        if (agents is null || agents.Count < 2)
        {
            yield return StreamEventService.Error("FanOut workflow requires at least 2 agents.");
            yield return StreamEventService.Done();
            yield break;
        }

        // Last agent is synthesizer, rest are parallel workers
        var workerAgents = agents.Take(agents.Count - 1).ToList();
        var workerNames = wf.Agents.Take(wf.Agents.Length - 1).ToArray();
        var synthesizerAgent = agents.Last();
        var synthesizerName = wf.Agents.Last();

        foreach (var agentName in wf.Agents)
            yield return StreamEventService.WorkflowStep(agentName, "pending");

        // ── Run workers in parallel via BuildConcurrent ──
        var concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent(workerAgents);
        var messages = new List<ChatMessage> { new(ChatRole.User, request.Message) };

        foreach (var name in workerNames)
        {
            yield return StreamEventService.AgentStart(name);
            yield return StreamEventService.WorkflowStep(name, "running");
        }

        string? lastExecutorId = null;
        var workerResults = new List<string>();

        await using var run = await InProcessExecution.RunStreamingAsync(concurrentWorkflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var wfEvent in run.WatchStreamAsync())
        {
            switch (wfEvent)
            {
                case AgentResponseUpdateEvent responseUpdate:
                    if (responseUpdate.ExecutorId != lastExecutorId)
                    {
                        lastExecutorId = responseUpdate.ExecutorId;
                        yield return StreamEventService.AgentStart(responseUpdate.ExecutorId);
                    }
                    var text = responseUpdate.Update?.Text;
                    if (!string.IsNullOrEmpty(text))
                        yield return StreamEventService.AgentToken(responseUpdate.ExecutorId, text);
                    break;

                case WorkflowOutputEvent outputEvent:
                    var finalMessages = outputEvent.As<List<ChatMessage>>();
                    if (finalMessages is not null)
                    {
                        foreach (var msg in finalMessages.Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text)))
                            workerResults.Add(msg.Text!);
                    }
                    break;
            }
        }

        foreach (var name in workerNames)
            yield return StreamEventService.WorkflowStep(name, "completed");

        // ── Synthesizer step ──
        yield return StreamEventService.AgentStart(synthesizerName);
        yield return StreamEventService.WorkflowStep(synthesizerName, "running");

        var synthesisPrompt = "You received analyses from different agents. Synthesize them into a single comprehensive response.\n\n"
            + string.Join("\n\n---\n\n", workerResults.Select((r, i) => $"Agent {i + 1}:\n{r}"));

        var synthResponse = await synthesizerAgent.RunAsync(synthesisPrompt);
        var synthText = synthResponse.ToString() ?? "";
        yield return StreamEventService.AgentToken(synthesizerName, synthText);
        yield return StreamEventService.WorkflowStep(synthesizerName, "completed");

        yield return StreamEventService.WorkflowOutput(synthText);
        yield return StreamEventService.Done();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private List<AIAgent>? ResolveAgents(WorkflowInfo wf)
    {
        try
        {
            return wf.Agents.Select(name => _registry.GetAgent(name)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve agents for workflow {Name}", wf.Name);
            return null;
        }
    }
}
