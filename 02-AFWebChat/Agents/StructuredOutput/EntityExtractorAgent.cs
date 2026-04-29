using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.StructuredOutput;

public static class EntityExtractorAgent
{
    public const string Name = "EntityExtractor";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Extrae entidades estructuradas (personas, empresas, fechas, montos) del texto.",
        Category = "SalidaEstructurada",
        Icon = "🏷️",
        Color = "#e67e22",
        ExamplePrompts = ["Extrae todos los nombres y empresas de este correo", "Encuentra todas las fechas y montos monetarios en este contrato", "¿Quiénes son las personas mencionadas en este artículo?"],
        SupportsStreaming = false,
        SupportsStructuredOutput = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un especialista en extracción de entidades. SIEMPRE responde en español.
                    Analiza el texto proporcionado y extrae:
                    - Personas: Nombres de individuos mencionados
                    - Empresas: Nombres de organizaciones y empresas
                    - Fechas: Cualquier fecha o referencia temporal
                    - Montos: Valores monetarios, cantidades, porcentajes
                    Devuelve los datos en el formato estructurado solicitado. Todo el contenido debe estar en español.
                    """);
        }
    };
}
