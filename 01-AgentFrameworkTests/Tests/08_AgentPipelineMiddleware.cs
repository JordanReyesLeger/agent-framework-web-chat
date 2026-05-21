using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 08: Pipeline y Middleware del agente.
/// Demuestra cómo extender el comportamiento del agente usando middleware,
/// que permite interceptar, modificar o registrar las llamadas antes y después de la ejecución.
/// El pipeline sigue el patrón: Middleware → Agente → Modelo.
/// </summary>
public class _08_AgentPipelineMiddleware
{
    private readonly ITestOutputHelper _output;

    public _08_AgentPipelineMiddleware(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---------- Pruebas ----------

    /// <summary>
    /// Agrega un middleware simple que registra las invocaciones antes y después de la ejecución.
    /// Patrón clásico de logging/observabilidad para agentes.
    /// </summary>
    [Fact]
    public async Task Should_Execute_Middleware_Before_And_After()
    {
        var middlewareLogs = new List<string>();

        // Crear el agente con middleware usando el patrón Builder
        // AsBuilder() convierte el agente en un builder al que podemos agregar middleware
        AIAgent baseAgent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil. Responde en una oración.");

        // Use() agrega un middleware que intercepta la ejecución
        var agent = baseAgent.AsBuilder()
            .Use(async (messages, session, options, next, ct) =>
            {
                // ANTES de la ejecución del agente
                middlewareLogs.Add($"[BEFORE] Request received at {DateTime.UtcNow:HH:mm:ss}");

                // Continuar con la cadena de ejecución (llama al siguiente middleware o al agente)
                await next(messages, session, options, ct);

                // DESPUÉS de la ejecución del agente
                middlewareLogs.Add($"[AFTER] Response generated at {DateTime.UtcNow:HH:mm:ss}");
            })
            .Build();

        AgentSession agentSession = await agent.CreateSessionAsync();
        AgentResponse response = await agent.RunAsync("What is the meaning of life?", agentSession);

        // Verificar que el middleware se ejecutó en el orden correcto
        Assert.Equal(2, middlewareLogs.Count);
        Assert.StartsWith("[BEFORE]", middlewareLogs[0]);
        Assert.StartsWith("[AFTER]", middlewareLogs[1]);

        _output.WriteLine("✅ Middleware ejecutado correctamente:");
        foreach (var log in middlewareLogs)
        {
            _output.WriteLine($"   {log}");
        }
        _output.WriteLine($"   Respuesta: {response.Text}");
    }

    /// <summary>
    /// Encadena múltiples middlewares para crear un pipeline de procesamiento.
    /// Los middlewares se ejecutan en el orden en que se agregan (como capas de cebolla).
    /// </summary>
    [Fact]
    public async Task Should_Chain_Multiple_Middleware()
    {
        var executionOrder = new List<string>();

        AIAgent baseAgent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil. Responde brevemente.");

        var agent = baseAgent.AsBuilder()
            // Middleware 1: Logging (capa externa)
            .Use(async (messages, session, options, next, ct) =>
            {
                executionOrder.Add("1-ENTER: Logging middleware");
                await next(messages, session, options, ct);
                executionOrder.Add("1-EXIT: Logging middleware");
            })
            // Middleware 2: Timing (capa intermedia)
            .Use(async (messages, session, options, next, ct) =>
            {
                executionOrder.Add("2-ENTER: Timing middleware");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await next(messages, session, options, ct);
                sw.Stop();
                executionOrder.Add($"2-EXIT: Timing middleware ({sw.ElapsedMilliseconds}ms)");
            })
            // Middleware 3: Validation (capa interna, más cerca del agente)
            .Use(async (messages, session, options, next, ct) =>
            {
                executionOrder.Add("3-ENTER: Validation middleware");
                await next(messages, session, options, ct);
                executionOrder.Add("3-EXIT: Validation middleware");
            })
            .Build();

        AgentSession agentSession = await agent.CreateSessionAsync();
        AgentResponse response = await agent.RunAsync("Hello!", agentSession);

        // Verificar el patrón de cebolla: 1→2→3→agente→3→2→1
        Assert.Equal(6, executionOrder.Count);

        _output.WriteLine("✅ Pipeline de middlewares ejecutado (patrón cebolla):");
        for (int i = 0; i < executionOrder.Count; i++)
        {
            _output.WriteLine($"   [{i + 1}] {executionOrder[i]}");
        }
        _output.WriteLine($"   Respuesta: {response.Text}");
    }

    /// <summary>
    /// Middleware que modifica el comportamiento inyectando contexto adicional.
    /// Ejemplo: agregar un disclaimer al final de cada respuesta.
    /// </summary>
    [Fact]
    public async Task Should_Use_Middleware_To_Track_With_Metadata()
    {
        AIAgent baseAgent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil. Siempre responde de forma concisa.");

        bool middlewareExecuted = false;
        string? capturedQuestion = null;

        var agent = baseAgent.AsBuilder()
            .Use(async (messages, session, options, next, ct) =>
            {
                // Capturar metadatos antes de la ejecución
                middlewareExecuted = true;
                capturedQuestion = messages?.LastOrDefault()?.Text;

                _output.WriteLine($"🔍 Middleware capturó: '{capturedQuestion}'");

                await next(messages, session, options, ct);

                _output.WriteLine($"🔍 Middleware: Respuesta completada exitosamente");
            })
            .Build();

        AgentSession agentSession = await agent.CreateSessionAsync();
        AgentResponse response = await agent.RunAsync("What is C#?", agentSession);

        Assert.True(middlewareExecuted, "El middleware no se ejecutó");
        Assert.NotNull(capturedQuestion);
        Assert.NotNull(response.Text);

        _output.WriteLine($"\n✅ Middleware con metadatos:");
        _output.WriteLine($"   Pregunta capturada: {capturedQuestion}");
        _output.WriteLine($"   Respuesta: {response.Text}");
    }
}
