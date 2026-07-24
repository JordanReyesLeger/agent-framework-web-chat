using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Basic;

public static class SummarizerAgent
{
    public const string Name = "Summarizer";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Genera resúmenes concisos de textos largos, artículos o documentos. (Único agente que usa la Chat Completions API clásica — sin razonamiento.)",
        Category = "Básico",
        Icon = "📝",
        Color = "#238636",
        ExamplePrompts = ["Resume este artículo en 3 puntos clave", "Dame un resumen ejecutivo de este texto", "¿Cuáles son las conclusiones principales de este documento?"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            // Agente de demostración: se mantiene deliberadamente en Chat Completions
            // (sin razonamiento) mientras el resto de agentes usan la Responses API.
            var chatClient = factory.CreateChatCompletionsClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un experto en resumir textos.
                    Cuando el usuario proporcione un texto:
                    1. Genera un resumen conciso capturando los puntos clave.
                    2. Usa viñetas para múltiples puntos clave.
                    3. Mantén el significado y tono originales.
                    4. Si el texto es corto, proporciona un resumen breve de una línea.
                    5. Si se solicita un formato específico (resumen ejecutivo, viñetas, una línea), adáptate.
                    """);
        }
    };
}
