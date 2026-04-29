using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Enterprise;

/// <summary>
/// Enterprise agent that orchestrates multiple sub-agents (SQL + RAG + WebSearch)
/// to create comprehensive project plans grounded in real data.
/// </summary>
public static class MultiAgentPlannerAgent
{
    public const string Name = "MultiAgentPlanner";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Orquestador empresarial que combina datos SQL, documentos RAG y búsqueda web para crear planes de proyecto completos con datos reales.",
        Category = "Empresarial",
        Icon = "🧠",
        Color = "#6C3483",
        Tools = [
            "GetSchema", "QuerySql", "QuerySqlTabular",
            "SearchDocuments",
            "ScrapeWebPage",
            "SearchWithBingGrounding"
        ],
        ExamplePrompts = [
            "Crea un plan de proyecto para migrar nuestra base de datos a la nube",
            "Analiza nuestros datos de ventas y genera un plan de acción con mejores prácticas de la industria",
            "Investiga tendencias del mercado y cruza con nuestros datos internos para una propuesta estratégica"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            // SQL plugins
            var getSchemaPlugin = sp.GetRequiredService<GetSchemaPlugin>();
            var querySqlPlugin = sp.GetRequiredService<QuerySqlPlugin>();

            // RAG plugin
            var searchPlugin = sp.GetRequiredService<AzureSearchPlugin>();

            // Web plugins
            var webScraping = sp.GetRequiredService<WebScrapingPlugin>();
            var bingPlugin = sp.GetRequiredService<BingGroundingPlugin>();

            var tools = new List<AITool>();
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(getSchemaPlugin));
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(querySqlPlugin));
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(searchPlugin));
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(webScraping));
            tools.AddRange(AIFunctionFactoryExtensions.CreateFromInstance(bingPlugin));

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un **Planificador Multi-Agente Empresarial** — un consultor estratégico de IA que combina
                    múltiples fuentes de datos para crear planes de proyecto integrales.

                    ## FUENTES DE DATOS DISPONIBLES:
                    1. **Base de Datos SQL** — Datos internos de la organización (usa GetSchema, QuerySql, QuerySqlTabular)
                    2. **Documentos RAG** — Documentación y conocimiento indexado (usa SearchDocuments)
                    3. **Web en tiempo real** — Información actualizada de internet (usa ScrapeWebPage, SearchWithBingGrounding)

                    ## FLUJO DE TRABAJO:
                    1. **Descubrimiento**: Entiende el objetivo del usuario. Pregunta si necesitas clarificar.
                    2. **Recopilación de datos internos**: Consulta la base de datos SQL para obtener métricas relevantes.
                    3. **Búsqueda de conocimiento**: Busca documentación interna relevante vía RAG.
                    4. **Investigación externa**: Busca mejores prácticas y tendencias en la web.
                    5. **Síntesis**: Integra toda la información en un plan estructurado.

                    ## FORMATO DE SALIDA:
                    Siempre genera planes con esta estructura en Markdown:
                    
                    ### 📋 Resumen Ejecutivo
                    (Párrafo breve del objetivo y alcance)
                    
                    ### 📊 Datos Internos
                    (Tabla con métricas clave de la base de datos)
                    
                    ### 📚 Conocimiento Relevante
                    (Insights de documentos internos)
                    
                    ### 🌐 Mejores Prácticas de la Industria
                    (Tendencias y referencias web)
                    
                    ### 🎯 Plan de Acción
                    (Lista numerada de pasos con responsables y fechas estimadas)
                    
                    ### ⚠️ Riesgos y Mitigaciones
                    (Tabla de riesgos identificados)

                    ## REGLAS:
                    - Siempre intenta consultar las 3 fuentes antes de generar el plan.
                    - Si una fuente no está disponible, continúa con las demás e indica la limitación.
                    - Usa tablas Markdown para presentar datos comparativos.
                    - Cita las fuentes de cada sección.
                    - Responde en el mismo idioma que el usuario.
                    """,
                tools: tools);
        }
    };
}
