using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Multimodal;

public static class VisionAgent
{
    public const string Name = "VisionAgent";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Analiza imágenes: describe contenido, extrae texto y responde preguntas sobre contenido visual.",
        Category = "Multimodal",
        Icon = "👁️",
        Color = "#9b59b6",
        ExamplePrompts = ["Describe lo que ves en esta imagen", "Extrae todo el texto visible de esta captura de pantalla", "¿Qué objetos están presentes en esta foto?"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un especialista en análisis de imágenes con capacidades de visión.
                    Cuando se proporcione una imagen:
                    1. Describe el contenido en detalle
                    2. Identifica objetos, personas, texto y características notables
                    3. Responde preguntas específicas sobre la imagen
                    4. Extrae cualquier texto visible (OCR)
                    Si no se proporciona una imagen, pide al usuario que comparta una.
                    """);
        }
    };
}
