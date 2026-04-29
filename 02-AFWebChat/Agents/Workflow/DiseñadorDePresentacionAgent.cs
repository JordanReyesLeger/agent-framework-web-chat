using AFWebChat.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AFWebChat.Agents.Workflow;

public static class DiseñadorDePresentacionAgent
{
    public const string Name = "DiseñadorDePresentacion";

    public static AgentDefinition CreateDefinition() => new()
    {
        Name = Name,
        Description = "Diseñador de presentaciones que estructura la información en slides listos para mostrar.",
        Category = "Workflow",
        Icon = "🎬",
        Color = "#9b59b6",
        ExamplePrompts = [
            "Arma las slides para la junta directiva",
            "Estructura una presentación ejecutiva con estos datos",
            "Crea el deck de PowerPoint para la reunión de resultados"
        ],
        SupportsStreaming = true,
        Factory = sp =>
        {
            var factory = sp.GetRequiredService<ChatClientFactory>();
            var chatClient = factory.CreateChatClient();

            return chatClient.AsAIAgent(
                name: Name,
                instructions: """
                    Eres un Diseñador de Presentaciones ejecutivas. Con el reporte recibido, genera:

                    ## 🎬 Presentación Ejecutiva (8 Slides)

                    ---
                    ### 📑 Slide 1: Portada
                    **[Título del Reporte]**
                    Período | Autor | Fecha | Confidencial

                    ---
                    ### 📑 Slide 2: Agenda
                    1. Resumen ejecutivo
                    2. KPIs del período
                    3. Logros principales
                    4. Puntos de atención
                    5. Recomendaciones
                    6. Próximos pasos

                    ---
                    ### 📑 Slide 3: Dashboard de KPIs
                    Vista de semáforo con los indicadores principales.
                    Cada KPI con su valor, meta, variación y tendencia visual.

                    ---
                    ### 📑 Slide 4: Resultados Destacados 🏆
                    Top 3 logros con impacto cuantificado y contexto.

                    ---
                    ### 📑 Slide 5: Puntos de Atención ⚠️
                    Temas que requieren decisión, con opciones y recomendación.

                    ---
                    ### 📑 Slide 6: Comparativo de Tendencias
                    Tabla o gráfico conceptual mostrando la evolución.

                    ---
                    ### 📑 Slide 7: Plan de Acción
                    | Acción | Responsable | Fecha | Prioridad |
                    |--------|-------------|-------|-----------|

                    ---
                    ### 📑 Slide 8: Próximos Pasos y Cierre
                    - Decisiones pendientes
                    - Próxima reunión
                    - Contacto para dudas

                    ---

                    ### 🗣️ Notas del Presentador
                    Puntos clave a mencionar verbalmente en cada slide.

                    Diseña para impacto visual: poco texto, datos grandes, colores de semáforo.
                    """);
        }
    };
}
