using AFWebChat.Agents;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;

namespace AFWebChat.Agents.Domain;

/// <summary>
/// Agente orquestador que usa el patrón "Foundry Agent versioned" de Microsoft.Agents.AI.Foundry.
/// Crea/recupera un agente versionado en Foundry con herramienta OpenAPI que llama a AF-WebChat's
/// /api/chat/send para delegar trabajo a agentes especializados.
/// Usa AgentAdministrationClient para gestionar el agente y AsAIAgent(agentRecord) para consumirlo.
/// Soporta sesiones persistentes, streaming y todas las funcionalidades estándar de AIAgent.
/// </summary>
public static class FoundryOrchestratorAgent
{
    private const string FoundryAgentName = "AFWebChat-FoundryOrchestrator";

    private const string FoundryInstructions = @"Eres FoundryOrchestrator — un agente orquestador versionado en Azure AI Foundry
con una herramienta OpenAPI que llama a AF-WebChat.

Tienes acceso a la API de AF-WebChat via la herramienta OpenAPI 'af-webchat-api'.
Cuando el usuario te pida algo, usa esa herramienta para enviar el mensaje al agente apropiado.

Agentes disponibles: GeneralAssistant, Translator, Summarizer, LegalAdvisor, CodeReviewer, SqlAzure.

Si el usuario no especifica un agente, usa 'GeneralAssistant'.
Siempre pasa el mensaje del usuario a la herramienta y devuelve la respuesta del agente.
Responde en español a menos que el usuario escriba en otro idioma.";

    public static AgentDefinition CreateDefinition()
    {
        return new AgentDefinition
        {
            Name = "FoundryAgent",
            Description = "Agente orquestador de Foundry (versioned) con herramienta OpenAPI — delega a agentes de AF-WebChat via API.",
            Category = "Foundry",
            Icon = "🏗️",
            Color = "#0078d4",
            ExamplePrompts =
            [
                "Pregúntale al agente GeneralAssistant qué es Semantic Kernel",
                "Usa el agente Translator para traducir 'hello world' al español",
                "Envía un mensaje al agente Summarizer"
            ],
            Factory = sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<AIAgent>>();
                var endpointProject = config["AzureOpenAI:EndpointProject"];
                var chatDeployment = config["AzureOpenAI:ChatDeployment"] ?? "gpt-4o";
                var tunnelUrl = config["DevTunnel:Url"] ?? "https://localhost:5001";

                if (string.IsNullOrEmpty(endpointProject) || !endpointProject.Contains("api/projects"))
                    throw new InvalidOperationException(
                        "FoundryAgent requiere Azure AI Foundry configurado. " +
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
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    logger.LogInformation("Foundry agent '{AgentName}' not found, creating...", FoundryAgentName);
                }
                catch (System.ClientModel.ClientResultException ex) when (ex.Status == 404)
                {
                    logger.LogInformation("Foundry agent '{AgentName}' not found, creating...", FoundryAgentName);
                }

                // If not found, create it with OpenAPI tool
                if (agentRecord is null)
                {
                    var openApiSpec = BuildOpenApiSpec(tunnelUrl);
                    var specData = BinaryData.FromString(
                        System.Text.Json.JsonSerializer.Serialize(openApiSpec));

                    var agentDefinition = new DeclarativeAgentDefinition(model: chatDeployment)
                    {
                        Instructions = FoundryInstructions,
                        Tools =
                        {
                            new OpenAPITool(new OpenApiFunctionDefinition(
                                "af-webchat-api",
                                specData,
                                new OpenAPIAnonymousAuthenticationDetails())
                            {
                                Description = "Envía un mensaje a un agente de AF-WebChat y devuelve la respuesta."
                            })
                        }
                    };

                    var agentVersion = aiProjectClient.AgentAdministrationClient.CreateAgentVersion(
                        agentName: FoundryAgentName,
                        options: new(agentDefinition));

                    logger.LogInformation("Created Foundry versioned agent '{AgentName}' (id: {Id}, version: {Version})",
                        FoundryAgentName, agentVersion.Value.Id, agentVersion.Value.Version);

                    agentRecord = aiProjectClient.AgentAdministrationClient.GetAgent(FoundryAgentName);
                }

                // Wrap as standard AIAgent using the Foundry Agent versioned pattern
#pragma warning disable OPENAI001 // FoundryAgent is in preview
                Microsoft.Agents.AI.Foundry.FoundryAgent agent = aiProjectClient.AsAIAgent(agentRecord);
#pragma warning restore OPENAI001
                return agent;
            }
        };
    }

    /// <summary>
    /// Builds the OpenAPI 3.0 spec object for the AF-WebChat /api/chat/send endpoint.
    /// </summary>
    private static object BuildOpenApiSpec(string baseUrl)
    {
        return new
        {
            openapi = "3.0.0",
            info = new
            {
                title = "AF-WebChat Agent API",
                version = "1.0.0",
                description = "API para enviar mensajes a agentes de AF-WebChat"
            },
            servers = new[] { new { url = baseUrl } },
            paths = new Dictionary<string, object>
            {
                ["/api/chat/send"] = new
                {
                    post = new
                    {
                        operationId = "chat-with-agent",
                        summary = "Envía un mensaje a un agente de AF-WebChat",
                        description = "Envía un mensaje a un agente específico y devuelve la respuesta.",
                        requestBody = new
                        {
                            required = true,
                            content = new Dictionary<string, object>
                            {
                                ["application/json"] = new
                                {
                                    schema = new
                                    {
                                        type = "object",
                                        required = new[] { "sessionId", "message", "agentName" },
                                        properties = new Dictionary<string, object>
                                        {
                                            ["sessionId"] = new { type = "string", description = "ID de sesión" },
                                            ["message"] = new { type = "string", description = "Mensaje del usuario" },
                                            ["agentName"] = new { type = "string", description = "Agente: GeneralAssistant, Translator, Summarizer, LegalAdvisor, CodeReviewer, SqlAzure" }
                                        }
                                    }
                                }
                            }
                        },
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new
                            {
                                description = "Respuesta del agente",
                                content = new Dictionary<string, object>
                                {
                                    ["application/json"] = new
                                    {
                                        schema = new
                                        {
                                            type = "object",
                                            properties = new Dictionary<string, object>
                                            {
                                                ["sessionId"] = new { type = "string" },
                                                ["agentName"] = new { type = "string" },
                                                ["text"] = new { type = "string", description = "Respuesta del agente" },
                                                ["timestamp"] = new { type = "string", format = "date-time" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
