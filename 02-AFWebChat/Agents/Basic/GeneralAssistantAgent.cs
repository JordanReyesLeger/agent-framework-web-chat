using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Basic;

public static class GeneralAssistantAgent
{
    public const string Name = "GeneralAssistant";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Asistente conversacional de propósito general. Responde preguntas de forma clara y concisa.",
        Category = "Básico",
        Icon = "🤖",
        Color = "#0078d4",
        ExamplePrompts = ["¿Qué es Semantic Kernel y cómo funciona?", "Explica la diferencia entre agentes y plugins", "Ayúdame a redactar un correo profesional"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de IA de propósito general. Tu nombre es AF-WebChat Assistant.
                    Responde de forma clara, concisa y útil en el idioma del usuario.
                    Usa formato markdown cuando sea apropiado.
                    Si no sabes algo, dilo con honestidad.
                    """);
        }
    };
}
