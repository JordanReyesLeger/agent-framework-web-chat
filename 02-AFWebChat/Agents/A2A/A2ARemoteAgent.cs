using A2A;
using AFWebChat.Models;
using Microsoft.Agents.AI;

namespace AFWebChat.Agents.A2A;

/// <summary>
/// Adapta un agente remoto que habla el protocolo A2A (Agent2Agent) a un <see cref="AIAgent"/>
/// estándar del Agent Framework, de modo que se comporta igual que cualquier otro agente de
/// AF-WebChat: aparece en el catálogo, soporta streaming, sesiones, workflows y orquestaciones.
/// </summary>
/// <remarks>
/// El agente remoto puede estar escrito en cualquier lenguaje o framework (Python, Go, LangGraph,
/// CrewAI, etc.). Lo único que se necesita es su endpoint A2A.
/// </remarks>
public static class A2ARemoteAgent
{
    /// <summary>Categoría bajo la que se agrupan los agentes A2A en la UI.</summary>
    public const string CategoryName = "A2A";

    /// <summary>Nombre del <see cref="HttpClient"/> con nombre usado para hablar con agentes A2A remotos.</summary>
    public const string HttpClientName = "A2ARemote";

    public static AgentDefinition CreateDefinition(A2ARemoteAgentSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Url);

        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"A2A: la URL del agente remoto '{settings.Name}' no es una URL http(s) absoluta válida: '{settings.Url}'.");
        }

        var description = string.IsNullOrWhiteSpace(settings.Description)
            ? $"Agente remoto vía protocolo A2A ({endpoint})."
            : settings.Description!;

        return new AgentDefinition
        {
            Name = settings.Name,
            Description = description,
            Category = CategoryName,
            Icon = settings.Icon ?? "🛰️",
            Color = settings.Color ?? "#7b61ff",
            ExamplePrompts = settings.ExamplePrompts is { Length: > 0 }
                ? settings.ExamplePrompts
                :
                [
                    "¿Qué puedes hacer?",
                    "Preséntate y describe tus capacidades",
                    "Explícame qué es el protocolo A2A"
                ],
            SupportsStreaming = true,
            Factory = sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                var client = new A2AClient(endpoint, httpClientFactory.CreateClient(HttpClientName));

                return client.AsAIAgent(
                    id: settings.Name,
                    name: settings.Name,
                    description: description,
                    loggerFactory: loggerFactory);
            }
        };
    }
}
