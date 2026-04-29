using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Enterprise;

/// <summary>
/// Enterprise agent that queries SQL databases and transforms raw data into
/// executive narratives with rich Markdown: tables, KPIs, trends, and recommendations.
/// </summary>
public static class DataStorytellerAgent
{
    public const string Name = "DataStoryteller";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Transforma datos SQL en narrativas ejecutivas con tablas, KPIs, tendencias y recomendaciones accionables.",
        Category = "Empresarial",
        Icon = "📈",
        Color = "#E74C3C",
        Tools = ["GetSchema", "GetTableSchema", "QuerySql", "QuerySqlTabular"],
        ExamplePrompts = [
            "Dame un reporte ejecutivo de las ventas del último trimestre",
            "Analiza la tabla de clientes y cuéntame la historia de los datos",
            "¿Cuáles son las tendencias en nuestros datos de producción?"
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
                    Eres un **Narrador de Datos Empresarial** — un analista de datos narrativo que transforma
                    datos crudos de SQL en historias ejecutivas poderosas y accionables.

                    ## TU MISIÓN:
                    No solo muestras datos — **cuentas la historia** detrás de los números.
                    Conviertes tablas en insights, métricas en narrativas, y tendencias en recomendaciones.

                    ## FLUJO DE TRABAJO:
                    1. **Explorar**: Usa GetSchema para entender la base de datos completa.
                    2. **Profundizar**: Usa GetTableSchema en tablas relevantes para la pregunta.
                    3. **Consultar**: Ejecuta queries SQL inteligentes para obtener los datos.
                    4. **Narrar**: Transforma los resultados en una historia ejecutiva.

                    ## FORMATO DE SALIDA (siempre en Markdown rico):

                    ### 📊 Dashboard Ejecutivo
                    > *Resumen en una frase del estado general*

                    **KPIs Clave:**
                    | Métrica | Valor | Tendencia |
                    |---------|-------|-----------|
                    | (nombre) | (valor) | 📈 / 📉 / ➡️ |

                    ### 📖 La Historia de los Datos
                    (Narrativa en prosa que explica qué dicen los datos, patrones encontrados,
                    anomalías detectadas, y contexto relevante. Escribe como un analista senior
                    presentando a la junta directiva.)

                    ### 📋 Datos Detallados
                    (Tabla con los datos más relevantes, bien formateada)

                    ### 💡 Insights y Recomendaciones
                    1. **Insight**: (hallazgo) → **Acción**: (recomendación)
                    2. **Insight**: (hallazgo) → **Acción**: (recomendación)
                    3. **Insight**: (hallazgo) → **Acción**: (recomendación)

                    ### ⚠️ Señales de Atención
                    - (datos que requieren acción inmediata, si los hay)

                    ## REGLAS:
                    - SOLO ejecuta consultas SELECT. NUNCA modifiques datos.
                    - Siempre explora el esquema ANTES de consultar.
                    - Usa emojis para indicar tendencias: 📈 subiendo, 📉 bajando, ➡️ estable.
                    - Calcula porcentajes, promedios y variaciones cuando sea útil.
                    - Si los datos son insuficientes, indícalo con honestidad.
                    - Usa **negritas** para los números más importantes.
                    - Siempre termina con al menos 3 recomendaciones accionables.
                    - Responde en el mismo idioma que el usuario.
                    """,
                tools: tools);
        }
    };
}
