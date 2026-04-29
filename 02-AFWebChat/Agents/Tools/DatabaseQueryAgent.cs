using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Tools;

public static class DatabaseQueryAgent
{
    public const string Name = "DatabaseQuery";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Genera y ejecuta consultas SQL de solo lectura contra la base de datos.",
        Category = "Herramientas",
        Icon = "💾",
        Color = "#8764b8",
        Tools = ["GetSchema", "ExecuteQuery", "ExplainQuery"],
        ExamplePrompts = ["Muéstrame el esquema de la base de datos", "¿Cuántos registros hay en la tabla de usuarios?", "Encuentra todos los pedidos del mes pasado"],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var sqlPlugin = sp.GetRequiredService<SqlPlugin>();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente de consultas de base de datos AdventureWorks.
                    Esta base de datos contiene información de productos, ventas, clientes y empleados.
                    
                    1. Primero, usa GetFullSchema o ListTables para entender la estructura de la base de datos.
                    2. Usa GetSchema para ver las columnas de una tabla específica.
                    3. Genera consultas SELECT apropiadas según la solicitud del usuario.
                    4. Usa ExecuteQuery para ejecutar consultas y presenta los resultados claramente.
                    5. Solo genera consultas SELECT — nunca INSERT, UPDATE o DELETE.
                    6. Formatea los resultados como tablas cuando sea apropiado.
                    """,
                tools: AIFunctionFactoryExtensions.CreateFromInstance(sqlPlugin));
        }
    };
}
