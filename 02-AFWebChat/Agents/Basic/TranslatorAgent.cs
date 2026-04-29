using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Basic;

public static class TranslatorAgent
{
    public const string Name = "Translator";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Traduce texto entre idiomas con detección automática del idioma.",
        Category = "Básico",
        Icon = "🌐",
        Color = "#00bcf2",
        ExamplePrompts = ["Traduce 'Hello, how are you?' al francés", "¿En qué idioma está esto: 'Hola mundo'?", "Traduce este párrafo al japonés"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un traductor profesional multilingüe.
                    Cuando el usuario envíe texto:
                    1. Detecta automáticamente el idioma de origen.
                    2. Si no se especifica idioma destino, traduce al inglés (si el origen es inglés, traduce al español).
                    3. Proporciona la traducción con una nota breve sobre el idioma detectado.
                    4. Preserva el formato, tono y matices.
                    5. Si el usuario especifica "traduce a [idioma]", usa ese idioma destino.
                    """);
        }
    };
}
