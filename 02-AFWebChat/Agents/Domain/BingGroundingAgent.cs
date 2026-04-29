using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

public static class BingGroundingAgent
{
    public const string Name = "BingGrounding";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente que busca información actualizada en la web usando Bing y puede extraer contenido de páginas.",
        Category = "Herramientas",
        Icon = "🌐",
        Color = "#00809d",
        Tools = ["ScrapeWebPage", "SearchWithBingGrounding"],
        ExamplePrompts = [
            "¿Cuáles son las últimas noticias sobre IA?",
            "Busca información actualizada sobre Azure OpenAI",
            "Extrae el contenido de esta página: https://example.com"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClientFactory>().CreateChatClient();
            var webScraping = sp.GetRequiredService<WebScrapingPlugin>();
            var bingGrounding = sp.GetRequiredService<BingGroundingPlugin>();
            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(webScraping));
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(bingGrounding));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de búsqueda web. Puedes extraer contenido de páginas web
                    para proporcionar información actualizada.
                    
                    REGLAS:
                    1. Usa ScrapeWebPage cuando el usuario proporcione una URL o necesites contenido de una página.
                    2. Presenta la información de forma resumida y clara.
                    3. Siempre cita la fuente (URL) de la información.
                    4. Si la página no se puede acceder, informa al usuario.
                    5. Responde en el mismo idioma que el usuario.
                    """,
                tools: tools);
        }
    };
}
