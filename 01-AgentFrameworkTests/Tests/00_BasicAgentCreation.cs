using AgentFrameworkTests.Helpers;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Xunit;
using Xunit.Abstractions;

namespace AgentFrameworkTests.Tests;

/// <summary>
/// Módulo 00: Creación básica de agentes.
/// Demuestra las diferentes formas de crear un agente usando Agent Framework.
/// </summary>
public class _00_BasicAgentCreation
{
    private readonly ITestOutputHelper _output;

    public _00_BasicAgentCreation(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Crea un agente con opciones personalizadas como nombre, instrucciones y descripción.
    /// Las instrucciones definen el comportamiento y personalidad del agente.
    /// </summary>
    [Fact]
    public async Task Should_Create_Agent_With_Custom_Options()
    {
        // Agente basico:
        /*
        using System;
        using Azure.AI.OpenAI;
        using Azure.Identity;
        using Microsoft.Agents.AI;

        AIAgent agent = new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
                new AzureCliCredential())
            .GetChatClient("gpt-4o-mini")
            .AsAIAgent(instructions: "You are a friendly assistant. Keep your answers brief.");

        Console.WriteLine(await agent.RunAsync("What is the largest city in France?"));
        */


        // Crear el agente con opciones personalizadas
        // Las instrucciones son el "system prompt" que guía el comportamiento del agente
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente de taller. Siempre responde de forma concisa y educativa.",
            name: "AsistenteWorkshop",
            description: "Agente de demostración para el taller de Agent Framework");

        Assert.NotNull(agent);
        Assert.Equal("AsistenteWorkshop", agent.Name);

        _output.WriteLine($"✅ Agente '{agent.Name}' creado con instrucciones personalizadas");
        _output.WriteLine($"   Descripción: {agent.Description}");
    }

    /// <summary>
    /// Crea un agente y envía un mensaje simple para obtener una respuesta.
    /// Demuestra el flujo completo: crear agente → enviar mensaje → recibir respuesta.
    /// </summary>
    [Fact]
    public async Task Should_Create_Agent_And_Get_Basic_Response()
    {
        // Crear un agente con instrucciones específicas
        AIAgent agent = TestConfiguration.CreateAgent(
            instructions: "Eres un asistente útil. Siempre responde en una oración corta.");

        // Crear sesión (requerida para RunAsync)
        AgentSession session = await agent.CreateSessionAsync();

        // Enviar un mensaje al agente y obtener la respuesta
        AgentResponse response = await agent.RunAsync("Cuanto es 2 + 2?", session);

        // Verificar que obtuvimos una respuesta válida
        Assert.NotNull(response);
        Assert.NotNull(response.Text);

        _output.WriteLine($"✅ Respuesta del agente: {response.Text}");
    }
}
