using Microsoft.Agents.AI.Workflows;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

// ---------- Modelos y ejecutores para pruebas de enrutamiento ----------

/// <summary>
/// Resultado del análisis de sentimiento (simulado, sin IA).
/// </summary>
internal sealed class SentimentResult
{
    public bool IsPositive { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Ejecutor que analiza el sentimiento del texto de forma determinística.
/// Retorna positivo si contiene palabras positivas, negativo en caso contrario.
/// No utiliza IA — ideal para pruebas predecibles.
/// </summary>
internal sealed class SentimentAnalyzerExecutor() : Executor<string, SentimentResult>("SentimentAnalyzer")
{
    private static readonly string[] PositiveWords =
        ["good", "great", "excellent", "happy", "love", "wonderful", "amazing"];

    public override ValueTask<SentimentResult> HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
    {
        bool isPositive = PositiveWords.Any(w =>
            message.Contains(w, StringComparison.OrdinalIgnoreCase));
        return ValueTask.FromResult(new SentimentResult
        {
            IsPositive = isPositive,
            Text = message
        });
    }
}

/// <summary>
/// Ejecutor que procesa mensajes positivos y genera la salida del workflow.
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class PositiveHandlerExecutor() : Executor<SentimentResult>("PositiveHandler")
{
    public override async ValueTask HandleAsync(
        SentimentResult message, IWorkflowContext context, CancellationToken ct = default)
        => await context.YieldOutputAsync($"POSITIVE: {message.Text}", ct);
}

/// <summary>
/// Ejecutor que procesa mensajes negativos y genera la salida del workflow.
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class NegativeHandlerExecutor() : Executor<SentimentResult>("NegativeHandler")
{
    public override async ValueTask HandleAsync(
        SentimentResult message, IWorkflowContext context, CancellationToken ct = default)
        => await context.YieldOutputAsync($"NEGATIVE: {message.Text}", ct);
}

/// <summary>
/// Ejecutor que aplica formato al texto con corchetes.
/// </summary>
internal sealed class FormatterExecutor() : Executor<string, string>("Formatter")
{
    public override ValueTask<string> HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
        => ValueTask.FromResult($"[{message}]");
}

/// <summary>
/// Ejecutor final que genera la salida del workflow con el texto procesado.
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class FinalOutputExecutor() : Executor<string>("FinalOutput")
{
    public override async ValueTask HandleAsync(
        string message, IWorkflowContext context, CancellationToken ct = default)
        => await context.YieldOutputAsync($"Final: {message}", ct);
}

/// <summary>
/// Módulo 10: Workflows — Edges y enrutamiento condicional.
/// Los edges definen cómo fluyen los datos entre ejecutores en un workflow.
///
/// Tipos de edges:
/// - Edge directo: AddEdge(from, to) — siempre se ejecuta
/// - Edge condicional: AddEdge(from, to, condition: Func&lt;object?, bool&gt;) — solo si la condición es verdadera
///
/// El enrutamiento condicional permite crear workflows con bifurcaciones
/// donde diferentes caminos se ejecutan según el resultado de un ejecutor.
/// La función condition recibe el resultado del ejecutor origen como object? y debe retornar bool.
/// </summary>
public class _10_WorkflowsEdges
{
    private readonly ITestOutputHelper _output;

    public _10_WorkflowsEdges(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Edges directos en cadena: cada ejecutor se conecta al siguiente sin condiciones.
    /// El resultado fluye secuencialmente: Uppercase → Formatter → FinalOutput.
    /// Demuestra que con AddEdge(from, to) el flujo es automático e incondicional.
    /// </summary>
    [Fact]
    public async Task Should_Route_Through_Direct_Edges()
    {
        // Reutilizamos UppercaseExecutor del módulo 09 (mismo namespace)
        var uppercase = new UppercaseExecutor();
        var formatter = new FormatterExecutor();
        var finalOutput = new FinalOutputExecutor();

        var workflow = new WorkflowBuilder(uppercase)
            .AddEdge(uppercase, formatter)
            .AddEdge(formatter, finalOutput)
            .WithOutputFrom(finalOutput)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "test data");

        string? result = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is ExecutorCompletedEvent completed)
                _output.WriteLine($"  {completed.ExecutorId} → {completed.Data}");
            else if (evt is WorkflowOutputEvent outputEvent)
                result = outputEvent.Data?.ToString();
        }

        Assert.NotNull(result);
        Assert.Contains("TEST DATA", result!);
        _output.WriteLine($"\n✅ Enrutamiento directo completado: {result}");
    }

    /// <summary>
    /// Edges condicionales: el flujo se bifurca según el resultado del análisis.
    /// Si el texto contiene palabras positivas → PositiveHandler.
    /// Si no → NegativeHandler.
    ///
    /// La condición es una función Func&lt;object?, bool&gt; que recibe el resultado
    /// del ejecutor origen y determina si el edge debe activarse.
    /// </summary>
    [Fact]
    public async Task Should_Route_Conditionally_To_Positive_Handler()
    {
        var analyzer = new SentimentAnalyzerExecutor();
        var positiveHandler = new PositiveHandlerExecutor();
        var negativeHandler = new NegativeHandlerExecutor();

        // Los edges condicionales usan el parámetro condition:
        // El resultado del analyzer (SentimentResult) se pasa como object? a la condición
        var workflow = new WorkflowBuilder(analyzer)
            .AddEdge<SentimentResult>(analyzer, positiveHandler,
                condition: result => result is SentimentResult s && s.IsPositive)
            .AddEdge<SentimentResult>(analyzer, negativeHandler,
                condition: result => result is SentimentResult s && !s.IsPositive)
            .WithOutputFrom(positiveHandler, negativeHandler)
            .Build();

        // Texto positivo → debería ir al PositiveHandler
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "This is a great day!");

        string? output = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                output = outputEvent.Data?.ToString();
                _output.WriteLine($"  Resultado: {output}");
            }
        }

        Assert.NotNull(output);
        Assert.Contains("POSITIVE", output!);
        _output.WriteLine("\n✅ Enrutamiento condicional ejecutó la ruta positiva correctamente.");
    }

    /// <summary>
    /// Verifica que el edge condicional dirige al camino negativo cuando corresponde.
    /// Texto sin palabras positivas → NegativeHandler.
    /// Complementa la prueba anterior para validar ambas ramas del flujo.
    /// </summary>
    [Fact]
    public async Task Should_Route_Conditionally_To_Negative_Handler()
    {
        var analyzer = new SentimentAnalyzerExecutor();
        var positiveHandler = new PositiveHandlerExecutor();
        var negativeHandler = new NegativeHandlerExecutor();

        var workflow = new WorkflowBuilder(analyzer)
            .AddEdge<SentimentResult>(analyzer, positiveHandler,
                condition: result => result is SentimentResult s && s.IsPositive)
            .AddEdge<SentimentResult>(analyzer, negativeHandler,
                condition: result => result is SentimentResult s && !s.IsPositive)
            .WithOutputFrom(positiveHandler, negativeHandler)
            .Build();

        // Texto negativo (sin palabras positivas) → debería ir al NegativeHandler
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input: "This is terrible and awful");

        string? output = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                output = outputEvent.Data?.ToString();
                _output.WriteLine($"  Resultado: {output}");
            }
        }

        Assert.NotNull(output);
        Assert.Contains("NEGATIVE", output!);
        _output.WriteLine("\n✅ Enrutamiento condicional ejecutó la ruta negativa correctamente.");
    }
}
