using AgentFrameworkTests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 01: Ejecución de agentes.
/// Demuestra RunAsync (respuesta completa) y RunStreamingAsync (respuesta en streaming).
/// </summary>
public class _01_RunningAgents
{
    private readonly ITestOutputHelper _output;

    public _01_RunningAgents(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Ejecuta un agente con RunAsync() para obtener la respuesta completa de una sola vez.
    /// Ideal cuando necesitas la respuesta completa antes de procesarla.
    /// </summary>
    [Fact]
    public async Task Should_Run_Agent_And_Get_Complete_Response()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un experto en geografía. Siempre responde en una oración.");

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // RunAsync espera a que se complete toda la respuesta
        AgentResponse response = await agent.RunAsync("What is the capital of France?", session);

        Assert.NotNull(response);
        Assert.NotNull(response.Text);

        _output.WriteLine("✅ Respuesta completa (RunAsync):");
        _output.WriteLine($"   {response.Text}");
    }

    /// <summary>
    /// Ejecuta un agente con RunStreamingAsync() para recibir la respuesta token por token.
    /// Ideal para mostrar respuestas en tiempo real al usuario (como ChatGPT).
    /// </summary>
    [Fact]
    public async Task Should_Stream_Response_Token_By_Token()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un profesor de ciencias. Explica los conceptos de forma simple en 2-3 oraciones.");

        // Crear sesión (requerida para RunStreamingAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // RunStreamingAsync devuelve los tokens a medida que se generan
        var fullResponse = new System.Text.StringBuilder();

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Explain photosynthesis.", session))
        {
            // Cada 'update' contiene un fragmento incremental de la respuesta
            if (update.Text is not null)
            {
                fullResponse.AppendLine(update.Text);
            }
        }

        var responseText = fullResponse.ToString();
        Assert.NotEmpty(responseText);

        _output.WriteLine("✅ Respuesta en streaming (RunStreamingAsync):");
        _output.WriteLine($"   {responseText}");
    }

    /// <summary>
    /// Ejecuta un agente con opciones de ejecución personalizadas (ChatClientAgentRunOptions).
    /// Permite configurar parámetros como temperatura, tokens máximos, etc., por ejecución.
    /// </summary>
    [Fact]
    public async Task Should_Run_Agent_With_Custom_Run_Options()
    {
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un escritor creativo. Escribe poemas cortos.");

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Configurar opciones personalizadas para esta ejecución específica
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = 0.9f, // Alta temperatura = más creatividad
            MaxOutputTokens = 100
        });

        AgentResponse response = await agent.RunAsync("Write a haiku about programming.", session, runOptions);

        Assert.NotNull(response);
        Assert.NotNull(response.Text);

        _output.WriteLine("✅ Respuesta con opciones personalizadas (Temperature=0.9):");
        _output.WriteLine($"   {response.Text}");
    }
}
