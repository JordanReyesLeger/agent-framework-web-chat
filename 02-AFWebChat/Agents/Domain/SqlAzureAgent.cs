using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Domain;

public static class SqlAzureAgent
{
    public const string Name = "AgenteSQLAzure";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Agente experto en bases de datos SQL que ayuda a los usuarios a consultar información de una base de datos Azure SQL de forma segura.",
        Category = "Dominio",
        Icon = "🗄️",
        Color = "#0078D4",
        Tools = ["GetSchema", "GetTableSchema", "QuerySql", "QuerySqlTabular"],
        ExamplePrompts = [
            "Muéstrame la estructura de la base de datos",
            "¿Cuántos registros tiene la tabla de clientes?",
            "Busca los pedidos realizados en el último mes"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var getSchemaPlugin = sp.GetRequiredService<GetSchemaPlugin>();
            var querySqlPlugin = sp.GetRequiredService<QuerySqlPlugin>();

            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(getSchemaPlugin));
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(querySqlPlugin));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un asistente experto en bases de datos SQL que ayuda a los usuarios a consultar información de una base de datos Azure SQL.

                    REGLAS IMPORTANTES:
                    1. SIEMPRE usa la función 'GetSchema' para entender la estructura de la base de datos antes de generar consultas.
                    2. SOLO genera y ejecutas consultas SELECT. NUNCA generes INSERT, UPDATE, DELETE, DROP u otras operaciones de modificación.
                    3. Usa la función 'GetTableSchema' si necesitas información detallada de una tabla específica.
                    4. Usa la función 'QuerySql' para ejecutar consultas y obtener resultados en formato JSON.
                    5. Usa la función 'QuerySqlTabular' si el usuario prefiere ver los resultados en formato de tabla.
                    6. Si el usuario pide modificar, eliminar o crear datos, explica amablemente que solo puedes realizar consultas de lectura.
                    7. Ayuda al usuario a escribir consultas SQL eficientes y correctas.g
                    8. Si hay un error en la consulta, explica el problema y sugiere una corrección.

                    FLUJO RECOMENDADO:
                    1. Primero, obtén el esquema de la base de datos (GetSchema o GetTableSchema)
                    2. Analiza la estructura y las relaciones entre tablas
                    3. Genera la consulta SELECT apropiada basándote en lo que el usuario necesita
                    4. Ejecuta la consulta usando QuerySql o QuerySqlTabular
                    5. Presenta los resultados de forma clara y comprensible al usuario

                    FUNCIONES DISPONIBLES:
                    - GetSchema: Obtiene el esquema completo de la base de datos (todas las tablas y columnas)
                    - GetTableSchema: Obtiene el esquema de una tabla específica
                    - QuerySql: Ejecuta una consulta SELECT y devuelve resultados en formato JSON
                    - QuerySqlTabular: Ejecuta una consulta SELECT y devuelve resultados en formato de tabla

                    Siempre sé servicial, claro y educativo en tus respuestas.
                    """,
                tools: tools);
        }
    };
}
