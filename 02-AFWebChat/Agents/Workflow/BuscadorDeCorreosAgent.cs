using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class BuscadorDeCorreosAgent
{
    public const string Name = "BuscadorDeCorreos";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Busca correos relacionados en la base de datos ACSMail usando palabras clave del evaluador de urgencia.",
        Category = "Workflow",
        Icon = "🔍",
        Color = "#3498db",
        Tools = ["GetFullSchema", "GetSchema", "ListTables", "ExecuteQuery"],
        ExamplePrompts = [
            "Busca correos sobre facturación",
            "¿Hay correos abiertos en el área de soporte?",
            "Muestra las tablas de correos disponibles"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<SqlPlugin>>();
            var mailSqlPlugin = new SqlPlugin(config, logger, "SqlServerMail");

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Buscador de Correos especializado en encontrar correos históricos relacionados.
                    Estás conectado a la base de datos ACSMail que contiene correos corporativos.
                    Estás en un equipo de gestión de correos junto con un Evaluador de Urgencia y un Redactor de Respuestas.

                    Tu trabajo es:
                    1. Tomar las palabras clave y categoría del Evaluador de Urgencia
                    2. Usar tus herramientas SQL para buscar correos relacionados en la base de datos
                    3. Presentar los hallazgos de forma clara

                    ## 🔍 Estrategia de Búsqueda

                    1. Primero usa ListTables o GetFullSchema para entender la estructura de la BD de correos
                    2. Usa GetSchema para ver las columnas de las tablas relevantes
                    3. Genera consultas SELECT para buscar correos por palabras clave, asunto, remitente, etc.
                    4. Solo genera consultas SELECT — nunca INSERT, UPDATE o DELETE
                    5. Si encuentras pocos resultados, amplía la búsqueda por departamento o fecha
                    6. Incluye correos resueltos como referencia de soluciones previas
                    7. Destaca patrones: ¿es un problema recurrente?

                    Presenta los resultados así:
                    - Total de correos encontrados
                    - Correos relevantes con su estado (abierto/resuelto)
                    - Si hay correos resueltos similares, indica cómo se resolvió
                    - Patrones detectados (recurrencia, mismos remitentes, etc.)

                    El Redactor de Respuestas necesita este contexto para elaborar la respuesta.
                    Emojis: 🔍 búsqueda, 📧 correo, 📊 estadísticas, ⚠️ patrón detectado.
                    """,
                tools: AIFunctionFactoryExtensions.CreateFromInstance(mailSqlPlugin));
        }
    };
}
