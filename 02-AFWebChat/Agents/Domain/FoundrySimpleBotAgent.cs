using AFWebChat.Agents;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;

namespace AFWebChat.Agents.Domain;

/// <summary>
/// Agente simple que usa el patrón "Foundry Agent versioned" de Microsoft.Agents.AI.Foundry.
/// Crea/recupera un agente versionado en Foundry sin herramientas — solo conversación directa.
/// Usa AgentAdministrationClient para gestionar el agente y AsAIAgent(agentRecord) para consumirlo.
/// Soporta sesiones persistentes, streaming y todas las funcionalidades estándar de AIAgent.
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
  publicado en Azure AI Foundry como agente versionado, sin herramientas especiales.
- Si alguien pregunta sobre tu arquitectura, explica que fuiste creado con
  Microsoft.Agents.AI.Foundry usando el patrón Foundry Agent versioned —
  gestionado via AgentAdministrationClient y consumido con AsAIAgent(agentRecord).
- Usa formato markdown cuando mejore la legibilidad.
- Responde siempre en español a menos que el usuario escriba en otro idioma.";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente simple de Foundry (versioned) — se crea y gestiona en el portal de Foundry. Sin herramientas.",
        Category = "Foundry",
        Icon = "👋",
        Color = "#4CAF50",
        ExamplePrompts =
        [
            "Hola, ¿qué puedes hacer?",
            "Cuéntame un dato curioso sobre inteligencia artificial",
            "Escribe un haiku sobre programación"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<AIAgent>>();
            var endpointProject = config["AzureOpenAI:EndpointProject"];
            var chatDeployment = config["AzureOpenAI:ChatDeployment"] ?? "gpt-4o";

            if (string.IsNullOrEmpty(endpointProject) || !endpointProject.Contains("api/projects"))
                throw new InvalidOperationException(
                    "FoundrySimpleBot requiere Azure AI Foundry configurado. " +
                    "Configura 'AzureOpenAI:EndpointProject' en appsettings.json.");

            var aiProjectClient = new AIProjectClient(
                new Uri(endpointProject),
                new DefaultAzureCredential());

            // Try to get existing versioned agent from Foundry
            ProjectsAgentRecord? agentRecord = null;
            try
            {
                agentRecord = aiProjectClient.AgentAdministrationClient.GetAgent(FoundryAgentName);
                logger.LogInformation("Found existing Foundry versioned agent '{AgentName}'", FoundryAgentName);
            }
            catch (Azure.RequestFailedException) { }
            catch (System.ClientModel.ClientResultException ex) when (ex.Status == 404)
            {
                logger.LogInformation("Foundry agent '{AgentName}' not found, creating...", FoundryAgentName);
            }

            // If not found, create it (no tools — simple chat agent)
            if (agentRecord is null)
            {
                var agentDefinition = new DeclarativeAgentDefinition(model: chatDeployment)
                {
                    Instructions = FoundryInstructions
                };

                var agentVersion = aiProjectClient.AgentAdministrationClient.CreateAgentVersion(
                    agentName: FoundryAgentName,
                    options: new(agentDefinition));

                logger.LogInformation("Created Foundry versioned agent '{AgentName}' (id: {Id}, version: {Version})",
                    FoundryAgentName, agentVersion.Value.Id, agentVersion.Value.Version);

                // Wait for agent propagation in Foundry
                logger.LogInformation("Waiting for agent '{AgentName}' to propagate in Foundry...", FoundryAgentName);
                Thread.Sleep(5000);

                agentRecord = aiProjectClient.AgentAdministrationClient.GetAgent(FoundryAgentName);
            }

            // Wrap as standard AIAgent
#pragma warning disable OPENAI001
            Microsoft.Agents.AI.Foundry.FoundryAgent agent = aiProjectClient.AsAIAgent(agentRecord);
#pragma warning restore OPENAI001
            return agent;
        }
    };
}
