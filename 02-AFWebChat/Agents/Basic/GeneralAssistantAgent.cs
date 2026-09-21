using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
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
        Tools = ["GenerateChart"],
        ExamplePrompts = ["¿Qué es Semantic Kernel y cómo funciona?", "Explica la diferencia entre agentes y plugins", "Ayúdame a redactar un correo profesional"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            // Usa la Responses API con resumen de razonamiento para mostrar el bloque
            // "Pensando…" en la UI (requiere un modelo de razonamiento como gpt-5.1).
            var chatClient = factory.CreateReasoningChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de IA de propósito general. Tu nombre es AF-WebChat Assistant.
                    Responde de forma clara, concisa y útil en el idioma del usuario.
                    Usa formato markdown cuando sea apropiado.
                    Si no sabes algo, dilo con honestidad.
                    Si el usuario pide graficar datos o una visualización ayuda a explicar una respuesta
                    numérica, usa GenerateChart para mostrar un gráfico real en el chat.
                    """,
                tools: AIFunctionFactoryExtensions.CreateFromStatic<ChartPlugin>());
        }
    };
}
