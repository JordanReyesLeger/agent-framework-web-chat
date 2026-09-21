using AFWebChat.Agents;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

/// <summary>
/// Agente simple que usa el patrón "Foundry Agent versioned" de Microsoft.Agents.AI.Foundry.
/// El agente vive en Foundry; aquí solo se publica su definición y se consume vía
/// AsAIAgent(agentRecord). Soporta sesiones persistentes, streaming y todas las
/// funcionalidades estándar de AIAgent.
///
/// Nota sobre herramientas: GenerateChart es una función LOCAL que se ejecuta en este proceso,
/// pero debe declararse en la definición publicada en Foundry para que el modelo la vea
/// (ver <see cref="FoundryAgentProvisioning"/>). La lista que se pasa a AsAIAgent aporta la
/// implementación con la que el SDK resuelve las llamadas que solicita Foundry.
/// </summary>
public static class FoundrySimpleBotAgent
{
    public const string Name = "FoundrySimpleBot";
    private const string FoundryAgentName = "AFWebChat-FoundrySimpleBot";

    private const string FoundryInstructions = @"Eres FoundrySimpleBot — un asistente de IA amigable y minimalista
que se ejecuta como agente versionado en Azure AI Foundry.

Comportamientos clave:
- Mantén las respuestas concisas y conversacionales (2-3 párrafos máximo).
- Usa un tono cálido y cercano.
- Cuando te pregunten qué puedes hacer, explica que eres un agente básico de chat
  publicado en Azure AI Foundry como agente versionado, con una herramienta de gráficos
  que se ejecuta del lado del cliente.
- Si alguien pregunta sobre tu arquitectura, explica que fuiste creado con
  Microsoft.Agents.AI.Foundry usando el patrón Foundry Agent versioned —
  gestionado via AgentAdministrationClient y consumido con AsAIAgent(agentRecord).
- Si el usuario pide graficar datos o una visualización ayuda a explicar una respuesta
  numérica, usa GenerateChart e incluye su resultado tal cual en tu respuesta.
- Usa formato markdown cuando mejore la legibilidad.
- Responde siempre en español a menos que el usuario escriba en otro idioma.";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente simple de Foundry (versioned) — se crea y gestiona en el portal de Foundry. Con gráficos del lado del cliente.",
        Category = "Foundry",
        Icon = "👋",
        Color = "#4CAF50",
        Tools = ["GenerateChart"],
        ExamplePrompts =
        [
            "Hola, ¿qué puedes hacer?",
            "Cuéntame un dato curioso sobre inteligencia artificial",
            "Grafica en barras: Ene 10, Feb 25, Mar 18"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<AIAgent>>();
            var endpointProject = config["AzureOpenAI:EndpointProject"];
            var chatDeployment = config["AzureOpenAI:ChatDeployment"] ?? "gpt-5.4";

            if (string.IsNullOrEmpty(endpointProject) || !endpointProject.Contains("api/projects"))
                throw new InvalidOperationException(
                    "FoundrySimpleBot requiere Azure AI Foundry configurado. " +
                    "Configura 'AzureOpenAI:EndpointProject' en appsettings.json.");

            var aiProjectClient = new AIProjectClient(
                new Uri(endpointProject),
                new DefaultAzureCredential());

            // Herramientas locales: se ejecutan en este proceso, pero deben declararse en
            // Foundry para que el modelo sepa que existen.
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromStatic<ChartPlugin>());
            var declaredTools = FoundryAgentProvisioning.ToResponseTools(tools).ToList();

            var agentRecord = FoundryAgentProvisioning.EnsureAgentVersion(
                aiProjectClient,
                FoundryAgentName,
                buildDefinition: () =>
                {
                    var definition = new DeclarativeAgentDefinition(model: chatDeployment)
                    {
                        Instructions = FoundryInstructions
                    };
                    foreach (var tool in declaredTools) definition.Tools.Add(tool);
                    return definition;
                },
                revisionSource: chatDeployment + FoundryInstructions +
                    string.Join(",", tools.Select(t => t.Name)),
                logger: logger);

            // Wrap as standard AIAgent. La lista de herramientas aporta la implementación
            // que resuelve las llamadas que solicita Foundry.
#pragma warning disable OPENAI001
            Microsoft.Agents.AI.Foundry.FoundryAgent agent = aiProjectClient.AsAIAgent(agentRecord, tools);
#pragma warning restore OPENAI001
            return agent;
        }
    };
}
