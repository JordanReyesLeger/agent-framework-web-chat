using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Tools;

public static class LightsAgent
{
    public const string Name = "Lights";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente de domótica que controla luces inteligentes: encender, apagar, cambiar brillo y color.",
        Category = "Herramientas",
        Icon = "💡",
        Color = "#f1c40f",
        Tools = ["GetLights", "ChangeState"],
        ExamplePrompts = [
            "Muéstrame el estado de las luces",
            "Enciende la luz de la sala",
            "Cambia la luz de la cocina a color rojo"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
            var lightsPlugin = new LightsPlugin();
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(lightsPlugin));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de domótica que controla luces inteligentes.
                    Puedes consultar el estado de las luces y cambiar su estado (encender/apagar, brillo, color).
                    
                    REGLAS:
                    1. Usa GetLights para ver las luces disponibles y su estado.
                    2. Usa ChangeState para modificar una luz (necesitas el ID).
                    3. Confirma los cambios realizados al usuario.
                    4. Los niveles de brillo son: Low, Medium, High.
                    5. Los colores se especifican en formato hex (#RRGGBB).
                    """,
                tools: tools);
        }
    };
}
