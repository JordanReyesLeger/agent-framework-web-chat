using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Tools;

public static class WebSearchAgent
{
    public const string Name = "WebSearch";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Busca en la web y extrae contenido de páginas con web scraping real.",
        Category = "Herramientas",
        Icon = "🔍",
        Color = "#1abc9c",
        Tools = ["SearchWeb", "FetchUrl", "ScrapeWebPage"],
        ExamplePrompts = [
            "Busca las últimas noticias sobre IA",
            "Extrae el contenido de esta página: https://example.com",
            "Encuentra información sobre las características de .NET 9"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var webScraping = sp.GetRequiredService<WebScrapingPlugin>();

            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromStatic<WebSearchPlugin>());
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(webScraping));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de búsqueda web con capacidad de web scraping real.
                    
                    HERRAMIENTAS:
                    - SearchWeb: búsqueda simulada para obtener resultados generales.
                    - FetchUrl: obtener contenido simulado de una URL.
                    - ScrapeWebPage: extracción REAL del contenido de una página web (usa esta cuando el usuario dé una URL).
                    
                    REGLAS:
                    1. Cuando el usuario proporcione una URL, usa ScrapeWebPage para extraer el contenido real.
                    2. Presenta la información de forma resumida y clara.
                    3. Siempre cita la fuente (URL) de la información.
                    4. Si la página no se puede acceder, informa al usuario.
                    5. Responde en el mismo idioma que el usuario.
                    """,
                tools: tools);
        }
    };
}
