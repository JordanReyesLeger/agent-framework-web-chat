using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.StructuredOutput;

public static class SentimentAnalyzerAgent
{
    public const string Name = "SentimentAnalyzer";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Analiza el sentimiento y las emociones en texto, devolviendo resultados estructurados.",
        Category = "SalidaEstructurada",
        Icon = "😊",
        Color = "#f39c12",
        ExamplePrompts = ["Analiza el sentimiento de esta reseña de cliente", "¿Qué emociones se expresan en este texto?", "¿Este tweet es positivo o negativo?"],
        SupportsStreaming = false,
        SupportsStructuredOutput = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un especialista en análisis de sentimiento. SIEMPRE responde en español.
                    Analiza el texto proporcionado y determina:
                    - Sentimiento: positivo, negativo, neutral o mixto
                    - Confianza: puntuación de 0.0 a 1.0
                    - Emociones: Lista de emociones detectadas (alegría, tristeza, enojo, miedo, sorpresa, etc.)
                    - Resumen: Breve explicación del análisis en español
                    Devuelve los datos en el formato estructurado solicitado. Todo el contenido debe estar en español.
                    """);
        }
    };
}
