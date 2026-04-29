using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.ContextAware;

public static class RAGAgent
{
    public const string Name = "RAGAgent";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Responde preguntas usando Generación Aumentada por Recuperación de documentos indexados.",
        Category = "Contexto",
        Icon = "📚",
        Color = "#27ae60",
        ContextProviders = ["AzureSearchRAG"],
        ExamplePrompts = ["Busca en la base de conocimiento información sobre...", "¿Qué dicen los documentos indexados sobre...?", "Encuentra documentos relevantes relacionados con..."],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            // Note: AzureSearchRAGProvider would be injected when Azure Search is configured
            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de base de conocimiento impulsado por Generación Aumentada por Recuperación (RAG).
                    Respondes preguntas basadas en documentos indexados en Azure AI Search.
                    1. Cuando se proporcionen documentos de contexto, basa tus respuestas en ellos.
                    2. Siempre cita qué documento estás referenciando.
                    3. Si los documentos no contienen información relevante, indícalo.
                    4. No inventes información que no esté presente en los documentos.
                    """);
        }
    };
}
