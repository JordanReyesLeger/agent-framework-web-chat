using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

public static class AzureSearchAgent
{
    public const string Name = "RAGAgent";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente RAG que busca información en documentos indexados usando Azure AI Search.",
        Category = "Contexto",
        Icon = "📚",
        Color = "#27ae60",
        Tools = ["SearchDocuments"],
        ExamplePrompts = [
            "Busca información sobre contratos de servicio",
            "¿Qué documentos tenemos sobre políticas de seguridad?",
            "Encuentra información relevante sobre el proyecto X"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
            var searchPlugin = sp.GetRequiredService<AzureSearchPlugin>();
            var config = sp.GetRequiredService<IConfiguration>();
            var clientId = config["Tenant:ClientId"] ?? "default";
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(searchPlugin));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: $"""
                    Eres un asistente experto en búsqueda de documentos. Ayudas a los usuarios a encontrar
                    información relevante en la base de conocimiento indexada usando Azure AI Search.
                    
                    IMPORTANTE: Operas para el cliente '{clientId}'. Solo tienes acceso a documentos
                    de este cliente. Los resultados ya están filtrados automáticamente por ClientId.
                    
                    REGLAS:
                    1. SIEMPRE usa la función SearchDocuments para buscar información.
                    2. Presenta los resultados de forma clara y organizada.
                    3. Si no se encuentran resultados, sugiere reformular la búsqueda.
                    4. Cita las fuentes (título, expediente, tipo de documento) en tus respuestas.
                    5. Responde en el mismo idioma que el usuario.
                    6. Si hay un error de Azure Search, muéstralo al usuario tal cual.
                    """,
                tools: tools);
        }
    };
}
