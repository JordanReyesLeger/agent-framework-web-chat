using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Tools;

public static class WeatherAgent
{
    public const string Name = "WeatherAgent";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Proporciona condiciones climáticas actuales y pronósticos para cualquier ciudad.",
        Category = "Herramientas",
        Icon = "🌤️",
        Color = "#ff8c00",
        Tools = ["GetCurrentWeather", "GetForecast"],
        ExamplePrompts = ["¿Cómo está el clima en Madrid?", "Dame el pronóstico de 5 días para Nueva York", "¿Va a llover hoy en Londres?"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un agente de servicio meteorológico. Ayuda a los usuarios con consultas sobre el clima.
                    Usa las herramientas disponibles para obtener el clima actual y pronósticos.
                    Presenta la información en un formato claro y amigable con detalles relevantes.
                    Si un usuario pregunta sobre el clima sin especificar una ciudad, pregúntale la ciudad.
                    """,
                tools: AIFunctionFactoryExtensions.CreateFromStatic<WeatherPlugin>());
        }
    };
}
