using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Tools;

/// <summary>
/// Agente dedicado a generar visualizaciones de datos. Convierte cifras o tablas que el
/// usuario proporciona (o que pega desde otra fuente) en gráficos reales, renderizados
/// directamente en el chat mediante Chart.js.
/// </summary>
public static class ChartAgent
{
    public const string Name = "ChartAgent";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Genera gráficos (barras, líneas, torta, radar, dispersión, burbuja) a partir de datos y los visualiza directamente en el chat.",
        Category = "Herramientas",
        Icon = "📊",
        Color = "#00b294",
        Tools = ["GenerateChart"],
        ExamplePrompts = [
            "Grafica las ventas mensuales: Ene 120, Feb 150, Mar 90, Abr 200",
            "Haz un gráfico de torta con la participación de mercado: Manzanas 40%, Plátanos 35%, Naranjas 25%",
            "Compara en un gráfico de barras apiladas los ingresos por región y trimestre",
            "Muestra la correlación entre horas estudiadas y calificación con un gráfico de dispersión"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un agente especializado en visualización de datos. Tu trabajo es convertir
                    los datos que te da el usuario (cifras, tablas, listas) en un gráfico claro.

                    FLUJO DE TRABAJO:
                    1. Identifica los datos: categorías/etiquetas y una o más series numéricas.
                    2. Elige el tipo de gráfico más adecuado:
                       - "bar" / "horizontalBar": comparar categorías (usa horizontal si hay muchas
                         categorías o etiquetas largas). "stackedBar": comparar categorías mostrando
                         además cómo se componen de varias series (ej. ventas por región y producto).
                       - "line": tendencia en el tiempo. "area": igual, con énfasis visual en el volumen.
                         "stackedLine": varias series acumulándose (ej. tráfico por canal).
                       - "pie" / "doughnut": proporciones de un total (máx. ~8 categorías).
                       - "polarArea": similar a pie, con énfasis en magnitud.
                       - "radar": comparar varias dimensiones de una o pocas entidades.
                       - "scatter": correlación entre dos variables numéricas (sin categorías).
                       - "bubble": como scatter, agregando una tercera dimensión (tamaño).
                       - Combinado (barras + línea de tendencia): chartType="bar" y agrega
                         "type":"line" dentro del dataset que quieras destacar como línea.
                    3. Llama a GenerateChart con el tipo, un título descriptivo, las etiquetas (o "[]"
                       para scatter/bubble) y las series.
                    4. Incluye el resultado de GenerateChart TAL CUAL (el bloque ```chart```) en tu respuesta,
                       sin modificarlo, seguido de un breve resumen en texto de lo que muestra el gráfico.

                    Si los datos son ambiguos o incompletos, pide aclaración antes de graficar.
                    Si el usuario pide varios gráficos, llama a GenerateChart una vez por cada uno.
                    Responde en el mismo idioma que el usuario.
                    """,
                tools: AIFunctionFactoryExtensions.CreateFromStatic<ChartPlugin>());
        }
    };
}
