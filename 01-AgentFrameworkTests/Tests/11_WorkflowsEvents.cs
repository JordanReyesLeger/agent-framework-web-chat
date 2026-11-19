using Microsoft.Agents.AI.Workflows;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

// ---------- Ejecutores auxiliares para pruebas de eventos ----------

/// <summary>
/// Ejecutor que duplica el texto de entrada (lo repite dos veces).
/// Permite verificar transformaciones intermedias en el pipeline.
/// </summary>
internal sealed class DoublerExecutor() : Executor<string, string>("Doubler")
{
    public override ValueTask<string> HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
        => ValueTask.FromResult($"{message}{message}");
}

/// <summary>
/// Ejecutor que agrega un sufijo "_DONE" al texto y emite la salida del workflow.
/// Usa YieldOutputAsync para generar un WorkflowOutputEvent observable.
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class SuffixOutputExecutor() : Executor<string>("SuffixOutput")
{
    public override async ValueTask HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
        => await context.YieldOutputAsync($"{message}_DONE", ct);
}

// ---------- Ejecutores para el patrón de loop (juego de adivinanza) ----------

/// <summary>
/// Señales de retroalimentación para el juego de adivinanza.
/// El juez envía estas señales al adivinador para guiar la búsqueda binaria.
/// </summary>
internal enum GuessSignal { Init, TooHigh, TooLow }

/// <summary>
/// Ejecutor que adivina un número usando búsqueda binaria.
/// Ajusta los límites inferior y superior según la retroalimentación del juez.
/// Envía su adivinanza al ejecutor conectado (judge) mediante SendMessageAsync.
///
/// La anotación [SendsMessage] indica al workflow qué tipos de mensaje puede enviar
/// este ejecutor a través de sus edges conectados.
/// </summary>
[SendsMessage(typeof(int))]
internal sealed class GuesserExecutor : Executor<GuessSignal>
{
    private int _low;
    private int _high;

    public GuesserExecutor(int low, int high) : base("Guesser")
    {
        _low = low;
        _high = high;
    }

    private int MidPoint => (_low + _high) / 2;

    public override async ValueTask HandleAsync(
        GuessSignal message, IWorkflowContext context, CancellationToken ct = default)
    {
        switch (message)
        {
            case GuessSignal.TooHigh:
                _high = MidPoint - 1;
                break;
            case GuessSignal.TooLow:
                _low = MidPoint + 1;
                break;
        }

        // Enviar la adivinanza al ejecutor conectado (judge)
        await context.SendMessageAsync(MidPoint, cancellationToken: ct);
    }
}

/// <summary>
/// Ejecutor que juzga si la adivinanza es correcta.
/// Si el número coincide con el objetivo, emite la salida final (YieldOutputAsync).
/// Si no, envía una señal de retroalimentación (TooHigh/TooLow) al adivinador.
///
/// Combina [SendsMessage] para la retroalimentación y [YieldsOutput] para la salida final.
/// Este doble rol permite crear loops que convergen hacia una solución.
/// </summary>
[SendsMessage(typeof(GuessSignal))]
[YieldsOutput(typeof(string))]
internal sealed class JudgeNumberExecutor : Executor<int>
{
    private readonly int _target;
    private int _attempts;

    public JudgeNumberExecutor(int target) : base("Judge")
    {
        _target = target;
    }

    public override async ValueTask HandleAsync(
        int message, IWorkflowContext context, CancellationToken ct = default)
    {
        _attempts++;

        if (message == _target)
        {
            // ¡Acertó! Emitir salida final del workflow
            await context.YieldOutputAsync($"Found {_target} in {_attempts} attempts!", ct);
        }
        else if (message > _target)
        {
            // Muy alto → enviar señal de retroalimentación
            await context.SendMessageAsync(GuessSignal.TooHigh, cancellationToken: ct);
        }
        else
        {
            // Muy bajo → enviar señal de retroalimentación
            await context.SendMessageAsync(GuessSignal.TooLow, cancellationToken: ct);
        }
    }
}

/// <summary>
/// Módulo 11: Workflows — Eventos y patrones avanzados.
///
/// Los workflows emiten eventos durante la ejecución que permiten:
/// - ExecutorCompletedEvent: observar cuándo cada ejecutor termina y qué resultado produjo
/// - WorkflowOutputEvent: capturar la salida final del workflow (emitida por YieldOutputAsync)
///
/// Patrones avanzados:
/// - Loop (bucle de retroalimentación): dos ejecutores conectados en ciclo con
///   SendMessageAsync para enviar mensajes tipados entre ellos. El loop termina
///   cuando un ejecutor emite YieldOutputAsync en lugar de SendMessageAsync.
/// </summary>
public class _11_WorkflowsEvents
{
    private readonly ITestOutputHelper _output;

