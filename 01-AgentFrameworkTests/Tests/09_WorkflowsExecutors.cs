using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

// ---------- Ejecutores auxiliares para la prueba de workflows ----------

/// <summary>
/// Ejecutor que convierte el texto a mayúsculas.
/// Demuestra el patrón básico Executor&lt;TIn, TOut&gt; con HandleAsync.
/// </summary>
internal sealed class UppercaseExecutor() : Executor<string, string>("Uppercase")
{
    public override ValueTask<string> HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
        => ValueTask.FromResult(message.ToUpperInvariant());
}

/// <summary>
/// Ejecutor que invierte el texto recibido.
/// </summary>
internal sealed class ReverseExecutor() : Executor<string, string>("Reverse")
{
    public override ValueTask<string> HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
        => ValueTask.FromResult(new string(message.Reverse().ToArray()));
}

/// <summary>
/// Ejecutor que cuenta las palabras del texto y retorna el resultado como cadena.
/// </summary>
internal sealed class WordCountExecutor() : Executor<string, string>("WordCount")
{
    public override ValueTask<string> HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
    {
        int count = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return ValueTask.FromResult($"Words: {count}");
    }
}

/// <summary>
/// Ejecutor que almacena datos en estado compartido y retorna una clave de referencia.
/// Demuestra el uso de QueueStateUpdateAsync en IWorkflowContext.
/// </summary>
internal sealed class StateWriterExecutor() : Executor<string, string>("StateWriter")
{
    public const string StateScopeName = "TestScope";

    public override async ValueTask<string> HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
    {
        string key = Guid.NewGuid().ToString("N");
        await context.QueueStateUpdateAsync(key, message.ToUpperInvariant(),
            scopeName: StateScopeName, ct);
        return key;
    }
}

/// <summary>
/// Ejecutor que lee datos del estado compartido y genera la salida final del workflow.
/// Usa [YieldsOutput] y context.YieldOutputAsync para emitir la salida.
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class StateReaderExecutor() : Executor<string>("StateReader")
{
    public override async ValueTask HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
    {
        var storedValue = await context.ReadStateAsync<string>(
            message, scopeName: StateWriterExecutor.StateScopeName, ct);
        await context.YieldOutputAsync($"Read from state: {storedValue}", ct);
    }
}

/// <summary>
/// Módulo 09: Workflows — Ejecutores (Executors).
/// Introduce el concepto de workflows y ejecutores en Agent Framework.
/// Un Executor es la unidad fundamental de trabajo que procesa un mensaje de entrada
/// y produce un resultado. Los ejecutores se conectan mediante edges para formar workflows.
///
/// Patrones clave:
/// - Executor&lt;TIn, TOut&gt;: recibe TIn, retorna TOut (pipeline secuencial)
/// - Executor&lt;TIn&gt;: recibe TIn, no retorna valor; usa context.YieldOutputAsync o context.SendMessageAsync
/// - WorkflowBuilder: construye el grafo de ejecutores conectados por edges
/// - InProcessExecution.RunStreamingAsync: ejecuta el workflow y devuelve un StreamingRun
/// </summary>
public class _09_WorkflowsExecutors
{
    private readonly ITestOutputHelper _output;

    public _09_WorkflowsExecutors(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Workflow secuencial simple: dos ejecutores conectados por un edge directo.
    /// El resultado de HandleAsync del primer ejecutor fluye como entrada del segundo.
    /// Patrón: Entrada("hello world") → Uppercase("HELLO WORLD") → Reverse("DLROW OLLEH")
    /// </summary>
    [Fact]
    public async Task Should_Run_Sequential_Workflow_With_Two_Executors()
    {
        // Crear los ejecutores (instancias independientes)
        var uppercase = new UppercaseExecutor();
        var reverse = new ReverseExecutor();

        // Construir el workflow:
        // - WorkflowBuilder(start) define el ejecutor inicial
        // - AddEdge(from, to) conecta ejecutores secuencialmente
        // - WithOutputFrom(executor) indica cuál executor provee la salida final
        var workflow = new WorkflowBuilder(uppercase)
            .AddEdge(uppercase, reverse)
            .WithOutputFrom(reverse)
            .Build();

        // Ejecutar el workflow en modo streaming
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "hello world");

        var capturedEvents = new List<string>();
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is ExecutorCompletedEvent completed)
            {
                capturedEvents.Add($"{completed.ExecutorId}: {completed.Data}");
                _output.WriteLine($"  Ejecutor completado — {completed.ExecutorId}: {completed.Data}");
            }
            else if (evt is WorkflowOutputEvent outputEvent)
            {
                capturedEvents.Add($"Output: {outputEvent.Data}");
                _output.WriteLine($"  Salida del workflow: {outputEvent.Data}");
            }
        }

        // Verificar que se emitieron eventos de ejecución
        Assert.NotEmpty(capturedEvents);
        _output.WriteLine("\n✅ Workflow secuencial ejecutado con 2 ejecutores conectados por edge.");
    }

    /// <summary>
    /// Workflow con tres ejecutores en cadena para demostrar pipelines más largos.
    /// Cada ejecutor recibe la salida del anterior, creando una cadena de transformaciones.
    /// Patrón: Uppercase → Reverse → WordCount
    /// </summary>
    [Fact]
    public async Task Should_Run_Three_Executor_Pipeline()
    {
        var uppercase = new UppercaseExecutor();
        var reverse = new ReverseExecutor();
        var wordCount = new WordCountExecutor();

        // Cadena de 3 ejecutores
        var workflow = new WorkflowBuilder(uppercase)
            .AddEdge(uppercase, reverse)
            .AddEdge(reverse, wordCount)
            .WithOutputFrom(wordCount)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "one two three");

        var events = new List<string>();
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is ExecutorCompletedEvent completed)
            {
                events.Add(completed.Data?.ToString() ?? "");
                _output.WriteLine($"  {completed.ExecutorId} → {completed.Data}");
            }
        }

        // Deben haberse completado al menos los 3 ejecutores
        Assert.True(events.Count >= 3, $"Se esperaban >= 3 eventos, se obtuvieron {events.Count}");
        _output.WriteLine($"\n✅ Pipeline de 3 ejecutores completado con {events.Count} eventos.");
    }

    /// <summary>
    /// Workflow que utiliza estado compartido (shared state) entre ejecutores.
    /// Un ejecutor escribe datos en el estado del workflow y otro los lee.
    /// Esto permite compartir información entre ejecutores sin pasar datos directamente.
    ///
    /// Patrón:
    /// - StateWriter: guarda el texto en mayúsculas bajo una clave generada
    /// - StateReader: lee el valor usando la clave y emite la salida con YieldOutputAsync
    /// </summary>
    [Fact]
    public async Task Should_Share_State_Between_Executors()
    {
        var writer = new StateWriterExecutor();
        var reader = new StateReaderExecutor();

        var workflow = new WorkflowBuilder(writer)
            .AddEdge(writer, reader)
            .WithOutputFrom(reader)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "shared data");

        string? outputText = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                outputText = outputEvent.Data?.ToString();
                _output.WriteLine($"  Salida: {outputText}");
            }
        }

        Assert.NotNull(outputText);
        Assert.Contains("SHARED DATA", outputText!);
        _output.WriteLine("\n✅ Estado compartido entre ejecutores funciona correctamente.");
    }
}
