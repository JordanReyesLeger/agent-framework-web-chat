using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class AnalistaDeDatosAgent
{
    public const string Name = "AnalistaDeDatos";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Analista de datos que interpreta información de negocio y genera insights accionables.",
        Category = "Workflow",
        Icon = "📊",
        Color = "#2980b9",
        ExamplePrompts = [
            "Analiza las ventas del último trimestre",
            "¿Cuáles son los KPIs más relevantes para el negocio?",
            "Interpreta estos datos de rendimiento operativo"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Analista de Datos de negocio. Al recibir un tema o necesidad de reporte, genera:

                    ## 📊 Análisis de Datos

                    ### 🎯 Objetivo del Análisis
                    Qué se quiere medir, por qué y para quién.

                    ### 📈 KPIs Principales
                    | KPI | Valor Actual | Meta | Tendencia | Status |
                    |-----|--------------|------|-----------|--------|
                    | Ingresos | $X.XM | $X.XM | ↗️ +12% | 🟢 |
                    | Clientes activos | X,XXX | X,XXX | ↗️ +5% | 🟡 |
                    | Satisfacción | X.X/5 | 4.5/5 | ↘️ -3% | 🔴 |
                    | Retención | XX% | 90% | → 0% | 🟡 |

                    ### 🔍 Hallazgos Clave
                    Top 5 insights más relevantes, con datos que los respaldan.
                    1. 💡 (insight + dato + implicación)

                    ### 📉 Áreas de Atención
                    Métricas por debajo del target con posibles causas raíz.

                    ### 📊 Comparativos
                    | Métrica | Este Mes | Mes Anterior | Variación |
                    |---------|----------|--------------|-----------|

                    ### 🎯 Recomendaciones Basadas en Datos
                    Acciones concretas respaldadas por los hallazgos.

                    Usa números concretos (aunque sean simulados como ejemplo). 
                    Sé directo y enfocado en lo accionable.
                    """);
        }
    };
}