    public _11_WorkflowsEvents(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Captura todos los eventos ExecutorCompletedEvent emitidos durante la ejecución.
    /// Cada vez que un ejecutor completa su HandleAsync, el workflow emite un evento.
    ///
    /// Los ExecutorCompletedEvent permiten:
    /// - Observar el progreso del workflow paso a paso
    /// - Logging y trazabilidad de cada etapa
    /// - Depuración de flujos complejos
    ///
    /// Propiedades clave: ExecutorId (nombre del ejecutor) y Data (resultado producido).
    /// </summary>
    [Fact]
    public async Task Should_Capture_Executor_Completed_Events()
    {
        // Pipeline de 3 ejecutores para generar múltiples eventos
        var uppercase = new UppercaseExecutor();    // Del módulo 09
        var doubler = new DoublerExecutor();
        var suffix = new SuffixOutputExecutor();

        var workflow = new WorkflowBuilder(uppercase)
            .AddEdge(uppercase, doubler)
            .AddEdge(doubler, suffix)
            .WithOutputFrom(suffix)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "hi");

        var completedEvents = new List<ExecutorCompletedEvent>();
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is ExecutorCompletedEvent completed)
            {
                completedEvents.Add(completed);
                _output.WriteLine(
                    $"  Evento: {completed.ExecutorId} completó con: {completed.Data}");
            }
        }

        // Al menos Uppercase y Doubler emiten ExecutorCompletedEvent
        // (SuffixOutput usa YieldOutputAsync, que emite WorkflowOutputEvent)
        Assert.True(completedEvents.Count >= 2,
            $"Se esperaban al menos 2 eventos, se obtuvieron {completedEvents.Count}");

        _output.WriteLine(
            $"\n✅ Capturados {completedEvents.Count} eventos ExecutorCompletedEvent.");
    }

    /// <summary>
    /// Captura el evento WorkflowOutputEvent que indica la salida final del workflow.
    /// Este evento se emite cuando un ejecutor marcado con [YieldsOutput] llama
    /// a context.YieldOutputAsync(). A diferencia de ExecutorCompletedEvent que indica
    /// fin de un paso, WorkflowOutputEvent indica la salida declarada del workflow.
    /// </summary>
    [Fact]
    public async Task Should_Capture_Workflow_Output_Event()
    {
        var uppercase = new UppercaseExecutor();
        var suffix = new SuffixOutputExecutor();

        var workflow = new WorkflowBuilder(uppercase)
            .AddEdge(uppercase, suffix)
            .WithOutputFrom(suffix)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "test");

        var outputEvents = new List<WorkflowOutputEvent>();
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                outputEvents.Add(outputEvent);
                _output.WriteLine($"  WorkflowOutput: {outputEvent.Data}");
            }
        }

        Assert.Single(outputEvents);
        Assert.Contains("DONE", outputEvents[0].Data?.ToString() ?? "");

        _output.WriteLine("\n✅ Evento WorkflowOutputEvent capturado correctamente.");
    }

    /// <summary>
    /// Patrón de loop (bucle de retroalimentación) entre dos ejecutores.
    ///
    /// Un adivinador (GuesserExecutor) propone un número usando búsqueda binaria.
    /// Un juez (JudgeNumberExecutor) evalúa si acertó:
    /// - Si acertó → emite YieldOutputAsync (termina el loop y el workflow)
    /// - Si no → envía SendMessageAsync con TooHigh o TooLow (continúa el loop)
    ///
    /// El loop se crea conectando los dos ejecutores bidireccionalmente:
    /// guesser → judge (envía adivinanza int)
    /// judge → guesser (envía señal GuessSignal)
    ///
    /// Este patrón es útil para iteraciones convergentes, refinamiento progresivo,
    /// y cualquier escenario donde necesites retroalimentación iterativa.
    /// </summary>
    [Fact]
    public async Task Should_Execute_Loop_Until_Number_Found()
    {
        int target = 42;
        var guesser = new GuesserExecutor(1, 100);
        var judge = new JudgeNumberExecutor(target);

        // Loop: guesser ↔ judge (conexión bidireccional)
        var workflow = new WorkflowBuilder(guesser)
            .AddEdge(guesser, judge)   // Adivinanza (int) → juez
            .AddEdge(judge, guesser)   // Retroalimentación (GuessSignal) → adivinador
            .WithOutputFrom(judge)     // El workflow termina cuando el juez emite output
            .Build();

        // Iniciar con la señal Init para el primer intento
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, GuessSignal.Init);

        string? result = null;
        int eventCount = 0;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            eventCount++;
            if (evt is ExecutorCompletedEvent completed)
            {
                _output.WriteLine($"  {completed.ExecutorId}: {completed.Data}");
            }
            else if (evt is WorkflowOutputEvent outputEvent)
            {
                result = outputEvent.Data?.ToString();
                _output.WriteLine($"  🎯 Resultado: {result}");
            }
        }

        Assert.NotNull(result);
        Assert.Contains(target.ToString(), result!);
        _output.WriteLine(
            $"\n✅ Loop completado. El número {target} fue encontrado " +
            $"después de {eventCount} eventos en total.");
    }
}
