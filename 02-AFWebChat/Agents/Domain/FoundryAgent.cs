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
/// Agente orquestador que usa el patrón "Foundry Agent versioned" de Microsoft.Agents.AI.Foundry.
/// Publica en Foundry un agente con una herramienta OpenAPI que llama a /api/chat/send de esta
/// misma app para delegar trabajo a agentes especializados, y se consume vía AsAIAgent(agentRecord).
///
/// Nota sobre herramientas: combina los dos modos. La tool OpenAPI la ejecuta Foundry; GenerateChart
/// es una función local que se declara en la definición publicada (para que el modelo la vea) pero
/// se ejecuta en este proceso — ver <see cref="FoundryAgentProvisioning"/>.
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

También tienes GenerateChart, que se ejecuta localmente: úsala cuando el usuario pida un gráfico
o cuando la respuesta de un agente contenga cifras que se entiendan mejor visualmente. Incluye su
resultado tal cual en tu respuesta.
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
            Tools = ["af-webchat-api", "GenerateChart"],
            ExamplePrompts =
            [
                "Pregúntale al agente GeneralAssistant qué es Semantic Kernel",
                "Usa el agente Translator para traducir 'hello world' al español",
                "Grafica en barras: Ene 10, Feb 25, Mar 18"
            ],
            Factory = sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<AIAgent>>();
                var endpointProject = config["AzureOpenAI:EndpointProject"];
                var chatDeployment = config["AzureOpenAI:ChatDeployment"] ?? "gpt-5.4";
                var tunnelUrl = config["DevTunnel:Url"] ?? "https://localhost:5001";

                if (string.IsNullOrEmpty(endpointProject) || !endpointProject.Contains("api/projects"))
                    throw new InvalidOperationException(
                        "FoundryAgent requiere Azure AI Foundry configurado. " +
                        "Configura 'AzureOpenAI:EndpointProject' en appsettings.json.");

                var aiProjectClient = new AIProjectClient(
                    new Uri(endpointProject),
                    new DefaultAzureCredential());

                // Herramientas locales (se ejecutan aquí) — se declaran en Foundry junto a la
                // herramienta OpenAPI, que sí se ejecuta del lado del servicio.
                var tools = new List<AITool>();
                tools.AddRange(AIFunctionFactoryExtensions.CreateFromStatic<ChartPlugin>());
                var declaredTools = FoundryAgentProvisioning.ToResponseTools(tools).ToList();

                var agentRecord = FoundryAgentProvisioning.EnsureAgentVersion(
                    aiProjectClient,
                    FoundryAgentName,
                    buildDefinition: () =>
                    {
                        var specData = BinaryData.FromString(
                            System.Text.Json.JsonSerializer.Serialize(BuildOpenApiSpec(tunnelUrl)));

                        var definition = new DeclarativeAgentDefinition(model: chatDeployment)
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
                        foreach (var tool in declaredTools) definition.Tools.Add(tool);
                        return definition;
                    },
                    // La URL del túnel forma parte de la definición: si cambia, hay que republicar.
                    revisionSource: chatDeployment + FoundryInstructions + tunnelUrl +
                        string.Join(",", tools.Select(t => t.Name)),
                    logger: logger);

                // Wrap as standard AIAgent using the Foundry Agent versioned pattern.
                // La lista de herramientas aporta la implementación local de GenerateChart.
#pragma warning disable OPENAI001 // FoundryAgent is in preview
                Microsoft.Agents.AI.Foundry.FoundryAgent agent = aiProjectClient.AsAIAgent(agentRecord, tools);
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
